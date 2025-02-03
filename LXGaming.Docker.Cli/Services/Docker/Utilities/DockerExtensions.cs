using Docker.DotNet.Models;

namespace LXGaming.Docker.Cli.Services.Docker.Utilities;

public static class DockerExtensions {

    public static string GetName(this ContainerInspectResponse response) {
        return DockerUtils.GetName(response.Name);
    }

    public static string? GetService(this ContainerInspectResponse response) {
        return DockerUtils.GetService(response.Config.Labels);
    }

    public static bool IsDefaultHostname(this ContainerInspectResponse response) {
        return DockerUtils.IsDefaultHostname(response.Config.Hostname, response.ID);
    }

    public static bool IsHostNetwork(this ContainerInspectResponse response) {
        return DockerUtils.IsHostNetwork(response.NetworkSettings.Networks);
    }
}