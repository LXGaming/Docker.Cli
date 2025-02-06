using LXGaming.Docker.Cli.Models;
using Spectre.Console.Cli;

namespace LXGaming.Docker.Cli.Commands.Update;

public class UpdateSettings : CommandSettings {

    [CommandOption("-s|--style <None|Quiet|Status>")]
    public OutputStyle Style { get; init; }
}