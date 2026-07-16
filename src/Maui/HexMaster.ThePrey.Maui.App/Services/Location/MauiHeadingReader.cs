using Microsoft.Extensions.Logging;
using Microsoft.Maui.Devices.Sensors;

namespace HexMaster.ThePrey.Maui.App.Services.Location;

/// <summary>
/// Default <see cref="IHeadingReader"/> backed by the MAUI <see cref="ICompass"/>. Streams
/// <c>ReadingChanged</c> as heading degrees (clockwise from north). An unsupported/failed compass is
/// swallowed (logged) so the self arrow simply renders without rotation. Excluded from the unit-test
/// build (the view model is tested against a fake).
/// </summary>
public sealed class MauiHeadingReader : IHeadingReader
{
    private readonly ICompass _compass;
    private readonly ILogger<MauiHeadingReader> _logger;
    private bool _listening;

    public MauiHeadingReader(ICompass compass, ILogger<MauiHeadingReader> logger)
    {
        _compass = compass;
        _logger = logger;
    }

    public event Action<double>? HeadingChanged;

    public void Start()
    {
        if (_listening || !_compass.IsSupported)
            return;

        try
        {
            _compass.ReadingChanged += OnReadingChanged;
            _compass.Start(SensorSpeed.UI, applyLowPassFilter: true);
            _listening = true;
        }
        catch (Exception ex)
        {
            _compass.ReadingChanged -= OnReadingChanged;
            _logger.LogInformation(ex, "Compass heading unavailable.");
        }
    }

    public void Stop()
    {
        if (!_listening)
            return;
        _listening = false;
        _compass.ReadingChanged -= OnReadingChanged;
        try { _compass.Stop(); }
        catch (Exception ex) { _logger.LogInformation(ex, "Stopping the compass failed."); }
    }

    private void OnReadingChanged(object? sender, CompassChangedEventArgs e) =>
        HeadingChanged?.Invoke(e.Reading.HeadingMagneticNorth);
}
