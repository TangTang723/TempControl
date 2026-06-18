using PrismTemperatureMonitor.Models;
using PrismTemperatureMonitor.Services;

namespace PrismTemperatureMonitor.Tests;

public sealed class LaserDeviceServiceTests
{
    [Fact]
    public void ConnectAndReadWaveSettings_UsesIndependentSerialPortsPerDevice()
    {
        var first = CreateDevice("A", "COM1");
        var second = CreateDevice("B", "COM2");
        var firstSettings = CreateSettings(1, 10);
        var secondSettings = CreateSettings(2, 20);
        var factory = new FakeLaserSerialPortFactory();
        factory.EnqueuePort(new FakeLaserSerialPort(firstSettings));
        factory.EnqueuePort(new FakeLaserSerialPort(secondSettings));
        var service = new LaserDeviceService(factory);
        service.AddOrUpdateDevice(first);
        service.AddOrUpdateDevice(second);

        service.Connect(first.Id);
        service.Connect(second.Id);
        var firstRead = service.ReadWaveSettings(first.Id);
        var secondRead = service.ReadWaveSettings(second.Id);

        Assert.Equal(1, firstRead.WaveNumber);
        Assert.Equal(10, firstRead.OutputFrequency);
        Assert.Equal(2, secondRead.WaveNumber);
        Assert.Equal(20, secondRead.OutputFrequency);
        Assert.True(service.IsConnected(first.Id));
        Assert.True(service.IsConnected(second.Id));
    }

    [Fact]
    public void WriteWaveSettings_WritesSelectedDeviceAndRefreshesCache()
    {
        var device = CreateDevice("A", "COM1");
        var initialSettings = CreateSettings(1, 10);
        var updatedSettings = CreateSettings(3, 99);
        var fakePort = new FakeLaserSerialPort(initialSettings);
        var factory = new FakeLaserSerialPortFactory();
        factory.EnqueuePort(fakePort);
        var service = new LaserDeviceService(factory);
        service.AddOrUpdateDevice(device);
        service.Connect(device.Id);

        var confirmed = service.WriteWaveSettings(device.Id, updatedSettings);

        Assert.Equal(3, confirmed.WaveNumber);
        Assert.Equal(99, confirmed.OutputFrequency);
        Assert.Equal(3, fakePort.CurrentSettings.WaveNumber);
        Assert.Equal(99, fakePort.CurrentSettings.OutputFrequency);
        var cached = service.GetCachedWaveSettings(device.Id);
        Assert.NotNull(cached);
        Assert.Equal(confirmed.WaveNumber, cached.WaveNumber);
        Assert.Equal(confirmed.OutputFrequency, cached.OutputFrequency);
    }

    [Fact]
    public void SystemSettings_ReadAndWriteRefreshesCache()
    {
        var device = CreateDevice("A", "COM1");
        var fakePort = new FakeLaserSerialPort(CreateSettings(1, 10))
        {
            CurrentSystemSettings = new LaserSystemSettings
            {
                RedLightExternalEnabled = true,
                WaveExternalEnabled = false,
                LaserTriggerExternalEnabled = true,
                AnalogModulationEnabled = true,
                DigitalModulationEnabled = false,
                LaserTriggerModeIndex = 1
            }
        };
        var factory = new FakeLaserSerialPortFactory();
        factory.EnqueuePort(fakePort);
        var service = new LaserDeviceService(factory);
        service.AddOrUpdateDevice(device);
        service.Connect(device.Id);

        var read = service.ReadSystemSettings(device.Id);
        var updated = read.Clone();
        updated.WaveExternalEnabled = true;
        updated.DigitalModulationEnabled = true;
        var confirmed = service.WriteSystemSettings(device.Id, updated);

        Assert.True(read.RedLightExternalEnabled);
        Assert.False(read.WaveExternalEnabled);
        Assert.True(confirmed.WaveExternalEnabled);
        Assert.True(confirmed.DigitalModulationEnabled);
        Assert.True(fakePort.CurrentSystemSettings.WaveExternalEnabled);
        var cached = service.GetCachedSystemSettings(device.Id);
        Assert.NotNull(cached);
        Assert.True(cached.DigitalModulationEnabled);
        Assert.Equal(1, cached.LaserTriggerModeIndex);
    }

