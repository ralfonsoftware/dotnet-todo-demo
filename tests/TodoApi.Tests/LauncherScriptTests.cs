using System.Diagnostics;
using System.Runtime.Versioning;
using Shouldly;

namespace TodoApi.Tests;

[UnsupportedOSPlatform("windows")]
public sealed class LauncherScriptTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string ScriptPath =
        Path.Combine(RepositoryRoot, "scripts", "run-aspire-dashboard.sh");

    [Fact]
    public void Script_Exists_And_IsExecutable()
    {
        if (OperatingSystem.IsWindows()) Assert.Skip("The launcher is a POSIX shell script.");

        File.Exists(ScriptPath).ShouldBeTrue();

        var mode = File.GetUnixFileMode(ScriptPath);
        (mode & UnixFileMode.UserExecute).ShouldBe(UnixFileMode.UserExecute);
        (mode & UnixFileMode.GroupExecute).ShouldBe(UnixFileMode.GroupExecute);
        (mode & UnixFileMode.OtherExecute).ShouldBe(UnixFileMode.OtherExecute);
    }

    [Fact]
    public async Task Script_FailsClearly_WhenDockerIsUnavailable()
    {
        if (OperatingSystem.IsWindows()) Assert.Skip("The launcher is a POSIX shell script.");

        var result = await RunAsync(new Dictionary<string, string?>
        {
            ["DOCKER_BIN"] = "/nonexistent/docker"
        });

        result.ExitCode.ShouldNotBe(0);
        (result.StandardError + result.StandardOutput).ShouldContain("Docker");
    }

    [Fact]
    public async Task Script_RunsPinnedDashboardImage_WithRequiredPortsAndAnonymousAccess()
    {
        if (OperatingSystem.IsWindows()) Assert.Skip("The launcher is a POSIX shell script.");

        var fakeDocker = Path.GetTempFileName();
        var argumentsLog = Path.GetTempFileName();

        try
        {
            await File.WriteAllTextAsync(fakeDocker,
                """
                #!/usr/bin/env bash
                printf '%s\n' "$*" >> "${DOCKER_ARGS_FILE:?}"
                exit 0
                """, TestContext.Current.CancellationToken);

            File.SetUnixFileMode(fakeDocker,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

            var result = await RunAsync(new Dictionary<string, string?>
            {
                ["DOCKER_BIN"] = fakeDocker,
                ["DOCKER_ARGS_FILE"] = argumentsLog
            });

            result.ExitCode.ShouldBe(0, result.StandardError);

            var invocations = await File.ReadAllLinesAsync(argumentsLog, TestContext.Current.CancellationToken);
            var run = invocations
                .Where(line => line.StartsWith("run ", StringComparison.Ordinal))
                .ShouldHaveSingleItem();

            run.ShouldContain("--rm");
            run.ShouldContain("--name todo-api-aspire-dashboard");
            run.ShouldContain("--publish 18888:18888");
            run.ShouldContain("--publish 4317:18889");
            run.ShouldContain("--publish 4318:18890");
            run.ShouldContain("--env DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true");
            run.ShouldContain("mcr.microsoft.com/aspire/dashboard:13.6.4");
            run.ShouldNotContain(":latest");

            invocations.ShouldContain(line =>
                line.StartsWith("rm --force todo-api-aspire-dashboard", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(fakeDocker);
            File.Delete(argumentsLog);
        }
    }

    private static async Task<ScriptResult> RunAsync(Dictionary<string, string?> environment)
    {
        var startInfo = new ProcessStartInfo("/bin/bash")
        {
            WorkingDirectory = RepositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add(ScriptPath);

        foreach (var (name, value) in environment)
        {
            startInfo.Environment[name] = value;
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the launcher script.");

        var standardOutput = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);

        await process.WaitForExitAsync(TestContext.Current.CancellationToken);

        return new ScriptResult(
            process.ExitCode,
            await standardOutput,
            await standardError);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TodoApi.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate the repository root.");
    }

    private sealed record ScriptResult(int ExitCode, string StandardOutput, string StandardError);
}
