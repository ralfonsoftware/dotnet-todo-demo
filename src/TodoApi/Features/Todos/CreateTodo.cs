using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.HttpResults;
using TodoApi.Contracts;
using TodoApi.Domain;
using TodoApi.Storage;

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
        TimeProvider clock)
    {
        var errors = TodoInput.Validate(request.Title, request.Description);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var item = store.Create(
            TodoInput.NormalizeTitle(request.Title!),
            TodoInput.NormalizeDescription(request.Description),
            request.Priority ?? Priority.Medium,
            clock.GetUtcNow());

        return TypedResults.Created($"/todos/{item.Id}", TodoResponse.From(item));
    }
}