    [Fact]
    public void RealtimeStatus_ReadsMachineStateParameterPointCountAndDb25()
    {
        var device = CreateDevice("A", "COM1");
        var fakePort = new FakeLaserSerialPort(CreateSettings(1, 10))
        {
            CurrentRealtimeStatus = new LaserRealtimeStatus
            {
                OutputPointCount = 321,
                MachineAlarmByte = 0x01,
                MachineLightByte = 0x20,
                PowerStateByte = 0x11,
                Db25InputIo = 0x22,
                Db25InputWave = 0x33,
                Db25OutputIo = 0x01,
                AnalogInput = 25,
                ErrorLowByte = 0x00,
                ErrorHighByte = 0x00,
                StateLowByte = 0x44,
                StateHighByte = 0x04,
                LaserPower = 150
            }
        };
        var factory = new FakeLaserSerialPortFactory();
        factory.EnqueuePort(fakePort);
        var service = new LaserDeviceService(factory);
        service.AddOrUpdateDevice(device);
        service.Connect(device.Id);

        var status = service.ReadRealtimeStatus(device.Id);

        Assert.Equal(321, status.OutputPointCount);
        Assert.Equal(150, status.LaserPower);
        Assert.True(status.IsLaserOutputActive);
        Assert.True(status.IsRedLightActive);
        Assert.False(status.IsAlarmActive);
        Assert.Equal(2.5, status.AnalogVoltage);
        var cached = service.GetCachedRealtimeStatus(device.Id);
        Assert.NotNull(cached);
        Assert.Equal(321, cached.OutputPointCount);
    }

    [Fact]
    public void HwdDevice_UsesHwdWaveOperations()
    {
        var device = CreateDevice("HWD", "COM8");
        device.Model = LaserDeviceModel.HWD;
        var fakePort = new FakeHwdLaserSerialPort();
        var factory = new MixedLaserSerialPortFactory(fakePort);
        var service = new LaserDeviceService(factory);
        service.AddOrUpdateDevice(device);

        service.Connect(device.Id);
        var settings = service.ReadHwdWaveNumberSettings(device.Id, 7);

        Assert.Equal(LaserDeviceCapabilities.CommonControl
            | LaserDeviceCapabilities.RealtimeStatus
            | LaserDeviceCapabilities.HwdWaveSettings
            | LaserDeviceCapabilities.HwdSystemSettings,
            service.GetCapabilities(device.Id));
        Assert.Equal(7, settings.WaveNumber);
        Assert.Equal([0x24], fakePort.Commands);
    }

    [Fact]
    public void HwdFastRealtimeStatus_ReadsPointCountAndRealtimePower()
    {
        var device = CreateDevice("HWD", "COM8");
        device.Model = LaserDeviceModel.HWD;
        var fakePort = new FakeHwdLaserSerialPort();
        var factory = new MixedLaserSerialPortFactory(fakePort);
        var service = new LaserDeviceService(factory);
        service.AddOrUpdateDevice(device);
        service.Connect(device.Id);

        var status = service.ReadHwdFastRealtimeStatus(device.Id);

        Assert.Equal(123456, status.OutputPointCount);
        Assert.Equal(300, status.LaserPower);
        Assert.True(status.IsLaserOutputActive);
        Assert.Equal([0x21, 0x2E], fakePort.Commands);
    }

