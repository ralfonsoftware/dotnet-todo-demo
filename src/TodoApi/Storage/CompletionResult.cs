using TodoApi.Domain;

namespace TodoApi.Storage;

public abstract record CompletionResult
{
    private CompletionResult()
    {
    }

    public sealed record NotFound : CompletionResult;

    public sealed record Completed(TodoItem Item) : CompletionResult;

    public sealed record AlreadyDone(TodoItem Item) : CompletionResult;
}
