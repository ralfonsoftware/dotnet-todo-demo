using System.Net;
using Shouldly;
using TodoApi.Domain;
using TodoApi.Pages;

namespace TodoApi.Tests.Pages;

/// <summary>
/// Pins the presentation contract shared by the board view, stylesheet, and
/// JavaScript. A hook renamed on one side of the seam only fails here when the
/// other sides have not followed.
/// </summary>
public sealed class PresentationHookTests : ApiTest
{
    private const string ConfirmHook = "data-confirm";
    private const string ThemeToggleHook = "data-theme-toggle";
    private const string ThemeKey = "todo-theme";

    [Fact]
    public async Task Board_RendersEveryPresentationHook()
    {
        foreach (var priority in Enum.GetValues<Priority>())
        {
            Seed($"{priority} task", priority: priority);
        }

        var html = await GetTextAsync("/");

        html.ShouldContain($"id=\"{PresentationHooks.NewTitleId}\"");
        html.ShouldContain(ConfirmHook);
        html.ShouldContain(ThemeToggleHook);
        html.ShouldContain(ThemeKey);

        foreach (var priority in Enum.GetValues<Priority>())
        {
            html.ShouldContain(PresentationHooks.BadgeClass(priority));
            html.ShouldContain(PresentationHooks.PriorityOptionClass(priority));
        }
    }

    [Fact]
    public async Task ServedAssets_DefineEveryPresentationHook()
    {
        var css = await GetTextAsync("/css/site.css");
        var js = await GetTextAsync("/js/site.js");

        foreach (var priority in Enum.GetValues<Priority>())
        {
            var modifier = PresentationHooks.PriorityModifier(priority);

            css.ShouldContain($".badge--{modifier}");
            css.ShouldContain($".prio--{modifier}");
        }

        js.ShouldContain(ConfirmHook);
        js.ShouldContain(ThemeToggleHook);
        js.ShouldContain(ThemeKey);
        js.ShouldContain(PresentationHooks.NewTitleId);
    }

    private async Task<string> GetTextAsync(string url)
    {
        using var response = await Client.GetAsync(url, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }
}
