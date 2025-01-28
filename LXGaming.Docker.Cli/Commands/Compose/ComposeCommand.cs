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
            AnsiConsole.MarkupLine($"[red]Directory does not exist: {path}[/]");
            return 1;
        }

        var choices = !string.IsNullOrEmpty(settings.Name)
            ? GetFilteredChoices(settings.Path, settings.Name)
            : GetChoices(settings.Path);
        if (choices.Count == 0) {
            AnsiConsole.MarkupLine($"[red]No compositions available[/]");
            return 1;
        }

        Choice choice;
        var autoChoice = settings.AutoSelect ? GetAutoChoice(choices) : null;
        if (autoChoice != null) {
            choice = autoChoice;
        } else {
            var selection = new SelectionPrompt<Choice> {
                Title = "[yellow]Select composition:[/]",
                PageSize = 10,
                Mode = SelectionMode.Leaf,
                SearchEnabled = true
            };

            AddChoices(selection, path, choices);
            choice = AnsiConsole.Prompt(selection);
        }

        if (!File.Exists(choice.Id)) {
            AnsiConsole.MarkupLine($"[red]File does not exist: {choice.Id}[/]");
            return 1;
        }

        if (!settings.SkipConfirmation && !ConsoleUtils.Confirm($"[yellow]Confirm update for[/] [blue]{choice.Name}[/][yellow]?[/]")) {
            AnsiConsole.MarkupLine("[red]Cancelled[/]");
            return 1;
        }

        var existingContainers = await DockerService.ProcessStatusComposeAsync([choice.Id], choice.Name);

        AnsiConsole.MarkupLine($"[yellow]Pulling[/] [blue]{choice.Name}[/]");
        try {
            await DockerService.PullComposeAsync([choice.Id], choice.Name);
            AnsiConsole.MarkupLine($"Pulled [green]{choice.Name}[/][grey]...[/]");
        } catch (Exception ex) {
            AnsiConsole.MarkupLine($"Failed [red]{choice.Name}[/]: {ex.Message}");
        }

        AnsiConsole.MarkupLine($"[yellow]Stopping[/] [blue]{choice.Name}[/]");
        await DockerService.StopComposeAsync([choice.Id], choice.Name);
        AnsiConsole.MarkupLine($"Stopped [green]{choice.Name}[/][grey]...[/]");

        AnsiConsole.MarkupLine($"[yellow]Removing[/] [blue]{choice.Name}[/]");
        await DockerService.RemoveComposeAsync([choice.Id], choice.Name, stop: true);
        AnsiConsole.MarkupLine($"Removed [green]{choice.Name}[/][grey]...[/]");

        AnsiConsole.MarkupLine($"[yellow]Creating[/] [blue]{choice.Name}[/]");
        await DockerService.UpComposeAsync([choice.Id], choice.Name, noStart: true);
        AnsiConsole.MarkupLine($"Created [green]{choice.Name}[/][grey]...[/]");

        var containers = await DockerService.ProcessStatusComposeAsync([choice.Id], choice.Name);

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
                AnsiConsole.MarkupLine($"[yellow]Starting[/] [blue]{container.Name}[/]");
                await DockerService.StartContainerAsync([container.ID]);
                AnsiConsole.MarkupLine($"Started [green]{container.Name}[/][grey]...[/]");
            }
        } else if (ConsoleUtils.Confirm($"[yellow]Start[/] [blue]{choice.Name}[/][yellow]?[/]")) {
            AnsiConsole.MarkupLine($"[yellow]Starting[/] [blue]{choice.Name}[/]");
            await DockerService.StartComposeAsync([choice.Id], choice.Name);
            AnsiConsole.MarkupLine($"Started [green]{choice.Name}[/][grey]...[/]");
        }

        if (settings.CheckNames) {
            foreach (var container in containers) {
                var service = container.GetService();
                if (!string.IsNullOrEmpty(service)
                    && !string.Equals(container.Name, service, StringComparison.OrdinalIgnoreCase)) {
                    AnsiConsole.MarkupLine($"[red]Container and Service mismatch (expected {container.Name}, got {service})[/]");
                }

                if (!container.IsDefaultHostname()
                    && !container.IsHostNetwork()
                    && !string.Equals(container.Name, container.Config.Hostname, StringComparison.OrdinalIgnoreCase)) {
                    AnsiConsole.MarkupLine($"[red]Container and Hostname mismatch (expected {container.Name}, got {container.Config.Hostname})[/]");
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