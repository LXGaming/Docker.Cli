using Docker.DotNet.Models;

namespace LXGaming.Docker.Cli.Services.Docker.Utilities;

public static class DockerExtensions {

    public static string GetName(this ContainerInspectResponse response) {
        return DockerUtils.GetName(response.Name);
    }

    public static string? GetService(this ContainerInspectResponse response) {
        if (response.Config == null) {
            throw new ArgumentException("Config cannot be null.");
        }

        return DockerUtils.GetService(response.Config.Labels);
    }

    public static bool IsDefaultHostname(this ContainerInspectResponse response) {
        if (response.Config == null) {
            throw new ArgumentException("Config cannot be null.");
        }

        return DockerUtils.IsDefaultHostname(response.Config.Hostname, response.ID);
    }

    public static bool IsLocalHostname(this ContainerInspectResponse response) {
        if (response.Config == null) {
            throw new ArgumentException("Config cannot be null.");
        }

        return DockerUtils.IsLocalHostname(response.Config.Hostname);
    }

    public static bool IsHostNetwork(this ContainerInspectResponse response) {
        if (response.NetworkSettings == null) {
            throw new ArgumentException("NetworkSettings cannot be null.");
        }

        return DockerUtils.IsHostNetwork(response.NetworkSettings.Networks);
    }
}