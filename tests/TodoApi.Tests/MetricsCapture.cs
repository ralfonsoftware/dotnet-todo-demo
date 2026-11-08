using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Shouldly;

namespace TodoApi.Tests;

/// <summary>
/// One recorded measurement. Tags are copied out of the callback so the span
/// that owns them can be released safely.
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
/// Captures measurements from every instrument on the given meter. MeterListener
/// is process-global, so tests that use this run in a non-parallel collection
/// and dispose the capture.
/// </summary>
internal sealed class MetricsCapture : IDisposable
{
    private readonly MeterListener listener;
    private readonly ConcurrentQueue<MetricMeasurement> measurements = new();

    public MetricsCapture(string meterName)
    {
        listener = new MeterListener
        {
            InstrumentPublished = (instrument, current) =>
            {
                if (string.Equals(instrument.Meter.Name, meterName, StringComparison.Ordinal))
                {
                    current.EnableMeasurementEvents(instrument);
                }
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            Record(instrument, value, tags));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            Record(instrument, value, tags));

        listener.Start();
    }

    public IReadOnlyList<MetricMeasurement> All(string instrumentName) =>
        measurements
            .Where(measurement => string.Equals(
                measurement.InstrumentName, instrumentName, StringComparison.Ordinal))
            .ToArray();

    public MetricMeasurement Single(string instrumentName)
    {
        var matching = All(instrumentName);
        matching.Count.ShouldBe(1);
        return matching[0];
    }

    public void ObserveGauges() => listener.RecordObservableInstruments();

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

        measurements.Enqueue(new MetricMeasurement(instrument.Name, instrument.Unit, value, copied));
    }

    public void Dispose() => listener.Dispose();
}
