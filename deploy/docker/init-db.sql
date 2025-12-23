-- Initial database schema

-- Accounts table (EF Core managed)
CREATE TABLE IF NOT EXISTS accounts (
    id SERIAL PRIMARY KEY,
    email VARCHAR(255) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    last_login TIMESTAMP WITH TIME ZONE,
    is_banned BOOLEAN DEFAULT FALSE
);

-- Characters table (EF Core managed)
CREATE TABLE IF NOT EXISTS characters (
    id SERIAL PRIMARY KEY,
    account_id INTEGER REFERENCES accounts(id) ON DELETE CASCADE,
    name VARCHAR(50) UNIQUE NOT NULL,
    class VARCHAR(50) NOT NULL,
    level INTEGER DEFAULT 1,
    experience BIGINT DEFAULT 0,
    zone_id INTEGER DEFAULT 1,
    position_x FLOAT DEFAULT 0,
    position_y FLOAT DEFAULT 0,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    played_time INTERVAL DEFAULT '0'
);

-- Inventory table (Dapper managed - high frequency)
CREATE TABLE IF NOT EXISTS inventory (
    id SERIAL PRIMARY KEY,
    character_id INTEGER REFERENCES characters(id) ON DELETE CASCADE,
    item_id INTEGER NOT NULL,
    slot INTEGER NOT NULL,
    quantity INTEGER DEFAULT 1,
    data JSONB DEFAULT '{}',
    UNIQUE(character_id, slot)
);

-- Create index for fast inventory lookups
CREATE INDEX IF NOT EXISTS idx_inventory_character ON inventory(character_id);

-- Chat logs (Dapper managed - append only)
CREATE TABLE IF NOT EXISTS chat_logs (
    id BIGSERIAL PRIMARY KEY,
    channel VARCHAR(50) NOT NULL,
    sender_id INTEGER REFERENCES characters(id),
    message TEXT NOT NULL,
    timestamp TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- Partition chat logs by month for performance
-- (In production, set up proper partitioning)

-- Leaderboards (Dapper managed - complex queries)
CREATE TABLE IF NOT EXISTS leaderboards (
    id SERIAL PRIMARY KEY,
    category VARCHAR(50) NOT NULL,
    character_id INTEGER REFERENCES characters(id) ON DELETE CASCADE,
    score BIGINT NOT NULL,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    UNIQUE(category, character_id)
);

CREATE INDEX IF NOT EXISTS idx_leaderboards_category_score ON leaderboards(category, score DESC);
