using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.HttpResults;
using TodoApi.Contracts;
using TodoApi.Domain;
using TodoApi.Storage;

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
        ITodoStore store)
    {
        var errors = TodoInput.Validate(request.Title, request.Description);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var item = store.Update(
            id,
            TodoInput.NormalizeTitle(request.Title!),
            TodoInput.NormalizeDescription(request.Description),
            request.Priority ?? Priority.Medium);

        return item is null
            ? TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Todo item not found.")
            : TypedResults.Ok(TodoResponse.From(item));
    }
}
