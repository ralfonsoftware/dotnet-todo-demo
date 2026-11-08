using Shouldly;
using TodoApi.Telemetry;

namespace TodoApi.Tests.Telemetry;

/// <summary>
/// Pins the literal observability schema. This is the only test that names the
/// strings; every other test and the production code reference
/// <see cref="TodoOperation"/> and <see cref="TodoTelemetry"/> instead.
/// </summary>
public sealed class TodoVocabularyTests
{
    [Fact]
    public void Vocabulary_PinsEverySourceMeterOperationTagInstrumentUnitAndOutcome()
    {
        TodoOperation.List.Name.ShouldBe("list");
        TodoOperation.Get.Name.ShouldBe("get");
        TodoOperation.Create.Name.ShouldBe("create");
        TodoOperation.Update.Name.ShouldBe("update");
        TodoOperation.Complete.Name.ShouldBe("complete");
        TodoOperation.Delete.Name.ShouldBe("delete");

        TodoOperation.List.SpanName.ShouldBe("todos.list");
        TodoOperation.Get.SpanName.ShouldBe("todos.get");
        TodoOperation.Create.SpanName.ShouldBe("todos.create");
        TodoOperation.Update.SpanName.ShouldBe("todos.update");
        TodoOperation.Complete.SpanName.ShouldBe("todos.complete");
        TodoOperation.Delete.SpanName.ShouldBe("todos.delete");

        TodoOperation.List.ToString().ShouldBe("list");
        TodoOperation.Get.ToString().ShouldBe("get");
        TodoOperation.Create.ToString().ShouldBe("create");
        TodoOperation.Update.ToString().ShouldBe("update");
        TodoOperation.Complete.ToString().ShouldBe("complete");
        TodoOperation.Delete.ToString().ShouldBe("delete");

        TodoTelemetry.ActivitySourceName.ShouldBe("TodoApi.TodosStore");
        TodoTelemetry.MeterName.ShouldBe("TodoApi.Todos");

        TodoTelemetry.Tags.Operation.ShouldBe("todo.operation");
        TodoTelemetry.Tags.Id.ShouldBe("todo.id");
        TodoTelemetry.Tags.Priority.ShouldBe("todo.priority");
        TodoTelemetry.Tags.Outcome.ShouldBe("todo.outcome");

        TodoTelemetry.Instruments.ItemsCreated.ShouldBe("todo.items.created");
        TodoTelemetry.Instruments.ItemsCompleted.ShouldBe("todo.items.completed");
        TodoTelemetry.Instruments.ItemsDeleted.ShouldBe("todo.items.deleted");
        TodoTelemetry.Instruments.StoreFailures.ShouldBe("todo.store.failures");
        TodoTelemetry.Instruments.ItemsTotal.ShouldBe("todo.items.total");
        TodoTelemetry.Instruments.ItemsDone.ShouldBe("todo.items.done");
        TodoTelemetry.Instruments.OperationDuration.ShouldBe("todo.store.operation.duration");

        TodoTelemetry.Units.Items.ShouldBe("{item}");
        TodoTelemetry.Units.Failures.ShouldBe("{failure}");
        TodoTelemetry.Units.Seconds.ShouldBe("s");

        TodoTelemetry.Outcomes.Found.ShouldBe("found");
        TodoTelemetry.Outcomes.NotFound.ShouldBe("not found");
        TodoTelemetry.Outcomes.Created.ShouldBe("created");
        TodoTelemetry.Outcomes.Updated.ShouldBe("updated");
        TodoTelemetry.Outcomes.Completed.ShouldBe("completed");
        TodoTelemetry.Outcomes.AlreadyDone.ShouldBe("already done");
        TodoTelemetry.Outcomes.Deleted.ShouldBe("deleted");
    }
}
