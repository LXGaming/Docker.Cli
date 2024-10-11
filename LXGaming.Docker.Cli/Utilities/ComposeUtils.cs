using Ductus.FluentDocker.Commands;
using Ductus.FluentDocker.Model.Containers;
using Ductus.FluentDocker.Services;

namespace LXGaming.Docker.Cli.Utilities;

public static class ComposeUtils {

    public static void Create(IHostService hostService, string? projectName = null, params string[] files) {
        var response = Up(hostService, projectName, true, files);
        response.EnsureSuccess($"Could not create composite service from file(s) {string.Join(", ", files)}");
    }

    public static IList<Container> List(IHostService hostService, string? projectName = null, params string[] files) {
        var response = hostService.Host.ComposePs(projectName, ["--all"], null, hostService.Certificates, files);
        response.EnsureSuccess($"Could not list composite service from file(s) {string.Join(", ", files)}");

        var ids = response.Data.ToArray();
        return ids.Length != 0 ? DockerUtils.InspectContainers(hostService, ids) : Array.Empty<Container>();
    }

    public static void Pull(IHostService hostService, string? projectName = null, params string[] files) {
        var response = hostService.Host.ComposePull(new Compose.ComposePullCommandArgs {
            AltProjectName = projectName,
            DownloadAllTagged = false,
            SkipImageVerification = false,
            Services = Array.Empty<string>(),
            Certificates = hostService.Certificates,
            ComposeFiles = files
        });
        response.EnsureSuccess($"Could not pull composite service from file(s) {string.Join(", ", files)}");
    }

    public static void Remove(IHostService hostService, string? projectName = null, bool force = false, bool removeVolumes = false, params string[] files) {
        var response = hostService.Host.ComposeRm(projectName, force, removeVolumes, null, null, hostService.Certificates, files);
        response.EnsureSuccess($"Could not remove composite service from file(s) {string.Join(", ", files)}");
    }

    public static void Start(IHostService hostService, string? projectName = null, params string[] files) {
        var response = Up(hostService, projectName, false, files);
        response.EnsureSuccess($"Could not start composite service from file(s) {string.Join(", ", files)}");
    }

    public static void Stop(IHostService hostService, string? projectName = null, TimeSpan? timeout = null, params string[] files) {
        var response = hostService.Host.ComposeStop(projectName, timeout, null, null, hostService.Certificates, files);
        response.EnsureSuccess($"Could not stop composite service from file(s) {string.Join(", ", files)}");
    }

    private static CommandResponse<IList<string>> Up(IHostService hostService, string? projectName = null, bool noStart = false, params string[] files) {
        return hostService.Host.ComposeUpCommand(new Compose.ComposeUpCommandArgs {
            AltProjectName = projectName,
            ForceRecreate = false,
            NoRecreate = false,
            DontBuild = false,
            BuildBeforeCreate = false,
            Timeout = null,
            RemoveOrphans = false,
            UseColor = false,
            NoStart = noStart,
            Services = null,
            Env = null,
            Certificates = hostService.Certificates,
            ComposeFiles = files,
            ProjectDirectory = null
        });
    }
}