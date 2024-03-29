﻿using Spectre.Console;

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
        AnsiConsole.Status()
            .Spinner(Spinner.Known.Ascii)
            .Start("[yellow]Initialising[/]", action);
    }
}