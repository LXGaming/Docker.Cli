using LXGaming.Docker.Cli.Commands.Compose;
using LXGaming.Docker.Cli.Commands.Update;
using LXGaming.Docker.Cli.Utilities;
using Spectre.Console;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(configurator => {
    configurator.SetApplicationName(Constants.Application.Name);
    configurator.SetApplicationVersion(Constants.Application.Version);
    configurator.PropagateExceptions();

    configurator.AddCommand<ComposeCommand>("compose");
    configurator.AddCommand<UpdateCommand>("update");
});

try {
    return await app.RunAsync(args);
} catch (Exception ex) {
    AnsiConsole.WriteException(ex);
    return 1;
} finally {
    AnsiConsole.Cursor.Show();
    AnsiConsole.Reset();
}