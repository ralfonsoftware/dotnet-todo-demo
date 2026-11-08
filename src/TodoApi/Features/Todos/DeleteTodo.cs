using Microsoft.AspNetCore.Http.HttpResults;
using TodoApi.Storage;

namespace TodoApi.Features.Todos;

internal static class DeleteTodo
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/todos/{id:int}", Delete)
            .WithName("DeleteTodo")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static Results<NoContent, ProblemHttpResult> Delete(int id, ITodoStore store) =>
        store.Delete(id)
            ? TypedResults.NoContent()
            : TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Todo item not found.");
}
