using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using Docker.DotNet.Models;
using LXGaming.Docker.Cli.Models;
using LXGaming.Docker.Cli.Utilities;
using Newtonsoft.Json;

namespace LXGaming.Docker.Cli.Services.Docker;

public class DockerService {

    public static Task<ProcessResult> ConfigComposeAsync(IEnumerable<string> files, string? projectName = null) {
        var startInfo = CreateStartInfo(false);
        AddComposeArguments(startInfo.ArgumentList, files, projectName);
        startInfo.ArgumentList.AddRange([
            "config",
            "--quiet"
        ]);
        return ExecuteAsync(startInfo);
    }

    public static async Task<List<ContainerInspectResponse>> ProcessStatusComposeAsync(IEnumerable<string> files,
        string? projectName = null) {
        var startInfo = CreateStartInfo(true);
        AddComposeArguments(startInfo.ArgumentList, files, projectName);
        startInfo.ArgumentList.AddRange([
            "ps",
            "--all",
            "--no-trunc",
            "--quiet"
        ]);

        var containers = new List<string>();
        var result = await ExecuteAsync(startInfo, null, (_, data) => {
            containers.Add(data);
        });

        if (result.ExitCode != 0) {
            throw new InvalidOperationException($"Unexpected ExitCode: {result.ExitCode}");
        }

        if (containers.Count == 0) {
            return [];
        }

        return await InspectContainerAsync(containers);
    }

    public static Task<ProcessResult> PullComposeAsync(IEnumerable<string> files, string? projectName = null) {
        var startInfo = CreateStartInfo(false);
        AddComposeArguments(startInfo.ArgumentList, files, projectName);
        startInfo.ArgumentList.Add("pull");
        return ExecuteAsync(startInfo);
    }

    public static Task<ProcessResult> RemoveComposeAsync(IEnumerable<string> files, string? projectName = null,
        bool stop = false, bool volumes = false) {
        var startInfo = CreateStartInfo(false);
        AddComposeArguments(startInfo.ArgumentList, files, projectName);
        startInfo.ArgumentList.AddRange([
            "rm",
            "--force"
        ]);

        if (stop) {
            startInfo.ArgumentList.Add("--stop");
        }

        if (volumes) {
            startInfo.ArgumentList.Add("--volumes");
        }

        return ExecuteAsync(startInfo);
    }

    public static Task<ProcessResult> StartComposeAsync(IEnumerable<string> files, string? projectName = null) {
        var startInfo = CreateStartInfo(false);
        AddComposeArguments(startInfo.ArgumentList, files, projectName);
        startInfo.ArgumentList.Add("start");
        return ExecuteAsync(startInfo);
    }

    public static Task<ProcessResult> UpComposeAsync(IEnumerable<string> files, string? projectName = null,
        bool forceRecreate = false, bool noStart = false, bool removeOrphans = false) {
        var startInfo = CreateStartInfo(false);
        AddComposeArguments(startInfo.ArgumentList, files, projectName);
        startInfo.ArgumentList.AddRange([
            "up",
            "--detach"
        ]);

        if (forceRecreate) {
            startInfo.ArgumentList.Add("--force-recreate");
        }

        if (noStart) {
            startInfo.ArgumentList.Add("--no-start");
        }

        if (removeOrphans) {
            startInfo.ArgumentList.Add("--remove-orphans");
        }

        return ExecuteAsync(startInfo);
    }

    public static async Task<List<ContainerInspectResponse>> InspectContainerAsync(IEnumerable<string> containers) {
        var startInfo = CreateStartInfo(true);
        startInfo.ArgumentList.AddRange([
            "container", "inspect",
            "--format", "json"
        ]);
        startInfo.ArgumentList.AddRange(containers);

        var stringBuilder = new StringBuilder();
        var result = await ExecuteAsync(startInfo, null, (_, data) => {
            stringBuilder.Append(data);
        });

        if (result.ExitCode != 0) {
            throw new InvalidOperationException($"Unexpected ExitCode: {result.ExitCode}");
        }

        return JsonConvert.DeserializeObject<List<ContainerInspectResponse>>(stringBuilder.ToString())
               ?? throw new JsonException($"Failed to deserialize {nameof(List<ContainerInspectResponse>)}");
    }

