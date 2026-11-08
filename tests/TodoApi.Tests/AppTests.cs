using System.Net;
using Shouldly;

namespace TodoApi.Tests;

public sealed class AppTests(TodoApiFactory factory) : IClassFixture<TodoApiFactory>
{
    [Fact]
    public async Task OpenApiDocument_IsServed()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ScalarUi_IsServed()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/scalar", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
