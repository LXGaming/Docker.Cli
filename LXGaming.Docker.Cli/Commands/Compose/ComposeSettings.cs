using Spectre.Console.Cli;

namespace LXGaming.Docker.Cli.Commands.Compose;

public class ComposeSettings : CommandSettings {

    [CommandArgument(0, "<path>")]
    public required string Path { get; init; }

    [CommandArgument(1, "[name]")]
    public string? Name { get; init; }

    [CommandOption("--auto-select")]
    public bool AutoSelect { get; init; }

    [CommandOption("--check-names")]
    public bool CheckNames { get; init; }

    [CommandOption("-r|--restore-state")]
    public bool RestoreState { get; init; }

    [CommandOption("--restore-state-quiet")]
    public bool RestoreStateQuiet { get; init; }

    [CommandOption("--restore-state-status")]
    public bool RestoreStateStatus { get; init; }

    [CommandOption("--skip-confirmation")]
    public bool SkipConfirmation { get; init; }
}