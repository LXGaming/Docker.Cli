namespace LXGaming.Docker.Cli.Models;

public record ProcessResult {

    public int ExitCode { get; init; }

    public DateTime StartTime { get; init; }

    public DateTime ExitTime { get; init; }
}