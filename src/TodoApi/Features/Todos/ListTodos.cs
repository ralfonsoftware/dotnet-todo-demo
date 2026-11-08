using Microsoft.AspNetCore.Http.HttpResults;
using TodoApi.Contracts;
using TodoApi.Storage;

namespace TodoApi.Features.Todos;

internal static class ListTodos
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/todos", List)
            .WithName("ListTodos")
            .Produces<TodoResponse[]>();

    private static Ok<TodoResponse[]> List(ITodoStore store) =>
        TypedResults.Ok(store.GetAll().Select(TodoResponse.From).ToArray());
}
