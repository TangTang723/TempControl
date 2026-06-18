using PrismTemperatureMonitor.Models;
using PrismTemperatureMonitor.Services;

namespace PrismTemperatureMonitor.Tests;

public sealed class LaserDeviceStartupInitializerTests
{
    [Fact]
    public void Initialize_ConnectsAllDevicesOnlyOnce()
    {
        var service = new CountingLaserDeviceService();
        var polling = new CountingPollingService();
        var initializer = new LaserDeviceStartupInitializer(service, polling);

        initializer.Initialize();
        initializer.Initialize();

        Assert.Equal(1, service.ConnectAllCallCount);
        Assert.Equal(1, polling.StartCallCount);
    }

    private sealed class CountingPollingService : ILaserRealtimePollingService
    {
        public int StartCallCount { get; private set; }

        public void Start()
        {
            StartCallCount++;
        }
    }

    private sealed class CountingLaserDeviceService : ILaserDeviceService
    {
        public int ConnectAllCallCount { get; private set; }

        public IReadOnlyList<LaserDeviceConfig> GetDevices() => [];
        public void SaveDevices(IEnumerable<LaserDeviceConfig> devices) { }
        public void AddOrUpdateDevice(LaserDeviceConfig device) { }
        public void RemoveDevice(Guid deviceId) { }
        public void Connect(Guid deviceId) { }
        public void Disconnect(Guid deviceId) { }
        public bool IsConnected(Guid deviceId) => false;
        public LaserDeviceCapabilities GetCapabilities(Guid deviceId) => LaserDeviceCapabilities.None;
        public void ConnectAllAndReadWaveSettings() => ConnectAllCallCount++;
        public LaserWaveSettings? GetCachedWaveSettings(Guid deviceId) => null;
        public LaserWaveSettings ReadWaveSettings(Guid deviceId) => throw new NotSupportedException();
        public LaserWaveSettings ReadWaveNumberSettings(Guid deviceId, int number) => throw new NotSupportedException();
        public LaserWaveSettings WriteWaveSettings(Guid deviceId, LaserWaveSettings settings) => throw new NotSupportedException();
        public LaserWaveSettings SwitchWave(Guid deviceId, int waveNumber) => throw new NotSupportedException();
        public HwdLaserWaveSettings? GetCachedHwdWaveSettings(Guid deviceId) => null;
        public HwdLaserWaveSettings ReadHwdWaveSettings(Guid deviceId) => throw new NotSupportedException();
        public HwdLaserWaveSettings ReadHwdWaveNumberSettings(Guid deviceId, int waveNumber) => throw new NotSupportedException();
        public HwdLaserWaveSettings WriteHwdWaveSettings(Guid deviceId, HwdLaserWaveSettings settings) => throw new NotSupportedException();
        public HwdLaserWaveSettings SwitchHwdWave(Guid deviceId, int waveNumber) => throw new NotSupportedException();
        public HwdLaserSystemSettings? GetCachedHwdSystemSettings(Guid deviceId) => null;
        public HwdLaserSystemSettings ReadHwdSystemSettings(Guid deviceId) => throw new NotSupportedException();
        public HwdLaserSystemSettings WriteHwdSystemSettings(Guid deviceId, HwdLaserSystemSettings settings) => throw new NotSupportedException();
        public LaserSystemSettings? GetCachedSystemSettings(Guid deviceId) => null;
        public LaserSystemSettings ReadSystemSettings(Guid deviceId) => throw new NotSupportedException();
        public LaserSystemSettings WriteSystemSettings(Guid deviceId, LaserSystemSettings settings) => throw new NotSupportedException();
        public LaserRealtimeStatus? GetCachedRealtimeStatus(Guid deviceId) => null;
        public LaserDisplaySnapshot? GetCachedDisplaySnapshot(Guid deviceId) => null;
        public LaserRealtimeStatus ReadRealtimeStatus(Guid deviceId) => throw new NotSupportedException();
        public LaserRealtimeStatus ReadHwdFastRealtimeStatus(Guid deviceId) => throw new NotSupportedException();
        public void SetContinuousOutput(Guid deviceId, bool enabled) { }
        public void PointWeld(Guid deviceId) { }
        public void SetLaserLock(Guid deviceId, bool locked) { }
        public void SetRedLight(Guid deviceId, bool enabled) { }
        public void ClearPointCount(Guid deviceId) { }
        public void ClearError(Guid deviceId) { }
    }
}
