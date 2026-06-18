using PrismTemperatureMonitor.Services;

namespace PrismTemperatureMonitor.Tests;

public sealed class HwdLaserSettingModelTests
{
    [Fact]
    public void AppendCheckBytes_AddsLittleEndianFcs()
    {
        var model = new HwdLaserSettingModel(new FakeLaserSerialPort());
        byte[] raw = [0x7E, 0x01, 0x16, 0x01, 0x7E];

        var frame = model.AppendCheckBytes(raw);

        var expectedFcs = BitConverter.GetBytes(model.CalcFcs([0x01, 0x16, 0x01]));
        Assert.Equal(0x7E, frame[0]);
        Assert.Equal(0x01, frame[1]);
        Assert.Equal(0x16, frame[2]);
        Assert.Equal(0x01, frame[3]);
        Assert.Equal(expectedFcs[0], frame[4]);
        Assert.Equal(expectedFcs[1], frame[5]);
        Assert.Equal(0x7E, frame[6]);
    }

    [Fact]
    public void AppendCheckBytes_AcceptsFrameWithoutEndBoundary()
    {
        var model = new HwdLaserSettingModel(new FakeLaserSerialPort());
        byte[] raw = [0x7E, 0x01, 0x16, 0x00];

        var frame = model.AppendCheckBytes(raw);

        var expectedFcs = BitConverter.GetBytes(model.CalcFcs([0x01, 0x16, 0x00]));
        Assert.Equal([0x7E, 0x01, 0x16, 0x00, expectedFcs[0], expectedFcs[1], 0x7E], frame);
    }

    [Fact]
    public void EncodeAndDecodeFrame_EscapesHdlcBoundaryBytes()
    {
        var model = new HwdLaserSettingModel(new FakeLaserSerialPort());
        byte[] raw = [0x7E, 0x01, 0x16, 0x7E, 0x7D, 0x7E];

        var encoded = model.EncodeFrame(raw);
        var decoded = model.DecodeFrame(encoded, encoded.Length);

        Assert.Equal([0x7E, 0x01, 0x16, 0x7D, 0x5E, 0x7D, 0x5D, 0x7E], encoded);
        Assert.Equal(raw, decoded);
    }

    [Fact]
    public void SetRedLaser_WritesCommandAndAcceptsEchoResponse()
    {
        var port = new FakeLaserSerialPort();
        var model = new HwdLaserSettingModel(port);
        model.Open("COM1");
        port.EnqueueResponse(BuildResponse(model, 0x16, [0x01]));

        var result = model.SetRedLaser(true);

        Assert.True(result);
        Assert.Equal([0x7E, 0x01, 0x16, 0x01], port.LastWrite.Take(4).ToArray());
        Assert.Equal(0x7E, port.LastWrite[^1]);
    }

    [Fact]
    public void GetFastRealtimeData_ReadsReversedPointCountAndPower()
    {
        var port = new FakeLaserSerialPort();
        var model = new HwdLaserSettingModel(port);
        model.Open("COM1");
        port.EnqueueResponse(BuildResponse(model, 0x21, [0x40, 0xE2, 0x01, 0x00, 0x2C, 0x01]));

        var result = model.GetFastRealtimeData(out var count, out var realtimePower);

        Assert.True(result);
        Assert.Equal(123456, count);
        Assert.Equal(300, realtimePower);
    }

    [Fact]
    public void GetWaveNumberAndData_ReadsRawPayload()
    {
        var port = new FakeLaserSerialPort();
        var model = new HwdLaserSettingModel(port);
        model.Open("COM1");
        port.EnqueueResponse(BuildResponse(model, 0x23, [0x03]));
        port.EnqueueResponse(BuildResponse(model, 0x24, [0x03, 0x01, 0x02, 0x03]));

        var numberResult = model.GetWaveNumber(out var waveNumber);
        var dataResult = model.GetWaveData(waveNumber, out var waveData);

        Assert.True(numberResult);
        Assert.True(dataResult);
        Assert.Equal(0x03, waveNumber);
        Assert.Equal([0x03, 0x01, 0x02, 0x03], waveData);
    }

    [Fact]
    public void SetRedLaser_ReturnsFalseWhenResponseCommandDoesNotMatch()
    {
        var port = new FakeLaserSerialPort();
        var model = new HwdLaserSettingModel(port);
        model.Open("COM1");
        port.EnqueueResponse(BuildResponse(model, 0x17, [0x01]));

        var result = model.SetRedLaser(true);

        Assert.False(result);
    }

    [Fact]
    public void SetRedLaser_ReturnsFalseWhenResponseFcsIsInvalid()
    {
        var port = new FakeLaserSerialPort();
        var model = new HwdLaserSettingModel(port);
        model.Open("COM1");
        var response = BuildResponse(model, 0x16, [0x01]);
        response[^3] ^= 0xFF;
        port.EnqueueResponse(response);

        var result = model.SetRedLaser(true);

        Assert.False(result);
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

    private sealed class FakeLaserSerialPort : ILaserSerialPort
    {
        private readonly Queue<byte[]> _responses = [];

        public string PortName { get; set; } = string.Empty;

        public int BaudRate { get; set; }

        public int ReadTimeout { get; set; }

        public bool IsOpen { get; private set; }

        public byte[] LastWrite { get; private set; } = [];

        public void EnqueueResponse(byte[] response)
        {
            _responses.Enqueue(response);
        }

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
            LastWrite = buffer.Skip(offset).Take(count).ToArray();
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            var response = _responses.Dequeue();
            Array.Copy(response, 0, buffer, offset, response.Length);
            return response.Length;
        }
    }
}
