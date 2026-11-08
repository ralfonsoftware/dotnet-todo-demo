using System.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using TodoApi.Contracts;
using TodoApi.Logging;
using TodoApi.Storage;
using TodoApi.Telemetry;

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
        TimeProvider clock,
        ILogger<TodoLogCategory> logger)
    {
        var result = store.Complete(id, clock.GetUtcNow());

        switch (result)
        {
            case CompletionResult.NotFound:
                TodoLogs.NotFound(logger, id, TodoOperation.Complete.Name);
                return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Todo item not found.");
            case CompletionResult.Completed completed:
                TodoLogs.CompleteSucceeded(logger, TodoOperation.Complete.Name, completed.Item.Id, completed.Item.Priority);
                return TypedResults.Ok(TodoResponse.From(completed.Item));
            case CompletionResult.AlreadyDone alreadyDone:
                TodoLogs.CompleteAlreadyDone(logger, TodoOperation.Complete.Name, alreadyDone.Item.Id);
                return TypedResults.Ok(TodoResponse.From(alreadyDone.Item));
            default:
                throw new UnreachableException();
        }
    }
}
