using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using TodoApi.Contracts;

namespace TodoApi.Tests.Features.Todos;

public sealed class CompleteTodoTests : ApiTest
{
    [Fact]
    public async Task Complete_WhenItemIsOpen_SetsDoneAndTimestamp()
    {
        var item = Seed("Ship the demo", "Before the review");

        using var response = await Client.PostAsync(
            $"/todos/{item.Id}/complete", content: null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<TodoResponse>(
            ApiJson.Options, TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body.Id.ShouldBe(item.Id);
        body.Title.ShouldBe("Ship the demo");
        body.Description.ShouldBe("Before the review");
        body.Done.ShouldBeTrue();
        body.CompletedAt.ShouldBe(TodoApiFactory.StartTime);
        body.CreatedAt.ShouldBe(item.CreatedAt);
    }

    [Fact]
    public async Task Complete_WhenCalledAgain_PreservesTheOriginalTimestamp()
    {
        var item = Seed();

        using var first = await Client.PostAsync(
            $"/todos/{item.Id}/complete", content: null, TestContext.Current.CancellationToken);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        Factory.Clock.Advance(TimeSpan.FromHours(2));

        using var second = await Client.PostAsync(
            $"/todos/{item.Id}/complete", content: null, TestContext.Current.CancellationToken);
        second.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await second.Content.ReadFromJsonAsync<TodoResponse>(
            ApiJson.Options, TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body.Done.ShouldBeTrue();
        body.CompletedAt.ShouldBe(TodoApiFactory.StartTime);
    }

    [Fact]
    public async Task Complete_WhenItemIsMissing_Returns404Problem()
    {
        using var response = await Client.PostAsync(
            "/todos/9999/complete", content: null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        document.RootElement.GetProperty("status").GetInt32().ShouldBe(404);
    }

    [Fact]
    public async Task Complete_WhenItemWasDeleted_Returns404()
    {
        var item = Seed();
        Store.Delete(item.Id).ShouldNotBeNull();

        using var response = await Client.PostAsync(
            $"/todos/{item.Id}/complete", content: null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Complete_WhenIdIsNotANumber_Returns404()
    {
        using var response = await Client.PostAsync(
            "/todos/abc/complete", content: null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
