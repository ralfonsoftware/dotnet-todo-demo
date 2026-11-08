using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shouldly;
using TodoApi.Pages;

namespace TodoApi.Tests.Pages;

public sealed class BoardErrorsTests
{
    [Fact]
    public void FirstErrorMessage_WhenTheOnlyErrorIsAnException_ReturnsFallback()
    {
        var modelState = new ModelStateDictionary();
        AddExceptionError(modelState, "editTitle");

        BoardErrors.FirstErrorMessage(modelState).ShouldBe(BoardErrors.Fallback);
    }

    [Fact]
    public void FirstErrorMessage_WhenAMessageErrorExists_ReturnsThatMessage()
    {
        var modelState = new ModelStateDictionary();
        AddExceptionError(modelState, "editDescription");
        modelState.AddModelError("editTitle", "Title is required.");

        BoardErrors.FirstErrorMessage(modelState).ShouldBe("Title is required.");
    }

    [Fact]
    public void FirstErrorMessage_WhenThereAreNoErrors_ReturnsFallback()
    {
        BoardErrors.FirstErrorMessage(new ModelStateDictionary()).ShouldBe(BoardErrors.Fallback);
    }

    private static void AddExceptionError(ModelStateDictionary modelState, string key)
    {
        modelState.SetModelValue(key, rawValue: null, attemptedValue: null);
        modelState[key]!.Errors.Add(new ModelError(new InvalidOperationException()));
    }
}
