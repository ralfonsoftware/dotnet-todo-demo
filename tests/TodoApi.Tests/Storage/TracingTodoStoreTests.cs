using Shouldly;
using TodoApi.Domain;
using TodoApi.Storage;

namespace TodoApi.Tests.Storage;

[Collection(TracingCollection.Name)]
public sealed class TracingTodoStoreTests
{
    [Fact]
    public void List_EmitsASingleFoundSpan()
    {
        var store = new TracingTodoStore(new InMemoryTodoStore());
        using var capture = new ActivityCapture(TracingTodoStore.ActivitySourceName);

        store.GetAll();

        var span = capture.Single(TracingTodoStore.ActivitySourceName);
        span.DisplayName.ShouldBe("todos.list");
        span.GetTagItem("todo.operation").ShouldBe("list");
        span.GetTagItem("todo.outcome").ShouldBe("found");
        span.GetTagItem("todo.id").ShouldBeNull();
        span.GetTagItem("todo.priority").ShouldBeNull();
    }

    [Fact]
    public void Get_WhenItemExists_EmitsASingleFoundSpan()
    {
        var inner = new InMemoryTodoStore();
        var item = inner.Create("Buy milk", "Semi-skimmed", Priority.High, DateTimeOffset.UnixEpoch);
        var store = new TracingTodoStore(inner);
        using var capture = new ActivityCapture(TracingTodoStore.ActivitySourceName);

        var found = store.Get(item.Id);

        found.ShouldBe(item);
        var span = capture.Single(TracingTodoStore.ActivitySourceName);
        span.DisplayName.ShouldBe("todos.get");
        span.GetTagItem("todo.operation").ShouldBe("get");
        span.GetTagItem("todo.id").ShouldBe(item.Id);
        span.GetTagItem("todo.priority").ShouldBe("high");
        span.GetTagItem("todo.outcome").ShouldBe("found");
    }

    [Fact]
    public void Get_WhenItemIsMissing_EmitsASingleNotFoundSpan()
    {
        var store = new TracingTodoStore(new InMemoryTodoStore());
        using var capture = new ActivityCapture(TracingTodoStore.ActivitySourceName);

        store.Get(42).ShouldBeNull();

        var span = capture.Single(TracingTodoStore.ActivitySourceName);
        span.DisplayName.ShouldBe("todos.get");
        span.GetTagItem("todo.operation").ShouldBe("get");
        span.GetTagItem("todo.id").ShouldBe(42);
        span.GetTagItem("todo.priority").ShouldBeNull();
        span.GetTagItem("todo.outcome").ShouldBe("not found");
    }

    [Fact]
    public void Create_EmitsASingleCreatedSpanWithIdAndPriority()
    {
        var store = new TracingTodoStore(new InMemoryTodoStore());
        using var capture = new ActivityCapture(TracingTodoStore.ActivitySourceName);

        var created = store.Create("Buy milk", "Semi-skimmed", Priority.Low, DateTimeOffset.UnixEpoch);

        var span = capture.Single(TracingTodoStore.ActivitySourceName);
        span.DisplayName.ShouldBe("todos.create");
        span.GetTagItem("todo.operation").ShouldBe("create");
        span.GetTagItem("todo.id").ShouldBe(created.Id);
        span.GetTagItem("todo.priority").ShouldBe("low");
        span.GetTagItem("todo.outcome").ShouldBe("created");
    }

    [Fact]
    public void Update_WhenItemExists_EmitsASingleUpdatedSpan()
    {
        var inner = new InMemoryTodoStore();
        var item = inner.Create("Old title", "Old description", Priority.Low, DateTimeOffset.UnixEpoch);
        var store = new TracingTodoStore(inner);
        using var capture = new ActivityCapture(TracingTodoStore.ActivitySourceName);

        var updated = store.Update(item.Id, "New title", "New description", Priority.High);

        updated.ShouldNotBeNull();
        var span = capture.Single(TracingTodoStore.ActivitySourceName);
        span.DisplayName.ShouldBe("todos.update");
        span.GetTagItem("todo.operation").ShouldBe("update");
        span.GetTagItem("todo.id").ShouldBe(item.Id);
        span.GetTagItem("todo.priority").ShouldBe("high");
        span.GetTagItem("todo.outcome").ShouldBe("updated");
    }

