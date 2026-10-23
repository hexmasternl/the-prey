using HexMaster.ThePrey.Games.Features.CheckAppVersion;
using Microsoft.Extensions.Configuration;

namespace HexMaster.ThePrey.Games.Tests.Features.CheckAppVersion;

public sealed class CheckAppVersionQueryHandlerTests
{
    private static CheckAppVersionQueryHandler CreateSut(string? minimumVersion)
    {
        var settings = new Dictionary<string, string?>
        {
            [CheckAppVersionQueryHandler.MinimumVersionConfigurationKey] = minimumVersion
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
        return new CheckAppVersionQueryHandler(configuration);
    }

    [Fact]
    public async Task Handle_ShouldReturnUpdateRequired_WhenBelowMinimum()
    {
        var sut = CreateSut("1.2.0");

        var result = await sut.Handle(new CheckAppVersionQuery("1.1.9"), CancellationToken.None);

        Assert.Equal(AppVersionCheckResult.UpdateRequired, result);
    }

    [Fact]
    public async Task Handle_ShouldReturnUpToDate_WhenEqualToMinimum()
    {
        var sut = CreateSut("1.2.0");

        var result = await sut.Handle(new CheckAppVersionQuery("1.2.0"), CancellationToken.None);

        Assert.Equal(AppVersionCheckResult.UpToDate, result);
    }

    [Fact]
    public async Task Handle_ShouldReturnUpToDate_WhenAboveMinimum()
    {
        var sut = CreateSut("1.2.0");

        var result = await sut.Handle(new CheckAppVersionQuery("2.0.0"), CancellationToken.None);

        Assert.Equal(AppVersionCheckResult.UpToDate, result);
    }

    [Fact]
    public async Task Handle_ShouldCompareNumerically_WhenComponentsHaveDifferentDigitCounts()
    {
        // Lexically "1.10.0" < "1.9.0"; numerically it is greater and must pass.
        var sut = CreateSut("1.9.0");

        var result = await sut.Handle(new CheckAppVersionQuery("1.10.0"), CancellationToken.None);

        Assert.Equal(AppVersionCheckResult.UpToDate, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_ShouldReturnUpToDate_WhenMinimumNotConfigured(string? minimum)
    {
        var sut = CreateSut(minimum);

        var result = await sut.Handle(new CheckAppVersionQuery("0.0.1"), CancellationToken.None);

        Assert.Equal(AppVersionCheckResult.UpToDate, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-version")]
    [InlineData("1.x.0")]
    [InlineData("1.2.3.4")]
    public async Task Handle_ShouldThrowArgumentException_WhenClientVersionMalformed(string? version)
    {
        var sut = CreateSut("1.2.0");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.Handle(new CheckAppVersionQuery(version), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenQueryIsNull()
    {
        var sut = CreateSut("1.2.0");

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            sut.Handle(null!, CancellationToken.None));
    }
}
