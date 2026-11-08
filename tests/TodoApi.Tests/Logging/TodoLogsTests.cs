using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using TodoApi.Domain;
using TodoApi.Logging;
using TodoApi.Storage;
using TodoApi.Telemetry;

namespace TodoApi.Tests.Logging;

public sealed class TodoLogsTests : IDisposable
{
    private readonly LoggingTodoApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly ITodoStore _store;

    public TodoLogsTests()
    {
        _client = _factory.CreateClient();
        _store = _factory.Services.GetRequiredService<ITodoStore>();
    }

    [Fact]
    public async Task Create_Succeeded_LogsTodoIdAndPriorityAtInformation()
    {
        using var response = await PostJson("""{"title":"Buy milk","priority":"high"}""");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var log = SingleLog(TodoEventIds.CreateSucceeded);
        log.Level.ShouldBe(LogLevel.Information);
        log.Properties["Operation"].ShouldBe(TodoOperation.Create.Name);
        log.Properties["TodoId"].ShouldBe(1);
        log.Properties["Priority"].ShouldBe(Priority.High);
    }

    [Fact]
    public async Task Update_Succeeded_LogsTodoIdAndPriorityAtInformation()
    {
        var item = Seed(priority: Priority.Low);

        using var response = await SendJsonAsync(
            HttpMethod.Put,
            $"/todos/{item.Id}",
            """{"title":"Updated","priority":"high"}""");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var log = SingleLog(TodoEventIds.UpdateSucceeded);
        log.Level.ShouldBe(LogLevel.Information);
        log.Properties["Operation"].ShouldBe(TodoOperation.Update.Name);
        log.Properties["TodoId"].ShouldBe(item.Id);
        log.Properties["Priority"].ShouldBe(Priority.High);
    }

