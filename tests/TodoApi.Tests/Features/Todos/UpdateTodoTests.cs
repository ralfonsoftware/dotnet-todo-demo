using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using TodoApi.Contracts;
using TodoApi.Domain;

namespace TodoApi.Tests.Features.Todos;

public sealed class UpdateTodoTests : ApiTest
{
    [Fact]
    public async Task Update_ReplacesEditableFieldsAndKeepsCreatedAt()
    {
        var item = Seed("Old title", "Old description", Priority.Low);

        using var response = await SendJsonAsync(
            HttpMethod.Put,
            $"/todos/{item.Id}",
            """{"title":"  New title  ","description":"  New description  ","priority":"high"}""");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<TodoResponse>(
            ApiJson.Options, TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body.Id.ShouldBe(item.Id);
        body.Title.ShouldBe("New title");
        body.Description.ShouldBe("New description");
        body.Priority.ShouldBe(Priority.High);
        body.CreatedAt.ShouldBe(item.CreatedAt);
        body.Done.ShouldBeFalse();
        body.CompletedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Update_WithoutDescriptionAndPriority_ResetsBoth()
    {
        var item = Seed("Old title", "Old description", Priority.High);

        using var response = await SendJsonAsync(
            HttpMethod.Put,
            $"/todos/{item.Id}",
            """{"title":"New title"}""");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<TodoResponse>(
            ApiJson.Options, TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body.Description.ShouldBeNull();
        body.Priority.ShouldBe(Priority.Medium);
    }

    [Fact]
    public async Task Update_WhenItemIsMissing_Returns404Problem()
    {
        using var response = await SendJsonAsync(
            HttpMethod.Put,
            "/todos/9999",
            """{"title":"Valid"}""");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        document.RootElement.GetProperty("status").GetInt32().ShouldBe(404);
    }

    [Fact]
    public async Task Update_WithBlankTitle_ReturnsValidationProblem()
    {
        var item = Seed();

        using var response = await SendJsonAsync(
            HttpMethod.Put,
            $"/todos/{item.Id}",
            """{"title":"  "}""");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        document.RootElement.GetProperty("errors").TryGetProperty("title", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Update_WithServerManagedDone_Returns400()
    {
        var item = Seed();

        using var response = await SendJsonAsync(
            HttpMethod.Put,
            $"/todos/{item.Id}",
            """{"title":"Valid","done":true}""");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_WhenItemIsCompleted_KeepsCompletion()
    {
        var item = Seed();
        var completed = Store.Complete(item.Id, Now);
        completed.ShouldNotBeNull().Done.ShouldBeTrue();

        Factory.Clock.Advance(TimeSpan.FromHours(1));

        using var response = await SendJsonAsync(
            HttpMethod.Put,
            $"/todos/{item.Id}",
            """{"title":"Edited after completion"}""");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<TodoResponse>(
            ApiJson.Options, TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body.Title.ShouldBe("Edited after completion");
        body.Done.ShouldBeTrue();
        body.CompletedAt.ShouldBe(completed.CompletedAt);
    }
}
