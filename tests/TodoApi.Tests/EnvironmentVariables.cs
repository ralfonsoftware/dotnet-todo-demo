namespace TodoApi.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class EnvironmentVariablesCollection
{
    public const string Name = "Environment variables";
}

/// <summary>
/// Temporarily removes or replaces the standard OTEL_* variables for a test and
/// restores their previous values on dispose.
/// </summary>
internal sealed class EnvironmentVariables : IDisposable
{
    private static readonly string[] Names =
    [
        "OTEL_EXPORTER_OTLP_ENDPOINT",
        "OTEL_EXPORTER_OTLP_PROTOCOL",
        "OTEL_RESOURCE_ATTRIBUTES",
        "OTEL_SDK_DISABLED",
        "OTEL_SERVICE_NAME"
    ];

    private readonly Dictionary<string, string?> original = new(StringComparer.Ordinal);

    private EnvironmentVariables()
    {
    }

    public static EnvironmentVariables Clear()
    {
        var environment = new EnvironmentVariables();

        foreach (var name in Names)
        {
            environment.Save(name);
            Environment.SetEnvironmentVariable(name, null);
        }

        return environment;
    }

    public EnvironmentVariables Set(params (string Name, string? Value)[] values)
    {
        foreach (var (name, value) in values)
        {
            Save(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        return this;
    }

    public void Dispose()
    {
        foreach (var (name, value) in original)
        {
            Environment.SetEnvironmentVariable(name, value);
        }
    }

    private void Save(string name)
    {
        original.TryAdd(name, Environment.GetEnvironmentVariable(name));
    }
}
