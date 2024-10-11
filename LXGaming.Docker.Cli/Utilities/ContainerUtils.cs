using Ductus.FluentDocker.Model.Containers;

namespace LXGaming.Docker.Cli.Utilities;

public static class ContainerUtils {

    public static string? GetService(Container container) {
        return container.Config.Labels.TryGetValue("com.docker.compose.service", out var value) ? value : null;
    }

    public static bool IsDefaultHostname(Container container) {
        return string.Equals(container.Config.Hostname, container.Id[..12]);
    }

    public static bool IsHostNetwork(Container container) {
        return container.NetworkSettings.Networks.Count == 1 && container.NetworkSettings.Networks.ContainsKey("host");
    }
}