using HexMaster.ThePrey.Maui.App.Services.Location;

namespace HexMaster.ThePrey.Maui.App.Tests;

public class LocationConsentPolicyTests
{
    [Fact]
    public void ShouldShowDisclosure_ShouldReturnFalse_WhenAcceptedAndPermissionGranted()
    {
        var result = LocationConsentPolicy.ShouldShowDisclosure(hasAcceptedConsent: true, permissionGranted: true);

        Assert.False(result);
    }

    [Fact]
    public void ShouldShowDisclosure_ShouldReturnTrue_WhenNeverAccepted()
    {
        var result = LocationConsentPolicy.ShouldShowDisclosure(hasAcceptedConsent: false, permissionGranted: true);

        Assert.True(result);
    }

    [Fact]
    public void ShouldShowDisclosure_ShouldReturnTrue_WhenAcceptedButPermissionRevoked()
    {
        var result = LocationConsentPolicy.ShouldShowDisclosure(hasAcceptedConsent: true, permissionGranted: false);

        Assert.True(result);
    }

    [Fact]
    public void ShouldShowDisclosure_ShouldReturnTrue_WhenNeitherAcceptedNorGranted()
    {
        var result = LocationConsentPolicy.ShouldShowDisclosure(hasAcceptedConsent: false, permissionGranted: false);

        Assert.True(result);
    }
}
