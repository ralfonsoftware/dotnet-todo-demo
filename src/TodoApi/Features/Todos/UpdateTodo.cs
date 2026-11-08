using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.HttpResults;
using TodoApi.Contracts;
using TodoApi.Domain;
using TodoApi.Logging;
using TodoApi.Storage;
using TodoApi.Telemetry;

namespace TodoApi.Features.Todos;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateTodoRequest(
    string? Title,
    string? Description,
    Priority? Priority,
    DateOnly? DueDate);

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
        if (!TodoDraft.TryCreate(
                request.Title,
                request.Description,
                request.Priority,
                request.DueDate,
                out var draft,
                out var errors))
        {
            TodoLogs.UpdateValidationRejected(logger, TodoOperation.Update.Name, id, errors.Count);
            return TypedResults.ValidationProblem(errors);
        }

        var item = store.Update(id, draft);

        return item is null
            ? TodoResults.NotFound()
            : TypedResults.Ok(TodoResponse.From(item));
    }
}
