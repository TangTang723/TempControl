using PrismTemperatureMonitor.Models;
using PrismTemperatureMonitor.Services;

namespace PrismTemperatureMonitor.Tests;

public sealed class HwdLaserDeviceSessionTests
{
    [Fact]
    public void HwdSystemSettings_DoesNotOwnTriggerMode()
    {
        Assert.Null(typeof(HwdLaserSystemSettings).GetProperty("LaserTriggerModeIndex"));
    }

    [Fact]
    public void ReadWaveNumberSettings_OnlyReadsRequestedWave()
    {
        var port = new FakeHwdSerialPort();
        var model = new HwdLaserSettingModel(port) { CommandDelayMilliseconds = 0 };
        var session = CreateSession(model);
        port.ResponseFactory = command => BuildResponse(model, command.Command, CreateWavePayload(command.Data[0]));
        session.Connect();

        var settings = session.ReadWaveNumberSettings(6);

        Assert.Equal(6, settings.WaveNumber);
        Assert.Equal([0x24], port.Commands.Select(command => command.Command));
        Assert.Equal([0x06], port.Commands[0].Data);
    }

    [Fact]
    public void ReadWaveSettings_ReadsDefaultEditingWaveWithoutReadingOutputWaveNumber()
    {
        var port = new FakeHwdSerialPort();
        var model = new HwdLaserSettingModel(port) { CommandDelayMilliseconds = 0 };
        var session = CreateSession(model);
        port.ResponseFactory = command => BuildResponse(model, command.Command, CreateWavePayload(command.Data[0]));
        session.Connect();

        var settings = session.ReadWaveSettings();

        Assert.Equal(1, settings.WaveNumber);
        Assert.Equal([0x24], port.Commands.Select(command => command.Command));
        Assert.Equal([0x01], port.Commands[0].Data);
    }

    [Fact]
    public void SwitchWave_OnlySwitchesOutputWave()
    {
        var port = new FakeHwdSerialPort();
        var model = new HwdLaserSettingModel(port) { CommandDelayMilliseconds = 0 };
        var session = CreateSession(model);
        port.ResponseFactory = command => BuildResponse(model, command.Command, command.Data);
        session.Connect();

        var settings = session.SwitchWave(4);

        Assert.Equal(4, settings.CurrentOutputWaveNumber);
        Assert.Equal([0x18], port.Commands.Select(command => command.Command));
    }

    [Fact]
    public void WriteWaveSettings_WritesThenReadsBackForConfirmation()
    {
        var port = new FakeHwdSerialPort();
        var model = new HwdLaserSettingModel(port) { CommandDelayMilliseconds = 0 };
        var session = CreateSession(model);
        port.ResponseFactory = command => command.Command switch
        {
            0x1A => BuildResponse(model, 0x1A, [0x01]),
            0x24 => BuildResponse(model, 0x24, CreateWavePayload(command.Data[0])),
            _ => throw new InvalidOperationException()
        };
        session.Connect();

        var settings = session.WriteWaveSettings(new HwdLaserWaveSettings { WaveNumber = 3 });

        Assert.Equal(3, settings.WaveNumber);
        Assert.Equal([0x1A, 0x24], port.Commands.Select(command => command.Command));
    }

    [Fact]
    public void ReadSystemSettings_ConvertsInternalFlagsToExternalFlags()
    {
        var port = new FakeHwdSerialPort();
        var model = new HwdLaserSettingModel(port) { CommandDelayMilliseconds = 0 };
        var session = CreateSession(model);
        port.ResponseFactory = command => command.Command switch
        {
            0x27 => BuildResponse(model, 0x27, [0x00]),
            0x28 => BuildResponse(model, 0x28, [0x01]),
            _ => throw new InvalidOperationException()
        };
        session.Connect();

        var settings = session.ReadSystemSettings();

        Assert.True(settings.LaserTriggerExternalEnabled);
        Assert.False(settings.WaveExternalEnabled);
        Assert.Equal([0x27, 0x28], port.Commands.Select(command => command.Command));
    }

    [Fact]
    public void WriteSystemSettings_WritesExternalFlagsAndDoesNotWriteReservedTriggerMode()
    {
        var port = new FakeHwdSerialPort();
        var model = new HwdLaserSettingModel(port) { CommandDelayMilliseconds = 0 };
        var session = CreateSession(model);
        port.ResponseFactory = command => BuildResponse(model, command.Command, command.Data);
        session.Connect();

        var result = session.WriteSystemSettings(new HwdLaserSystemSettings
        {
            LaserTriggerExternalEnabled = true,
            WaveExternalEnabled = false
        });

        Assert.Equal([0x14, 0x17], port.Commands.Select(command => command.Command));
        Assert.Equal([0x00], port.Commands[0].Data);
        Assert.Equal([0x01], port.Commands[1].Data);
        Assert.True(result.LaserTriggerExternalEnabled);
        Assert.False(result.WaveExternalEnabled);
    }

