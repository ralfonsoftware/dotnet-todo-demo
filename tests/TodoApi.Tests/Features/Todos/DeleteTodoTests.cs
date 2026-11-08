using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using TodoApi.Contracts;
using TodoApi.Domain;

namespace TodoApi.Tests.Features.Todos;

public sealed class DeleteTodoTests : ApiTest
{
    [Fact]
    public async Task Delete_WhenItemExists_Returns204AndRemovesIt()
    {
        var item = Seed();

        using var response = await Client.DeleteAsync($"/todos/{item.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();

        using var getResponse = await Client.GetAsync($"/todos/{item.Id}", TestContext.Current.CancellationToken);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using var secondDelete = await Client.DeleteAsync($"/todos/{item.Id}", TestContext.Current.CancellationToken);
        secondDelete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_WhenItemIsMissing_Returns404Problem()
    {
        using var response = await Client.DeleteAsync("/todos/9999", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        document.RootElement.GetProperty("status").GetInt32().ShouldBe(404);
    }

    [Fact]
    public async Task Delete_WhenItemIsCompleted_Returns204()
    {
        var item = Seed();
        Store.Complete(item.Id, Now)!.Done.ShouldBeTrue();

        using var response = await Client.DeleteAsync($"/todos/{item.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_DoesNotReuseTheId()
    {
        var item = Seed();

        using var deleteResponse = await Client.DeleteAsync($"/todos/{item.Id}", TestContext.Current.CancellationToken);
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var createResponse = await SendJsonAsync(
            HttpMethod.Post,
            "/todos",
            """{"title":"Replacement"}""");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var body = await createResponse.Content.ReadFromJsonAsync<TodoResponse>(
            ApiJson.Options, TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body.Id.ShouldBe(item.Id + 1);
    }
}
