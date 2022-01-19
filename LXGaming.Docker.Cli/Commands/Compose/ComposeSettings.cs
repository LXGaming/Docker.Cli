using Spectre.Console.Cli;

namespace LXGaming.Docker.Cli.Commands.Compose;

public class ComposeSettings : CommandSettings {

    [CommandArgument(0, "<path>")]
    public string Path { get; init; } = string.Empty;

    [CommandOption("-r|--restore-state")]
    public bool RestoreState { get; init; }
}