    [Fact]
    public void Update_WhenItemIsMissing_EmitsASingleNotFoundSpan()
    {
        var store = new TracingTodoStore(new InMemoryTodoStore());
        using var capture = new ActivityCapture(TracingTodoStore.ActivitySourceName);

        store.Update(42, "New title", null, Priority.Medium).ShouldBeNull();

        var span = capture.Single(TracingTodoStore.ActivitySourceName);
        span.DisplayName.ShouldBe("todos.update");
        span.GetTagItem("todo.operation").ShouldBe("update");
        span.GetTagItem("todo.id").ShouldBe(42);
        span.GetTagItem("todo.priority").ShouldBe("medium");
        span.GetTagItem("todo.outcome").ShouldBe("not found");
    }

    [Fact]
    public void Complete_WhenItemIsOpen_EmitsASingleCompletedSpanWithPreCompletionPriority()
    {
        var inner = new InMemoryTodoStore();
        var item = inner.Create("Buy milk", null, Priority.High, DateTimeOffset.UnixEpoch);
        var store = new TracingTodoStore(inner);
        using var capture = new ActivityCapture(TracingTodoStore.ActivitySourceName);

        var completed = store.Complete(item.Id, DateTimeOffset.UnixEpoch.AddHours(1));

        completed.ShouldNotBeNull();
        completed.Done.ShouldBeTrue();
        var span = capture.Single(TracingTodoStore.ActivitySourceName);
        span.DisplayName.ShouldBe("todos.complete");
        span.GetTagItem("todo.operation").ShouldBe("complete");
        span.GetTagItem("todo.id").ShouldBe(item.Id);
        span.GetTagItem("todo.priority").ShouldBe("high");
        span.GetTagItem("todo.outcome").ShouldBe("completed");
    }

    [Fact]
    public void Complete_WhenItemIsAlreadyDone_EmitsASingleAlreadyDoneSpan()
    {
        var inner = new InMemoryTodoStore();
        var item = inner.Create("Buy milk", null, Priority.Medium, DateTimeOffset.UnixEpoch);
        inner.Complete(item.Id, DateTimeOffset.UnixEpoch.AddHours(1));
        var store = new TracingTodoStore(inner);
        using var capture = new ActivityCapture(TracingTodoStore.ActivitySourceName);

        var completed = store.Complete(item.Id, DateTimeOffset.UnixEpoch.AddHours(2));

        completed.ShouldNotBeNull();
        completed.CompletedAt.ShouldBe(DateTimeOffset.UnixEpoch.AddHours(1));
        var span = capture.Single(TracingTodoStore.ActivitySourceName);
        span.DisplayName.ShouldBe("todos.complete");
        span.GetTagItem("todo.operation").ShouldBe("complete");
        span.GetTagItem("todo.id").ShouldBe(item.Id);
        span.GetTagItem("todo.priority").ShouldBe("medium");
        span.GetTagItem("todo.outcome").ShouldBe("already done");
    }

    [Fact]
    public void Complete_WhenItemIsMissing_EmitsASingleNotFoundSpan()
    {
        var store = new TracingTodoStore(new InMemoryTodoStore());
        using var capture = new ActivityCapture(TracingTodoStore.ActivitySourceName);

        store.Complete(42, DateTimeOffset.UnixEpoch).ShouldBeNull();

        var span = capture.Single(TracingTodoStore.ActivitySourceName);
        span.DisplayName.ShouldBe("todos.complete");
        span.GetTagItem("todo.operation").ShouldBe("complete");
        span.GetTagItem("todo.id").ShouldBe(42);
        span.GetTagItem("todo.priority").ShouldBeNull();
        span.GetTagItem("todo.outcome").ShouldBe("not found");
    }

