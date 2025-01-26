using Spectre.Console;

namespace LXGaming.Docker.Cli.Utilities;

public static class ConsoleUtils {

    public static bool Confirm(string prompt) {
        return AnsiConsole.Prompt(new ConfirmationPrompt(prompt) {
            DefaultValue = true,
            ShowChoices = false,
            ShowDefaultValue = false
        });
    }

    public static void Status(Action<StatusContext> action) {
        CreateStatus().Start("[yellow]Initialising[/]", action);
    }

    public static Task StatusAsync(Func<StatusContext, Task> action) {
        return CreateStatus().StartAsync("[yellow]Initialising[/]", action);
    }

    private static Status CreateStatus() {
        return AnsiConsole.Status()
            .Spinner(Spinner.Known.Ascii);
    }
}