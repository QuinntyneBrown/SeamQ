using Microsoft.Extensions.DependencyInjection;
using SeamQ.Core.Abstractions;
using SeamQ.Core.Configuration;

namespace SeamQ.Cli;

/// <summary>
/// Extension methods for registering all SeamQ services.
/// </summary>
public static class ServiceRegistration
{
    public static IServiceCollection AddSeamQServices(this IServiceCollection services)
    {
        // Configuration
        services.AddSingleton<SeamQConfig>(_ => ConfigLoader.Load());

        return services;
    }
}

/// <summary>
/// Loads SeamQ configuration from seamq.config.json or defaults.
/// </summary>
public static class ConfigLoader
{
    public static SeamQConfig Load(string? configPath = null)
    {
        var path = configPath ?? FindConfigFile();
        if (path is not null && File.Exists(path))
        {
            var json = File.ReadAllText(path);
            var config = System.Text.Json.JsonSerializer.Deserialize<SeamQConfig>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return config ?? new SeamQConfig();
        }
        return new SeamQConfig();
    }

    private static string? FindConfigFile()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "seamq.config.json");
            if (File.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }
}
