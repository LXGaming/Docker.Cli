using System.Reflection;

namespace LXGaming.Docker.Cli.Utilities; 

public static class Constants {

    public static class Application {

        public const string Name = "Docker.Cli";
        
        public static readonly string Version = Toolbox.GetAssemblyVersion(Assembly.GetExecutingAssembly());
    }
}