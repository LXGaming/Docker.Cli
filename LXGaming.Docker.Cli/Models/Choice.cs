namespace LXGaming.Docker.Cli.Models;

public record Choice(string Id, string? Name) {

    public override string ToString() {
        return Name ?? "null";
    }
}