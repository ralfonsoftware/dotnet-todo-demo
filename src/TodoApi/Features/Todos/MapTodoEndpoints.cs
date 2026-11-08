namespace TodoApi.Features.Todos;

public static class TodoEndpoints
{
    public static IEndpointRouteBuilder MapTodoEndpoints(this IEndpointRouteBuilder app)
    {
        ListTodos.Map(app);
        GetTodo.Map(app);
        CreateTodo.Map(app);

        return app;
    }
}
