using System.IO;
using System.Linq;
using LXGaming.Docker.Cli.Models;
using LXGaming.Docker.Cli.Utilities;
using Spectre.Console;
using Spectre.Console.Cli;

namespace LXGaming.Docker.Cli.Commands.Compose {

    public class ComposeCommand : Command<ComposeSettings> {

        public override ValidationResult Validate(CommandContext context, ComposeSettings settings) {
            if (!Directory.Exists(settings.Path)) {
                return ValidationResult.Error($"Path not found - {settings.Path}");
            }

            return base.Validate(context, settings);
        }

        public override int Execute(CommandContext context, ComposeSettings settings) {
            var path = Path.GetFullPath(settings.Path);
            if (!Directory.Exists(path)) {
                AnsiConsole.MarkupLine($"[red]Directory not found - {settings.Path}[/]");
                return 1;
            }

            var selection = new SelectionPrompt<Choice> {
                PageSize = 10,
                Title = "[yellow]Select composition:[/]",
                Mode = SelectionMode.Leaf
            };

            selection.AddChoices(GetFiles(path));

            var directories = Directory.GetDirectories(path, "*", SearchOption.AllDirectories);
            foreach (var directory in directories) {
                var files = GetFiles(directory);
                if (files.Length == 0) {
                    continue;
                }

                selection.AddChoiceGroup(new Choice {
                    Id = directory,
                    Name = Path.GetRelativePath(path, directory)
                }, files);
            }

            var choice = AnsiConsole.Prompt(selection);
            if (!File.Exists(choice.Id)) {
                AnsiConsole.MarkupLine($"[red]File not found - {settings.Path}[/]");
                return 1;
            }

            if (!ConsoleUtils.Confirm($"[yellow]Confirm update for[/] [blue]{choice.Name}[/][yellow]?[/]")) {
                AnsiConsole.MarkupLine("[red]Cancelled[/]");
                return 1;
            }

            var hostService = DockerUtils.CreateHostService();

            AnsiConsole.Status()
                .Spinner(Spinner.Known.Ascii)
                .Start($"[yellow]Initializing[/]", ctx => {
                    ctx.Status($"[yellow]Stopping[/] [blue]{choice.Name}[/]");
                    ComposeUtils.Stop(hostService, choice.Name, null, choice.Id);
                    AnsiConsole.MarkupLine($"Stopped [green]{choice.Name}[/][grey]...[/]");

                    ctx.Status($"[yellow]Removing[/] [blue]{choice.Name}[/]");
                    ComposeUtils.Remove(hostService, choice.Name, true, false, choice.Id);
                    AnsiConsole.MarkupLine($"Removed [green]{choice.Name}[/][grey]...[/]");

                    ctx.Status($"[yellow]Pulling[/] [blue]{choice.Name}[/]");
                    ComposeUtils.Pull(hostService, choice.Name, choice.Id);
                    AnsiConsole.MarkupLine($"Pulled [green]{choice.Name}[/][grey]...[/]");

                    ctx.Status($"[yellow]Creating[/] [blue]{choice.Name}[/]");
                    ComposeUtils.Create(hostService, choice.Name, choice.Id);
                    AnsiConsole.MarkupLine($"Created [green]{choice.Name}[/][grey]...[/]");
                });

            if (!ConsoleUtils.Confirm($"[yellow]Start[/] [blue]{choice.Name}[/][yellow]?[/]")) {
                return 0;
            }

            AnsiConsole.Status()
                .Spinner(Spinner.Known.Ascii)
                .Start($"[yellow]Initializing[/]", ctx => {
                    ctx.Status($"[yellow]Starting[/] [blue]{choice.Name}[/]");
                    ComposeUtils.Start(hostService, choice.Name, choice.Id);
                    AnsiConsole.MarkupLine($"Started [green]{choice.Name}[/][grey]...[/]");
                });

            return 0;
        }

        private static Choice[] GetFiles(string path) {
            return Directory.GetFiles(path, "*.yml")
                .Select(file => {
                    var name = Path.GetFileNameWithoutExtension(file);
                    return new Choice {
                        Id = file,
                        Name = !string.IsNullOrWhiteSpace(name) ? name : Path.GetFileName(file)
                    };
                })
                .OrderBy(choice => choice.Name)
                .ToArray();
        }
    }
}