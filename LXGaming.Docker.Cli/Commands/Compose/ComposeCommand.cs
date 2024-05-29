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

        var choices = new Dictionary<Choice, List<Choice>>();
        AppendFiles(choices, path);
        foreach (var directory in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories)) {
            AppendFiles(choices, directory, Path.GetRelativePath(path, directory));
        }

        if (!string.IsNullOrEmpty(settings.Name)) {
            FilterChoices(choices, settings.Name);
        }

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

            foreach (var (key, value) in choices) {
                if (string.Equals(key.Id, path)) {
                    selection.AddChoices(value);
                } else {
                    selection.AddChoiceGroup(key, value);
                }
            }

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

    private static void AppendFiles(Dictionary<Choice, List<Choice>> choices, string path, string? name = null) {
        var files = GetFiles(path);
        if (files.Count == 0) {
            return;
        }

        choices.Add(new Choice(path, name), files);
    }

    private static void FilterChoices(Dictionary<Choice, List<Choice>> choices, string name) {
        foreach (var key in choices.Keys.ToList()) {
            var value = choices[key];
            value.RemoveAll(choice => !string.Equals(choice.Name, name, StringComparison.OrdinalIgnoreCase));
            if (value.Count == 0) {
                choices.Remove(key);
            }
        }
    }

    private static Choice? GetAutoChoice(Dictionary<Choice, List<Choice>> choices) {
        if (choices.Count == 1) {
            var (_, value) = choices.Single();
            if (value.Count == 1) {
                return value.Single();
            }
        }

        return null;
    }

    private static List<Choice> GetFiles(string path) {
        return Directory.EnumerateFiles(path, "*.yml")
            .Select(file => {
                var name = Path.GetFileNameWithoutExtension(file);
                return new Choice(file, !string.IsNullOrWhiteSpace(name) ? name : Path.GetFileName(file));
            })
            .OrderBy(choice => choice.Name)
            .ToList();
    }
}