using LXGaming.Docker.Cli.Services.Docker;
using Spectre.Console;
using Spectre.Console.Cli;

namespace LXGaming.Docker.Cli.Commands.Update;

public class UpdateCommand : AsyncCommand<UpdateSettings> {

    public override async Task<int> ExecuteAsync(CommandContext context, UpdateSettings settings) {
        var images = await DockerService.ListImageAsync();
        if (images.Count == 0) {
            AnsiConsole.MarkupLine("[red]No images found[/]");
            return 1;
        }

        for (var index = 0; index < images.Count; index++) {
            var image = images[index];
            AnsiConsole.MarkupLine($"[yellow][[{index + 1}/{images.Count}]] Pulling[/] [blue]{image}[/]");

            try {
                await DockerService.PullImageAsync(image);
                AnsiConsole.MarkupLine($"Pulled [green]{image}[/][grey]...[/]");
            } catch (Exception ex) {
                AnsiConsole.MarkupLine($"Failed [red]{image}[/]: {ex.Message}");
            }
        }

        return 0;
    }
}