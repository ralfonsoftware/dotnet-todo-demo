using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using TodoApi.Domain;
using TodoApi.Logging;
using TodoApi.Storage;
using TodoApi.Telemetry;
using TodoApi.Tests.Logging;

namespace TodoApi.Tests.Pages;

/// <summary>
/// Board mutations call the Todos Store in-process, so they must emit the same
/// structured Todo Item outcome events as the HTTP slices.
/// </summary>
public sealed class BoardMutationLogTests : IDisposable
{
    private readonly LoggingTodoApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly ITodoStore _store;

    public BoardMutationLogTests()
    {
        _client = _factory.CreateClient();
        _store = _factory.Services.GetRequiredService<ITodoStore>();
    }

    [Fact]
    public async Task Create_Succeeded_LogsNewTodoIdAndPriorityAtInformation()
    {
        using var response = await PostFormAsync("Create", new()
        {
            ["NewTitle"] = "  Buy milk  ",
            ["NewPriority"] = "High"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var item = _store.GetAll().ShouldHaveSingleItem();
        var log = SingleLog(TodoEventIds.CreateSucceeded, TodoOperation.Create.Name);
        log.Level.ShouldBe(LogLevel.Information);
        log.Properties["TodoId"].ShouldBe(item.Id);
        log.Properties["Priority"].ShouldBe(Priority.High);
    }

    [Fact]
    public async Task Update_Succeeded_LogsTodoIdAndUpdatedPriorityAtInformation()
    {
        var item = Seed(priority: Priority.Low);

        using var response = await PostFormAsync("Update", new()
        {
            ["editTitle"] = "Updated title",
            ["editPriority"] = "High"
        }, id: item.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var log = SingleLog(TodoEventIds.UpdateSucceeded, TodoOperation.Update.Name);
        log.Level.ShouldBe(LogLevel.Information);
        log.Properties["TodoId"].ShouldBe(item.Id);
        log.Properties["Priority"].ShouldBe(Priority.High);
    }

    [Fact]
    public async Task Update_WhenItemIsMissing_LogsWarningWithTodoIdAndOperation()
    {
        using var response = await PostFormAsync("Update", new()
        {
            ["editTitle"] = "Valid title",
            ["editPriority"] = "Medium"
        }, id: 9999);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var log = SingleLog(TodoEventIds.NotFound, TodoOperation.Update.Name);
        log.Level.ShouldBe(LogLevel.Warning);
        log.Properties["TodoId"].ShouldBe(9999);
    }

    [Fact]
    public async Task Complete_Succeeded_LogsTodoIdAndPriorityAtInformation()
    {
        var item = Seed(priority: Priority.High);

        using var response = await PostFormAsync("Complete", new(), id: item.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var log = SingleLog(TodoEventIds.CompleteSucceeded, TodoOperation.Complete.Name);
        log.Level.ShouldBe(LogLevel.Information);
        log.Properties["TodoId"].ShouldBe(item.Id);
        log.Properties["Priority"].ShouldBe(Priority.High);
    }

    [Fact]
    public async Task Complete_WhenCalledAgain_LogsSucceededOnceAndAlreadyDoneOnce()
    {
        var item = Seed();

        using var first = await PostFormAsync("Complete", new(), id: item.Id);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var second = await PostFormAsync("Complete", new(), id: item.Id);
        second.StatusCode.ShouldBe(HttpStatusCode.OK);

        _factory.Logs.Records
            .Count(record => record.EventId.Id == TodoEventIds.CompleteSucceeded)
            .ShouldBe(1);

        var log = SingleLog(TodoEventIds.CompleteAlreadyDone, TodoOperation.Complete.Name);
        log.Level.ShouldBe(LogLevel.Information);
        log.Properties["TodoId"].ShouldBe(item.Id);
    }

    [Fact]
    public async Task Complete_WhenItemIsMissing_LogsWarningWithTodoIdAndOperation()
    {
        using var response = await PostFormAsync("Complete", new(), id: 9999);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var log = SingleLog(TodoEventIds.NotFound, TodoOperation.Complete.Name);
        log.Level.ShouldBe(LogLevel.Warning);
        log.Properties["TodoId"].ShouldBe(9999);
    }

    [Fact]
    public async Task Delete_Succeeded_LogsTodoIdAndPriorityFromTheDeletedItemAtInformation()
    {
        var item = Seed(priority: Priority.Low);

        using var response = await PostFormAsync("Delete", new(), id: item.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var log = SingleLog(TodoEventIds.DeleteSucceeded, TodoOperation.Delete.Name);
        log.Level.ShouldBe(LogLevel.Information);
        log.Properties["TodoId"].ShouldBe(item.Id);
        log.Properties["Priority"].ShouldBe(Priority.Low);
    }

    [Fact]
    public async Task Delete_WhenItemIsMissing_LogsWarningWithTodoIdAndOperation()
    {
        using var response = await PostFormAsync("Delete", new(), id: 9999);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var log = SingleLog(TodoEventIds.NotFound, TodoOperation.Delete.Name);
        log.Level.ShouldBe(LogLevel.Warning);
        log.Properties["TodoId"].ShouldBe(9999);
    }

    [Fact]
    public async Task Create_Rejected_LogsErrorCountAtWarningAndNoSuccess()
    {
        using var response = await PostFormAsync("Create", new()
        {
            ["NewTitle"] = "   ",
            ["NewPriority"] = "Medium"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var log = SingleLog(TodoEventIds.CreateValidationRejected, TodoOperation.Create.Name);
        log.Level.ShouldBe(LogLevel.Warning);
        log.Properties["ErrorCount"].ShouldBe(1);
        _factory.Logs.Records.ShouldNotContain(
            record => record.EventId.Id == TodoEventIds.CreateSucceeded);
    }

    [Fact]
    public async Task Update_Rejected_LogsTodoIdAndErrorCountAtWarningAndNoSuccess()
    {
        var item = Seed();

        using var response = await PostFormAsync("Update", new()
        {
            ["editTitle"] = " ",
            ["editPriority"] = "Medium"
        }, id: item.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var log = SingleLog(TodoEventIds.UpdateValidationRejected, TodoOperation.Update.Name);
        log.Level.ShouldBe(LogLevel.Warning);
        log.Properties["TodoId"].ShouldBe(item.Id);
        log.Properties["ErrorCount"].ShouldBe(1);
        _factory.Logs.Records.ShouldNotContain(
            record => record.EventId.Id == TodoEventIds.UpdateSucceeded);
    }

    private TodoItem Seed(
        string title = "Seeded todo",
        Priority priority = Priority.Medium) =>
        _store.Create(Drafts.Of(title, priority: priority), TodoApiFactory.StartTime);

    private CapturedLogRecord SingleLog(int eventId, string operation) =>
        _factory.Logs.Records
            .Where(record =>
                record.EventId.Id == eventId &&
                Equals(record.Properties["Operation"], operation))
            .ShouldHaveSingleItem();

    private async Task<HttpResponseMessage> PostFormAsync(
        string handler,
        Dictionary<string, string> fields,
        int? id = null,
        string view = "open")
    {
        var token = await GetAntiforgeryTokenAsync($"/?view={view}");
        fields["__RequestVerificationToken"] = token;

        var url = $"/?handler={handler}&view={view}" +
            (id is null ? string.Empty : $"&id={id}");

        return await _client.PostAsync(
            url,
            new FormUrlEncodedContent(fields),
            TestContext.Current.CancellationToken);
    }

    private async Task<string> GetAntiforgeryTokenAsync(string url)
    {
        using var response = await _client.GetAsync(url, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");

        match.Success.ShouldBeTrue("The page should render an antiforgery token.");

        return match.Groups[1].Value;
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}
