# MMO Game Architecture - Design Document

> **IMPORTANT**: All human decisions made during conversations with Claude must be recorded in this document. This ensures continuity across sessions and serves as the single source of truth for architectural decisions.

---

## Core Technology Stack

| Layer | Technology | Rationale |
|-------|------------|-----------|
| **Game Framework** | MonoGame | Cross-platform, mature, C# native |
| **Networking** | LiteNetLib | Pure C#, ENet-like reliability, no native deps |
| **ECS** | DefaultEcs | Built-in events, serialization, good balance of features/performance |
| **HTTP API** | ASP.NET Core | Same language, excellent perf, built-in auth |
| **Database** | PostgreSQL | JSONB support, concurrent writes, free |
| **ORM** | Hybrid EF Core + Dapper | EF for CRUD/migrations, Dapper for hot paths |
| **Voice Chat** | LiveKit (self-hosted) | Room-based, open source, Unity SDK, simpler than Janus |

---

## Repository Strategy: Monorepo

**Decision: Single monorepo for all code.**

| Approach | Pros | Cons |
|----------|------|------|
| **Monorepo** (chosen) | Atomic commits across client/server, shared code easy, single CI/CD | Larger repo size |
| **Polyrepo** | Independent versioning, smaller clones | Coordination overhead, shared code via packages |
| **Hybrid** | Flexible | Complex tooling |

**Rationale:**
- Shared code (`Game.Shared`) changes affect both client and server
- Atomic commits ensure compatibility
- Single PR for cross-cutting changes
- Simpler CI/CD pipeline

---

## Network Architecture

### Model: Hybrid Authoritative + Client Prediction

```
Client Input → Local Prediction → Send to Server
                    ↓
Server Validates → Authoritative State → Broadcast
                    ↓
Client Receives → Reconciliation (if mismatch)
```

**Key principles:**
- Client predicts movement/actions immediately for responsiveness
- Server validates and has final authority (anti-cheat)
- Client reconciles when server state differs from prediction
- Target: 100-200ms of client-side prediction buffer

### LiteNetLib vs ENet Comparison

