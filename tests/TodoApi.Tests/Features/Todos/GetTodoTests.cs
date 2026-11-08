using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using TodoApi.Contracts;

namespace TodoApi.Tests.Features.Todos;

public sealed class GetTodoTests : ApiTest
{
    [Fact]
    public async Task Get_WhenItemExists_ReturnsEveryField()
    {
        var item = Seed("Buy milk", "Semi-skimmed", dueDate: new DateOnly(2026, 11, 12));

        using var response = await Client.GetAsync($"/todos/{item.Id}", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");

        var body = await response.Content.ReadFromJsonAsync<TodoResponse>(
            ApiJson.Options, TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body.ShouldBe(TodoResponse.From(item));
        body.DueDate.ShouldBe(new DateOnly(2026, 11, 12));
    }

    [Fact]
    public async Task Get_WhenItemIsMissing_Returns404Problem()
    {
        using var response = await Client.GetAsync("/todos/9999", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        document.RootElement.GetProperty("status").GetInt32().ShouldBe(404);
        document.RootElement.GetProperty("title").GetString().ShouldBe("Todo item not found.");
    }

    [Fact]
    public async Task Get_WhenIdIsNotANumber_Returns404()
    {
        using var response = await Client.GetAsync("/todos/abc", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
