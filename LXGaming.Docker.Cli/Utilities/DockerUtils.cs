using Ductus.FluentDocker.Builders;
using Ductus.FluentDocker.Commands;
using Ductus.FluentDocker.Common;
using Ductus.FluentDocker.Services;

namespace LXGaming.Docker.Cli.Utilities {

    public static class DockerUtils {

        public static IHostService CreateHostService() {
            return new Builder()
                .UseHost()
                .UseNative()
                .Build();
        }

        public static void Pull(IHostService hostService, string image) {
            var result = hostService.Host.Pull(image, hostService.Certificates);
            if (!result.Success) {
                throw new FluentDockerException($"Could not pull image {image}");
            }
        }
    }
}