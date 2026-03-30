using System.CommandLine;

namespace SeamQ.Cli.Commands;

public static class ServeCommand
{
    public static Command Create()
    {
        var portOption = new Option<int>("--port", () => 5050, "Port number for the local web server");

        var command = new Command("serve", "Launch local web server to browse ICDs")
        {
            portOption
        };

        command.SetHandler(async (port) =>
        {
            await Task.CompletedTask; throw new NotImplementedException("Serve command not yet implemented");
        }, portOption);

        return command;
    }
}
