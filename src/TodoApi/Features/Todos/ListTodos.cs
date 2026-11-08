using TodoApi.Contracts;
using TodoApi.Storage;

namespace TodoApi.Features.Todos;

internal static class ListTodos
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/todos", (ITodoStore store) =>
                TypedResults.Ok(store.GetAll().Select(TodoResponse.From).ToArray()))
            .WithName("ListTodos")
            .Produces<TodoResponse[]>();
}
