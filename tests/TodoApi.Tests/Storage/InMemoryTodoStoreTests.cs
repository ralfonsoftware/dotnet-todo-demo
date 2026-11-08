using Shouldly;
using TodoApi.Domain;
using TodoApi.Storage;

namespace TodoApi.Tests.Storage;

public sealed class InMemoryTodoStoreTests
{
    [Fact]
    public void Complete_WhenItemIsMissing_ReturnsNotFound()
    {
        var store = new InMemoryTodoStore();

        store.Complete(42, DateTimeOffset.UnixEpoch)
            .ShouldBeOfType<CompletionResult.NotFound>();
    }

    [Fact]
    public void Complete_WhenItemIsOpen_ReturnsCompletedWithDoneAndTheSuppliedTimestamp()
    {
        var store = new InMemoryTodoStore();
        var item = store.Create(Drafts.Of("Buy milk", null, Priority.High), DateTimeOffset.UnixEpoch);

        var result = store.Complete(item.Id, DateTimeOffset.UnixEpoch.AddHours(1))
            .ShouldBeOfType<CompletionResult.Completed>();

        result.Item.Done.ShouldBeTrue();
        result.Item.CompletedAt.ShouldBe(DateTimeOffset.UnixEpoch.AddHours(1));
        store.Get(item.Id).ShouldBe(result.Item);
    }

    [Fact]
    public void Complete_WhenItemIsAlreadyDone_ReturnsAlreadyDoneAndKeepsTheOriginalTimestamp()
    {
        var store = new InMemoryTodoStore();
        var item = store.Create(Drafts.Of("Buy milk", null, Priority.Medium), DateTimeOffset.UnixEpoch);
        store.Complete(item.Id, DateTimeOffset.UnixEpoch.AddHours(1))
            .ShouldBeOfType<CompletionResult.Completed>();

        var result = store.Complete(item.Id, DateTimeOffset.UnixEpoch.AddHours(2))
            .ShouldBeOfType<CompletionResult.AlreadyDone>();

        result.Item.Done.ShouldBeTrue();
        result.Item.CompletedAt.ShouldBe(DateTimeOffset.UnixEpoch.AddHours(1));
        store.Get(item.Id).ShouldBe(result.Item);
    }
}