    [Fact]
    public async Task Complete_Succeeded_LogsTodoIdAndPriorityAtInformation()
    {
        var item = Seed(priority: Priority.High);

        using var response = await _client.PostAsync(
            $"/todos/{item.Id}/complete", content: null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var log = SingleLog(TodoEventIds.CompleteSucceeded);
        log.Level.ShouldBe(LogLevel.Information);
        log.Properties["Operation"].ShouldBe(TodoOperation.Complete.Name);
        log.Properties["TodoId"].ShouldBe(item.Id);
        log.Properties["Priority"].ShouldBe(Priority.High);
    }

    [Fact]
    public async Task Complete_WhenCalledAgain_LogsSucceededOnceAndAlreadyDoneOnce()
    {
        var item = Seed();

        using var first = await _client.PostAsync(
            $"/todos/{item.Id}/complete", content: null, TestContext.Current.CancellationToken);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var second = await _client.PostAsync(
            $"/todos/{item.Id}/complete", content: null, TestContext.Current.CancellationToken);
        second.StatusCode.ShouldBe(HttpStatusCode.OK);

        _factory.Logs.Records
            .Count(record => record.EventId.Id == TodoEventIds.CompleteSucceeded)
            .ShouldBe(1);

        var log = SingleLog(TodoEventIds.CompleteAlreadyDone);
        log.Level.ShouldBe(LogLevel.Information);
        log.Properties["Operation"].ShouldBe(TodoOperation.Complete.Name);
        log.Properties["TodoId"].ShouldBe(item.Id);
    }

    [Fact]
    public async Task Delete_Succeeded_LogsTodoIdAndPriorityFromTheDeletedItemAtInformation()
    {
        var item = Seed(priority: Priority.Low);

        using var response = await _client.DeleteAsync(
            $"/todos/{item.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var log = SingleLog(TodoEventIds.DeleteSucceeded);
        log.Level.ShouldBe(LogLevel.Information);
        log.Properties["Operation"].ShouldBe(TodoOperation.Delete.Name);
        log.Properties["TodoId"].ShouldBe(item.Id);
        log.Properties["Priority"].ShouldBe(Priority.Low);
    }

    [Fact]
    public async Task Get_WhenItemIsMissing_LogsWarningWithTodoIdAndOperation()
    {
        using var response = await _client.GetAsync("/todos/9999", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var log = SingleLog(TodoEventIds.NotFound);
        log.Level.ShouldBe(LogLevel.Warning);
        log.Properties["Operation"].ShouldBe(TodoOperation.Get.Name);
        log.Properties["TodoId"].ShouldBe(9999);
    }

    [Fact]
    public async Task Get_Succeeded_LogsTodoIdAtDebug()
    {
        var item = Seed();

        using var response = await _client.GetAsync($"/todos/{item.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var log = SingleLog(TodoEventIds.GetSucceeded);
        log.Level.ShouldBe(LogLevel.Debug);
        log.Properties["Operation"].ShouldBe(TodoOperation.Get.Name);
        log.Properties["TodoId"].ShouldBe(item.Id);
    }

    [Fact]
    public async Task List_Succeeded_LogsCountAtDebug()
    {
        Seed();
        Seed();
        Seed();

        using var response = await _client.GetAsync("/todos", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var log = SingleLog(TodoEventIds.ListSucceeded);
        log.Level.ShouldBe(LogLevel.Debug);
        log.Properties["Operation"].ShouldBe(TodoOperation.List.Name);
        log.Properties["Count"].ShouldBe(3);
    }

    [Fact]
    public async Task Update_WhenItemIsMissing_LogsWarningWithTodoIdAndOperation()
    {
        using var response = await SendJsonAsync(
            HttpMethod.Put, "/todos/9999", """{"title":"Valid"}""");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var log = SingleLog(TodoEventIds.NotFound);
        log.Level.ShouldBe(LogLevel.Warning);
        log.Properties["Operation"].ShouldBe(TodoOperation.Update.Name);
        log.Properties["TodoId"].ShouldBe(9999);
    }

    [Fact]
    public async Task Complete_WhenItemIsMissing_LogsWarningWithTodoIdAndOperation()
    {
        using var response = await _client.PostAsync(
            "/todos/9999/complete", content: null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var log = SingleLog(TodoEventIds.NotFound);
        log.Level.ShouldBe(LogLevel.Warning);
        log.Properties["Operation"].ShouldBe(TodoOperation.Complete.Name);
        log.Properties["TodoId"].ShouldBe(9999);
    }

    [Fact]
    public async Task Delete_WhenItemIsMissing_LogsWarningWithTodoIdAndOperation()
    {
        using var response = await _client.DeleteAsync("/todos/9999", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var log = SingleLog(TodoEventIds.NotFound);
        log.Level.ShouldBe(LogLevel.Warning);
        log.Properties["Operation"].ShouldBe(TodoOperation.Delete.Name);
        log.Properties["TodoId"].ShouldBe(9999);
    }

    [Fact]
    public async Task Create_Rejected_LogsErrorCountAtWarning()
    {
        var description = new string('x', 2001);

        using var response = await PostJson(
            $$"""{"title":"   ","description":"{{description}}"}""");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var log = SingleLog(TodoEventIds.CreateValidationRejected);
        log.Level.ShouldBe(LogLevel.Warning);
        log.Properties["Operation"].ShouldBe(TodoOperation.Create.Name);
        log.Properties["ErrorCount"].ShouldBe(2);
        _factory.Logs.Records.ShouldNotContain(
            record => record.EventId.Id == TodoEventIds.CreateSucceeded);
    }

    [Fact]
    public async Task Update_Rejected_LogsErrorCountAtWarning()
    {
        var item = Seed();

        using var response = await SendJsonAsync(
            HttpMethod.Put, $"/todos/{item.Id}", """{"title":"   "}""");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var log = SingleLog(TodoEventIds.UpdateValidationRejected);
        log.Level.ShouldBe(LogLevel.Warning);
        log.Properties["Operation"].ShouldBe(TodoOperation.Update.Name);
        log.Properties["TodoId"].ShouldBe(item.Id);
        log.Properties["ErrorCount"].ShouldBe(1);
        _factory.Logs.Records.ShouldNotContain(
            record => record.EventId.Id == TodoEventIds.UpdateSucceeded);
    }

    [Fact]
    public async Task TodoOperationLogs_NeverContainTitlesOrDescriptions()
    {
        const string title = "Distinctive title 7f3c9a";
        const string updatedTitle = "Another distinctive title b81e42";
        const string description = "Distinctive description 52d0ce";

        using var created = await PostJson(
            $$"""{"title":"{{title}}","description":"{{description}}","priority":"high"}""");
        created.StatusCode.ShouldBe(HttpStatusCode.Created);

        var item = _store.GetAll().ShouldHaveSingleItem();

        using var updated = await SendJsonAsync(
            HttpMethod.Put,
            $"/todos/{item.Id}",
            $$"""{"title":"{{updatedTitle}}","description":"{{description}}"}""");
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var completed = await _client.PostAsync(
            $"/todos/{item.Id}/complete", content: null, TestContext.Current.CancellationToken);
        completed.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var read = await _client.GetAsync($"/todos/{item.Id}", TestContext.Current.CancellationToken);
        read.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var listed = await _client.GetAsync("/todos", TestContext.Current.CancellationToken);
        listed.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var deleted = await _client.DeleteAsync($"/todos/{item.Id}", TestContext.Current.CancellationToken);
        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        _factory.Logs.Records.ShouldNotBeEmpty();

        foreach (var record in _factory.Logs.Records)
        {
            AssertDoesNotLeak(record.Message);
            AssertDoesNotLeak(record.EventId.Name ?? string.Empty);

            foreach (var (key, value) in record.Properties)
            {
                AssertDoesNotLeak(key);
                AssertDoesNotLeak(value?.ToString() ?? string.Empty);
            }
        }

        void AssertDoesNotLeak(string text)
        {
            text.ShouldNotContain(title);
            text.ShouldNotContain(updatedTitle);
            text.ShouldNotContain(description);
        }
    }

    private TodoItem Seed(string title = "Seeded todo", Priority priority = Priority.Medium) =>
        _store.Create(title, null, priority, TodoApiFactory.StartTime);

    private CapturedLogRecord SingleLog(int eventId) =>
        _factory.Logs.Records.Where(record => record.EventId.Id == eventId).ShouldHaveSingleItem();

    private Task<HttpResponseMessage> PostJson(string json) =>
        _client.PostAsync(
            "/todos",
            new StringContent(json, Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);

    private Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string url, string json) =>
        _client.SendAsync(
            new HttpRequestMessage(method, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            },
            TestContext.Current.CancellationToken);

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}
