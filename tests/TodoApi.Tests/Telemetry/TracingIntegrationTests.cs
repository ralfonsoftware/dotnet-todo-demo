using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TodoApi.Contracts;
using TodoApi.Storage;

namespace TodoApi.Tests.Telemetry;

[Collection(TracingCollection.Name)]
public sealed class TracingIntegrationTests
{
    [Fact]
    public async Task StoreSpan_IsAChildOfTheHttpRequestSpan()
    {
        using var capture = new ActivityCapture(
            TracingTodoStore.ActivitySourceName,
            "Microsoft.AspNetCore");
        using var factory = new TelemetryEnabledFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/todos",
            new { title = "Trace me 9a41", description = "Private detail 9a41", priority = "high" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var storeSpan = capture.Single(TracingTodoStore.ActivitySourceName);
        var serverSpan = capture.From("Microsoft.AspNetCore")
            .Single(activity => activity.Kind == ActivityKind.Server);

        storeSpan.Kind.ShouldBe(ActivityKind.Internal);
        storeSpan.TraceId.ShouldBe(serverSpan.TraceId);
        storeSpan.ParentSpanId.ShouldBe(serverSpan.SpanId);
        storeSpan.GetTagItem("todo.operation").ShouldBe("create");
        storeSpan.GetTagItem("todo.outcome").ShouldBe("created");

        storeSpan.Events.ShouldBeEmpty();

        foreach (var tag in storeSpan.TagObjects)
        {
            var value = tag.Value?.ToString() ?? string.Empty;
            value.ShouldNotContain("Trace me 9a41");
            value.ShouldNotContain("Private detail 9a41");
        }
    }

    [Fact]
    public async Task CreateCompleteDelete_ProduceOneStoreSpanNestedUnderEachHttpRequest()
    {
        using var capture = new ActivityCapture(
            TracingTodoStore.ActivitySourceName,
            "Microsoft.AspNetCore");
        using var factory = new TelemetryEnabledFactory();
        using var client = factory.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        using var createResponse = await client.PostAsJsonAsync(
            "/todos",
            new { title = "Trace the sequence", priority = "high" },
            cancellationToken);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var item = await createResponse.Content.ReadFromJsonAsync<TodoResponse>(
            ApiJson.Options, cancellationToken);
        item.ShouldNotBeNull();

        using var completeResponse = await client.PostAsync(
            $"/todos/{item.Id}/complete", content: null, cancellationToken);
        completeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var deleteResponse = await client.DeleteAsync($"/todos/{item.Id}", cancellationToken);
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var storeSpans = capture.From(TracingTodoStore.ActivitySourceName);
        storeSpans.Count.ShouldBe(3);
        storeSpans[0].GetTagItem("todo.operation").ShouldBe("create");
        storeSpans[1].GetTagItem("todo.operation").ShouldBe("complete");
        storeSpans[2].GetTagItem("todo.operation").ShouldBe("delete");

        var serverSpans = capture.From("Microsoft.AspNetCore")
            .Where(activity => activity.Kind == ActivityKind.Server)
            .ToArray();
        serverSpans.Length.ShouldBe(3);

        foreach (var storeSpan in storeSpans)
        {
            var serverSpan = serverSpans.Single(span => span.TraceId == storeSpan.TraceId);
            storeSpan.ParentSpanId.ShouldBe(serverSpan.SpanId);
        }
    }

    [Fact]
    public void TodoStore_IsTheTracingDecorator_AndTheInMemoryStoreIsStillAvailable()
    {
        using var factory = new TodoApiFactory();

        factory.Services.GetRequiredService<ITodoStore>().ShouldBeOfType<TracingTodoStore>();
        factory.Services.GetRequiredService<InMemoryTodoStore>().ShouldNotBeNull();
    }
}
