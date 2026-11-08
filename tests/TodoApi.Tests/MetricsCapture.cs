using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Shouldly;

namespace TodoApi.Tests;

/// <summary>
/// One recorded measurement. Tags are copied out of the callback because the
/// callback's tag span is only valid for the duration of the callback.
/// </summary>
internal readonly record struct MetricMeasurement(
    string InstrumentName,
    string? Unit,
    object Value,
    IReadOnlyDictionary<string, object?> Tags)
{
    public object? Tag(string key) => Tags.TryGetValue(key, out var value) ? value : null;
}

/// <summary>
/// Captures measurements from every instrument on the named meter that the
/// given <see cref="IMeterFactory"/> created. Meters from other factories, such
/// as those of parallel test hosts, are ignored, so tests that use this can run
/// in parallel. Dispose the capture at the end of the test.
/// </summary>
internal sealed class MetricsCapture : IDisposable
{
    private readonly MeterListener _listener;
    private readonly ConcurrentQueue<MetricMeasurement> _measurements = new();

    public MetricsCapture(IMeterFactory meterFactory, string meterName)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        _listener = new MeterListener
        {
            InstrumentPublished = (instrument, current) =>
            {
                if (ReferenceEquals(instrument.Meter.Scope, meterFactory)
                    && string.Equals(instrument.Meter.Name, meterName, StringComparison.Ordinal))
                {
                    current.EnableMeasurementEvents(instrument);
                }
            }
        };

        _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            Record(instrument, value, tags));
        _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            Record(instrument, value, tags));

        _listener.Start();
    }

    public IReadOnlyList<MetricMeasurement> All(string instrumentName) =>
        _measurements
            .Where(measurement => string.Equals(
                measurement.InstrumentName, instrumentName, StringComparison.Ordinal))
            .ToArray();

    public MetricMeasurement Single(string instrumentName)
    {
        var matching = All(instrumentName);
        matching.Count.ShouldBe(1);
        return matching[0];
    }

    public void ObserveGauges() => _listener.RecordObservableInstruments();

    private void Record<T>(
        Instrument instrument,
        T value,
        ReadOnlySpan<KeyValuePair<string, object?>> tags)
        where T : struct
    {
        var copied = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var tag in tags)
        {
            copied[tag.Key] = tag.Value;
        }

        _measurements.Enqueue(new MetricMeasurement(instrument.Name, instrument.Unit, value, copied));
    }

    public void Dispose() => _listener.Dispose();
}
