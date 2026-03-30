using System.CommandLine;
using SeamQ.Cli;

var rootCommand = CommandBuilder.BuildRootCommand();
return await rootCommand.InvokeAsync(args);
