using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Shouldly;
using TodoApi.Contracts;
using TodoApi.Domain;

namespace TodoApi.Tests.Features.Todos;

public sealed class CreateTodoTests : ApiTest
{
    [Fact]
    public async Task Create_WithMinimalPayload_AppliesDefaults()
    {
        using var response = await PostJson("""{"title":"  Buy milk  "}""");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location?.ToString().ShouldBe("/todos/1");

        var body = await response.Content.ReadFromJsonAsync<TodoResponse>(
            ApiJson.Options, TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body.Id.ShouldBe(1);
        body.Title.ShouldBe("Buy milk");
        body.Description.ShouldBeNull();
        body.CreatedAt.ShouldBe(TodoApiFactory.StartTime);
        body.CreatedAt.Offset.ShouldBe(TimeSpan.Zero);
        body.CompletedAt.ShouldBeNull();
        body.Priority.ShouldBe(Priority.Medium);
        body.Done.ShouldBeFalse();

        var raw = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        JsonDocument.Parse(raw).RootElement.GetProperty("priority").GetString().ShouldBe("medium");
    }

    [Fact]
    public async Task Create_WithEveryField_StoresNormalisedValues()
    {
        using var response = await PostJson(
            """{"title":"  Plan sprint  ","description":"  Write stories  ","priority":"HIGH"}""");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<TodoResponse>(
            ApiJson.Options, TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body.Title.ShouldBe("Plan sprint");
        body.Description.ShouldBe("Write stories");
        body.Priority.ShouldBe(Priority.High);
        body.Done.ShouldBeFalse();
    }

    [Fact]
    public async Task Create_AssignsIncreasingIds()
    {
        using var first = await PostJson("""{"title":"First"}""");
        using var second = await PostJson("""{"title":"Second"}""");

        var firstBody = await first.Content.ReadFromJsonAsync<TodoResponse>(
            ApiJson.Options, TestContext.Current.CancellationToken);
        var secondBody = await second.Content.ReadFromJsonAsync<TodoResponse>(
            ApiJson.Options, TestContext.Current.CancellationToken);

        firstBody.ShouldNotBeNull().Id.ShouldBe(1);
        secondBody.ShouldNotBeNull().Id.ShouldBe(2);
        first.Headers.Location?.ToString().ShouldBe("/todos/1");
        second.Headers.Location?.ToString().ShouldBe("/todos/2");
    }

    [Fact]
    public async Task Create_WithoutTitle_ReturnsValidationProblem()
    {
        using var response = await PostJson("""{}""");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        using var document = await ReadJson(response);
        document.RootElement.GetProperty("status").GetInt32().ShouldBe(400);
        document.RootElement.GetProperty("errors").TryGetProperty("title", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Create_WithBlankTitle_ReturnsValidationProblem()
    {
        using var response = await PostJson("""{"title":"   "}""");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadJson(response)).RootElement.GetProperty("errors").TryGetProperty("title", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Create_WithOverlongTitle_ReturnsValidationProblem()
    {
        var title = new string('a', TodoInputLimits.TitleMaxLength + 1);
        using var response = await PostJson($$"""{"title":"{{title}}"}""");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadJson(response)).RootElement.GetProperty("errors").TryGetProperty("title", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Create_WithOverlongDescription_ReturnsValidationProblem()
    {
        var description = new string('a', TodoInputLimits.DescriptionMaxLength + 1);
        using var response = await PostJson(
            $$"""{"title":"Valid","description":"{{description}}"}""");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadJson(response)).RootElement.GetProperty("errors").TryGetProperty("description", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Create_WithUnknownMember_Returns400()
    {
        using var response = await PostJson("""{"title":"Valid","bogus":true}""");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_WithServerManagedDone_Returns400()
    {
        using var response = await PostJson("""{"title":"Valid","done":true}""");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_WithIntegerPriority_Returns400()
    {
        using var response = await PostJson("""{"title":"Valid","priority":1}""");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_WithMalformedJson_Returns400()
    {
        using var response = await PostJson("""{"title":""");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_WithWrongContentType_Returns415()
    {
        using var content = new StringContent("title=Buy milk", Encoding.UTF8, "text/plain");
        using var response = await Client.PostAsync("/todos", content, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
    }

    private Task<HttpResponseMessage> PostJson(string json) =>
        Client.PostAsync(
            "/todos",
            new StringContent(json, Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);

    private static async Task<JsonDocument> ReadJson(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
}

internal static class TodoInputLimits
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 2000;
}
