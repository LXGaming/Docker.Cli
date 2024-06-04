using Ductus.FluentDocker.Common;
using LXGaming.Docker.Cli.Models;
using LXGaming.Docker.Cli.Utilities;
using Spectre.Console;
using Spectre.Console.Cli;

namespace LXGaming.Docker.Cli.Commands.Compose;

public class ComposeCommand : Command<ComposeSettings> {

    public override ValidationResult Validate(CommandContext context, ComposeSettings settings) {
        if (!Directory.Exists(settings.Path)) {
            return ValidationResult.Error($"Path does not exist: {settings.Path}");
        }

        return base.Validate(context, settings);
    }

    public override int Execute(CommandContext context, ComposeSettings settings) {
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

        var hostService = DockerUtils.CreateHostService();

        var existingContainers = ComposeUtils.List(hostService, choice.Name, choice.Id);

        ConsoleUtils.Status(ctx => {
            ctx.Status($"[yellow]Stopping[/] [blue]{choice.Name}[/]");
            ComposeUtils.Stop(hostService, choice.Name, null, choice.Id);
            AnsiConsole.MarkupLine($"Stopped [green]{choice.Name}[/][grey]...[/]");

            ctx.Status($"[yellow]Removing[/] [blue]{choice.Name}[/]");
            ComposeUtils.Remove(hostService, choice.Name, true, false, choice.Id);
            AnsiConsole.MarkupLine($"Removed [green]{choice.Name}[/][grey]...[/]");

            ctx.Status($"[yellow]Pulling[/] [blue]{choice.Name}[/]");
            try {
                ComposeUtils.Pull(hostService, choice.Name, choice.Id);
                AnsiConsole.MarkupLine($"Pulled [green]{choice.Name}[/][grey]...[/]");
            } catch (FluentDockerException ex) {
                AnsiConsole.MarkupLine($"Failed [red]{choice.Name}[/]: {ex.Message}");
            }

            ctx.Status($"[yellow]Creating[/] [blue]{choice.Name}[/]");
            ComposeUtils.Create(hostService, choice.Name, choice.Id);
            AnsiConsole.MarkupLine($"Created [green]{choice.Name}[/][grey]...[/]");
        });

        var containers = ComposeUtils.List(hostService, choice.Name, choice.Id);

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

            ConsoleUtils.Status(ctx => {
                foreach (var container in restoreContainers) {
                    ctx.Status($"[yellow]Starting[/] [blue]{container.Name}[/]");
                    DockerUtils.Start(hostService, container.Id);
                    AnsiConsole.MarkupLine($"Started [green]{container.Name}[/][grey]...[/]");
                }
            });
        } else if (ConsoleUtils.Confirm($"[yellow]Start[/] [blue]{choice.Name}[/][yellow]?[/]")) {
            ConsoleUtils.Status(ctx => {
                ctx.Status($"[yellow]Starting[/] [blue]{choice.Name}[/]");
                ComposeUtils.Start(hostService, choice.Name, choice.Id);
                AnsiConsole.MarkupLine($"Started [green]{choice.Name}[/][grey]...[/]");
            });
        }

        if (settings.CheckNames) {
            foreach (var container in containers) {
                if (!container.Config.Labels.TryGetValue("com.docker.compose.service", out var service)) {
                    continue;
                }

                if (!string.Equals(container.Name, service, StringComparison.OrdinalIgnoreCase)) {
                    AnsiConsole.MarkupLine($"[red]Container and Service name mismatch (expected {container.Name}, got {service})[/]");
                }
            }
        }

        return 0;
    }

    private static void AddChoices(SelectionPrompt<Choice> prompt, string path, IEnumerable<Choice> choices) {
        var items = new Dictionary<string, ISelectionItem<Choice>>();
        foreach (var choice in choices) {
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

        return filteredChoices;
    }

    private static HashSet<Choice> GetChoices(string path) {
        var choices = new HashSet<Choice>();

        foreach (var file in EnumerateFiles(path, SearchOption.AllDirectories, ".yml")) {
            var fileName = Path.GetFileName(file);
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

            var name = !string.IsNullOrWhiteSpace(fileNameWithoutExtension) ? fileNameWithoutExtension : fileName;

            choices.Add(new Choice(file, name));
        }

        return choices;
    }
}