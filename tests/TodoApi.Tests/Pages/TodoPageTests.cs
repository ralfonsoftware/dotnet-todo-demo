using System.Net;
using System.Text.RegularExpressions;
using Shouldly;
using TodoApi.Domain;

namespace TodoApi.Tests.Pages;

public sealed class TodoPageTests : ApiTest
{
    [Fact]
    public async Task Home_WhenStoreIsEmpty_ShowsEmptyStateAndComposer()
    {
        var html = await GetHtmlAsync("/");

        html.ShouldContain("data-testid=\"empty-state\"");
        html.ShouldContain("What needs doing?");
        html.ShouldContain("__RequestVerificationToken");
    }

    [Fact]
    public async Task Home_ListsOpenTodosSortedByPriority()
    {
        Seed("Low task", priority: Priority.Low);
        Seed("High task", priority: Priority.High);

        var html = await GetHtmlAsync("/");

        html.ShouldContain("High task");
        html.ShouldContain("Low task");
        html.IndexOf("High task", StringComparison.Ordinal)
            .ShouldBeLessThan(html.IndexOf("Low task", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Home_OpenViewHidesDoneTodos_AndDoneViewShowsThem()
    {
        var item = Seed("Finished thing");
        Store.Complete(item.Id, Now);

        var open = await GetHtmlAsync("/");
        var done = await GetHtmlAsync("/?view=done");

        open.ShouldNotContain("Finished thing");
        done.ShouldContain("Finished thing");
        done.ShouldContain("Done:");
    }

    [Fact]
    public async Task Create_PersistsTodoAndReturnsToBoard()
    {
        var response = await PostFormAsync("Create", new()
        {
            ["NewTitle"] = "  Buy milk  ",
            ["NewDescription"] = "  Semi-skimmed  ",
            ["NewPriority"] = "High"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var item = Store.GetAll().ShouldHaveSingleItem();
        item.Title.ShouldBe("Buy milk");
        item.Description.ShouldBe("Semi-skimmed");
        item.Priority.ShouldBe(Priority.High);
        item.CreatedAt.ShouldBe(Now);

        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        html.ShouldContain("Buy milk");
    }

    [Fact]
    public async Task Create_WithBlankTitle_ShowsValidationErrorAndStoresNothing()
    {
        var response = await PostFormAsync("Create", new()
        {
            ["NewTitle"] = "   ",
            ["NewPriority"] = "Medium"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        html.ShouldContain("Title is required.");
        Store.GetAll().ShouldBeEmpty();
    }

    [Fact]
    public async Task Create_EscapesTitleMarkup()
    {
        var response = await PostFormAsync("Create", new()
        {
            ["NewTitle"] = "<script>alert(1)</script>",
            ["NewPriority"] = "Low"
        });

        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        html.ShouldContain("&lt;script&gt;alert(1)&lt;/script&gt;");
        html.ShouldNotContain("<script>alert(1)</script>");
    }

    [Fact]
    public async Task CreateAndUpdate_WithPaddedMaxLengthTitle_TrimsBeforeMeasuring()
    {
        var createdTitle = new string('a', 200);

        var created = await PostFormAsync("Create", new()
        {
            ["NewTitle"] = $"  {createdTitle}  ",
            ["NewPriority"] = "Medium"
        });

        created.StatusCode.ShouldBe(HttpStatusCode.OK);
        var createdHtml = await created.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        createdHtml.ShouldNotContain("Title must be 200 characters or fewer.");
        var item = Store.GetAll().ShouldHaveSingleItem();
        item.Title.ShouldBe(createdTitle);

        var editedTitle = new string('b', 200);

        var edited = await PostFormAsync("Update", new()
        {
            ["editTitle"] = $"  {editedTitle}  ",
            ["editPriority"] = "High"
        }, id: item.Id);

        edited.StatusCode.ShouldBe(HttpStatusCode.OK);
        var editedHtml = await edited.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        editedHtml.ShouldNotContain("Title must be 200 characters or fewer.");
        Store.Get(item.Id).ShouldNotBeNull().Title.ShouldBe(editedTitle);
    }

    [Fact]
    public async Task Complete_MarksTodoDoneWithCompletionTime()
    {
        var item = Seed("Walk the dog");

        var response = await PostFormAsync("Complete", new(), id: item.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var completed = Store.Get(item.Id).ShouldNotBeNull();
        completed.Done.ShouldBeTrue();
        completed.CompletedAt.ShouldBe(Now);
    }

    [Fact]
    public async Task Update_ReplacesEditableFields()
    {
        var item = Seed("Old title", description: "Old note", priority: Priority.Low);

        var response = await PostFormAsync("Update", new()
        {
            ["editTitle"] = "  New title  ",
            ["editDescription"] = "  New note  ",
            ["editPriority"] = "High"
        }, id: item.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = Store.Get(item.Id).ShouldNotBeNull();
        updated.Title.ShouldBe("New title");
        updated.Description.ShouldBe("New note");
        updated.Priority.ShouldBe(Priority.High);
        updated.CreatedAt.ShouldBe(item.CreatedAt);
    }

    [Fact]
    public async Task Update_WithoutPriority_DefaultsToMedium()
    {
        var item = Seed("Keep me", priority: Priority.Low);

        var response = await PostFormAsync("Update", new()
        {
            ["editTitle"] = "Keep me"
        }, id: item.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        Store.Get(item.Id).ShouldNotBeNull().Priority.ShouldBe(Priority.Medium);
    }

    [Fact]
    public async Task Update_WithBlankTitle_KeepsEditFormOpenAndStoresNothing()
    {
        var item = Seed("Keep me");

        var response = await PostFormAsync("Update", new()
        {
            ["editTitle"] = " ",
            ["editPriority"] = "Medium"
        }, id: item.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        html.ShouldContain("Title is required.");
        html.ShouldContain($"id=\"edit-title-{item.Id}\"");
        Store.Get(item.Id).ShouldNotBeNull().Title.ShouldBe("Keep me");
    }

    [Fact]
    public async Task Delete_RemovesTodo()
    {
        var item = Seed("Throw me away");

        var response = await PostFormAsync("Delete", new(), id: item.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        Store.Get(item.Id).ShouldBeNull();
    }

    [Fact]
    public async Task EditDeepLink_RendersEditorWithCurrentValues()
    {
        Seed("Editable todo", description: "Some note", priority: Priority.High);

        var html = await GetHtmlAsync("/?edit=1");

        html.ShouldContain("name=\"editTitle\"");
        html.ShouldContain("value=\"Editable todo\"");
        html.ShouldContain("name=\"editPriority\"");
    }

    [Fact]
    public async Task Post_WithoutAntiforgeryToken_IsRejected()
    {
        var response = await Client.PostAsync(
            "/?handler=Create&view=open",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["NewTitle"] = "Forged",
                ["NewPriority"] = "Medium"
            }),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        Store.GetAll().ShouldBeEmpty();
    }

    private async Task<string> GetHtmlAsync(string url)
    {
        using var response = await Client.GetAsync(url, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    private async Task<HttpResponseMessage> PostFormAsync(
        string handler,
        Dictionary<string, string> fields,
        int? id = null,
        string view = "open")
    {
        var token = await GetAntiforgeryTokenAsync($"/?view={view}");
        fields["__RequestVerificationToken"] = token;

        var url = $"/?handler={handler}&view={view}" + (id is null ? string.Empty : $"&id={id}");

        return await Client.PostAsync(
            url,
            new FormUrlEncodedContent(fields),
            TestContext.Current.CancellationToken);
    }

    private async Task<string> GetAntiforgeryTokenAsync(string url)
    {
        var html = await GetHtmlAsync(url);
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");

        match.Success.ShouldBeTrue("The page should render an antiforgery token.");

        return match.Groups[1].Value;
    }
}
