using Microsoft.AspNetCore.Http.HttpResults;

namespace TodoApi.Features.Todos;

internal static class TodoResults
{
    /// <summary>
    /// The <c>404</c> problem for a missing Todo Item. The Todos Store
    /// decorator has already logged the not-found outcome.
    /// </summary>
    public static ProblemHttpResult NotFound() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Todo item not found.");
}
