using Ductus.FluentDocker.Common;
using Ductus.FluentDocker.Model.Containers;

namespace LXGaming.Docker.Cli.Utilities;

public static class DockerExtensions {

    public static void EnsureSuccess<T>(this CommandResponse<T> response, string? message = null) {
        if (response.Success) {
            return;
        }

        throw new FluentDockerException(!string.IsNullOrEmpty(message) ? $"{message}: {response.Error}" : response.Error);
    }
}