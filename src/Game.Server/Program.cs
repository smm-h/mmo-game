using Game.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add health checks
builder.Services.AddHealthChecks();

// Add game services
builder.Services.AddSingleton<ZoneManager>();
builder.Services.AddSingleton<NetworkService>();
builder.Services.AddHostedService<GameLoopService>();

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHealthChecks("/health");
app.MapControllers();

// Start the game server
var networkService = app.Services.GetRequiredService<NetworkService>();
networkService.Start();

Console.WriteLine("MMO Game Server starting...");
Console.WriteLine($"HTTP API: http://localhost:{builder.Configuration["Urls"]?.Split(':').Last() ?? "5000"}");
Console.WriteLine($"Game Port: {Game.Shared.Network.NetworkConfig.DefaultPort}");

app.Run();
