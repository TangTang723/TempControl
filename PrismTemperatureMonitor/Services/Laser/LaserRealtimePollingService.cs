using PrismTemperatureMonitor.Models;

namespace PrismTemperatureMonitor.Services;

public sealed class LaserRealtimePollingService : ILaserRealtimePollingService, IDisposable
{
    private static readonly TimeSpan HwdFastInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan HwdFullInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan HwqFullInterval = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan LoopInterval = TimeSpan.FromMilliseconds(20);

    private readonly ILaserDeviceService _laserDeviceService;
    private readonly Dictionary<Guid, DevicePollingSchedule> _schedules = [];
    private readonly CancellationTokenSource _cancellation = new();
    private Thread? _pollingThread;
    private int _isStarted;
    private int _isDisposed;

    public LaserRealtimePollingService(ILaserDeviceService laserDeviceService)
    {
        _laserDeviceService = laserDeviceService;
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) != 0, this);
        if (Interlocked.Exchange(ref _isStarted, 1) != 0)
        {
            return;
        }

        _pollingThread = new Thread(PollingLoop)
        {
            IsBackground = true,
            Name = "Laser Realtime Polling"
        };
        _pollingThread.Start();
    }

    private void PollingLoop()
    {
        while (!_cancellation.IsCancellationRequested)
        {
            PollConnectedDevices(DateTime.UtcNow);
            if (_cancellation.Token.WaitHandle.WaitOne(LoopInterval))
            {
                break;
            }
        }
    }

    private void PollConnectedDevices(DateTime utcNow)
    {
        var activeDeviceIds = new HashSet<Guid>();
        foreach (var device in _laserDeviceService.GetDevices())
        {
            if (!device.IsEnabled || !_laserDeviceService.IsConnected(device.Id))
            {
                continue;
            }

            activeDeviceIds.Add(device.Id);
            if (!_schedules.TryGetValue(device.Id, out var schedule))
            {
                schedule = new DevicePollingSchedule();
                _schedules[device.Id] = schedule;
            }

            if (device.Model == LaserDeviceModel.HWD)
            {
                PollHwdDevice(device.Id, schedule, utcNow);
            }
            else
            {
                PollHwqDevice(device.Id, schedule, utcNow);
            }
        }

        foreach (var deviceId in _schedules.Keys.Where(deviceId => !activeDeviceIds.Contains(deviceId)).ToArray())
        {
            _schedules.Remove(deviceId);
        }
    }

    private void PollHwdDevice(Guid deviceId, DevicePollingSchedule schedule, DateTime utcNow)
    {
        if (utcNow >= schedule.NextFastReadUtc)
        {
            TryRead(() => _laserDeviceService.ReadHwdFastRealtimeStatus(deviceId));
            schedule.NextFastReadUtc = utcNow + HwdFastInterval;
        }

        if (utcNow >= schedule.NextFullReadUtc)
        {
            TryRead(() => _laserDeviceService.ReadRealtimeStatus(deviceId));
            schedule.NextFullReadUtc = utcNow + HwdFullInterval;
        }
    }

    private void PollHwqDevice(Guid deviceId, DevicePollingSchedule schedule, DateTime utcNow)
    {
        if (utcNow < schedule.NextFullReadUtc)
        {
            return;
        }

        TryRead(() => _laserDeviceService.ReadRealtimeStatus(deviceId));
        schedule.NextFullReadUtc = utcNow + HwqFullInterval;
    }

    private static void TryRead(Func<LaserRealtimeStatus> read)
    {
        try
        {
            read();
        }
        catch
        {
            // A failed device must not stop polling the remaining devices.
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        _cancellation.Cancel();
        if (_pollingThread is { IsAlive: true } &&
            !ReferenceEquals(Thread.CurrentThread, _pollingThread))
        {
            _pollingThread.Join(TimeSpan.FromSeconds(1));
        }

        _cancellation.Dispose();
    }

    private sealed class DevicePollingSchedule
    {
        public DateTime NextFastReadUtc { get; set; } = DateTime.MinValue;

        public DateTime NextFullReadUtc { get; set; } = DateTime.MinValue;
    }
}
