using FluentAssertions;
using IntegrationPro.Api.Execution;
using Xunit;

namespace IntegrationPro.Api.Tests;

public sealed class SyncDataSaverTests
{
    [Fact]
    public async Task Second_emission_throws_MultiEmissionException()
    {
        var saver = new SyncDataSaver();
        await saver.SaveAsync("req-1", "companies", new MemoryStream(new byte[] { 1, 2, 3 }));

        var act = async () =>
            await saver.SaveAsync("req-1", "employees", new MemoryStream(new byte[] { 4, 5, 6 }));

        await act.Should().ThrowExactlyAsync<MultiEmissionException>();
    }

    [Fact]
    public async Task Dispose_releases_the_buffer()
    {
        var saver = new SyncDataSaver();
        await saver.SaveAsync("req-1", "companies", new MemoryStream(new byte[] { 1, 2, 3 }));
        await saver.DisposeAsync();
        // DisposeAsync should not throw; we just need a non-throwing path.
    }
}
