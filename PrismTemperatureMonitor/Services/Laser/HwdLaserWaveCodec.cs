using PrismTemperatureMonitor.Models;

namespace PrismTemperatureMonitor.Services;

public static class HwdLaserWaveCodec
{
    public const int PayloadLength = 85;

    public static HwdLaserWaveSettings Parse(byte[] payload, HwdPowerScale powerScale)
    {
        if (payload.Length < PayloadLength)
        {
            throw new ArgumentException("HWD 波形数据长度不足。", nameof(payload));
        }

        var settings = new HwdLaserWaveSettings
        {
            CurrentOutputWaveNumber = payload[0],
            WaveNumber = payload[1],
            EmitMode = Enum.IsDefined(typeof(HwdEmitMode), payload[2])
                ? (HwdEmitMode)payload[2]
                : HwdEmitMode.SAW,
            TriggerMode = payload[3] == 0 ? HwdTriggerMode.电平出光 : HwdTriggerMode.脉冲出光,
            MaximumPower = ReadUInt16(payload, 4) * GetPowerFactor(powerScale),
            PulseInterval = ReadUInt24(payload, 6) / 1000.0,
            AveragePower = ReadUInt32(payload, 73) / 100.0,
            SinglePointEnergy = ReadUInt32(payload, 77) / 100.0,
            ModulationPeriod = ReadUInt16(payload, 81) / 10.0,
            OutputRatio = ReadUInt16(payload, 83) / 100.0
        };

        for (var index = 0; index < 16; index++)
        {
            settings.Segments[index].Time = ReadUInt16(payload, 9 + (index * 2)) / 10.0;
            settings.Segments[index].Power = ReadUInt16(payload, 41 + (index * 2)) / 100.0;
        }

        return settings;
    }

    public static byte[] Serialize(HwdLaserWaveSettings settings, HwdPowerScale powerScale)
    {
        var payload = new byte[PayloadLength];
        payload[0] = ToByte(settings.CurrentOutputWaveNumber);
        payload[1] = ToByte(settings.WaveNumber);
        payload[2] = (byte)settings.EmitMode;
        payload[3] = settings.EmitMode == HwdEmitMode.FCW
            ? (byte)HwdTriggerMode.电平出光
            : (byte)settings.TriggerMode;

        WriteUInt16(payload, 4, ScaleToUInt16(settings.MaximumPower / GetPowerFactor(powerScale)));
        WriteUInt24(payload, 6, ScaleToUInt24(settings.PulseInterval * 1000));

        for (var index = 0; index < 16; index++)
        {
            var time = settings.EmitMode == HwdEmitMode.FCW && index is not 0 and not 2
                ? 0
                : settings.Segments[index].Time;
            var power = settings.EmitMode == HwdEmitMode.FCW && index != 1
                ? 0
                : settings.Segments[index].Power;
            WriteUInt16(payload, 9 + (index * 2), ScaleToUInt16(time * 10));
            WriteUInt16(payload, 41 + (index * 2), ScaleToUInt16(power * 100));
        }

        WriteUInt32(payload, 73, ScaleToUInt32(settings.AveragePower * 100));
        WriteUInt32(payload, 77, ScaleToUInt32(settings.SinglePointEnergy * 100));
        WriteUInt16(payload, 81, ScaleToUInt16(settings.ModulationPeriod * 10));
        WriteUInt16(payload, 83, ScaleToUInt16(settings.OutputRatio * 100));
        return payload;
    }

    private static double GetPowerFactor(HwdPowerScale powerScale)
    {
        return powerScale == HwdPowerScale.PointOneW ? 0.1 : 1.0;
    }

    private static ushort ReadUInt16(byte[] bytes, int offset)
    {
        return BitConverter.ToUInt16(bytes, offset);
    }

    private static uint ReadUInt24(byte[] bytes, int offset)
    {
        return (uint)(bytes[offset] | (bytes[offset + 1] << 8) | (bytes[offset + 2] << 16));
    }

    private static uint ReadUInt32(byte[] bytes, int offset)
    {
        return BitConverter.ToUInt32(bytes, offset);
    }

    private static void WriteUInt16(byte[] bytes, int offset, ushort value)
    {
        bytes[offset] = (byte)value;
        bytes[offset + 1] = (byte)(value >> 8);
    }

    private static void WriteUInt24(byte[] bytes, int offset, uint value)
    {
        bytes[offset] = (byte)value;
        bytes[offset + 1] = (byte)(value >> 8);
        bytes[offset + 2] = (byte)(value >> 16);
    }

    private static void WriteUInt32(byte[] bytes, int offset, uint value)
    {
        bytes[offset] = (byte)value;
        bytes[offset + 1] = (byte)(value >> 8);
        bytes[offset + 2] = (byte)(value >> 16);
        bytes[offset + 3] = (byte)(value >> 24);
    }

    private static byte ToByte(int value)
    {
        return (byte)Math.Clamp(value, byte.MinValue, byte.MaxValue);
    }

    private static ushort ScaleToUInt16(double value)
    {
        return (ushort)Math.Clamp(Math.Round(value), ushort.MinValue, ushort.MaxValue);
    }

    private static uint ScaleToUInt24(double value)
    {
        return (uint)Math.Clamp(Math.Round(value), 0, 0xFFFFFF);
    }

    private static uint ScaleToUInt32(double value)
    {
        return (uint)Math.Clamp(Math.Round(value), uint.MinValue, uint.MaxValue);
    }
}
