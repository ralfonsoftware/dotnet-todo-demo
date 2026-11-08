using System.Collections.Concurrent;
using System.Diagnostics;
using Shouldly;

namespace TodoApi.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TracingCollection
{
    public const string Name = "Tracing";
}

/// <summary>
/// Captures activities from the given sources. Activities are recorded on
/// start so assertions never race the end of a request span.
/// </summary>
internal sealed class ActivityCapture : IDisposable
{
    private readonly ActivityListener listener;
    private readonly ConcurrentQueue<Activity> started = new();

    public ActivityCapture(params string[] sourceNames)
    {
        listener = new ActivityListener
        {
            ShouldListenTo = source => sourceNames.Contains(source.Name, StringComparer.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = started.Enqueue
        };

        ActivitySource.AddActivityListener(listener);
    }

    public IReadOnlyList<Activity> From(string sourceName) =>
        started.Where(activity => activity.Source.Name == sourceName).ToArray();

    public Activity Single(string sourceName)
    {
        var activities = From(sourceName);
        activities.Count.ShouldBe(1);
        return activities[0];
    }

    public void Dispose() => listener.Dispose();
}
