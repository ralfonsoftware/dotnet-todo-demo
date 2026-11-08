namespace TodoApi.Tests;

public sealed class DeliberateFailureTests
{
    [Fact]
    public void Fails() => Assert.Fail("Deliberate failure to prove CI turns red.");
}
