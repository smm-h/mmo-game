using System.Text.Json;

namespace Game.Shared.Network;

/// <summary>
/// Network configuration loaded from JSON file.
/// Change transport by editing network.json.
/// </summary>
public class NetworkSettings
{
    public TransportType Transport { get; set; } = TransportType.LiteNetLib;
    public string ServerHost { get; set; } = "127.0.0.1";
    public int ServerPort { get; set; } = NetworkConfig.DefaultPort;
    public int TickRate { get; set; } = NetworkConfig.TickRate;

    private static NetworkSettings? _instance;
    private static readonly string ConfigPath = FindConfigPath();

    private static string FindConfigPath()
    {
        // Try current working directory first (repo root when running from CLI)
        var cwdPath = Path.Combine(Directory.GetCurrentDirectory(), "network.json");
        if (File.Exists(cwdPath)) return cwdPath;

        // Fall back to bin directory
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "network.json");
    }

    public static NetworkSettings Instance => _instance ??= Load();

    public static NetworkSettings Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var settings = JsonSerializer.Deserialize<NetworkSettings>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (settings != null)
                {
                    Console.WriteLine($"[Network] Loaded config: Transport={settings.Transport}");
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Network] Failed to load config: {ex.Message}");
        }

        // Create default config
        var defaultSettings = new NetworkSettings();
        Save(defaultSettings);
        return defaultSettings;
    }

    public static void Save(NetworkSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(ConfigPath, json);
            Console.WriteLine($"[Network] Saved config to {ConfigPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Network] Failed to save config: {ex.Message}");
        }
    }
}
