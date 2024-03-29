﻿using Ductus.FluentDocker.Common;
using LXGaming.Docker.Cli.Utilities;
using Spectre.Console;
using Spectre.Console.Cli;

namespace LXGaming.Docker.Cli.Commands.Update;

public class UpdateCommand : Command<UpdateSettings> {

    public override int Execute(CommandContext context, UpdateSettings settings) {
        var hostService = DockerUtils.CreateHostService();
        var images = hostService.GetImages()
            .Where(image => !string.Equals(image.Tag, "<none>"))
            .Select(image => $"{image.Name}:{image.Tag}")
            .OrderBy(image => image)
            .ToList();
        if (images.Count == 0) {
            AnsiConsole.MarkupLine("[red]No images found[/]");
            return 1;
        }

        ConsoleUtils.Status(ctx => {
            for (var index = 0; index < images.Count; index++) {
                var image = images[index];
                ctx.Status($"[yellow][[{index + 1}/{images.Count}]] Pulling[/] [blue]{image}[/]");

                try {
                    DockerUtils.Pull(hostService, image);
                    AnsiConsole.MarkupLine($"Pulled [green]{image}[/][grey]...[/]");
                } catch (FluentDockerException ex) {
                    AnsiConsole.MarkupLine($"Failed [red]{image}[/]: {ex.Message}");
                }
            }
        });

        return 0;
    }
}