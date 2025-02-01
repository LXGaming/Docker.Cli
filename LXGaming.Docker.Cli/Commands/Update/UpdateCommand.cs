﻿using LXGaming.Docker.Cli.Services.Docker;
using LXGaming.Docker.Cli.Utilities;
using Spectre.Console.Cli;

namespace LXGaming.Docker.Cli.Commands.Update;

public class UpdateCommand : AsyncCommand<UpdateSettings> {

    public override async Task<int> ExecuteAsync(CommandContext context, UpdateSettings settings) {
        var images = await DockerService.ListImageAsync();
        if (images.Count == 0) {
            ConsoleUtils.Error("No images found");
            return 1;
        }

        for (var index = 0; index < images.Count; index++) {
            var image = images[index];
            var prefix = GetPrefix(index + 1, images.Count);

            ConsoleUtils.Progress($"{prefix} Pulling {{0}}", image);

            try {
                await DockerService.PullImageAsync(image);
                ConsoleUtils.Success($"{prefix} Pulled {{0}}", image);
            } catch (Exception ex) {
                ConsoleUtils.Error($"{prefix} Failed to pull {{0}}: {{1}}", image, ex.Message);
            }
        }

        return 0;
    }

    private static string GetPrefix(int index, int size) {
        var sizeString = size.ToString();
        var indexString = index.ToString().PadLeft(sizeString.Length, ' ');

        return $"[grey][[[white]{indexString}[/]/[white]{sizeString}[/]]][/]";
    }
}