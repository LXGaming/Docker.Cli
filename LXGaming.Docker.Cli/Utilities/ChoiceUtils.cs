using LXGaming.Docker.Cli.Models;
using Spectre.Console;

namespace LXGaming.Docker.Cli.Utilities;

public static class ChoiceUtils {

    // https://docs.docker.com/compose/intro/compose-application-model/#the-compose-file
    private static readonly string[] ComposeFiles = [
        "compose.yaml", "compose.yml",
        "docker-compose.yaml", "docker-compose.yml"
    ];

    private static readonly string[] Extensions = [
        ".yaml", ".yml"
    ];

    public static void AddFiles(SelectionPrompt<Choice> prompt, string path, IEnumerable<Choice> choices) {
        var items = new Dictionary<string, ISelectionItem<Choice>>();
        foreach (var choice in choices.OrderBy(choice => choice.Id)) {
            var directory = GetDirectoryName(choice.Id);
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

    public static List<Choice> GetFilteredComposeProjects(string path, string name) {
        var contains = GetComposeProjects(path)
            .Where(choice => choice.Name?.Contains(name, StringComparison.OrdinalIgnoreCase) == true)
            .ToList();
        if (contains.Count == 0) {
            return [];
        }

        var equals = contains
            .Where(choice => choice.Name?.Equals(name, StringComparison.OrdinalIgnoreCase) == true)
            .ToList();
        if (equals.Count == 0) {
            return contains;
        }

        return equals;
    }

    public static IEnumerable<Choice> GetComposeProjects(string path) {
        var files = EnumerateFiles(path, SearchOption.TopDirectoryOnly, Extensions).ToList();
        var composeFile = files.FirstOrDefault(file => ComposeFiles.Contains(Path.GetFileName(file)));
        if (!string.IsNullOrEmpty(composeFile)) {
            yield return new Choice(composeFile, Path.GetFileName(path));
            yield break;
        }

        foreach (var file in files) {
            yield return new Choice(file, GetFileName(file));
        }

        foreach (var directory in Directory.EnumerateDirectories(path, "*", SearchOption.TopDirectoryOnly)) {
            foreach (var choice in GetComposeProjects(directory)) {
                yield return choice;
            }
        }
    }

    public static Choice? SingleOrDefault(ICollection<Choice> choices) {
        return choices.Count == 1 ? choices.Single() : null;
    }

    private static IEnumerable<string> EnumerateFiles(string path, SearchOption searchOption,
        params string[] extensions) {
        foreach (var file in Directory.EnumerateFiles(path, "*", searchOption)) {
            var extension = Path.GetExtension(file);
            if (!extensions.Contains(extension, StringComparer.OrdinalIgnoreCase)) {
                continue;
            }

            yield return file;
        }
    }

    private static string? GetDirectoryName(string path) {
        var directory = Path.GetDirectoryName(path);
        var file = Path.GetFileName(path);
        return ComposeFiles.Contains(file) ? Path.GetDirectoryName(directory) : directory;
    }

    private static string GetFileName(string path) {
        var fileName = Path.GetFileName(path);
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        return !string.IsNullOrWhiteSpace(fileNameWithoutExtension) ? fileNameWithoutExtension : fileName;
    }
}