    [Fact]
    public void CachedDisplaySnapshot_MapsHwqCachedValuesWithoutSendingCommands()
    {
        var device = CreateDevice("HWQ-1", "COM1");
        var fakePort = new FakeLaserSerialPort(CreateSettings(3, 25))
        {
            CurrentSystemSettings = new LaserSystemSettings { LaserTriggerModeIndex = 1 },
            CurrentRealtimeStatus = new LaserRealtimeStatus
            {
                LaserPower = 180,
                OutputPointCount = 26,
                AnalogInput = 35
            }
        };
        var factory = new FakeLaserSerialPortFactory();
        factory.EnqueuePort(fakePort);
        var service = new LaserDeviceService(factory);
        service.AddOrUpdateDevice(device);
        service.Connect(device.Id);
        service.ReadWaveSettings(device.Id);
        service.ReadSystemSettings(device.Id);
        service.ReadRealtimeStatus(device.Id);
        var writesBeforeSnapshot = fakePort.WriteCount;

        var snapshot = service.GetCachedDisplaySnapshot(device.Id);

        Assert.NotNull(snapshot);
        Assert.Equal("HWQ-1", snapshot.DeviceName);
        Assert.Equal(LaserDeviceModel.HWQ, snapshot.Model);
        Assert.Equal(180, snapshot.RealtimePower);
        Assert.Equal(3, snapshot.WaveNumber);
        Assert.Equal("CW", snapshot.WaveMode);
        Assert.Equal("电平出光", snapshot.TriggerMode);
        Assert.Equal(26, snapshot.OutputPointCount);
        Assert.Equal(3.5, snapshot.AnalogVoltage);
        Assert.Equal(writesBeforeSnapshot, fakePort.WriteCount);
    }

    [Fact]
    public void CachedDisplaySnapshot_ReturnsNullForDisconnectedDevice()
    {
        var device = CreateDevice("HWQ-1", "COM1");
        var service = new LaserDeviceService(new FakeLaserSerialPortFactory());
        service.AddOrUpdateDevice(device);

        Assert.Null(service.GetCachedDisplaySnapshot(device.Id));
    }

    [Fact]
    public void ConnectAll_WhenOneDeviceFails_ContinuesWithOtherDevices()
    {
        var hwd = CreateDevice("HWD", "COM8");
        hwd.Model = LaserDeviceModel.HWD;
        var hwq = CreateDevice("HWQ", "COM9");
        var hwqPort = new FakeLaserSerialPort(CreateSettings(2, 30));
        var factory = new MixedLaserSerialPortFactory(
            new FailingReadLaserSerialPort(),
            hwqPort);
        var service = new LaserDeviceService(factory);
        service.AddOrUpdateDevice(hwd);
        service.AddOrUpdateDevice(hwq);

        service.ConnectAllAndReadWaveSettings();

        Assert.True(service.IsConnected(hwq.Id));
        Assert.Equal(2, service.GetCachedWaveSettings(hwq.Id)?.WaveNumber);
    }

    private static LaserDeviceConfig CreateDevice(string name, string portName)
    {
        return new LaserDeviceConfig
        {
            Id = Guid.NewGuid(),
            Name = name,
            PortName = portName,
            BaudRate = 115200,
            IsEnabled = true
        };
    }

    private static LaserWaveSettings CreateSettings(int waveNumber, short outputFrequency)
    {
        var settings = new LaserWaveSettings
        {
            WaveNumber = waveNumber,
            WaveModeIndex = 1,
            OutputFrequency = outputFrequency,
            MaximumPeakPower = 500,
            MaximumAveragePower = 80,
            AverageFrequency = 20,
            EnergyAlarmUpper = 100,
            EnergyAlarmLower = 5,
            PresetLaserEnergy = 30,
            MaxOutputFrequency = 200
        };
        settings.Segments[0].Time = waveNumber;
        settings.Segments[0].Power = outputFrequency;
        return settings;
    }

    private sealed class FakeLaserSerialPortFactory : ILaserSerialPortFactory
    {
        private readonly Queue<FakeLaserSerialPort> _ports = [];

        public void EnqueuePort(FakeLaserSerialPort port)
        {
            _ports.Enqueue(port);
        }

        public ILaserSerialPort Create()
        {
            return _ports.Dequeue();
        }
    }

