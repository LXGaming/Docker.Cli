using LXGaming.Docker.Cli.Models;
using LXGaming.Docker.Cli.Services.Docker;
using LXGaming.Docker.Cli.Utilities;
using Spectre.Console;
using Spectre.Console.Cli;

namespace LXGaming.Docker.Cli.Commands.Pull;

public class PullCommand : AsyncCommand<PullSettings> {

    public override async Task<int> ExecuteAsync(CommandContext context, PullSettings settings,
        CancellationToken cancellationToken) {
        var images = await ListImageAsync();
        if (images.Count == 0) {
            ConsoleUtils.Error("No images found");
            return 1;
        }

        if (settings.Style == OutputStyle.Status) {
            await ConsoleUtils.StatusAsync(ctx => {
                return ExecuteAsync(settings.Style, images,
                    (message, args) => ctx.Status(ConsoleUtils.FormatStatus(message, args)));
            });
        } else {
            await ExecuteAsync(settings.Style, images, ConsoleUtils.Progress);
        }

        return 0;
    }

    private static async Task ExecuteAsync(OutputStyle style, List<string> images,
        Action<string?, object?[]> progress) {
        for (var index = 0; index < images.Count; index++) {
            var image = images[index];
            var prefix = ConsoleUtils.CreateListPrefix(index, images.Count);

            progress($"{prefix} Pulling {{0}}", [image]);
            var result = await DockerService.PullImageAsync(image, style is OutputStyle.Quiet or OutputStyle.Status);
            if (result.ExitCode == 0) {
                ConsoleUtils.Success(
                    style == OutputStyle.Status ? "Pulled {0}" : $"{prefix} Pulled {{0}}",
                    image);
            } else {
                ConsoleUtils.Error(
                    style == OutputStyle.Status ? "Failed to pull {0}" : $"{prefix} Failed to pull {{0}}",
                    image);
            }
        }
    }

    private static async Task<List<string>> ListImageAsync() {
        var images = await DockerService.ListImageAsync();
        return images
            .Where(image => !image.EndsWith(":<none>"))
            .OrderBy(image => image)
            .ToList();
    }
}