using Shouldly;
using TodoApi.Domain;
using TodoApi.Storage;
using TodoApi.Telemetry;

namespace TodoApi.Tests.Storage;

[Collection(TracingCollection.Name)]
public sealed class InstrumentedTodoStoreTests
{
    [Fact]
    public void List_EmitsASingleFoundSpan()
    {
        using var meterFactory = TestMeters.NewFactory();
        var store = new InstrumentedTodoStore(new InMemoryTodoStore(), meterFactory);
        using var capture = new ActivityCapture(TodoTelemetry.ActivitySourceName);

        store.GetAll();

        var span = capture.Single(TodoTelemetry.ActivitySourceName);
        span.DisplayName.ShouldBe(TodoOperation.List.SpanName);
        span.GetTagItem(TodoTelemetry.Tags.Operation).ShouldBe(TodoOperation.List.Name);
        span.GetTagItem(TodoTelemetry.Tags.Outcome).ShouldBe(TodoTelemetry.Outcomes.Found);
        span.GetTagItem(TodoTelemetry.Tags.Id).ShouldBeNull();
        span.GetTagItem(TodoTelemetry.Tags.Priority).ShouldBeNull();
    }

    [Fact]
    public void Get_WhenItemExists_EmitsASingleFoundSpan()
    {
        var inner = new InMemoryTodoStore();
        var item = inner.Create(Drafts.Of("Buy milk", "Semi-skimmed", Priority.High), DateTimeOffset.UnixEpoch);
        using var meterFactory = TestMeters.NewFactory();
        var store = new InstrumentedTodoStore(inner, meterFactory);
        using var capture = new ActivityCapture(TodoTelemetry.ActivitySourceName);

        var found = store.Get(item.Id);

        found.ShouldBe(item);
        var span = capture.Single(TodoTelemetry.ActivitySourceName);
        span.DisplayName.ShouldBe(TodoOperation.Get.SpanName);
        span.GetTagItem(TodoTelemetry.Tags.Operation).ShouldBe(TodoOperation.Get.Name);
        span.GetTagItem(TodoTelemetry.Tags.Id).ShouldBe(item.Id);
        span.GetTagItem(TodoTelemetry.Tags.Priority).ShouldBe("high");
        span.GetTagItem(TodoTelemetry.Tags.Outcome).ShouldBe(TodoTelemetry.Outcomes.Found);
    }

    [Fact]
    public void Get_WhenItemIsMissing_EmitsASingleNotFoundSpan()
    {
        using var meterFactory = TestMeters.NewFactory();
        var store = new InstrumentedTodoStore(new InMemoryTodoStore(), meterFactory);
        using var capture = new ActivityCapture(TodoTelemetry.ActivitySourceName);

        store.Get(42).ShouldBeNull();

        var span = capture.Single(TodoTelemetry.ActivitySourceName);
        span.DisplayName.ShouldBe(TodoOperation.Get.SpanName);
        span.GetTagItem(TodoTelemetry.Tags.Operation).ShouldBe(TodoOperation.Get.Name);
        span.GetTagItem(TodoTelemetry.Tags.Id).ShouldBe(42);
        span.GetTagItem(TodoTelemetry.Tags.Priority).ShouldBeNull();
        span.GetTagItem(TodoTelemetry.Tags.Outcome).ShouldBe(TodoTelemetry.Outcomes.NotFound);
    }

    [Fact]
    public void Create_EmitsASingleCreatedSpanWithIdAndPriority()
    {
        using var meterFactory = TestMeters.NewFactory();
        var store = new InstrumentedTodoStore(new InMemoryTodoStore(), meterFactory);
        using var capture = new ActivityCapture(TodoTelemetry.ActivitySourceName);

        var created = store.Create(Drafts.Of("Buy milk", "Semi-skimmed", Priority.Low), DateTimeOffset.UnixEpoch);

        var span = capture.Single(TodoTelemetry.ActivitySourceName);
        span.DisplayName.ShouldBe(TodoOperation.Create.SpanName);
        span.GetTagItem(TodoTelemetry.Tags.Operation).ShouldBe(TodoOperation.Create.Name);
        span.GetTagItem(TodoTelemetry.Tags.Id).ShouldBe(created.Id);
        span.GetTagItem(TodoTelemetry.Tags.Priority).ShouldBe("low");
        span.GetTagItem(TodoTelemetry.Tags.Outcome).ShouldBe(TodoTelemetry.Outcomes.Created);
    }

