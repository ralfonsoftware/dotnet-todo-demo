using System.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using TodoApi.Contracts;
using TodoApi.Storage;

namespace TodoApi.Features.Todos;

internal static class CompleteTodo
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/todos/{id:int}/complete", Complete)
            .WithName("CompleteTodo")
            .Produces<TodoResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static Results<Ok<TodoResponse>, ProblemHttpResult> Complete(
        int id,
        ITodoStore store,
        TimeProvider clock) =>
        store.Complete(id, clock.GetUtcNow()) switch
        {
            CompletionResult.NotFound => TodoResults.NotFound(),
            CompletionResult.Completed completed => TypedResults.Ok(TodoResponse.From(completed.Item)),
            CompletionResult.AlreadyDone alreadyDone => TypedResults.Ok(TodoResponse.From(alreadyDone.Item)),
            _ => throw new UnreachableException()
        };
}
