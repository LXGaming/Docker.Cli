﻿using Spectre.Console.Cli;

namespace LXGaming.Docker.Cli.Commands.Compose;

public class ComposeSettings : CommandSettings {

    [CommandArgument(0, "<path>")]
    public string Path { get; init; } = string.Empty;

    [CommandArgument(1, "[name]")]
    public string Name { get; init; } = string.Empty;

    [CommandOption("-r|--restore-state")]
    public bool RestoreState { get; init; }

    [CommandOption("--check-names")]
    public bool CheckNames { get; init; }
}