    private sealed class MixedLaserSerialPortFactory(params ILaserSerialPort[] ports) : ILaserSerialPortFactory
    {
        private readonly Queue<ILaserSerialPort> _ports = new(ports);

        public ILaserSerialPort Create()
        {
            return _ports.Dequeue();
        }
    }

    private sealed class FailingReadLaserSerialPort : ILaserSerialPort
    {
        public string PortName { get; set; } = string.Empty;

        public int BaudRate { get; set; }

        public int ReadTimeout { get; set; }

        public bool IsOpen { get; private set; }

        public void Open()
        {
            IsOpen = true;
        }

        public void Close()
        {
            IsOpen = false;
        }

        public void Write(byte[] buffer, int offset, int count)
        {
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            throw new IOException("Simulated HWD read failure.");
        }
    }

    private sealed class FakeHwdLaserSerialPort : ILaserSerialPort
    {
        private readonly Queue<byte[]> _responses = [];

        public List<byte> Commands { get; } = [];

        public string PortName { get; set; } = string.Empty;

        public int BaudRate { get; set; }

        public int ReadTimeout { get; set; }

        public bool IsOpen { get; private set; }

        public void Open()
        {
            IsOpen = true;
        }

        public void Close()
        {
            IsOpen = false;
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            var model = new HwdLaserSettingModel(this) { CommandDelayMilliseconds = 0 };
            var frame = buffer.Skip(offset).Take(count).ToArray();
            var command = frame[2];
            Commands.Add(command);
            if (command == 0x21)
            {
                _responses.Enqueue(BuildHwdResponse(
                    model,
                    command,
                    [0x40, 0xE2, 0x01, 0x00, 0x2C, 0x01]));
                return;
            }

            if (command == 0x2E)
            {
                _responses.Enqueue(BuildHwdResponse(model, command, [0x00]));
                return;
            }

            var requestedWave = frame.Length > 6 ? frame[3] : (byte)1;
            var payload = HwdLaserWaveCodec.Serialize(
                new HwdLaserWaveSettings { WaveNumber = requestedWave },
                HwdPowerScale.OneW);
            _responses.Enqueue(BuildHwdResponse(model, command, payload));
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            var response = _responses.Dequeue();
            Array.Copy(response, 0, buffer, offset, response.Length);
            return response.Length;
        }

        private static byte[] BuildHwdResponse(HwdLaserSettingModel model, byte command, byte[] data)
        {
            var raw = new byte[data.Length + 4];
            raw[0] = 0x7E;
            raw[1] = model.Address;
            raw[2] = command;
            Array.Copy(data, 0, raw, 3, data.Length);
            raw[^1] = 0x7E;
            return model.AppendCheckBytes(raw);
        }
    }

    private sealed class FakeLaserSerialPort : ILaserSerialPort
    {
        private readonly Queue<byte[]> _responses = [];

        public FakeLaserSerialPort(LaserWaveSettings settings)
        {
            CurrentSettings = settings.Clone();
            CurrentSystemSettings = new LaserSystemSettings();
            CurrentRealtimeStatus = new LaserRealtimeStatus();
        }

        public LaserWaveSettings CurrentSettings { get; private set; }

        public LaserSystemSettings CurrentSystemSettings { get; set; }

        public LaserRealtimeStatus CurrentRealtimeStatus { get; set; }

        public int WriteCount { get; private set; }

        public string PortName { get; set; } = string.Empty;

        public int BaudRate { get; set; }

        public int ReadTimeout { get; set; }

        public bool IsOpen { get; private set; }

        public void Open()
        {
            IsOpen = true;
        }