    [Fact]
    public void Update_WhenItemExists_EmitsASingleUpdatedSpan()
    {
        var inner = new InMemoryTodoStore();
        var item = inner.Create(Drafts.Of("Old title", "Old description", Priority.Low), DateTimeOffset.UnixEpoch);
        using var meterFactory = TestMeters.NewFactory();
        var store = new InstrumentedTodoStore(inner, meterFactory);
        using var capture = new ActivityCapture(TodoTelemetry.ActivitySourceName);

        var updated = store.Update(item.Id, Drafts.Of("New title", "New description", Priority.High));

        updated.ShouldNotBeNull();
        var span = capture.Single(TodoTelemetry.ActivitySourceName);
        span.DisplayName.ShouldBe(TodoOperation.Update.SpanName);
        span.GetTagItem(TodoTelemetry.Tags.Operation).ShouldBe(TodoOperation.Update.Name);
        span.GetTagItem(TodoTelemetry.Tags.Id).ShouldBe(item.Id);
        span.GetTagItem(TodoTelemetry.Tags.Priority).ShouldBe("high");
        span.GetTagItem(TodoTelemetry.Tags.Outcome).ShouldBe(TodoTelemetry.Outcomes.Updated);
    }

    [Fact]
    public void Update_WhenItemIsMissing_EmitsASingleNotFoundSpan()
    {
        using var meterFactory = TestMeters.NewFactory();
        var store = new InstrumentedTodoStore(new InMemoryTodoStore(), meterFactory);
        using var capture = new ActivityCapture(TodoTelemetry.ActivitySourceName);

        store.Update(42, Drafts.Of("New title", null, Priority.Medium)).ShouldBeNull();

        var span = capture.Single(TodoTelemetry.ActivitySourceName);
        span.DisplayName.ShouldBe(TodoOperation.Update.SpanName);
        span.GetTagItem(TodoTelemetry.Tags.Operation).ShouldBe(TodoOperation.Update.Name);
        span.GetTagItem(TodoTelemetry.Tags.Id).ShouldBe(42);
        span.GetTagItem(TodoTelemetry.Tags.Priority).ShouldBe("medium");
        span.GetTagItem(TodoTelemetry.Tags.Outcome).ShouldBe(TodoTelemetry.Outcomes.NotFound);
    }

    [Fact]
    public void Complete_WhenItemIsOpen_EmitsASingleCompletedSpanWithPreCompletionPriority()
    {
        var inner = new InMemoryTodoStore();
        var item = inner.Create(Drafts.Of("Buy milk", null, Priority.High), DateTimeOffset.UnixEpoch);
        using var meterFactory = TestMeters.NewFactory();
        var store = new InstrumentedTodoStore(inner, meterFactory);
        using var capture = new ActivityCapture(TodoTelemetry.ActivitySourceName);

        var completed = store.Complete(item.Id, DateTimeOffset.UnixEpoch.AddHours(1))
            .ShouldBeOfType<CompletionResult.Completed>();

        completed.Item.Done.ShouldBeTrue();
        var span = capture.Single(TodoTelemetry.ActivitySourceName);
        span.DisplayName.ShouldBe(TodoOperation.Complete.SpanName);
        span.GetTagItem(TodoTelemetry.Tags.Operation).ShouldBe(TodoOperation.Complete.Name);
        span.GetTagItem(TodoTelemetry.Tags.Id).ShouldBe(item.Id);
        span.GetTagItem(TodoTelemetry.Tags.Priority).ShouldBe("high");
        span.GetTagItem(TodoTelemetry.Tags.Outcome).ShouldBe(TodoTelemetry.Outcomes.Completed);
    }

    [Fact]
    public void Complete_WhenItemIsAlreadyDone_EmitsASingleAlreadyDoneSpan()
    {
        var inner = new InMemoryTodoStore();
        var item = inner.Create(Drafts.Of("Buy milk", null, Priority.Medium), DateTimeOffset.UnixEpoch);
        inner.Complete(item.Id, DateTimeOffset.UnixEpoch.AddHours(1));
        using var meterFactory = TestMeters.NewFactory();
        var store = new InstrumentedTodoStore(inner, meterFactory);
        using var capture = new ActivityCapture(TodoTelemetry.ActivitySourceName);

        var completed = store.Complete(item.Id, DateTimeOffset.UnixEpoch.AddHours(2))
            .ShouldBeOfType<CompletionResult.AlreadyDone>();

        completed.Item.CompletedAt.ShouldBe(DateTimeOffset.UnixEpoch.AddHours(1));
        var span = capture.Single(TodoTelemetry.ActivitySourceName);
        span.DisplayName.ShouldBe(TodoOperation.Complete.SpanName);
        span.GetTagItem(TodoTelemetry.Tags.Operation).ShouldBe(TodoOperation.Complete.Name);
        span.GetTagItem(TodoTelemetry.Tags.Id).ShouldBe(item.Id);
        span.GetTagItem(TodoTelemetry.Tags.Priority).ShouldBe("medium");
        span.GetTagItem(TodoTelemetry.Tags.Outcome).ShouldBe(TodoTelemetry.Outcomes.AlreadyDone);
    }

