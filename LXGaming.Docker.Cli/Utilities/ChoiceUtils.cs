using LXGaming.Docker.Cli.Models;
using Spectre.Console;

namespace LXGaming.Docker.Cli.Utilities;

public static class ChoiceUtils {

    public static void AddFiles(SelectionPrompt<Choice> prompt, string path, IEnumerable<Choice> choices) {
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

    public static List<Choice> GetFilteredComposeFiles(string path, string name) {
        var contains = GetComposeFiles(path)
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

    public static IEnumerable<Choice> GetComposeFiles(string path) {
        return EnumerateFiles(path, SearchOption.AllDirectories, ".yaml", ".yml");
    }

    public static Choice? SingleOrDefault(ICollection<Choice> choices) {
        return choices.Count == 1 ? choices.Single() : null;
    }

    private static IEnumerable<Choice> EnumerateFiles(string path, SearchOption searchOption,
        params string[] extensions) {
        foreach (var file in Directory.EnumerateFiles(path, "*", searchOption)) {
            var extension = Path.GetExtension(file);
            if (!extensions.Contains(extension, StringComparer.OrdinalIgnoreCase)) {
                continue;
            }

            var fileName = Path.GetFileName(file);
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file);
            var name = !string.IsNullOrWhiteSpace(fileNameWithoutExtension) ? fileNameWithoutExtension : fileName;
            yield return new Choice(file, name);
        }
    }
}