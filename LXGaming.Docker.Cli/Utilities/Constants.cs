using LXGaming.Common.Utilities;

namespace LXGaming.Docker.Cli.Utilities;

public static class Constants {

    public static class Application {

        public const string Name = "Docker.Cli";

        public static readonly string Version = AssemblyUtils.GetVersion(typeof(Constants).Assembly) ?? "Unknown";
    }
}