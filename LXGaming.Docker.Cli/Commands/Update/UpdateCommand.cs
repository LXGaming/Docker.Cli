using LXGaming.Docker.Cli.Services.Docker;
using LXGaming.Docker.Cli.Utilities;
using Spectre.Console.Cli;

namespace LXGaming.Docker.Cli.Commands.Update;

public class UpdateCommand : AsyncCommand<UpdateSettings> {

    public override async Task<int> ExecuteAsync(CommandContext context, UpdateSettings settings) {
        var images = await ListImageAsync();
        if (images.Count == 0) {
            ConsoleUtils.Error("No images found");
            return 1;
        }

        for (var index = 0; index < images.Count; index++) {
            var image = images[index];
            var prefix = ConsoleUtils.CreateListPrefix(index, images.Count);

            ConsoleUtils.Progress($"{prefix} Pulling {{0}}", image);
            var result = await DockerService.PullImageAsync(image, settings.Quiet);
            if (result.ExitCode == 0) {
                ConsoleUtils.Success($"{prefix} Pulled {{0}}", image);
            } else {
                ConsoleUtils.Error($"{prefix} Failed to pull {{0}}", image);
            }
        }

        return 0;
    }

    private static async Task<List<string>> ListImageAsync() {
        var images = await DockerService.ListImageAsync();
        return images
            .Where(image => !image.EndsWith(":<none>"))
            .OrderBy(image => image)
            .ToList();
    }
}