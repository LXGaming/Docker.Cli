using System.Globalization;
using Spectre.Console;

namespace LXGaming.Docker.Cli.Utilities;

public static class ConsoleUtils {

    public static bool Confirmation(string message, params object?[] args) {
        var value = Format(message, args);
        return AnsiConsole.Prompt(new ConfirmationPrompt($"[yellow]{value}?[/]") {
            DefaultValue = true,
            ShowChoices = false,
            ShowDefaultValue = false
        });
    }

    public static void Status(Action<StatusContext> action) {
        var status = FormatStatus("Initialising");
        CreateStatus().Start(status, action);
    }

    public static Task StatusAsync(Func<StatusContext, Task> action) {
        var status = FormatStatus("Initialising");
        return CreateStatus().StartAsync(status, action);
    }

    public static void Error(string? message, params object?[] args) {
        Error(null, message, args);
    }

    public static void Error(Exception? exception, string? message, params object?[] args) {
        Write(exception, $"[red]{message}[/]", args);
    }

    public static void Progress(string? message, params object?[] args) {
        Progress(null, message, args);
    }

    public static void Progress(Exception? exception, string? message, params object?[] args) {
        Write(exception, $"[blue]{message}[/][grey]...[/]", args);
    }

    public static void Question(string? message, params object?[] args) {
        Question(null, message, args);
    }

    public static void Question(Exception? exception, string? message, params object?[] args) {
        Write(exception, $"[yellow]{message}?[/]", args);
    }

    public static void Success(string? message, params object?[] args) {
        Success(null, message, args);
    }

    public static void Success(Exception? exception, string? message, params object?[] args) {
        Write(exception, $"[green]{message}[/]", args);
    }

    public static void Write(Exception? exception, string? message, params object?[] args) {
        if (message != null) {
            var value = Format(message, args);
            AnsiConsole.MarkupLine(value);
        } else {
            AnsiConsole.WriteLine("[null]");
        }

        if (exception != null) {
            AnsiConsole.WriteException(exception);
        }
    }

    public static string CreateListPrefix(int index, int count) {
        var countString = count.ToString();
        var indexString = (index + 1).ToString().PadLeft(countString.Length, ' ');

        return $"[grey][[[white]{indexString}[/]/[white]{countString}[/]]][/]";
    }

    public static string FormatStatus(string? message, params object?[] args) {
        return Format($"[blue]{message}[/]", args);
    }

    public static void Shutdown() {
        AnsiConsole.Cursor.Show();
        AnsiConsole.Reset();
    }

    private static Status CreateStatus() {
        return AnsiConsole.Status()
            .Spinner(Spinner.Known.Ascii);
    }

    private static string Format(string message, params object?[] args) {
        for (var index = 0; index < args.Length; index++) {
            var value = Markup.Escape(args[index]?.ToString() ?? "null");
            args[index] = $"[white]{value}[/]";
        }

        return string.Format(CultureInfo.CurrentCulture, message, args);
    }
}