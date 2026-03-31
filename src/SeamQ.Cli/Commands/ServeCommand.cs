using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using SeamQ.Cli.Rendering;
using SeamQ.Core.Configuration;
using SeamQ.Core.Models;

namespace SeamQ.Cli.Commands;

public static class ServeCommand
{
    public static Command Create(IServiceProvider serviceProvider)
    {
        var portOption = new Option<int>("--port", () => 5050, "Port number for the local web server");

        var command = new Command("serve", "Launch local web server to browse ICDs")
        {
            portOption
        };

        command.SetHandler(async (port) =>
        {
            var renderer = serviceProvider.GetRequiredService<IConsoleRenderer>();
            var config = serviceProvider.GetRequiredService<SeamQConfig>();
            var globalContext = serviceProvider.GetRequiredService<GlobalContext>();

            // Prompt mode: not applicable for interactive serve command
            if (globalContext.PromptMode)
            {
                var promptGen = serviceProvider.GetRequiredService<PromptFileGenerator>();
                promptGen.WriteUnsupportedWarning("serve");
                return;
            }

            var outputDir = Path.GetFullPath(config.Output.Directory);

            if (!Directory.Exists(outputDir))
            {
                renderer.WriteError($"Output directory not found: {outputDir}. Run 'seamq generate --all' first.");
                Environment.ExitCode = ExitCodes.FatalError;
                return;
            }

            renderer.WriteInfo($"Starting local server on http://localhost:{port}");
            renderer.WriteMuted($"Serving files from {outputDir}");
            renderer.WriteMuted("Press Ctrl+C to stop.");

            using var listener = new System.Net.HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}/");
            listener.Start();

            renderer.WriteSuccess($"listening on http://localhost:{port}");

            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    var contextTask = listener.GetContextAsync();
                    var completed = await Task.WhenAny(contextTask, Task.Delay(Timeout.Infinite, cts.Token));
                    if (completed != contextTask) break;

                    var context = contextTask.Result;
                    var requestPath = context.Request.Url?.LocalPath?.TrimStart('/') ?? "index.html";
                    if (string.IsNullOrEmpty(requestPath)) requestPath = "index.html";

                    var filePath = Path.Combine(outputDir, requestPath);
                    if (File.Exists(filePath))
                    {
                        var content = await File.ReadAllBytesAsync(filePath, cts.Token);
                        var ext = Path.GetExtension(filePath).ToLowerInvariant();
                        context.Response.ContentType = ext switch
                        {
                            ".html" => "text/html",
                            ".css" => "text/css",
                            ".js" => "application/javascript",
                            ".json" => "application/json",
                            ".svg" => "image/svg+xml",
                            ".png" => "image/png",
                            _ => "application/octet-stream"
                        };
                        context.Response.ContentLength64 = content.Length;
                        await context.Response.OutputStream.WriteAsync(content, cts.Token);
                    }
                    else
                    {
                        context.Response.StatusCode = 404;
                    }
                    context.Response.Close();
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on Ctrl+C
            }
            finally
            {
                listener.Stop();
                renderer.WriteLine();
                renderer.WriteMuted("Server stopped.");
            }
        }, portOption);

        return command;
    }
}
