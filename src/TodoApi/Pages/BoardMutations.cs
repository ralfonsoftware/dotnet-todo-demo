using Microsoft.Extensions.Logging;
using TodoApi.Domain;
using TodoApi.Logging;
using TodoApi.Storage;
using TodoApi.Telemetry;

namespace TodoApi.Pages;

/// <summary>
/// Performs board mutations against the Todos Store and emits the Todo Item
/// outcome events shared with the HTTP slices.
/// </summary>
internal sealed class BoardMutations(ITodoStore store, ILogger logger)
{
    public TodoItem Create(TodoDraft draft, DateTimeOffset now)
    {
        var item = store.Create(draft.Title, draft.Description, draft.Priority, now);
        TodoLogs.CreateSucceeded(logger, TodoOperation.Create.Name, item.Id, item.Priority);

        return item;
    }

    public TodoItem? Update(int id, TodoDraft draft)
    {
        var item = store.Update(id, draft.Title, draft.Description, draft.Priority);

        if (item is null)
        {
            TodoLogs.NotFound(logger, id, TodoOperation.Update.Name);
        }
        else
        {
            TodoLogs.UpdateSucceeded(logger, TodoOperation.Update.Name, item.Id, item.Priority);
        }

        return item;
    }

    public CompletionResult Complete(int id, DateTimeOffset now)
    {
        var result = store.Complete(id, now);

        switch (result)
        {
            case CompletionResult.NotFound:
                TodoLogs.NotFound(logger, id, TodoOperation.Complete.Name);
                break;
            case CompletionResult.Completed completed:
                TodoLogs.CompleteSucceeded(
                    logger, TodoOperation.Complete.Name, completed.Item.Id, completed.Item.Priority);
                break;
            case CompletionResult.AlreadyDone alreadyDone:
                TodoLogs.CompleteAlreadyDone(
                    logger, TodoOperation.Complete.Name, alreadyDone.Item.Id);
                break;
        }

        return result;
    }

    public TodoItem? Delete(int id)
    {
        var item = store.Delete(id);

        if (item is null)
        {
            TodoLogs.NotFound(logger, id, TodoOperation.Delete.Name);
        }
        else
        {
            TodoLogs.DeleteSucceeded(logger, TodoOperation.Delete.Name, item.Id, item.Priority);
        }

        return item;
    }

    public void CreateRejected(int errorCount) =>
        TodoLogs.CreateValidationRejected(logger, TodoOperation.Create.Name, errorCount);

    public void UpdateRejected(int id, int errorCount) =>
        TodoLogs.UpdateValidationRejected(logger, TodoOperation.Update.Name, id, errorCount);
}
