using PrismTemperatureMonitor.Models;

namespace PrismTemperatureMonitor.Services;

public interface ILaserDeviceSession : IDisposable
{
    LaserDeviceConfig Config { get; }

    bool IsConnected { get; }

    LaserRealtimeStatus? CachedRealtimeStatus { get; }

    void UpdateConfig(LaserDeviceConfig config);

    void Connect();

    void Disconnect();

    LaserRealtimeStatus ReadRealtimeStatus();

    void SetContinuousOutput(bool enabled);

    void PointWeld();

    void SetLaserLock(bool locked);

    void SetRedLight(bool enabled);

    void ClearPointCount();

    void ClearError();
}