    [Fact]
    public void Delete_WhenItemExists_EmitsASingleDeletedSpanWithPriority()
    {
        var inner = new InMemoryTodoStore();
        var item = inner.Create("Buy milk", null, Priority.Low, DateTimeOffset.UnixEpoch);
        var store = new TracingTodoStore(inner);
        using var capture = new ActivityCapture(TracingTodoStore.ActivitySourceName);

        store.Delete(item.Id).ShouldNotBeNull();

        var span = capture.Single(TracingTodoStore.ActivitySourceName);
        span.DisplayName.ShouldBe("todos.delete");
        span.GetTagItem("todo.operation").ShouldBe("delete");
        span.GetTagItem("todo.id").ShouldBe(item.Id);
        span.GetTagItem("todo.priority").ShouldBe("low");
        span.GetTagItem("todo.outcome").ShouldBe("deleted");
    }

    [Fact]
    public void Delete_WhenItemIsMissing_EmitsASingleNotFoundSpan()
    {
        var store = new TracingTodoStore(new InMemoryTodoStore());
        using var capture = new ActivityCapture(TracingTodoStore.ActivitySourceName);

        store.Delete(42).ShouldBeNull();

        var span = capture.Single(TracingTodoStore.ActivitySourceName);
        span.DisplayName.ShouldBe("todos.delete");
        span.GetTagItem("todo.operation").ShouldBe("delete");
        span.GetTagItem("todo.id").ShouldBe(42);
        span.GetTagItem("todo.priority").ShouldBeNull();
        span.GetTagItem("todo.outcome").ShouldBe("not found");
    }

    [Fact]
    public void EachStoreOperation_EmitsExactlyOneSpan()
    {
        var inner = new InMemoryTodoStore();
        var store = new TracingTodoStore(inner);
        using var capture = new ActivityCapture(TracingTodoStore.ActivitySourceName);

        var item = store.Create("Buy milk", null, Priority.Medium, DateTimeOffset.UnixEpoch);
        store.GetAll();
        store.Get(item.Id);
        store.Update(item.Id, "Buy oat milk", null, Priority.High);
        store.Complete(item.Id, DateTimeOffset.UnixEpoch.AddHours(1));
        store.Delete(item.Id);

        var spans = capture.From(TracingTodoStore.ActivitySourceName);
        spans.Count.ShouldBe(6);

        foreach (var operation in new[] { "list", "get", "create", "update", "complete", "delete" })
        {
            spans.Count(span => Equals(span.GetTagItem("todo.operation"), operation)).ShouldBe(1);
        }
    }

    [Fact]
    public void StoreSpans_NeverTagTitleOrDescription()
    {
        const string title = "Private title 5f0c";
        const string description = "Private description 5f0c";

        var inner = new InMemoryTodoStore();
        var store = new TracingTodoStore(inner);
        using var capture = new ActivityCapture(TracingTodoStore.ActivitySourceName);

        var item = store.Create(title, description, Priority.High, DateTimeOffset.UnixEpoch);
        store.GetAll();
        store.Get(item.Id);
        store.Update(item.Id, title, description, Priority.Low);
        store.Complete(item.Id, DateTimeOffset.UnixEpoch.AddHours(1));
        store.Delete(item.Id);

        var spans = capture.From(TracingTodoStore.ActivitySourceName);
        spans.Count.ShouldBe(6);

        foreach (var span in spans)
        {
            span.Events.ShouldBeEmpty();

            foreach (var tag in span.TagObjects)
            {
                var value = tag.Value?.ToString() ?? string.Empty;
                value.ShouldNotContain(title);
                value.ShouldNotContain(description);
            }
        }
    }
}
