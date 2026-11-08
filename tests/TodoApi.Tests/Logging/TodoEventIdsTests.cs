using System.Reflection;
using Shouldly;
using TodoApi.Logging;

namespace TodoApi.Tests.Logging;

public sealed class TodoEventIdsTests
{
    [Fact]
    public void EventIds_AreUnique_AndStayInTheDocumentedRanges()
    {
        var ids = typeof(TodoEventIds)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(int))
            .Select(field => (int)field.GetRawConstantValue()!)
            .ToArray();

        ids.ShouldNotBeEmpty();
        ids.Distinct().Count().ShouldBe(ids.Length);
        ids.ShouldAllBe(id => id >= 1000 && id <= 1399);
    }
}
