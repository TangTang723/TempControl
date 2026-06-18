using PrismTemperatureMonitor.Models;
using PrismTemperatureMonitor.Services;

namespace PrismTemperatureMonitor.Tests;

public sealed class HwdLaserWaveCodecTests
{
    [Theory]
    [InlineData(HwdPowerScale.OneW, 150, 150)]
    [InlineData(HwdPowerScale.PointOneW, 15, 150)]
    public void SerializeAndParse_RoundTripsWaveSettings(
        HwdPowerScale powerScale,
        double maximumPower,
        ushort expectedRawMaximumPower)
    {
        var settings = new HwdLaserWaveSettings
        {
            CurrentOutputWaveNumber = 3,
            WaveNumber = 5,
            EmitMode = HwdEmitMode.SCW,
            TriggerMode = HwdTriggerMode.脉冲出光,
            MaximumPower = maximumPower,
            PulseInterval = 12.345,
            AveragePower = 19.87,
            SinglePointEnergy = 20.21,
            ModulationPeriod = 30.1,
            OutputRatio = 75.25
        };
        settings.Segments[0].Time = 2.3;
        settings.Segments[0].Power = 0.17;
        settings.Segments[15].Time = 30.1;
        settings.Segments[15].Power = 99.99;

        var bytes = HwdLaserWaveCodec.Serialize(settings, powerScale);
        var parsed = HwdLaserWaveCodec.Parse(bytes, powerScale);

        Assert.Equal(85, bytes.Length);
        Assert.Equal(expectedRawMaximumPower, BitConverter.ToUInt16(bytes, 4));
        Assert.Equal(settings.CurrentOutputWaveNumber, parsed.CurrentOutputWaveNumber);
        Assert.Equal(settings.WaveNumber, parsed.WaveNumber);
        Assert.Equal(settings.EmitMode, parsed.EmitMode);
        Assert.Equal(settings.TriggerMode, parsed.TriggerMode);
        Assert.Equal(settings.MaximumPower, parsed.MaximumPower, 2);
        Assert.Equal(settings.PulseInterval, parsed.PulseInterval, 3);
        Assert.Equal(settings.AveragePower, parsed.AveragePower, 2);
        Assert.Equal(settings.SinglePointEnergy, parsed.SinglePointEnergy, 2);
        Assert.Equal(settings.ModulationPeriod, parsed.ModulationPeriod, 1);
        Assert.Equal(settings.OutputRatio, parsed.OutputRatio, 2);
        Assert.Equal(2.3, parsed.Segments[0].Time, 1);
        Assert.Equal(0.17, parsed.Segments[0].Power, 2);
        Assert.Equal(30.1, parsed.Segments[15].Time, 1);
        Assert.Equal(99.99, parsed.Segments[15].Power, 2);
    }

    [Fact]
    public void Serialize_FcwForcesLevelTrigger()
    {
        var settings = new HwdLaserWaveSettings
        {
            WaveNumber = 1,
            EmitMode = HwdEmitMode.FCW,
            TriggerMode = HwdTriggerMode.脉冲出光
        };
        settings.Segments[0].Time = 1.2;
        settings.Segments[0].Power = 20;
        settings.Segments[1].Time = 8;
        settings.Segments[1].Power = 65;
        settings.Segments[2].Time = 2.4;
        settings.Segments[2].Power = 30;
        settings.Segments[3].Time = 9;
        settings.Segments[3].Power = 90;

        var bytes = HwdLaserWaveCodec.Serialize(settings, HwdPowerScale.OneW);

        Assert.Equal((byte)HwdTriggerMode.电平出光, bytes[3]);
        Assert.Equal(1.2, BitConverter.ToUInt16(bytes, 9) / 10.0);
        Assert.Equal(65, BitConverter.ToUInt16(bytes, 43) / 100.0);
        Assert.Equal(2.4, BitConverter.ToUInt16(bytes, 13) / 10.0);
        Assert.Equal(0, BitConverter.ToUInt16(bytes, 11));
        Assert.Equal(0, BitConverter.ToUInt16(bytes, 15));
        Assert.Equal(0, BitConverter.ToUInt16(bytes, 41));
        Assert.Equal(0, BitConverter.ToUInt16(bytes, 45));
        Assert.Equal(0, BitConverter.ToUInt16(bytes, 47));
    }

    [Fact]
    public void Parse_RejectsShortPayload()
    {
        Assert.Throws<ArgumentException>(() =>
            HwdLaserWaveCodec.Parse(new byte[84], HwdPowerScale.OneW));
    }
}