    public static Task<ProcessResult> StartContainerAsync(IEnumerable<string> containers) {
        var startInfo = CreateStartInfo(false);
        startInfo.ArgumentList.AddRange([
            "container", "start"
        ]);
        startInfo.ArgumentList.AddRange(containers);
        return ExecuteAsync(startInfo);
    }

    public static async Task<List<string>> ListImageAsync() {
        var startInfo = CreateStartInfo(true);
        startInfo.ArgumentList.AddRange([
            "image", "ls",
            "--filter", "dangling=false",
            "--format", "{{.Repository}}:{{.Tag}}"
        ]);

        var images = new List<string>();
        var result = await ExecuteAsync(startInfo, null, (_, data) => {
            images.Add(data);
        });

        if (result.ExitCode != 0) {
            throw new InvalidOperationException($"Unexpected ExitCode: {result.ExitCode}");
        }

        return images;
    }

    public static Task<ProcessResult> PullImageAsync(string image, bool quiet = false) {
        var startInfo = CreateStartInfo(quiet);
        startInfo.ArgumentList.AddRange([
            "image", "pull", image
        ]);

        if (quiet) {
            startInfo.ArgumentList.Add("--quiet");
        }

        return ExecuteAsync(startInfo);
    }

    private static void AddComposeArguments(Collection<string> arguments, IEnumerable<string> files,
        string? projectName = null) {
        arguments.Add("compose");

        foreach (var file in files) {
            arguments.Add("--file");
            arguments.Add(file);
        }

        if (!string.IsNullOrEmpty(projectName)) {
            arguments.Add("--project-name");
            arguments.Add(projectName);
        }
    }

    private static ProcessStartInfo CreateStartInfo(bool capture) {
        return CreateStartInfo(capture, capture);
    }

    private static ProcessStartInfo CreateStartInfo(bool createNoWindow, bool redirect) {
        return new ProcessStartInfo {
            FileName = "docker",
            CreateNoWindow = createNoWindow,
            UseShellExecute = false,
            RedirectStandardError = redirect,
            RedirectStandardOutput = redirect
        };
    }

    private static async Task<ProcessResult> ExecuteAsync(ProcessStartInfo startInfo,
        Action<object, string>? onError = null, Action<object, string>? onOutput = null,
        CancellationToken cancellationToken = default) {
        using var process = new Process();
        process.StartInfo = startInfo;

        if (!process.Start()) {
            throw new InvalidOperationException("Failed to start process");
        }

        DateTime startTime;
        try {
            startTime = process.StartTime;
        } catch (Exception) {
            startTime = DateTime.Now;
        }

        if (startInfo.RedirectStandardError) {
            process.ErrorDataReceived += (sender, args) => OnDataReceived(sender, args, onError);
            process.BeginErrorReadLine();
        }

        if (startInfo.RedirectStandardOutput) {
            process.OutputDataReceived += (sender, args) => OnDataReceived(sender, args, onOutput);
            process.BeginOutputReadLine();
        }

        await process.WaitForExitAsync(cancellationToken);

        DateTime exitTime;
        try {
            exitTime = process.ExitTime;
        } catch (Exception) {
            exitTime = DateTime.Now;
        }

        return new ProcessResult {
            ExitCode = process.ExitCode,
            StartTime = startTime,
            ExitTime = exitTime
        };
    }

    private static void OnDataReceived(object sender, DataReceivedEventArgs args, Action<object, string>? action) {
        var data = args.Data;
        if (string.IsNullOrWhiteSpace(data)) {
            return;
        }

        try {
            action?.Invoke(sender, data);
        } catch (Exception ex) {
            ConsoleUtils.Error(ex, "Encountered an error while invoking callback");
        }
    }
}