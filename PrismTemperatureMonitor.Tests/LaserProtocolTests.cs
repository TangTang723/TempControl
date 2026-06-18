using PrismTemperatureMonitor.Models;
using PrismTemperatureMonitor.Services;

namespace PrismTemperatureMonitor.Tests;

public sealed class LaserProtocolTests
{
    [Fact]
    public void EncodeAndDecodeFrame_EscapesFrameBoundaryBytes()
    {
        byte[] raw = [0x7E, 0x0F, 0x04, 0x7E, 0x7D, 0x00, 0x7E];

        var encoded = LaserProtocol.EncodeFrame(raw);
        var decoded = LaserProtocol.DecodeFrame(encoded, encoded.Length);

        Assert.Equal([0x7E, 0x0F, 0x04, 0x7D, 0x5E, 0x7D, 0x5D, 0x00, 0x7E], encoded);
        Assert.Equal(raw, decoded);
    }

    [Fact]
    public void ConvertCrc_ProducesLegacyGetWaveParamCommand()
    {
        byte[] command = [0x7E, 0x0F, 0x04, 0x01, 0x00, 0x00, 0x7E];

        LaserProtocol.FillCrc(command);

        Assert.Equal([0x7E, 0x0F, 0x04, 0x01, 0xE2, 0xFA, 0x7E], command);
    }

    [Fact]
    public void WaveSettings_ConvertsToAndFromLegacyWaveParameterBytes()
    {
        var settings = new LaserWaveSettings
        {
            WaveNumber = 2,
            WaveModeIndex = 1,
            OutputFrequency = 10,
            MaximumPeakPower = 500,
            MaximumAveragePower = 80,
            AverageFrequency = 11,
            EnergyAlarmUpper = 120,
            EnergyAlarmLower = 5,
            PresetLaserEnergy = 30,
            MaxOutputFrequency = 200
        };
        settings.Segments[0].Time = 1.25;
        settings.Segments[0].Power = 20.5;
        settings.Segments[15].Time = 16.75;
        settings.Segments[15].Power = 90.25;

        var bytes = LaserProtocol.ToWaveParameterBytes(settings);
        var parsed = LaserProtocol.ParseWaveSettings([0x0F, 0x04, 0x00, 0x00, .. bytes]);

        Assert.Equal(86, bytes.Length);
        Assert.Equal(settings.WaveNumber, parsed.WaveNumber);
        Assert.Equal(settings.WaveModeIndex, parsed.WaveModeIndex);
        Assert.Equal(settings.OutputFrequency, parsed.OutputFrequency);
        Assert.Equal(settings.MaximumPeakPower, parsed.MaximumPeakPower);
        Assert.Equal(settings.MaximumAveragePower, parsed.MaximumAveragePower);
        Assert.Equal(settings.AverageFrequency, parsed.AverageFrequency);
        Assert.Equal(settings.EnergyAlarmUpper, parsed.EnergyAlarmUpper);
        Assert.Equal(settings.EnergyAlarmLower, parsed.EnergyAlarmLower);
        Assert.Equal(settings.PresetLaserEnergy, parsed.PresetLaserEnergy);
        Assert.Equal(settings.MaxOutputFrequency, parsed.MaxOutputFrequency);
        Assert.Equal(1.25, parsed.Segments[0].Time);
        Assert.Equal(20.5, parsed.Segments[0].Power);
        Assert.Equal(16.75, parsed.Segments[15].Time);
        Assert.Equal(90.25, parsed.Segments[15].Power);
    }

    [Fact]
    public void SystemSettings_BuildsSetCommandWithLowBitModulationFlags()
    {
        var settings = new LaserSystemSettings
        {
            RedLightExternalEnabled = true,
            WaveExternalEnabled = false,
            LaserTriggerExternalEnabled = true,
            AnalogModulationEnabled = true,
            DigitalModulationEnabled = true,
            LaserTriggerModeIndex = 1
        };

        var command = LaserProtocol.BuildSetSystemSettingsCommand(settings);

        Assert.Equal(0x7E, command[0]);
        Assert.Equal(0x0F, command[1]);
        Assert.Equal(0x82, command[2]);
        Assert.Equal(0x01, command[3]);
        Assert.Equal(0x00, command[4]);
        Assert.Equal(0x01, command[5]);
        Assert.Equal(0x01, command[6]);
        Assert.Equal(0x03, command[7]);
        Assert.Equal(0x01, command[8]);
        Assert.Equal(0x7E, command[^1]);
    }

    [Fact]
    public void SystemSettings_ParsesLegacyResponsePayload()
    {
        byte[] payload = [0x0F, 0x02, 0x01, 0x01, 0x00, 0x01, 0x02, 0x01];

        var settings = LaserProtocol.ParseSystemSettings(payload);

        Assert.True(settings.RedLightExternalEnabled);
        Assert.True(settings.WaveExternalEnabled);
        Assert.False(settings.LaserTriggerExternalEnabled);
        Assert.False(settings.AnalogModulationEnabled);
        Assert.True(settings.DigitalModulationEnabled);
        Assert.Equal(1, settings.LaserTriggerModeIndex);
    }
}
