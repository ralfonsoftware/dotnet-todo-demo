using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TodoApi.Storage;
using TodoApi.Tests.Storage;

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
    public void TodoStore_IsTheTracingDecorator_AndTheInMemoryStoreIsStillAvailable()
    {
        using var factory = new TodoApiFactory();

        factory.Services.GetRequiredService<ITodoStore>().ShouldBeOfType<TracingTodoStore>();
        factory.Services.GetRequiredService<InMemoryTodoStore>().ShouldNotBeNull();
    }

    private sealed class TelemetryEnabledFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("OpenTelemetry:Enabled", "true");
            builder.UseSetting("OpenTelemetry:ExporterEnabled", "false");
        }
    }
}
