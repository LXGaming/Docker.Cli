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
        if (context.Remaining.Raw.Count == 0) {
            return ValidationResult.Error("Missing compose arguments");
        }

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

        Dictionary<string, ContainerState> containerStates;
        if (settings.RestoreState) {
            try {
                containerStates = await GetContainerStatesAsync(files, projectName);
            } catch (Exception ex) {
                ConsoleUtils.Error(ex, "Encountered error while getting container states");
                containerStates = new Dictionary<string, ContainerState>();
            }
        } else {
            containerStates = new Dictionary<string, ContainerState>();
        }

        var result = await DockerService.ComposeAsync(files, projectName, context.Remaining.Raw);
        if (result.ExitCode != 0) {
            return 1;
        }

        var lazyContainers = new Lazy<Task<List<ContainerInspectResponse>>>(async () => {
            try {
                return await DockerService.ProcessStatusComposeAsync(files, projectName);
            } catch (Exception ex) {
                ConsoleUtils.Error(ex, "Encountered error while getting containers");
                return [];
            }
        });

        if (settings.RestoreState && containerStates.Count != 0) {
            var containers = await lazyContainers.Value;
            if (settings.Style == OutputStyle.Status) {
                await ConsoleUtils.StatusAsync(ctx => {
                    return RestoreStateAsync(settings.Style, containers, containerStates,
                        (message, args) => ctx.Status(ConsoleUtils.FormatStatus(message, args)));
                });
            } else {
                await RestoreStateAsync(settings.Style, containers, containerStates, ConsoleUtils.Progress);
            }
        } else if (settings.PromptStart && ConsoleUtils.Confirmation("Start {0}", projectName)) {
            var startResult = await DockerService.StartComposeAsync(files, projectName);
            if (startResult.ExitCode != 0) {
                // no-op
            }
        }

        if (settings.CheckNames) {
            var containers = await lazyContainers.Value;
            foreach (var container in containers) {
                var containerName = container.GetName();
                var service = container.GetService();

                if (!string.IsNullOrEmpty(service)
                    && !string.Equals(containerName, service, StringComparison.OrdinalIgnoreCase)) {
                    ConsoleUtils.Error("Container and Service mismatch [grey](expected {0}, got {1})[/]",
                        containerName, service);
                }

                if (!container.IsDefaultHostname()
                    && !container.IsHostNetwork()
                    && !string.Equals(containerName, container.Config.Hostname, StringComparison.OrdinalIgnoreCase)) {
                    ConsoleUtils.Error("Container and Hostname mismatch [grey](expected {0}, got {1})[/]",
                        containerName, container.Config.Hostname);
                }
            }
        }

        return 0;
    }

    private static async Task RestoreStateAsync(OutputStyle style, List<ContainerInspectResponse> containers,
        Dictionary<string, ContainerState> containerStates, Action<string?, object?[]> progress) {
        for (var index = 0; index < containers.Count; index++) {
            var container = containers[index];
            var containerName = container.GetName();
            if (!containerStates.TryGetValue(containerName, out var containerState)) {
                continue;
            }

            var prefix = ConsoleUtils.CreateListPrefix(index, containers.Count);
            if (containerState.Running) {
                progress($"{prefix} Starting {{0}}", [containerName]);
                var startResult = await DockerService.StartContainerAsync([container.ID],
                    style is OutputStyle.Quiet or OutputStyle.Status);
                if (startResult.ExitCode == 0) {
                    ConsoleUtils.Success(
                        style == OutputStyle.Status ? "Started {0}" : $"{prefix} Started {{0}}",
                        containerName);
                } else {
                    ConsoleUtils.Error(
                        style == OutputStyle.Status ? "Failed to start {0}" : $"{prefix} Failed to start {{0}}",
                        containerName);
                }
            }
        }
    }

    private static async Task<Dictionary<string, ContainerState>> GetContainerStatesAsync(IEnumerable<string> files,
        string? projectName = null) {
        var containers = await DockerService.ProcessStatusComposeAsync(files, projectName);
        return containers.ToDictionary(container => container.GetName(), container => container.State);
    }
}