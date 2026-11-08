using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using TodoApi.Logging;

namespace TodoApi.Tests.Logging;

/// <summary>
/// The Todos Store decorator owns Todo Item outcome logs (ADR-0006): every
/// caller gets them under one category, and each operation logs exactly once.
/// </summary>
public sealed class TodoLogOwnershipTests : IDisposable
{
    private static readonly string Category = typeof(TodoLogCategory).FullName!;

    private readonly LoggingTodoApiFactory _factory = new();
    private readonly HttpClient _client;

    public TodoLogOwnershipTests()
    {
        _client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task ApiCallAndBoardPost_LogUnderTheTodoLogCategory()
    {
        using var api = await PostJsonAsync("/todos", """{"title":"From the API"}""");
        api.StatusCode.ShouldBe(HttpStatusCode.Created);

        using var board = await (await BoardPost("Create", new() { ["NewTitle"] = "From the board", ["NewPriority"] = "Medium" }))();
        board.StatusCode.ShouldBe(HttpStatusCode.Redirect);

        using var rejected = await (await BoardPost("Create", new() { ["NewTitle"] = " ", ["NewPriority"] = "Medium" }))();
        rejected.StatusCode.ShouldBe(HttpStatusCode.OK);

        var created = Records(TodoEventIds.CreateSucceeded);
        created.Length.ShouldBe(2);
        created.ShouldAllBe(record => record.Category == Category);

        Records(TodoEventIds.CreateValidationRejected)
            .ShouldHaveSingleItem()
            .Category.ShouldBe(Category);
    }

    [Fact]
    public async Task EachApiOperation_LogsExactlyOneOutcomeRecord()
    {
        await ShouldLogOnce(TodoEventIds.CreateSucceeded, () => PostJsonAsync("/todos", """{"title":"Once"}"""));
        await ShouldLogOnce(TodoEventIds.GetSucceeded, () => _client.GetAsync("/todos/1", Token));
        await ShouldLogOnce(TodoEventIds.ListSucceeded, () => _client.GetAsync("/todos", Token));
        await ShouldLogOnce(TodoEventIds.UpdateSucceeded, () => SendJsonAsync(HttpMethod.Put, "/todos/1", """{"title":"Twice"}"""));
        await ShouldLogOnce(TodoEventIds.UpdateValidationRejected, () => SendJsonAsync(HttpMethod.Put, "/todos/1", """{"title":" "}"""));
        await ShouldLogOnce(TodoEventIds.CompleteSucceeded, () => _client.PostAsync("/todos/1/complete", null, Token));
        await ShouldLogOnce(TodoEventIds.CompleteAlreadyDone, () => _client.PostAsync("/todos/1/complete", null, Token));
        await ShouldLogOnce(TodoEventIds.DeleteSucceeded, () => _client.DeleteAsync("/todos/1", Token));
        await ShouldLogOnce(TodoEventIds.NotFound, () => _client.GetAsync("/todos/1", Token));
    }

    [Fact]
    public async Task EachBoardMutation_LogsExactlyOneOutcomeRecord()
    {
        await ShouldLogOnce(TodoEventIds.CreateSucceeded, await BoardPost("Create", new() { ["NewTitle"] = "Once", ["NewPriority"] = "Medium" }));
        await ShouldLogOnce(TodoEventIds.UpdateSucceeded, await BoardPost("Update", new() { ["editTitle"] = "Twice", ["editPriority"] = "High" }, id: 1));
        await ShouldLogOnce(TodoEventIds.CompleteSucceeded, await BoardPost("Complete", new(), id: 1));
        await ShouldLogOnce(TodoEventIds.DeleteSucceeded, await BoardPost("Delete", new(), id: 1));
        await ShouldLogOnce(TodoEventIds.NotFound, await BoardPost("Delete", new(), id: 1));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private CapturedLogRecord[] Records(int eventId) =>
        _factory.Logs.Records.Where(record => record.EventId.Id == eventId).ToArray();

    private async Task ShouldLogOnce(int eventId, Func<Task<HttpResponseMessage>> send)
    {
        var before = _factory.Logs.Records.Count(record => record.Category == Category);

        using var response = await send();

        var added = _factory.Logs.Records
            .Where(record => record.Category == Category)
            .Skip(before)
            .ToArray();

        added.ShouldHaveSingleItem($"Expected one Todo log record for event {eventId}.")
            .EventId.Id.ShouldBe(eventId);
    }

    private Task<HttpResponseMessage> PostJsonAsync(string url, string json) =>
        SendJsonAsync(HttpMethod.Post, url, json);

    private Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string url, string json) =>
        _client.SendAsync(
            new HttpRequestMessage(method, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            },
            Token);

    /// <summary>
    /// Fetches the antiforgery token now and returns the post to send later,
    /// so the board read that renders the token is not counted with the post.
    /// </summary>
    private async Task<Func<Task<HttpResponseMessage>>> BoardPost(
        string handler, Dictionary<string, string> fields, int? id = null)
    {
        using var page = await _client.GetAsync("/", Token);
        var html = await page.Content.ReadAsStringAsync(Token);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        token.Success.ShouldBeTrue("The page should render an antiforgery token.");
        fields["__RequestVerificationToken"] = token.Groups[1].Value;

        var url = $"/?handler={handler}&view=open" + (id is null ? string.Empty : $"&id={id}");
        return () => _client.PostAsync(url, new FormUrlEncodedContent(fields), Token);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}
