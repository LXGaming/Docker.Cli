﻿using Spectre.Console.Cli;

namespace LXGaming.Docker.Cli.Commands.Update;

public class UpdateSettings : CommandSettings {

    [CommandOption("-q|--quiet")]
    public bool Quiet { get; init; }
}