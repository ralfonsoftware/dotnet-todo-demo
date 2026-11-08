using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.HttpResults;
using TodoApi.Contracts;
using TodoApi.Domain;
using TodoApi.Logging;
using TodoApi.Storage;
using TodoApi.Telemetry;

namespace TodoApi.Features.Todos;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateTodoRequest(string? Title, string? Description, Priority? Priority);

internal static class CreateTodo
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/todos", Create)
            .WithName("CreateTodo")
            .Produces<TodoResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status415UnsupportedMediaType);

    private static Results<Created<TodoResponse>, ValidationProblem> Create(
        CreateTodoRequest request,
        ITodoStore store,
        TimeProvider clock,
        ILogger<TodoLogCategory> logger)
    {
        if (!TodoDraft.TryCreate(
                request.Title,
                request.Description,
                request.Priority,
                out var draft,
                out var errors))
        {
            TodoLogs.CreateValidationRejected(logger, TodoOperation.Create.Name, errors.Count);
            return TypedResults.ValidationProblem(errors);
        }

        var item = store.Create(
            draft.Title,
            draft.Description,
            draft.Priority,
            clock.GetUtcNow());

        TodoLogs.CreateSucceeded(logger, TodoOperation.Create.Name, item.Id, item.Priority);

        return TypedResults.Created($"/todos/{item.Id}", TodoResponse.From(item));
    }
}
