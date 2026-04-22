using System.Net;
using System.Text.Json;
using Docker.DotNet;
using Docker.DotNet.Models;
using LXGaming.Common.Utilities;

namespace LXGaming.Docker.Cli.Services.Docker.Utilities;

public static class DockerUtils {

    public static JsonSerializerOptions JsonSerializerOptions { get; private set;  }

    static DockerUtils() {
        var jsonSerializerType = typeof(IDockerClient).Assembly.GetType("Docker.DotNet.JsonSerializer")!;
        var instanceProperty = ReflectionUtils.GetRequiredProperty(jsonSerializerType, "Instance", true);
        var optionsField = ReflectionUtils.GetRequiredField(jsonSerializerType, "_options", false);
        var jsonSerializer = instanceProperty.GetMethod!.Invoke(null, []);
        JsonSerializerOptions = (JsonSerializerOptions) optionsField.GetValue(jsonSerializer)!;
    }

    public static string GetName(string name) {
        return name.TrimStart('/');
    }

    public static string? GetService(IDictionary<string, string> labels) {
        return labels.TryGetValue("com.docker.compose.service", out var value) ? value : null;
    }

    public static bool IsDefaultHostname(string hostname, string id) {
        return string.Equals(hostname, id[..12]);
    }

    public static bool IsLocalHostname(string hostname) {
        string localHostname;
        try {
            localHostname = Dns.GetHostName();
        } catch (Exception) {
            localHostname = Environment.MachineName;
        }

        return string.Equals(hostname, localHostname, StringComparison.OrdinalIgnoreCase)
               || hostname.StartsWith(localHostname + '.', StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsHostNetwork(IDictionary<string, EndpointSettings> networks) {
        return networks.Count == 1 && networks.ContainsKey("host");
    }
}