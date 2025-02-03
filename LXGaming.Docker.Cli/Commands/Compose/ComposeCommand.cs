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
            return ValidationResult.Error($"Path does not exist: {settings.Path}");
        }

        return base.Validate(context, settings);
    }

    public override async Task<int> ExecuteAsync(CommandContext context, ComposeSettings settings) {
        var path = Path.GetFullPath(settings.Path);
        if (!Directory.Exists(path)) {
            ConsoleUtils.Error("Directory does not exist {0}", path);
            return 1;
        }

        var choices = !string.IsNullOrEmpty(settings.Name)
            ? GetFilteredChoices(settings.Path, settings.Name)
            : GetChoices(settings.Path);
        if (choices.Count == 0) {
            ConsoleUtils.Error("No compositions available");
            return 1;
        }

        Choice choice;
        var autoChoice = settings.AutoSelect ? GetAutoChoice(choices) : null;
        if (autoChoice != null) {
            choice = autoChoice;
        } else {
            var selection = new SelectionPrompt<Choice> {
                Title = "[yellow]Select composition[/]",
                PageSize = 10,
                Mode = SelectionMode.Leaf,
                SearchEnabled = true
            };

            AddChoices(selection, path, choices);
            choice = AnsiConsole.Prompt(selection);
        }

        var file = choice.Id;
        if (!File.Exists(file)) {
            ConsoleUtils.Error("File does not exist {0}", file);
            return 1;
        }

        var files = new List<string> { file };
        var projectName = choice.Name;

        if (!settings.SkipConfirmation && !ConsoleUtils.Confirmation("Confirm update for {0}", projectName)) {
            ConsoleUtils.Error("Cancelled");
            return 1;
        }

        ConsoleUtils.Progress("Validating {0}", projectName);
        var configResult = await DockerService.ConfigComposeAsync(files, projectName);
        if (configResult.ExitCode == 0) {
            ConsoleUtils.Success("Validated {0}", projectName);
        } else {
            ConsoleUtils.Error("Failed to validate {0}", projectName);
            return 1;
        }

        List<ContainerInspectResponse> existingContainers;
        try {
            existingContainers = await DockerService.ProcessStatusComposeAsync(files, projectName);
        } catch (Exception ex) {
            existingContainers = [];
            ConsoleUtils.Error(ex, "Encountered error while getting existing containers");
        }

        ConsoleUtils.Progress("Pulling {0}", projectName);
        var pullResult = await DockerService.PullComposeAsync(files, projectName);
        if (pullResult.ExitCode == 0) {
            ConsoleUtils.Success("Pulled {0}", projectName);
        } else {
            ConsoleUtils.Error("Failed to pull {0}", projectName);
        }

        ConsoleUtils.Progress("Removing {0}", projectName);
        var removeResult = await DockerService.RemoveComposeAsync(files, projectName, stop: true);
        if (removeResult.ExitCode == 0) {
            ConsoleUtils.Success("Removed {0}", projectName);
        } else {
            ConsoleUtils.Error("Failed to remove {0}", projectName);
            return 1;
        }

        ConsoleUtils.Progress("Creating {0}", projectName);
        var upResult = await DockerService.UpComposeAsync(files, projectName, noStart: true);
        if (upResult.ExitCode == 0) {
            ConsoleUtils.Success("Created {0}", projectName);
        } else {
            ConsoleUtils.Error("Failed to create {0}", projectName);
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
            ConsoleUtils.Progress("Starting {0}", projectName);
            var startResult = await DockerService.StartComposeAsync(files, projectName);
            if (startResult.ExitCode == 0) {
                ConsoleUtils.Success("Started {0}", projectName);
            } else {
                ConsoleUtils.Error("Failed to start {0}", projectName);
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

    private static void AddChoices(SelectionPrompt<Choice> prompt, string path, IEnumerable<Choice> choices) {
        var items = new Dictionary<string, ISelectionItem<Choice>>();
        foreach (var choice in choices.OrderBy(choice => choice.Id)) {
            var directory = Path.GetDirectoryName(choice.Id);
            if (string.IsNullOrEmpty(directory) || string.Equals(path, directory)) {
                prompt.AddChoice(choice);
                continue;
            }

            var relativePath = Path.GetRelativePath(path, directory);
            if (string.IsNullOrEmpty(relativePath) || string.Equals(relativePath, ".")) {
                prompt.AddChoice(choice);
                continue;
            }

            if (items.TryGetValue(relativePath, out var existingItem)) {
                existingItem.AddChild(choice);
                continue;
            }

            var item = prompt.AddChoice(new Choice(directory, relativePath));
            item.AddChild(choice);
            items.Add(relativePath, item);
        }
    }

    private static IEnumerable<string> EnumerateFiles(string path, SearchOption searchOption, params string[] extensions) {
        foreach (var file in Directory.EnumerateFiles(path, "*", searchOption)) {
            var extension = Path.GetExtension(file);
            if (extensions.Contains(extension, StringComparer.OrdinalIgnoreCase)) {
                yield return file;
            }
        }
    }

    private static Choice? GetAutoChoice(ICollection<Choice> choices) {
        return choices.Count == 1 ? choices.Single() : null;
    }

    private static HashSet<Choice> GetFilteredChoices(string path, string name) {
        var choices = GetChoices(path);
        if (choices.Count == 0) {
            return choices;
        }

        var filteredChoices = new HashSet<Choice>();

        foreach (var choice in choices) {
            if (choice.Name?.Equals(name, StringComparison.OrdinalIgnoreCase) == true) {
                filteredChoices.Add(choice);
            }
        }

        if (filteredChoices.Count != 0) {
            return filteredChoices;
        }

        foreach (var choice in choices) {
            if (choice.Name?.Contains(name, StringComparison.OrdinalIgnoreCase) == true) {
                filteredChoices.Add(choice);
            }
        }

        return filteredChoices;
    }

    private static HashSet<Choice> GetChoices(string path) {
        var choices = new HashSet<Choice>();

        foreach (var file in EnumerateFiles(path, SearchOption.AllDirectories, ".yaml", ".yml")) {
            var fileName = Path.GetFileName(file);
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

            var name = !string.IsNullOrWhiteSpace(fileNameWithoutExtension) ? fileNameWithoutExtension : fileName;

            choices.Add(new Choice(file, name));
        }

        return choices;
    }
}