    [Fact]
    public void ReadFastRealtimeStatus_ReadsOutputStatusEverySecondPollAndUpdatesCache()
    {
        var port = new FakeHwdSerialPort();
        var model = new HwdLaserSettingModel(port) { CommandDelayMilliseconds = 0 };
        var session = CreateSession(model);
        var outputStatusReadCount = 0;
        port.ResponseFactory = command => command.Command switch
        {
            0x21 => BuildResponse(model, 0x21, [0x40, 0xE2, 0x01, 0x00, 0x2C, 0x01]),
            0x2E => BuildResponse(
                model,
                0x2E,
                [outputStatusReadCount++ == 0 ? (byte)0x00 : (byte)0x01]),
            _ => throw new InvalidOperationException()
        };
        session.Connect();

        var firstStatus = session.ReadFastRealtimeStatus();
        var secondStatus = session.ReadFastRealtimeStatus();
        var thirdStatus = session.ReadFastRealtimeStatus();

        Assert.Equal(123456, firstStatus.OutputPointCount);
        Assert.Equal(300, firstStatus.LaserPower);
        Assert.True(firstStatus.IsLaserOutputActive);
        Assert.True(secondStatus.IsLaserOutputActive);
        Assert.False(thirdStatus.IsLaserOutputActive);
        Assert.Equal([0x21, 0x2E, 0x21, 0x21, 0x2E], port.Commands.Select(command => command.Command));
        Assert.Equal(123456, session.CachedRealtimeStatus!.OutputPointCount);
        Assert.Equal(300, session.CachedRealtimeStatus.LaserPower);
        Assert.False(session.CachedRealtimeStatus.IsLaserOutputActive);
    }

    [Fact]
    public void ReadRealtimeStatus_DoesNotRepeatFastPointCountCommand()
    {
        var port = new FakeHwdSerialPort();
        var model = new HwdLaserSettingModel(port) { CommandDelayMilliseconds = 0 };
        var session = CreateSession(model);
        port.ResponseFactory = command => command.Command switch
        {
            0x25 => BuildResponse(model, 0x25, [0x00]),
            0x29 => BuildResponse(model, 0x29, [0x00]),
            0x2C => BuildResponse(model, 0x2C, [0x00]),
            0x2E => BuildResponse(model, 0x2E, [0x00]),
            _ => throw new InvalidOperationException()
        };
        session.Connect();

        var status = session.ReadRealtimeStatus();

        Assert.Equal([0x25, 0x29, 0x2C, 0x2E], port.Commands.Select(command => command.Command));
        Assert.True(status.IsLaserOutputActive);
    }

    private static HwdLaserDeviceSession CreateSession(HwdLaserSettingModel model)
    {
        return new HwdLaserDeviceSession(
            new LaserDeviceConfig
            {
                Id = Guid.NewGuid(),
                Name = "HWD",
                PortName = "COM8",
                Model = LaserDeviceModel.HWD,
                DeviceAddress = 0x01
            },
            model);
    }

    private static byte[] CreateWavePayload(byte waveNumber)
    {
        return HwdLaserWaveCodec.Serialize(
            new HwdLaserWaveSettings
            {
                CurrentOutputWaveNumber = 1,
                WaveNumber = waveNumber,
                EmitMode = HwdEmitMode.SAW
            },
            HwdPowerScale.OneW);
    }

    private static byte[] BuildResponse(HwdLaserSettingModel model, byte command, byte[] data)
    {
        var raw = new byte[data.Length + 4];
        raw[0] = 0x7E;
        raw[1] = model.Address;
        raw[2] = command;
        Array.Copy(data, 0, raw, 3, data.Length);
        raw[^1] = 0x7E;
        return model.AppendCheckBytes(raw);
    }

    private sealed class FakeHwdSerialPort : ILaserSerialPort
    {
        private readonly Queue<byte[]> _responses = [];

        public Func<(byte Command, byte[] Data), byte[]>? ResponseFactory { get; set; }

        public List<(byte Command, byte[] Data)> Commands { get; } = [];

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
            var frame = buffer.Skip(offset).Take(count).ToArray();
            var command = frame[2];
            var data = frame.Skip(3).Take(frame.Length - 6).ToArray();
            Commands.Add((command, data));
            _responses.Enqueue(ResponseFactory?.Invoke((command, data))
                ?? throw new InvalidOperationException("Missing fake HWD response."));
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            var response = _responses.Dequeue();
            Array.Copy(response, 0, buffer, offset, response.Length);
            return response.Length;
        }
    }
}
