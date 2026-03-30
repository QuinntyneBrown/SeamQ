using System.CommandLine;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SeamQ.Cli.Rendering;
using SeamQ.Core.Configuration;

namespace SeamQ.Cli.Commands;

public static class InitCommand
{
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new Command("init", "Generate seamq.config.json interactively");

        command.SetHandler(async () =>
        {
            var renderer = serviceProvider.GetRequiredService<IConsoleRenderer>();

            var configPath = Path.Combine(Directory.GetCurrentDirectory(), "seamq.config.json");

            if (File.Exists(configPath))
            {
                renderer.WriteWarning($"seamq.config.json already exists at {configPath}");
                return;
            }

            var defaultConfig = new SeamQConfig
            {
                Workspaces = new List<WorkspaceConfig>
                {
                    new WorkspaceConfig { Path = "./", Alias = "my-workspace" }
                }
            };

            var json = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            await File.WriteAllTextAsync(configPath, json);
            renderer.WriteSuccess($"created seamq.config.json");
            renderer.WriteMuted($"  {configPath}");
            renderer.WriteMuted("Edit this file to configure workspace paths, then run 'seamq scan'.");
        });

        return command;
    }
}
