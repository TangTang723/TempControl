using PrismTemperatureMonitor.Models;

namespace PrismTemperatureMonitor.Services;

public interface ILaserDeviceService
{
    IReadOnlyList<LaserDeviceConfig> GetDevices();

    void SaveDevices(IEnumerable<LaserDeviceConfig> devices);

    void AddOrUpdateDevice(LaserDeviceConfig device);

    void RemoveDevice(Guid deviceId);

    void Connect(Guid deviceId);

    void Disconnect(Guid deviceId);

    bool IsConnected(Guid deviceId);

    LaserDeviceCapabilities GetCapabilities(Guid deviceId);

    void ConnectAllAndReadWaveSettings();

    LaserWaveSettings? GetCachedWaveSettings(Guid deviceId);

    LaserWaveSettings ReadWaveSettings(Guid deviceId);
    LaserWaveSettings ReadWaveNumberSettings(Guid deviceId,int number);

    LaserWaveSettings WriteWaveSettings(Guid deviceId, LaserWaveSettings settings);

    LaserWaveSettings SwitchWave(Guid deviceId, int waveNumber);

    HwdLaserWaveSettings? GetCachedHwdWaveSettings(Guid deviceId);

    HwdLaserWaveSettings ReadHwdWaveSettings(Guid deviceId);

    HwdLaserWaveSettings ReadHwdWaveNumberSettings(Guid deviceId, int waveNumber);

    HwdLaserWaveSettings WriteHwdWaveSettings(Guid deviceId, HwdLaserWaveSettings settings);

    HwdLaserWaveSettings SwitchHwdWave(Guid deviceId, int waveNumber);

    HwdLaserSystemSettings? GetCachedHwdSystemSettings(Guid deviceId);

    HwdLaserSystemSettings ReadHwdSystemSettings(Guid deviceId);

    HwdLaserSystemSettings WriteHwdSystemSettings(Guid deviceId, HwdLaserSystemSettings settings);

    LaserSystemSettings? GetCachedSystemSettings(Guid deviceId);

    LaserSystemSettings ReadSystemSettings(Guid deviceId);

    LaserSystemSettings WriteSystemSettings(Guid deviceId, LaserSystemSettings settings);

    LaserRealtimeStatus? GetCachedRealtimeStatus(Guid deviceId);

    LaserDisplaySnapshot? GetCachedDisplaySnapshot(Guid deviceId);

    LaserRealtimeStatus ReadRealtimeStatus(Guid deviceId);

    LaserRealtimeStatus ReadHwdFastRealtimeStatus(Guid deviceId);

    void SetContinuousOutput(Guid deviceId, bool enabled);

    void PointWeld(Guid deviceId);

    void SetLaserLock(Guid deviceId, bool locked);

    void SetRedLight(Guid deviceId, bool enabled);

    void ClearPointCount(Guid deviceId);

    void ClearError(Guid deviceId);
}
