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
        ControlName(html, "NewTitle").ShouldBe("NewTitle");
        ControlName(html, "NewDescription").ShouldBe("NewDescription");
        ControlName(html, "new-priority-high").ShouldBe("NewPriority");
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
            ["NewDueDate"] = "2026-11-12",
            ["new-priority-high"] = "High"
        });

        AssertRedirectTo(response, "open");
        var html = await FollowRedirectAsync(response);

        var item = Store.GetAll().ShouldHaveSingleItem();
        item.Title.ShouldBe("Buy milk");
        item.Description.ShouldBe("Semi-skimmed");
        item.Priority.ShouldBe(Priority.High);
        item.DueDate.ShouldBe(new DateOnly(2026, 11, 12));
        item.CreatedAt.ShouldBe(Now);
        html.ShouldContain("Buy milk");
    }

    [Fact]
    public async Task Create_WithBlankTitle_ShowsValidationErrorAndStoresNothing()
    {
        var response = await PostFormAsync("Create", new()
        {
            ["NewTitle"] = "   ",
            ["new-priority-medium"] = "Medium"
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
            ["new-priority-low"] = "Low"
        });

        AssertRedirectTo(response, "open");
        var html = await FollowRedirectAsync(response);

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
            ["new-priority-medium"] = "Medium"
        });

        AssertRedirectTo(created, "open");
        var createdHtml = await FollowRedirectAsync(created);
        createdHtml.ShouldNotContain("Title must be 200 characters or fewer.");
        var item = Store.GetAll().ShouldHaveSingleItem();
        item.Title.ShouldBe(createdTitle);

        var editedTitle = new string('b', 200);

        var edited = await PostFormAsync("Update", new()
        {
            [$"edit-title-{item.Id}"] = $"  {editedTitle}  ",
            [$"edit-priority-high-{item.Id}"] = "High"
        }, id: item.Id);

        AssertRedirectTo(edited, "open");
        var editedHtml = await FollowRedirectAsync(edited);
        editedHtml.ShouldNotContain("Title must be 200 characters or fewer.");
        Store.Get(item.Id).ShouldNotBeNull().Title.ShouldBe(editedTitle);
    }

    [Fact]
    public async Task Create_FromDoneView_RedirectsToDoneView()
    {
        var response = await PostFormAsync("Create", new()
        {
            ["NewTitle"] = "Prune the inbox",
            ["new-priority-medium"] = "Medium"
        }, view: "done");

        AssertRedirectTo(response, "done");
        var html = await FollowRedirectAsync(response);

        html.ShouldContain("Nothing completed yet");
        Store.GetAll().ShouldHaveSingleItem().Title.ShouldBe("Prune the inbox");
    }

    [Fact]
    public async Task Create_WithBlankTitleFromDoneView_ShowsDoneViewWithValidationError()
    {
        var response = await PostFormAsync("Create", new()
        {
            ["NewTitle"] = "   ",
            ["new-priority-medium"] = "Medium"
        }, view: "done");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        html.ShouldContain("Title is required.");
        html.ShouldContain("Nothing completed yet");
        Store.GetAll().ShouldBeEmpty();
    }

    [Fact]
    public async Task Create_WithOutOfRangePriority_ShowsValidationPageAndStoresNothing()
    {
        var response = await PostFormAsync("Create", new()
        {
            ["NewTitle"] = "Never saved",
            ["new-priority-high"] = "999"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        Store.GetAll().ShouldBeEmpty();
    }

    [Fact]
    public async Task Create_WithMalformedDueDate_ShowsValidationPageAndStoresNothing()
    {
        var response = await PostFormAsync("Create", new()
        {
            ["NewTitle"] = "Never saved",
            ["NewDueDate"] = "2026-13-01",
            ["new-priority-medium"] = "Medium"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        Store.GetAll().ShouldBeEmpty();
    }

    [Fact]
    public async Task Complete_MarksTodoDoneWithCompletionTime()
    {
        var item = Seed("Walk the dog");

        var response = await PostFormAsync("Complete", new(), id: item.Id);

        AssertRedirectTo(response, "open");
        var html = await FollowRedirectAsync(response);

        html.ShouldContain("Done. One less thing to think about.");
        var completed = Store.Get(item.Id).ShouldNotBeNull();
        completed.Done.ShouldBeTrue();
        completed.CompletedAt.ShouldBe(Now);
    }

    [Fact]
    public async Task Complete_WhenTodoIsMissing_ReturnsToFilterWithNotFoundToast()
    {
        var item = Seed("Still here");

        var response = await PostFormAsync("Complete", new(), id: 9999, view: "done");

        AssertRedirectTo(response, "done");
        var html = await FollowRedirectAsync(response);

        html.ShouldContain("That todo no longer exists.");
        html.ShouldContain("Nothing completed yet");
        Store.Get(item.Id).ShouldNotBeNull().Done.ShouldBeFalse();
    }

    [Fact]
    public async Task Update_ReplacesEditableFields()
    {
        var item = Seed("Old title", description: "Old note", priority: Priority.Low);

        var response = await PostFormAsync("Update", new()
        {
            [$"edit-title-{item.Id}"] = "  New title  ",
            [$"edit-note-{item.Id}"] = "  New note  ",
            [$"edit-priority-high-{item.Id}"] = "High"
        }, id: item.Id);

        AssertRedirectTo(response, "open");
        await FollowRedirectAsync(response);

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
            [$"edit-title-{item.Id}"] = "Keep me"
        }, id: item.Id);

        AssertRedirectTo(response, "open");
        await FollowRedirectAsync(response);

        Store.Get(item.Id).ShouldNotBeNull().Priority.ShouldBe(Priority.Medium);
    }

    [Fact]
    public async Task Update_SetsAndClearsDueDate()
    {
        var item = Seed("Plan the week");
        var dueDate = new DateOnly(2026, 11, 12);

        var set = await PostFormAsync("Update", new()
        {
            [$"edit-title-{item.Id}"] = "Plan the week",
            [$"edit-due-{item.Id}"] = "2026-11-12",
            [$"edit-priority-medium-{item.Id}"] = "Medium"
        }, id: item.Id);

        AssertRedirectTo(set, "open");
        await FollowRedirectAsync(set);
        Store.Get(item.Id).ShouldNotBeNull().DueDate.ShouldBe(dueDate);

        var cleared = await PostFormAsync("Update", new()
        {
            [$"edit-title-{item.Id}"] = "Plan the week",
            [$"edit-due-{item.Id}"] = "",
            [$"edit-priority-medium-{item.Id}"] = "Medium"
        }, id: item.Id);

        AssertRedirectTo(cleared, "open");
        await FollowRedirectAsync(cleared);
        Store.Get(item.Id).ShouldNotBeNull().DueDate.ShouldBeNull();
    }

    [Fact]
    public async Task Update_WithMalformedDueDate_KeepsEditorOpenAndReportsIt()
    {
        var item = Seed("Keep me", dueDate: new DateOnly(2026, 11, 12));

        var response = await PostFormAsync("Update", new()
        {
            [$"edit-title-{item.Id}"] = "Keep me",
            [$"edit-due-{item.Id}"] = "2026-13-01",
            [$"edit-priority-medium-{item.Id}"] = "Medium"
        }, id: item.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        html.ShouldContain("Choose a valid due date.");
        html.ShouldContain($"id=\"edit-title-{item.Id}\"");
        Store.Get(item.Id).ShouldNotBeNull().DueDate.ShouldBe(new DateOnly(2026, 11, 12));
    }

    [Fact]
    public async Task Update_WithOutOfRangePriority_KeepsEditorOpenAndStoresNothing()
    {
        var item = Seed("Keep me", priority: Priority.Low);

        var response = await PostFormAsync("Update", new()
        {
            [$"edit-title-{item.Id}"] = "Keep me",
            [$"edit-priority-high-{item.Id}"] = "999"
        }, id: item.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        html.ShouldContain("Choose a valid priority.");
        html.ShouldContain($"id=\"edit-title-{item.Id}\"");

        var unchanged = Store.Get(item.Id).ShouldNotBeNull();
        unchanged.Title.ShouldBe("Keep me");
        unchanged.Priority.ShouldBe(Priority.Low);
    }

    [Fact]
    public async Task Update_WithBlankTitle_KeepsEditFormOpenAndStoresNothing()
    {
        var item = Seed("Keep me");

        var response = await PostFormAsync("Update", new()
        {
            [$"edit-title-{item.Id}"] = " ",
            [$"edit-priority-medium-{item.Id}"] = "Medium"
        }, id: item.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        html.ShouldContain("Title is required.");
        html.ShouldContain($"id=\"edit-title-{item.Id}\"");
        Store.Get(item.Id).ShouldNotBeNull().Title.ShouldBe("Keep me");
    }

    [Fact]
    public async Task Update_WhenTodoIsMissing_ReturnsToFilterWithNotFoundToast()
    {
        var item = Seed("Still here");

        var response = await PostFormAsync("Update", new()
        {
            [$"edit-title-{item.Id}"] = "Ghost",
            [$"edit-priority-high-{item.Id}"] = "High"
        }, id: 9999, view: "all", nameSourceId: item.Id);

        AssertRedirectTo(response, "all");
        var html = await FollowRedirectAsync(response);

        html.ShouldContain("That todo no longer exists.");
        html.ShouldContain("Still here");
        var unchanged = Store.Get(item.Id).ShouldNotBeNull();
        unchanged.Title.ShouldBe("Still here");
        Store.GetAll().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Delete_RemovesTodo()
    {
        var item = Seed("Throw me away");

        var response = await PostFormAsync("Delete", new(), id: item.Id);

        AssertRedirectTo(response, "open");
        var html = await FollowRedirectAsync(response);

        html.ShouldContain("Deleted &#x201C;Throw me away&#x201D;.");
        Store.Get(item.Id).ShouldBeNull();
    }

    [Fact]
    public async Task Delete_WhenTodoIsMissing_ReturnsToFilterWithNotFoundToast()
    {
        var item = Seed("Still here");

        var response = await PostFormAsync("Delete", new(), id: 9999, view: "open");

        AssertRedirectTo(response, "open");
        var html = await FollowRedirectAsync(response);

        html.ShouldContain("That todo no longer exists.");
        Store.Get(item.Id).ShouldNotBeNull();
    }

    [Fact]
    public async Task EditDeepLink_RendersEditorWithCurrentValues()
    {
        var item = Seed(
            "Editable todo",
            description: "Some note",
            priority: Priority.High,
            dueDate: new DateOnly(2026, 11, 12));

        var html = await GetHtmlAsync($"/?edit={item.Id}");

        ControlName(html, $"edit-title-{item.Id}").ShouldBe("editTitle");
        ControlName(html, $"edit-note-{item.Id}").ShouldBe("editDescription");
        ControlName(html, $"edit-priority-high-{item.Id}").ShouldBe("editPriority");
        ControlName(html, $"edit-due-{item.Id}").ShouldBe("editDueDate");
        ControlAttribute(html, $"edit-due-{item.Id}", "value").ShouldBe("2026-11-12");
        html.ShouldContain("value=\"Editable todo\"");
    }

    [Fact]
    public async Task Home_DoneViewWithNoCompletions_ShowsDoneEmptyCopy()
    {
        var html = await GetHtmlAsync("/?view=done");

        html.ShouldContain("Nothing completed yet");
        html.ShouldContain("Finished todos will collect here. Complete one to see it.");
    }

    [Fact]
    public async Task Home_AllViewWithNoTodos_ShowsAllEmptyCopy()
    {
        var html = await GetHtmlAsync("/?view=all");

        html.ShouldContain("Your list is empty");
        html.ShouldContain("Add your first todo above and it will show up here.");
    }

    [Fact]
    public async Task Home_PercentDoneRoundsTwoOfThreeTo67()
    {
        var first = Seed("First");
        var second = Seed("Second");
        Seed("Third");
        Store.Complete(first.Id, Now);
        Store.Complete(second.Id, Now);

        var html = await GetHtmlAsync("/");

        html.ShouldContain("67%");
        html.ShouldContain("2 of 3 done");
    }

    [Fact]
    public async Task Home_DoneViewSortsByMostRecentlyCompleted()
    {
        var first = Seed("Completed first");
        var second = Seed("Completed second");

        Store.Complete(first.Id, Now);
        Factory.Clock.Advance(TimeSpan.FromMinutes(5));
        Store.Complete(second.Id, Now);

        var html = await GetHtmlAsync("/?view=done");

        html.IndexOf("Completed second", StringComparison.Ordinal)
            .ShouldBeLessThan(html.IndexOf("Completed first", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Home_EditDeepLinkWithMissingId_RendersNoEditor()
    {
        Seed("Visible todo");

        var html = await GetHtmlAsync("/?edit=999");

        html.ShouldContain("Visible todo");
        html.ShouldNotContain("name=\"editTitle\"");
    }

    [Fact]
    public async Task Forms_TakeTheirLengthLimitsFromTodoDraft()
    {
        var item = Seed("Editable todo");

        var html = await GetHtmlAsync($"/?edit={item.Id}");

        foreach (var titleId in new[] { "NewTitle", $"edit-title-{item.Id}" })
        {
            ControlAttribute(html, titleId, "maxlength").ShouldBe($"{TodoDraft.TitleMaxLength}");
        }

        foreach (var noteId in new[] { "NewDescription", $"edit-note-{item.Id}" })
        {
            ControlAttribute(html, noteId, "maxlength").ShouldBe($"{TodoDraft.DescriptionMaxLength}");
        }
    }

    [Fact]
    public async Task Home_WithUnknownView_ShowsTheOpenFilter()
    {
        Seed("Open task");
        var done = Seed("Done task");
        Store.Complete(done.Id, Now);

        var html = await GetHtmlAsync("/?view=bogus");

        html.ShouldContain("Open task");
        html.ShouldNotContain("Done task");
        Regex.IsMatch(html, @"<a aria-current=""page"" href=""/\?view=open"">").ShouldBeTrue();
    }

    [Fact]
    public async Task Create_WithOverlongDescription_KeepsTheNoteOpen()
    {
        var response = await PostFormAsync("Create", new()
        {
            ["NewTitle"] = "Has a long note",
            ["NewDescription"] = new string('x', 2001),
            ["new-priority-medium"] = "Medium"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        html.ShouldContain("<details class=\"notes\" open=\"open\">");
    }

    [Fact]
    public async Task Home_FormatsItemDatesInTheSameZoneAsTheTodayLabel()
    {
        // 2026-11-08T11:16Z is already 9 November at UTC+14.
        Factory.Clock.SetLocalTimeZone(TimeZoneInfo.FindSystemTimeZoneById("Pacific/Kiritimati"));
        var open = Seed("Open in Kiritimati");
        var done = Seed("Done in Kiritimati");
        Store.Complete(done.Id, Now);

        var html = await GetHtmlAsync("/?view=all");

        html.ShouldContain("Monday, 9 November");
        // Razor encodes the attribute's "+" as "&#x2B;".
        Regex.IsMatch(html, @"Added\s*<time datetime=""2026-11-08T11:16:00\.0000000(\+|&#x2B;)00:00"">\s*9 Nov\s*</time>")
            .ShouldBeTrue("The Added date should be local while the datetime attribute stays UTC.");
        Regex.IsMatch(html, @"Done\s*<time datetime=""[^""]+"">\s*9 Nov\s*</time>")
            .ShouldBeTrue("The Done date should be local.");
        html.ShouldNotContain("8 Nov");
        open.CreatedAt.ShouldBe(Now);
    }

    [Fact]
    public async Task Home_OverdueViewListsOnlyOpenItemsPastTheirDueDate()
    {
        Seed("Late report", dueDate: new DateOnly(2000, 1, 1));
        Seed("Due soon", dueDate: new DateOnly(2999, 1, 1));
        Seed("No due date");
        SeedCompleted("Finished late", dueDate: new DateOnly(2000, 1, 1));

        var html = await GetHtmlAsync("/?view=overdue");

        html.ShouldContain("Late report");
        html.ShouldNotContain("Due soon");
        html.ShouldNotContain("No due date");
        html.ShouldNotContain("Finished late");
        Regex.IsMatch(html, @"<a aria-current=""page"" href=""/\?view=overdue"">\s*Overdue\s*<span class=""count"">1</span>")
            .ShouldBeTrue("The Overdue filter should be current and count only open past-due todos.");
    }

    [Fact]
    public async Task Home_OverdueViewWithNothingOverdue_ShowsOverdueEmptyCopy()
    {
        Seed("Due soon", dueDate: new DateOnly(2999, 1, 1));

        var html = await GetHtmlAsync("/?view=overdue");

        html.ShouldContain("Nothing overdue");
        html.ShouldContain("Open todos past their due date will collect here.");
    }

    [Fact]
    public async Task Home_OpenPastDueItem_ShowsDueDateAndOverdueClass()
    {
        Seed("Send the report", dueDate: new DateOnly(2000, 1, 1));

        var html = await GetHtmlAsync("/");

        Regex.IsMatch(html, @"Due\s*<time datetime=""2000-01-01"">\s*1 Jan\s*</time>")
            .ShouldBeTrue("An open item should show its due date.");
        Regex.IsMatch(html, @"class=""meta-text meta-due is-overdue""")
            .ShouldBeTrue("An open item past its due date should read as overdue.");
    }

    [Fact]
    public async Task Home_DonePastDueItem_ShowsDueDateWithoutOverdueClass()
    {
        var item = Seed("Filed the report", dueDate: new DateOnly(2000, 1, 1));
        Store.Complete(item.Id, Now);

        var html = await GetHtmlAsync("/?view=done");

        Regex.IsMatch(html, @"Due\s*<time datetime=""2000-01-01"">\s*1 Jan\s*</time>")
            .ShouldBeTrue("A done item should still show its due date.");
        html.ShouldNotContain("is-overdue");
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
        Dictionary<string, string> fieldsByControlId,
        int? id = null,
        string view = "open",
        int? nameSourceId = null)
    {
        var sourceId = nameSourceId ?? id;
        var sourceUrl = $"/?view={view}" +
            (sourceId is null ? string.Empty : $"&edit={sourceId}");
        var sourceHtml = await GetHtmlAsync(sourceUrl);

        var fields = fieldsByControlId.ToDictionary(
            field => ControlName(sourceHtml, field.Key),
            field => field.Value);
        fields["__RequestVerificationToken"] = AntiforgeryToken(sourceHtml);

        var postUrl = $"/?handler={handler}&view={view}" +
            (id is null ? string.Empty : $"&id={id}");

        return await Client.PostAsync(
            postUrl,
            new FormUrlEncodedContent(fields),
            TestContext.Current.CancellationToken);
    }

    private async Task<string> FollowRedirectAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = response.Headers.Location.ShouldNotBeNull();

        using var followed = await Client.GetAsync(location, TestContext.Current.CancellationToken);
        followed.StatusCode.ShouldBe(HttpStatusCode.OK);

        return await followed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    private static void AssertRedirectTo(HttpResponseMessage response, string view)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ShouldNotBeNull().ToString().ShouldBe($"/?view={view}");
    }

    private static string ControlName(string html, string controlId) =>
        ControlAttribute(html, controlId, "name");

    private static string ControlAttribute(string html, string controlId, string attribute)
    {
        var tag = Regex.Match(
            html,
            $@"<(?:input|textarea)\b(?=[^>]*\sid=""{Regex.Escape(controlId)}""(?:\s|/?>))[^>]*>",
            RegexOptions.IgnoreCase);

        tag.Success.ShouldBeTrue(
            $"The page should render an input or textarea whose id is \u201c{controlId}\u201d.");

        var value = Regex.Match(tag.Value, $@"\s{Regex.Escape(attribute)}=""(?<value>[^""]+)""");

        value.Success.ShouldBeTrue(
            $"The control with id \u201c{controlId}\u201d should have a {attribute} attribute.");

        return value.Groups["value"].Value;
    }

    private static string AntiforgeryToken(string html)
    {
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");

        match.Success.ShouldBeTrue("The page should render an antiforgery token.");

        return match.Groups[1].Value;
    }
}
