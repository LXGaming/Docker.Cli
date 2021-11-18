using System.Collections.Generic;
using Ductus.FluentDocker.Builders;
using Ductus.FluentDocker.Commands;
using Ductus.FluentDocker.Common;
using Ductus.FluentDocker.Model.Containers;
using Ductus.FluentDocker.Services;

namespace LXGaming.Docker.Cli.Utilities;

public static class DockerUtils {

    public static IHostService CreateHostService() {
        return new Builder()
            .UseHost()
            .UseNative()
            .Build();
    }

    public static Container InspectContainer(IHostService hostService, string id) {
        var result = hostService.Host.InspectContainer(id, hostService.Certificates);
        if (!result.Success) {
            throw new FluentDockerException($"Could not inspect container {id}");
        }

        return result.Data;
    }

    public static IList<Container> InspectContainers(IHostService hostService, params string[] ids) {
        var result = hostService.Host.InspectContainers(hostService.Certificates, ids);
        if (!result.Success) {
            throw new FluentDockerException($"Could not inspect container(s) {string.Join(", ", ids)}");
        }

        return result.Data;
    }

    public static void Pull(IHostService hostService, string image) {
        var result = hostService.Host.Pull(image, hostService.Certificates);
        if (!result.Success) {
            throw new FluentDockerException($"Could not pull image {image}");
        }
    }

    public static void Start(IHostService hostService, string id) {
        var result = hostService.Host.Start(id, hostService.Certificates);
        if (!result.Success) {
            throw new FluentDockerException($"Could not start container {id}");
        }
    }
}