using Spectre.Console;

namespace LXGaming.Docker.Cli.Models;

public record Choice(string Id, string? Name) {

    public override string ToString() {
        return Markup.Escape(Name ?? "null");
    }
}