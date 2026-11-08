using Microsoft.AspNetCore.Http.HttpResults;
using TodoApi.Contracts;
using TodoApi.Logging;
using TodoApi.Storage;
using TodoApi.Telemetry;

namespace TodoApi.Features.Todos;

internal static class ListTodos
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/todos", List)
            .WithName("ListTodos")
            .Produces<TodoResponse[]>();

    private static Ok<TodoResponse[]> List(
        ITodoStore store,
        ILogger<TodoLogCategory> logger)
    {
        var items = store.GetAll();

        TodoLogs.ListSucceeded(logger, TodoOperation.List.Name, items.Count);

        return TypedResults.Ok(items.Select(TodoResponse.From).ToArray());
    }
}
