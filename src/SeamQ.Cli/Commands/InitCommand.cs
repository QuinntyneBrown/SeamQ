using System.CommandLine;

namespace SeamQ.Cli.Commands;

public static class InitCommand
{
    public static Command Create()
    {
        var command = new Command("init", "Generate seamq.config.json interactively");

        command.SetHandler(async () =>
        {
            throw new NotImplementedException("Init command not yet implemented");
        });

        return command;
    }
}
