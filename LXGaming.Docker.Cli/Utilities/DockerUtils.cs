using Ductus.FluentDocker.Builders;
using Ductus.FluentDocker.Commands;
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
        var response = hostService.Host.InspectContainer(id, hostService.Certificates);
        response.EnsureSuccess($"Could not inspect container {id}");
        return response.Data;
    }

    public static IList<Container> InspectContainers(IHostService hostService, params string[] ids) {
        var response = hostService.Host.InspectContainers(hostService.Certificates, ids);
        response.EnsureSuccess($"Could not inspect container(s) {string.Join(", ", ids)}");
        return response.Data;
    }

    public static void Pull(IHostService hostService, string image) {
        var response = hostService.Host.Pull(image, hostService.Certificates);
        response.EnsureSuccess($"Could not pull image {image}");
    }

    public static void Start(IHostService hostService, string id) {
        var response = hostService.Host.Start(id, hostService.Certificates);
        response.EnsureSuccess($"Could not start container {id}");
    }
}