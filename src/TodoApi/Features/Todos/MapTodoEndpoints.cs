namespace TodoApi.Features.Todos;

public static class TodoEndpoints
{
    public static IEndpointRouteBuilder MapTodoEndpoints(this IEndpointRouteBuilder app)
    {
        ListTodos.Map(app);
        GetTodo.Map(app);

        return app;
    }
}
