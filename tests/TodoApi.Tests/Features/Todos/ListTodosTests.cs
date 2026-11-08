using System.Net;
using System.Text.Json;
using Shouldly;
using TodoApi.Domain;

namespace TodoApi.Tests.Features.Todos;

public sealed class ListTodosTests : ApiTest
{
    [Fact]
    public async Task GetAll_WhenStoreIsEmpty_ReturnsEmptyArray()
    {
        using var response = await Client.GetAsync("/todos", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("[]");
    }

    [Fact]
    public async Task GetAll_ReturnsItemsSortedByIdWithWireShape()
    {
        Seed("First", description: "One", priority: Priority.Low);
        Seed("Second");
        Seed("Third", priority: Priority.High);

        using var response = await Client.GetAsync("/todos", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var items = document.RootElement.EnumerateArray().ToArray();

        items.Length.ShouldBe(3);
        items.Select(item => item.GetProperty("id").GetInt32()).ShouldBe([1, 2, 3]);
        items.Select(item => item.GetProperty("title").GetString()).ShouldBe(["First", "Second", "Third"]);

        var first = items[0];
        first.GetProperty("description").GetString().ShouldBe("One");
        first.GetProperty("priority").GetString().ShouldBe("low");
        first.GetProperty("done").GetBoolean().ShouldBeFalse();
        first.GetProperty("completedAt").ValueKind.ShouldBe(JsonValueKind.Null);
        first.GetProperty("createdAt").GetString().ShouldBe("2026-11-08T11:16:00+00:00");
    }
}
