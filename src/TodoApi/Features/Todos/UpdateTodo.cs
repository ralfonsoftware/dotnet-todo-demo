using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.HttpResults;
using TodoApi.Contracts;
using TodoApi.Domain;
using TodoApi.Logging;
using TodoApi.Storage;
using TodoApi.Telemetry;

namespace TodoApi.Features.Todos;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateTodoRequest(string? Title, string? Description, Priority? Priority);

internal static class UpdateTodo
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPut("/todos/{id:int}", Update)
            .WithName("UpdateTodo")
            .Produces<TodoResponse>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static Results<Ok<TodoResponse>, ValidationProblem, ProblemHttpResult> Update(
        int id,
        UpdateTodoRequest request,
        ITodoStore store,
        ILogger<TodoLogCategory> logger)
    {
        var errors = TodoInput.Validate(request.Title, request.Description);
        if (errors.Count > 0)
        {
            TodoLogs.UpdateValidationRejected(logger, TodoOperation.Update.Name, id, errors.Count);
            return TypedResults.ValidationProblem(errors);
        }

        var item = store.Update(
            id,
            TodoInput.NormalizeTitle(request.Title!),
            TodoInput.NormalizeDescription(request.Description),
            request.Priority ?? Priority.Medium);

        if (item is null)
        {
            TodoLogs.NotFound(logger, id, TodoOperation.Update.Name);
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Todo item not found.");
        }

        TodoLogs.UpdateSucceeded(logger, TodoOperation.Update.Name, item.Id, item.Priority);
        return TypedResults.Ok(TodoResponse.From(item));
    }
}