    [Fact]
    public void Complete_WhenItemIsMissing_EmitsASingleNotFoundSpan()
    {
        using var meterFactory = TestMeters.NewFactory();
        var store = new InstrumentedTodoStore(new InMemoryTodoStore(), meterFactory);
        using var capture = new ActivityCapture(TodoTelemetry.ActivitySourceName);

        store.Complete(42, DateTimeOffset.UnixEpoch)
            .ShouldBeOfType<CompletionResult.NotFound>();

        var span = capture.Single(TodoTelemetry.ActivitySourceName);
        span.DisplayName.ShouldBe(TodoOperation.Complete.SpanName);
        span.GetTagItem(TodoTelemetry.Tags.Operation).ShouldBe(TodoOperation.Complete.Name);
        span.GetTagItem(TodoTelemetry.Tags.Id).ShouldBe(42);
        span.GetTagItem(TodoTelemetry.Tags.Priority).ShouldBeNull();
        span.GetTagItem(TodoTelemetry.Tags.Outcome).ShouldBe(TodoTelemetry.Outcomes.NotFound);
    }

    [Fact]
    public void Delete_WhenItemExists_EmitsASingleDeletedSpanWithPriority()
    {
        var inner = new InMemoryTodoStore();
        var item = inner.Create(Drafts.Of("Buy milk", null, Priority.Low), DateTimeOffset.UnixEpoch);
        using var meterFactory = TestMeters.NewFactory();
        var store = new InstrumentedTodoStore(inner, meterFactory);
        using var capture = new ActivityCapture(TodoTelemetry.ActivitySourceName);

        store.Delete(item.Id).ShouldNotBeNull();

        var span = capture.Single(TodoTelemetry.ActivitySourceName);
        span.DisplayName.ShouldBe(TodoOperation.Delete.SpanName);
        span.GetTagItem(TodoTelemetry.Tags.Operation).ShouldBe(TodoOperation.Delete.Name);
        span.GetTagItem(TodoTelemetry.Tags.Id).ShouldBe(item.Id);
        span.GetTagItem(TodoTelemetry.Tags.Priority).ShouldBe("low");
        span.GetTagItem(TodoTelemetry.Tags.Outcome).ShouldBe(TodoTelemetry.Outcomes.Deleted);
    }

    [Fact]
    public void Delete_WhenItemIsMissing_EmitsASingleNotFoundSpan()
    {
        using var meterFactory = TestMeters.NewFactory();
        var store = new InstrumentedTodoStore(new InMemoryTodoStore(), meterFactory);
        using var capture = new ActivityCapture(TodoTelemetry.ActivitySourceName);

        store.Delete(42).ShouldBeNull();

        var span = capture.Single(TodoTelemetry.ActivitySourceName);
        span.DisplayName.ShouldBe(TodoOperation.Delete.SpanName);
        span.GetTagItem(TodoTelemetry.Tags.Operation).ShouldBe(TodoOperation.Delete.Name);
        span.GetTagItem(TodoTelemetry.Tags.Id).ShouldBe(42);
        span.GetTagItem(TodoTelemetry.Tags.Priority).ShouldBeNull();
        span.GetTagItem(TodoTelemetry.Tags.Outcome).ShouldBe(TodoTelemetry.Outcomes.NotFound);
    }

    [Fact]
    public void EachStoreOperation_EmitsExactlyOneSpan()
    {
        var inner = new InMemoryTodoStore();
        using var meterFactory = TestMeters.NewFactory();
        var store = new InstrumentedTodoStore(inner, meterFactory);
        using var capture = new ActivityCapture(TodoTelemetry.ActivitySourceName);

        var item = store.Create(Drafts.Of("Buy milk", null, Priority.Medium), DateTimeOffset.UnixEpoch);
        store.GetAll();
        store.Get(item.Id);
        store.Update(item.Id, Drafts.Of("Buy oat milk", null, Priority.High));
        store.Complete(item.Id, DateTimeOffset.UnixEpoch.AddHours(1));
        store.Delete(item.Id);

        var spans = capture.From(TodoTelemetry.ActivitySourceName);
        spans.Count.ShouldBe(6);

        foreach (var operation in new[]
        {
            TodoOperation.List,
            TodoOperation.Get,
            TodoOperation.Create,
            TodoOperation.Update,
            TodoOperation.Complete,
            TodoOperation.Delete
        })
        {
            spans.Count(span => Equals(
                span.GetTagItem(TodoTelemetry.Tags.Operation), operation.Name)).ShouldBe(1);
        }
    }

    [Fact]
    public void StoreSpans_NeverTagTitleOrDescription()
    {
        const string title = "Private title 5f0c";
        const string description = "Private description 5f0c";

        var inner = new InMemoryTodoStore();
        using var meterFactory = TestMeters.NewFactory();
        var store = new InstrumentedTodoStore(inner, meterFactory);
        using var capture = new ActivityCapture(TodoTelemetry.ActivitySourceName);

        var item = store.Create(Drafts.Of(title, description, Priority.High), DateTimeOffset.UnixEpoch);
        store.GetAll();
        store.Get(item.Id);
        store.Update(item.Id, Drafts.Of(title, description, Priority.Low));
        store.Complete(item.Id, DateTimeOffset.UnixEpoch.AddHours(1));
        store.Delete(item.Id);

        var spans = capture.From(TodoTelemetry.ActivitySourceName);
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