        public void Close()
        {
            IsOpen = false;
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            WriteCount++;
            var command = LaserProtocol.DecodeFrame(buffer.Skip(offset).Take(count).ToArray(), count);
            switch (command[2])
            {
                case 0x03:
                    _responses.Enqueue(BuildResponse(0x03, [(byte)CurrentSettings.WaveNumber, (byte)CurrentSettings.WaveModeIndex, 0, 0, 0, 0]));
                    break;
                case 0x04:
                    _responses.Enqueue(BuildResponse(0x04, ToWaveResponseBytes(CurrentSettings)));
                    break;
                case 0x83:
                    CurrentSettings.WaveNumber = command[3];
                    _responses.Enqueue(BuildResponse(0x83, [(byte)CurrentSettings.WaveNumber]));
                    break;
                case 0x84:
                    CurrentSettings = LaserProtocol.ParseWaveSettings([0x0F, 0x84, 0x00, 0x00, .. command.Skip(3).Take(86)]);
                    _responses.Enqueue(BuildResponse(0x84, ToWaveResponseBytes(CurrentSettings)));
                    break;
                case 0x02:
                    _responses.Enqueue(BuildResponse(0x02, ToSystemSettingsBytes(CurrentSystemSettings)));
                    break;
                case 0x82:
                    CurrentSystemSettings = LaserProtocol.ParseSystemSettings(command.Skip(3).Take(6).ToArray());
                    _responses.Enqueue(BuildResponse(0x82, ToSystemSettingsBytes(CurrentSystemSettings)));
                    break;
                case 0x01:
                    _responses.Enqueue(BuildResponse(0x01, BitConverter.GetBytes(CurrentRealtimeStatus.OutputPointCount)));
                    break;
                case 0x10:
                    _responses.Enqueue(BuildResponse(0x10,
                    [
                        CurrentRealtimeStatus.MachineAlarmByte,
                        CurrentRealtimeStatus.MachineLightByte,
                        CurrentRealtimeStatus.PowerStateByte
                    ]));
                    break;
                case 0x12:
                    _responses.Enqueue(BuildResponse(0x12,
                    [
                        CurrentRealtimeStatus.Db25InputIo,
                        CurrentRealtimeStatus.Db25InputWave,
                        CurrentRealtimeStatus.Db25OutputIo,
                        CurrentRealtimeStatus.AnalogInput
                    ]));
                    break;
                case 0x34:
                    _responses.Enqueue(BuildResponse(0x34,
                    [
                        CurrentRealtimeStatus.ErrorLowByte,
                        CurrentRealtimeStatus.ErrorHighByte,
                        CurrentRealtimeStatus.StateLowByte,
                        CurrentRealtimeStatus.StateHighByte,
                        .. BitConverter.GetBytes(CurrentRealtimeStatus.LaserPower)
                    ]));
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected command 0x{command[2]:X2}.");
            }
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            var response = _responses.Dequeue();
            Array.Copy(response, 0, buffer, offset, response.Length);
            return response.Length;
        }

        private static byte[] BuildResponse(byte command, byte[] data)
        {
            var frame = new byte[data.Length + 6];
            frame[0] = 0x7E;
            frame[1] = 0x0F;
            frame[2] = command;
            Array.Copy(data, 0, frame, 3, data.Length);
            frame[^1] = 0x7E;
            LaserProtocol.FillCrc(frame);
            return LaserProtocol.EncodeFrame(frame);
        }

        private static byte[] ToWaveResponseBytes(LaserWaveSettings settings)
        {
            return [0x00, 0x00, .. LaserProtocol.ToWaveParameterBytes(settings)];
        }

        private static byte[] ToSystemSettingsBytes(LaserSystemSettings settings)
        {
            byte data5 = 0;
            if (settings.AnalogModulationEnabled)
            {
                data5 |= 0x01;
            }

            if (settings.DigitalModulationEnabled)
            {
                data5 |= 0x02;
            }

            return
            [
                settings.RedLightExternalEnabled ? (byte)0x01 : (byte)0x00,
                settings.WaveExternalEnabled ? (byte)0x01 : (byte)0x00,
                settings.LaserTriggerExternalEnabled ? (byte)0x01 : (byte)0x00,
                0x01,
                data5,
                settings.LaserTriggerModeIndex == 1 ? (byte)0x01 : (byte)0x00
            ];
        }
    }
}