| Aspect | ENet | LiteNetLib |
|--------|------|------------|
| **Language** | C (with C# bindings) | Pure C# |
| **Native Dependencies** | Yes (.dll/.so/.dylib) | None |
| **Cross-Platform Deploy** | Complex (bundle natives) | Simple (just DLLs) |
| **Performance (msg/sec)** | ~60k | ~50k |
| **Memory Allocations** | Lower (native) | Low (pooling built-in) |
| **Maintenance** | Sporadic (C lib stable) | Active (2024 updates) |
| **NuGet Package** | ENet-CSharp (community) | Official package |
| **.NET Support** | Via P/Invoke | Native .NET Standard 2.0+ |
| **API Style** | C-style callbacks | Modern events/delegates |
| **NAT Punch-through** | Manual implementation | Built-in |
| **Encryption** | None | AES built-in |
| **Channels** | 255 max | 255 max |
| **Battle-Tested Scale** | League of Legends, Cube 2 | Smaller games |
| **MonoGame Fit** | Works but complex | Excellent |

**Decision: LiteNetLib** - Pure C#, simpler deployment, modern API.

### Player Capacity Targets

| Metric | Target | Hard Limit |
|--------|--------|------------|
| Players per zone | 100-150 | 200 |
| Tick rate | 20 Hz | 60 Hz |
| Entity updates/sec | ~3000 | ~6000 |

---

## Scaling Strategy: Start Horizontal

**Decision: Start with horizontal architecture from day one.**

### Why Start Horizontal (Not Vertical)

| Factor | Vertical Start | Horizontal Start |
|--------|----------------|------------------|
| **Initial complexity** | Lower | Higher |
| **Refactor cost later** | Very high | None |
| **Code discipline** | Can cut corners | Forces good patterns |
| **Testing realism** | Fake conditions | Real distributed testing |
| **Team parallelism** | Limited | Multiple devs on zones |

**Rationale:** The cost of retrofitting horizontal scaling into a vertical codebase is extremely high. Writing horizontally-aware code from the start is a one-time investment that pays off as the game grows.

### Architecture from Day One

```
┌─────────────────────────────────────────────────┐
│              PERSISTENT WORLD                    │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐      │
│  │ Zone 1   │  │ Zone 2   │  │ Zone 3   │      │
│  │ Forest   │  │ Desert   │  │ Mountains│      │
│  └──────────┘  └──────────┘  └──────────┘      │
│       │              │              │           │
│       └──────────────┼──────────────┘           │
│                      ▼                          │
│            ┌──────────────────┐                 │
│            │  Instance Pool   │                 │
│            │ (Dungeons, etc.) │                 │
│            └──────────────────┘                 │
└─────────────────────────────────────────────────┘
```

### Zones vs Instances

| Concept | Zones | Instances |
|---------|-------|-----------|
| **Purpose** | Divide persistent world geographically | Duplicate content for groups |
| **Lifetime** | Always running | Created/destroyed on demand |
| **Players** | Hundreds, cross freely | Small groups, isolated |
| **Examples** | Forest, Desert, City | Dungeons, Arenas, Housing |
| **State** | Persistent | Temporary (progress saved on exit) |

### Dynamic Zone Management

**Can zones be joined or split?**

Yes, with proper implementation:

**Joining Zones (Merge):**
```
Zone A (30 players) + Zone B (40 players) → Zone AB (70 players)
```
- Triggered when population drops below threshold
- Players in Zone B receive transfer command
- Entities serialized and moved to Zone A
- Zone B shuts down
- Requires: Entity serialization, coordinated handoff

**Splitting Zones (Scale Out):**
```
Zone A (140 players) → Zone A1 (70 players) + Zone A2 (70 players)
```
- Triggered when approaching capacity
- New instance spun up
- Players near boundary offered transfer
- Requires: Load balancer, player distribution logic

**Implementation in ZoneManager:**
- `TryMergeInstances(zoneId)` - Combines low-pop instances
- `TrySplitInstance(zoneId, instanceId)` - Creates overflow instance

---

## ECS Architecture: DefaultEcs

### Why DefaultEcs over Arch

| Factor | DefaultEcs | Arch |
|--------|------------|------|
| **Storage** | Sparse sets | Archetypes |
| **Performance** | ~800M entities/sec | ~1.2B entities/sec |
| **Add/Remove Component** | O(1) fast | O(n) archetype migration |
| **Built-in Events** | Yes | Manual |
| **Serialization** | Built-in | Extension |
| **Learning Curve** | Medium | Medium |
| **Maturity** | 5+ years | 2+ years |

**Decision: DefaultEcs** - Better for MMO where player state changes frequently (buffs, inventory, status effects). Built-in events useful for game logic. Serialization helps with network sync.

### DefaultEcs Code Pattern

```csharp
// World and entity creation
var world = new World();
var entity = world.CreateEntity();
entity.Set(new Position(0, 0));
entity.Set(new Velocity(1, 1));

// System definition
public class MovementSystem : AEntitySetSystem<float>
{
    public MovementSystem(World world)
        : base(world.GetEntities().With<Position>().With<Velocity>().AsSet())
    { }

    protected override void Update(float dt, in Entity entity)
    {
        ref var pos = ref entity.Get<Position>();
        ref var vel = ref entity.Get<Velocity>();
        pos.X += vel.X * dt;
        pos.Y += vel.Y * dt;
    }
}

// Built-in messaging
world.Subscribe<DamageEvent>(OnDamage);
world.Publish(new DamageEvent(target, 50));
```

---

## Database Strategy

### PostgreSQL Configuration

- Primary use: Persistent player data, world state
- Enable JSONB for flexible game data
- Connection pooling via Npgsql

### Hybrid ORM Approach

**Use EF Core for:**
| Data | Reason |
|------|--------|
| User accounts | CRUD, relationships, migrations |
| Characters | Complex object graph |
| Transactions/Shop | ACID guarantees, change tracking |
| Guild management | Relationships, cascades |

**Use Dapper for:**
| Data | Reason |
|------|--------|
| Inventory | High read/write frequency |
| World state snapshots | Bulk operations |
| Leaderboards | Complex aggregations |
| Chat logs | Simple append-only |
| Analytics | Raw query performance |

---

## Voice Chat: LiveKit

### LiveKit vs Janus Comparison

| Aspect | LiveKit | Janus |
|--------|---------|-------|
| **Language** | Go | C |
| **Age** | 2021+ | 2014+ |
| **Self-Hosting** | Simple (single binary) | Complex (many deps) |
| **Cloud Offering** | LiveKit Cloud | No official |
| **Scaling** | Built-in clustering | Manual |
| **Kubernetes** | Helm charts official | Community charts |
| **Unity SDK** | Official, maintained | None official |
| **Documentation** | Excellent | Good but dense |
| **Learning Curve** | Easy | Steep |
| **CPU Usage** | Lower (Go efficiency) | Higher |
| **License** | Apache 2.0 | GPL v3 |

**Decision: LiveKit** - Simpler setup, official Unity SDK, Apache license, better for games.

### Why Not Vivox?

Vivox is "industry standard" because:
- Used by: Fortnite, PUBG, League of Legends, Destiny 2, EVE Online
- Console certification pre-approved (Xbox, PlayStation)
- COPPA/GDPR compliant
- Fully managed, no infrastructure

**Why we chose LiveKit instead:**
- Vivox is expensive at scale (per-minute pricing)
- Vendor lock-in
- We don't need spatial audio
- Self-hosted fits our needs

---

## Shader Architecture

MonoGame uses MGFX format (not raw HLSL/GLSL):

```
HLSL (.fx) → MGCB Tool → .xnb (platform-specific bytecode)
```

| Platform | Backend | Conversion |
|----------|---------|------------|
| Windows (DirectX) | DirectX 11 | HLSL → SM 4.0/5.0 |
| Windows (OpenGL) | OpenGL | HLSL → GLSL (auto) |
| Linux | OpenGL | HLSL → GLSL (auto) |
| macOS | OpenGL | HLSL → GLSL (auto) |
| Android | OpenGL ES | HLSL → GLSL ES (auto) |
| iOS | OpenGL ES/Metal | HLSL → GLSL ES/Metal |

**Key points:**
- Write HLSL once, MGCB converts per-platform
- Stick to Shader Model 3.0 for max compatibility
- Test on all target platforms (GL/DX differences exist)

---

## Project Structure

```
/
├── MMOGame.sln
├── claude.md                    # This file - all decisions
├── Content/                     # Shared game assets
│   ├── Textures/
│   ├── Fonts/
│   ├── Effects/
│   └── Content.mgcb
├── src/
│   ├── Game.Shared/            # Shared code (ECS, packets, network)
│   ├── Game.Client.Core/       # MonoGame client core
│   ├── Game.Client.DesktopGL/  # Desktop builds
│   ├── Game.Client.Android/    # Android builds
│   ├── Game.Client.iOS/        # iOS builds
│   └── Game.Server/            # Headless server
├── tests/
│   ├── Game.Shared.Tests/
│   └── Game.Server.Tests/
├── CLI/                        # Unified CLI tools
│   ├── game                    # Main CLI (server, client, build, logs)
│   ├── log-analyze             # Context-efficient log analysis
│   └── build-*.sh              # Platform build scripts
└── deploy/
    ├── docker/                 # Docker Compose setup
    ├── kubernetes/             # K8s manifests
    └── terraform/              # Infrastructure as code
```

---

## CLI Tools

> **IMPORTANT FOR CLAUDE**: Always run `./CLI/game --help` at the start of each session to see available commands. When you need to perform manual CLI operations not covered by the CLI tool, **offer to add them** to `CLI/game` instead of running raw commands.

### Main CLI (`./CLI/game`)

```bash
./CLI/game --help              # Show all commands
./CLI/game server --bg         # Start server in background
./CLI/game client              # Start client
./CLI/game stop --ports        # Kill processes on game ports
./CLI/game restart             # Stop and restart server
./CLI/game logs --summary      # Quick log summary (context-efficient)
./CLI/game logs --errors       # Show only errors
./CLI/game status              # Show running processes and ports
./CLI/game build               # Build all projects
```

### Log Analysis (`./CLI/log-analyze`)

Designed for context-efficient log reading (doesn't dump full logs):

```bash
./CLI/log-analyze summary      # Server log summary (5 lines)
./CLI/log-analyze errors       # Categorized errors only
./CLI/log-analyze network      # Network events summary
./CLI/log-analyze last 5       # Last 5 lines only
```

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `GAME_PORT` | 7777 | UDP game server port |
| `HTTP_PORT` | 5000 | HTTP API port |
| `LOG_DIR` | /tmp/mmo-game | Log file directory |

---

## Build & Deployment

### Client Builds

| Platform | Command | Output |
|----------|---------|--------|
| Linux | `./CLI/build-client-linux.sh` | `dist/linux/` |
| Windows | `./CLI/build-client-windows.sh` | `dist/windows/` |
| macOS | `./CLI/build-client-macos.sh` | `dist/macos-*/` |

### Server Deployment

**Development:**
```bash
./CLI/game server --bg         # Background with logging
./CLI/game server --follow     # Foreground with log tailing
```

**Docker (single zone):**
```bash
cd deploy/docker
docker compose -f docker-compose.yml -f docker-compose.dev.yml up
```

**Production (all zones):**
```bash
cd deploy/docker
docker compose up -d
```

**Kubernetes:**
```bash
kubectl apply -f deploy/kubernetes/
```

---

## Cost Projections

| Users | Infrastructure | Monthly Cost |
|-------|----------------|--------------|
| 10-100 | Docker on single VPS | $20-50 |
| 1,000 | 4 zone containers + DB | $150-200 |
| 10,000 | K8s cluster + managed DB | $400-500 |
| 100,000 | Multi-region K8s | $2,500-3,000 |

---

## Network Transport Abstraction

The network layer uses a transport abstraction allowing config-based switching between LiteNetLib and ENet:

```csharp
// INetworkTransport interface in Game.Shared/Network/
public interface INetworkTransport : IDisposable
{
    void Start(int port);
    void Connect(string host, int port);
    void SendToAll(byte[] data, DeliveryType delivery);
    event Action<int, byte[]>? OnDataReceived;
    // ... etc
}
```

**Switching transports** - edit `network.json`:
```json
{
  "transport": "LiteNetLib",  // or "ENet"
  "serverHost": "127.0.0.1",
  "serverPort": 7777,
  "tickRate": 20
}
```

**Implementations:**
- `LiteNetLibTransport.cs` - Full implementation (current)
- `ENetTransport.cs` - Stub ready for ENet-CSharp package

---

## Open Decisions

- [ ] Specific zone boundaries and map design
- [ ] Player transfer UX (loading screen vs seamless)
- [ ] CDN for asset delivery
- [ ] Analytics platform

---

## Claude Conventions

- **Never** add "Generated with Claude Code" or Co-Authored-By to commit messages
- Use short one-liner commit messages
- Don't hardcode environment-specific values (URLs, ports) in tracked configs

---

## Decision Log

| Date | Decision | Rationale |
|------|----------|-----------|
| 2024-12 | Use LiteNetLib | Pure C#, no native deps, modern API |
| 2024-12 | Use DefaultEcs | Better for frequent component changes, built-in events |
| 2024-12 | Use LiveKit | Simpler than Janus, official Unity SDK, Apache license |
| 2024-12 | Start horizontal | Avoid costly refactor later |
| 2024-12 | Monorepo | Atomic commits, shared code, simpler CI/CD |
| 2024-12 | Hybrid EF/Dapper | EF for accounts, Dapper for hot paths |
| 2024-12 | Transport abstraction | Config-based switching between LiteNetLib/ENet |
| 2024-12 | Bundled fonts | Use local fonts, not system fonts |
| 2024-12 | Unified CLI tool | Single `./CLI/game` for all dev commands |
| 2024-12 | Client prediction | Input buffering + server reconciliation |
