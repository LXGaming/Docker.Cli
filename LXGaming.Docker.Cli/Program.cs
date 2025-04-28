using LXGaming.Docker.Cli.Commands.Compose;
using LXGaming.Docker.Cli.Commands.Pull;
using LXGaming.Docker.Cli.Utilities;
using Spectre.Console;
using Spectre.Console.Cli;

AppDomain.CurrentDomain.ProcessExit += (_, _) => ConsoleUtils.Shutdown();
Console.CancelKeyPress += (_, _) => ConsoleUtils.Shutdown();

var app = new CommandApp();
app.Configure(config => {
    config.SetApplicationName(Constants.Application.Name);
    config.SetApplicationVersion(Constants.Application.Version);
    config.PropagateExceptions();

    config.AddCommand<ComposeCommand>("compose");
    config.AddCommand<PullCommand>("pull");
});

try {
    return await app.RunAsync(args);
} catch (Exception ex) {
    AnsiConsole.WriteException(ex);
    return 1;
} finally {
    ConsoleUtils.Shutdown();
}