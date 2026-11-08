using Microsoft.Extensions.Configuration;
using Shouldly;
using TodoApi.Telemetry;

namespace TodoApi.Tests.Telemetry;

public sealed class TelemetrySettingsTests
{
    [Fact]
    public void SectionName_IsOpenTelemetry()
    {
        TelemetrySettings.SectionName.ShouldBe("OpenTelemetry");
    }

    [Fact]
    public void FromConfiguration_BindsOpenTelemetrySection()
    {
        var configuration = Configuration(
            ("OpenTelemetry:Enabled", "false"),
            ("OpenTelemetry:ExporterEnabled", "false"),
            ("OpenTelemetry:ServiceName", "configured-service"),
            ("OpenTelemetry:Endpoint", "http://collector:4318"),
            ("OpenTelemetry:Protocol", "http/protobuf"),
            ("OpenTelemetry:SdkDisabled", "true"));

        var settings = TelemetrySettings.FromConfiguration(configuration);

        settings.Enabled.ShouldBe(false);
        settings.ExporterEnabled.ShouldBe(false);
        settings.ServiceName.ShouldBe("configured-service");
        settings.Endpoint.ShouldBe("http://collector:4318");
        settings.Protocol.ShouldBe("http/protobuf");
        settings.SdkDisabled.ShouldBeTrue();
    }

    [Fact]
    public void FromConfiguration_StandardOtelSettings_WinOverSection()
    {
        var configuration = Configuration(
            ("OpenTelemetry:ServiceName", "configured-service"),
            ("OpenTelemetry:Endpoint", "http://configured-collector:4317"),
            ("OpenTelemetry:Protocol", "grpc"),
            ("OTEL_SERVICE_NAME", "otel-service"),
            ("OTEL_EXPORTER_OTLP_ENDPOINT", "http://otel-collector:4318"),
            ("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf"));

        var settings = TelemetrySettings.FromConfiguration(configuration);

        settings.ServiceName.ShouldBe("otel-service");
        settings.Endpoint.ShouldBe("http://otel-collector:4318");
        settings.Protocol.ShouldBe("http/protobuf");
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("True", true)]
    [InlineData("false", false)]
    [InlineData("1", false)]
    [InlineData(null, false)]
    public void FromConfiguration_ParsesSdkDisabled_WithCaseInsensitiveTrue(string? value, bool expected)
    {
        var configuration = Configuration(("OTEL_SDK_DISABLED", value));

        TelemetrySettings.FromConfiguration(configuration).SdkDisabled.ShouldBe(expected);
    }

    [Theory]
    [InlineData("true", "false")]
    [InlineData("false", "true")]
    public void FromConfiguration_SdkDisabled_IsOrOfSectionAndStandardSetting(
        string sectionValue,
        string standardValue)
    {
        var configuration = Configuration(
            ("OpenTelemetry:SdkDisabled", sectionValue),
            ("OTEL_SDK_DISABLED", standardValue));

        TelemetrySettings.FromConfiguration(configuration).SdkDisabled.ShouldBeTrue();
    }

    [Fact]
    public void FromConfiguration_TrimsValues_AndIgnoresBlankValues()
    {
        var configuration = Configuration(
            ("OpenTelemetry:ServiceName", "  spaced-service  "),
            ("OpenTelemetry:Endpoint", "   "),
            ("OTEL_EXPORTER_OTLP_PROTOCOL", "  "),
            ("OTEL_SERVICE_NAME", ""));

        var settings = TelemetrySettings.FromConfiguration(configuration);

        settings.ServiceName.ShouldBe("spaced-service");
        settings.Endpoint.ShouldBeNull();
        settings.Protocol.ShouldBeNull();
    }

    [Fact]
    public void FromConfiguration_EmptyConfiguration_LeavesValuesUnset()
    {
        var settings = TelemetrySettings.FromConfiguration(new ConfigurationBuilder().Build());

        settings.Enabled.ShouldBeNull();
        settings.ExporterEnabled.ShouldBeNull();
        settings.ServiceName.ShouldBeNull();
        settings.Endpoint.ShouldBeNull();
        settings.Protocol.ShouldBeNull();
        settings.SdkDisabled.ShouldBeFalse();
    }

    private static IConfiguration Configuration(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(pair => pair.Key, pair => pair.Value))
            .Build();
}
