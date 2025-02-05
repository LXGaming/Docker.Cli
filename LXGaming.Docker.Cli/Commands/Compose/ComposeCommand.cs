using Docker.DotNet.Models;
using LXGaming.Docker.Cli.Models;
using LXGaming.Docker.Cli.Services.Docker;
using LXGaming.Docker.Cli.Services.Docker.Utilities;
using LXGaming.Docker.Cli.Utilities;
using Spectre.Console;
using Spectre.Console.Cli;

namespace LXGaming.Docker.Cli.Commands.Compose;

public class ComposeCommand : AsyncCommand<ComposeSettings> {

    public override ValidationResult Validate(CommandContext context, ComposeSettings settings) {
        if (!Directory.Exists(settings.Path)) {
            return Path.Exists(settings.Path)
                ? ValidationResult.Error("Path is not a directory")
                : ValidationResult.Error("Path does not exist");
        }

        return base.Validate(context, settings);
    }

    public override async Task<int> ExecuteAsync(CommandContext context, ComposeSettings settings) {
        var path = Path.GetFullPath(settings.Path);
        if (!Directory.Exists(path)) {
            ConsoleUtils.Error("Directory {0} does not exist", path);
            return 1;
        }

        var choices = !string.IsNullOrEmpty(settings.Name)
            ? ChoiceUtils.GetFilteredComposeFiles(path, settings.Name)
            : ChoiceUtils.GetComposeFiles(path).ToList();
        if (choices.Count == 0) {
            ConsoleUtils.Error("No compositions available");
            return 1;
        }

        Choice choice;
        var autoChoice = settings.AutoSelect ? ChoiceUtils.SingleOrDefault(choices) : null;
        if (autoChoice != null) {
            choice = autoChoice;
        } else {
            var selection = new SelectionPrompt<Choice> {
                Title = "[yellow]Select composition[/]",
                PageSize = 10,
                Mode = SelectionMode.Leaf,
                SearchEnabled = true
            };

            ChoiceUtils.AddFiles(selection, path, choices);
            choice = AnsiConsole.Prompt(selection);
        }

        var file = choice.Id;
        if (!File.Exists(file)) {
            ConsoleUtils.Error("File {0} does not exist", file);
            return 1;
        }

        var files = new List<string> { file };
        var projectName = choice.Name;

        if (!settings.SkipConfirmation && !ConsoleUtils.Confirmation("Confirm update for {0}", projectName)) {
            ConsoleUtils.Error("Cancelled");
            return 1;
        }

        var configResult = await DockerService.ConfigComposeAsync(files, projectName);
        if (configResult.ExitCode != 0) {
            return 1;
        }

        List<ContainerInspectResponse> existingContainers;
        try {
            existingContainers = await DockerService.ProcessStatusComposeAsync(files, projectName);
        } catch (Exception ex) {
            existingContainers = [];
            ConsoleUtils.Error(ex, "Encountered error while getting existing containers");
        }

        var pullResult = await DockerService.PullComposeAsync(files, projectName);
        if (pullResult.ExitCode != 0) {
            // no-op
        }

        var removeResult = await DockerService.RemoveComposeAsync(files, projectName, stop: true);
        if (removeResult.ExitCode != 0) {
            return 1;
        }

        var upResult = await DockerService.UpComposeAsync(files, projectName, noStart: true);
        if (upResult.ExitCode != 0) {
            return 1;
        }

        List<ContainerInspectResponse> containers;
        try {
            containers = await DockerService.ProcessStatusComposeAsync(files, projectName);
        } catch (Exception ex) {
            containers = [];
            ConsoleUtils.Error(ex, "Encountered error while getting containers");
        }

        if (settings.RestoreState && existingContainers.Count != 0) {
            var restoreContainers = containers
                .Where(container => {
                    return existingContainers
                        .Where(existingContainer => existingContainer.State.Running)
                        .Any(existingContainer => string.Equals(existingContainer.Name, container.Name));
                })
                .ToList();
            if (restoreContainers.Count == 0) {
                return 0;
            }

            foreach (var container in restoreContainers) {
                var containerName = container.GetName();

                ConsoleUtils.Progress("Starting {0}", containerName);
                var startResult = await DockerService.StartContainerAsync([container.ID]);
                if (startResult.ExitCode == 0) {
                    ConsoleUtils.Success("Started {0}", containerName);
                } else {
                    ConsoleUtils.Error("Failed to start {0}", containerName);
                }
            }
        } else if (ConsoleUtils.Confirmation("Start {0}", projectName)) {
            var startResult = await DockerService.StartComposeAsync(files, projectName);
            if (startResult.ExitCode != 0) {
                // no-op
            }
        }

        if (settings.CheckNames) {
            foreach (var container in containers) {
                var service = container.GetService();
                if (!string.IsNullOrEmpty(service)
                    && !string.Equals(container.Name, service, StringComparison.OrdinalIgnoreCase)) {
                    ConsoleUtils.Error("Container and Service mismatch [grey](expected {0}, got {1})[/]",
                        container.Name, service);
                }

                if (!container.IsDefaultHostname()
                    && !container.IsHostNetwork()
                    && !string.Equals(container.Name, container.Config.Hostname, StringComparison.OrdinalIgnoreCase)) {
                    ConsoleUtils.Error("Container and Hostname mismatch [grey](expected {0}, got {1})[/]",
                        container.Name, container.Config.Hostname);
                }
            }
        }

        return 0;
    }
}