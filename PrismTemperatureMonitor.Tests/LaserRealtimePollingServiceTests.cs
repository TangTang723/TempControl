using System.Reflection;
using PrismTemperatureMonitor.Models;
using PrismTemperatureMonitor.Services;

namespace PrismTemperatureMonitor.Tests;

public sealed class LaserRealtimePollingServiceTests
{
    [Fact]
    public void PollCycle_UsesModelSpecificRealtimeReads()
    {
        var service = new RecordingLaserDeviceService();
        var hwq = service.AddDevice(LaserDeviceModel.HWQ, isConnected: true);
        var hwd = service.AddDevice(LaserDeviceModel.HWD, isConnected: true);
        using var polling = new LaserRealtimePollingService(service);

        InvokePollCycle(polling, DateTime.UtcNow);

        Assert.Equal([hwq.Id, hwd.Id], service.FullRealtimeReads);
        Assert.Equal([hwd.Id], service.HwdFastRealtimeReads);
    }

    [Fact]
    public void PollCycle_RespectsHwqAndHwdIntervals()
    {
        var service = new RecordingLaserDeviceService();
        var hwq = service.AddDevice(LaserDeviceModel.HWQ, isConnected: true);
        var hwd = service.AddDevice(LaserDeviceModel.HWD, isConnected: true);
        using var polling = new LaserRealtimePollingService(service);
        var start = DateTime.UtcNow;

        InvokePollCycle(polling, start);
        InvokePollCycle(polling, start.AddMilliseconds(50));
        InvokePollCycle(polling, start.AddMilliseconds(200));
        InvokePollCycle(polling, start.AddSeconds(1));

        Assert.Equal(3, service.FullRealtimeReads.Count(deviceId => deviceId == hwq.Id));
        Assert.Equal(2, service.FullRealtimeReads.Count(deviceId => deviceId == hwd.Id));
        Assert.Equal(4, service.HwdFastRealtimeReads.Count(deviceId => deviceId == hwd.Id));
    }

    [Fact]
    public void PollCycle_SkipsDisconnectedDevicesAndContinuesAfterDeviceFailure()
    {
        var service = new RecordingLaserDeviceService();
        var failing = service.AddDevice(LaserDeviceModel.HWQ, isConnected: true);
        var disconnected = service.AddDevice(LaserDeviceModel.HWD, isConnected: false);
        var healthy = service.AddDevice(LaserDeviceModel.HWQ, isConnected: true);
        service.ThrowOnFullRead.Add(failing.Id);
        using var polling = new LaserRealtimePollingService(service);

        InvokePollCycle(polling, DateTime.UtcNow);

        Assert.Contains(failing.Id, service.FullRealtimeReads);
        Assert.Contains(healthy.Id, service.FullRealtimeReads);
        Assert.DoesNotContain(disconnected.Id, service.FullRealtimeReads);
        Assert.DoesNotContain(disconnected.Id, service.HwdFastRealtimeReads);
    }

    private static void InvokePollCycle(LaserRealtimePollingService polling, DateTime utcNow)
    {
        var method = typeof(LaserRealtimePollingService).GetMethod(
            "PollConnectedDevices",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(LaserRealtimePollingService), "PollConnectedDevices");
        method.Invoke(polling, [utcNow]);
    }

    private sealed class RecordingLaserDeviceService : ILaserDeviceService
    {
        private readonly List<LaserDeviceConfig> _devices = [];
        private readonly HashSet<Guid> _connected = [];

        public List<Guid> FullRealtimeReads { get; } = [];

        public List<Guid> HwdFastRealtimeReads { get; } = [];

        public HashSet<Guid> ThrowOnFullRead { get; } = [];

        public LaserDeviceConfig AddDevice(LaserDeviceModel model, bool isConnected)
        {
            var device = new LaserDeviceConfig
            {
                Name = model.ToString(),
                Model = model,
                IsEnabled = true
            };
            _devices.Add(device);
            if (isConnected)
            {
                _connected.Add(device.Id);
            }

            return device;
        }

        public IReadOnlyList<LaserDeviceConfig> GetDevices() => _devices.Select(device => device.Clone()).ToArray();
        public void SaveDevices(IEnumerable<LaserDeviceConfig> devices) { }
        public void AddOrUpdateDevice(LaserDeviceConfig device) { }
        public void RemoveDevice(Guid deviceId) { }
        public void Connect(Guid deviceId) => _connected.Add(deviceId);
        public void Disconnect(Guid deviceId) => _connected.Remove(deviceId);
        public bool IsConnected(Guid deviceId) => _connected.Contains(deviceId);
        public LaserDeviceCapabilities GetCapabilities(Guid deviceId) => LaserDeviceCapabilities.RealtimeStatus;
        public void ConnectAllAndReadWaveSettings() { }
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

        public LaserRealtimeStatus ReadRealtimeStatus(Guid deviceId)
        {
            FullRealtimeReads.Add(deviceId);
            if (ThrowOnFullRead.Contains(deviceId))
            {
                throw new InvalidOperationException("Simulated read failure.");
            }

            return new LaserRealtimeStatus();
        }

        public LaserRealtimeStatus ReadHwdFastRealtimeStatus(Guid deviceId)
        {
            HwdFastRealtimeReads.Add(deviceId);
            return new LaserRealtimeStatus();
        }

        public void SetContinuousOutput(Guid deviceId, bool enabled) { }
        public void PointWeld(Guid deviceId) { }
        public void SetLaserLock(Guid deviceId, bool locked) { }
        public void SetRedLight(Guid deviceId, bool enabled) { }
        public void ClearPointCount(Guid deviceId) { }
        public void ClearError(Guid deviceId) { }
    }
}
