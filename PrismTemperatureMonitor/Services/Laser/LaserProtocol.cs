using PrismTemperatureMonitor.Models;

namespace PrismTemperatureMonitor.Services;

public static class LaserProtocol
{
    private const byte FrameBoundary = 0x7E;
    private const byte Escape = 0x7D;

    public static byte[] EncodeFrame(byte[] data)
    {
        if (data.Length < 2 || data[0] != FrameBoundary || data[^1] != FrameBoundary)
        {
            throw new ArgumentException("激光器协议帧必须以 0x7E 开始和结束。", nameof(data));
        }

        var output = new List<byte> { FrameBoundary };
        for (var index = 1; index < data.Length - 1; index++)
        {
            if (data[index] == FrameBoundary)
            {
                output.Add(Escape);
                output.Add(0x5E);
            }
            else if (data[index] == Escape)
            {
                output.Add(Escape);
                output.Add(0x5D);
            }
            else
            {
                output.Add(data[index]);
            }
        }

        output.Add(FrameBoundary);
        return output.ToArray();
    }

    public static byte[] DecodeFrame(byte[] data, int count)
    {
        if (count < 2 || data[0] != FrameBoundary || data[count - 1] != FrameBoundary)
        {
            throw new ArgumentException("激光器返回帧格式无效。", nameof(data));
        }

        var output = new List<byte> { FrameBoundary };
        for (var index = 1; index < count - 1; index++)
        {
            if (data[index] == Escape)
            {
                if (index + 1 >= count - 1)
                {
                    throw new ArgumentException("激光器返回帧转义格式无效。", nameof(data));
                }

                if (data[index + 1] == 0x5E)
                {
                    output.Add(FrameBoundary);
                    index++;
                }
                else if (data[index + 1] == 0x5D)
                {
                    output.Add(Escape);
                    index++;
                }
            }
            else
            {
                output.Add(data[index]);
            }
        }

        output.Add(FrameBoundary);
        return output.ToArray();
    }

    public static ushort CalculateFcs(byte[] data)
    {
        ushort fcs = 0xFFFF;
        foreach (var item in data)
        {
            var j = (ushort)((fcs ^ item) & 15);
            fcs = (ushort)(fcs >> 4);
            fcs = (ushort)(fcs ^ (j * 4225));
            j = (ushort)((fcs ^ (item >> 4)) & 15);
            fcs = (ushort)(fcs >> 4);
            fcs = (ushort)(fcs ^ (j * 4225));
        }

        return (ushort)(fcs ^ 0xFFFF);
    }

    public static void FillCrc(byte[] frame)
    {
        var payload = frame.Skip(1).Take(frame.Length - 4).ToArray();
        var crc = BitConverter.GetBytes(CalculateFcs(payload));
        frame[^3] = crc[0];
        frame[^2] = crc[1];
    }

    public static byte[] BuildGetWaveNoCommand()
    {
        return [0x7E, 0x0F, 0x03, 0x14, 0xBE, 0x7E];
    }

    public static byte[] BuildGetWaveParamCommand(int waveNumber)
    {
        byte[] command = [0x7E, 0x0F, 0x04, (byte)waveNumber, 0, 0, 0x7E];
        FillCrc(command);
        return command;
    }

    public static byte[] BuildSetWaveNoCommand(int waveNumber)
    {
        byte[] command = [0x7E, 0x0F, 0x83, (byte)waveNumber, 0, 0, 0x7E];
        FillCrc(command);
        return command;
    }

    public static byte[] BuildSetWaveParamCommand(byte[] waveParameterBytes)
    {
        var command = new byte[waveParameterBytes.Length + 4];
        command[0] = FrameBoundary;
        command[1] = 0x0F;
        command[2] = 0x84;
        Array.Copy(waveParameterBytes, 0, command, 3, waveParameterBytes.Length);
        command[^1] = FrameBoundary;
        FillCrc(command);
        return command;
    }

    public static byte[] BuildGetSystemSettingsCommand()
    {
        return [0x7E, 0x0F, 0x02, 0x9D, 0xAF, 0x7E];
    }

    public static byte[] BuildGetPointCountCommand()
    {
        return [0x7E, 0x0F, 0x01, 0x06, 0x9D, 0x7E];
    }

    public static byte[] BuildGetMachineStateCommand()
    {
        return [0x7E, 0x0F, 0x10, 0x0E, 0x9C, 0x7E];
    }

    public static byte[] BuildGetDb25InfoCommand()
    {
        return [0x7E, 0x0F, 0x12, 0x1C, 0xBF, 0x7E];
    }

    public static byte[] BuildGetRealtimeParameterCommand()
    {
        return [0x7E, 0x0F, 0x34, 0x28, 0xFB, 0x7E];
    }

    public static byte[] BuildSetSystemSettingsCommand(LaserSystemSettings settings)
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

        byte[] command =
        [
            0x7E,
            0x0F,
            0x82,
            settings.RedLightExternalEnabled ? (byte)0x01 : (byte)0x00,
            settings.WaveExternalEnabled ? (byte)0x01 : (byte)0x00,
            settings.LaserTriggerExternalEnabled ? (byte)0x01 : (byte)0x00,
            0x01,
            data5,
            settings.LaserTriggerModeIndex == 1 ? (byte)0x01 : (byte)0x00,
            0,
            0,
            0x7E
        ];
        FillCrc(command);
        return command;
    }

    public static byte[] BuildClearErrorCommand()
    {
        return [0x7E, 0x0F, 0x90, 0x06, 0x18, 0x7E];
    }

    public static byte[] BuildClearPointCountCommand()
    {
        return [0x7E, 0x0F, 0x91, 0x8F, 0x09, 0x7E];
    }

    public static byte[] BuildRedLightCommand(bool enabled)
    {
        byte[] command = [0x7E, 0x0F, 0x92, enabled ? (byte)0x03 : (byte)0x02, 0, 0, 0x7E];
        FillCrc(command);
        return command;
    }

    public static byte[] BuildPointWeldCommand()
    {
        return [0x7E, 0x0F, 0x93, 0x9D, 0x2A, 0x7E];
    }

    public static byte[] BuildContinuousOutputCommand(bool enabled)
    {
        byte state = enabled ? (byte)0x03 : (byte)0x02;
        byte[] command = [0x7E, 0x0F, 0x94, state, 0, 0, 0x7E];
        FillCrc(command);
        return command;
    }

    public static byte[] BuildLaserLockCommand(bool locked)
    {
        byte state = locked ? (byte)0x00 : (byte)0x01;
        byte[] command = [0x7E, 0x0F, 0x96, state, 0, 0, 0x7E];
        FillCrc(command);
        return command;
    }

    public static byte[] ToWaveParameterBytes(LaserWaveSettings settings)
    {
        var values = new short[43];
        values[0] = (short)settings.WaveNumber;
        values[1] = (short)settings.WaveModeIndex;
        values[2] = settings.OutputFrequency;
        values[3] = settings.MaximumPeakPower;
        values[4] = settings.MaximumAveragePower;
        values[5] = settings.AverageFrequency;
        if (settings.WaveModeIndex == 0)
        {
            values[6] = (short)(settings.EnergyAlarmUpper * 100);
            values[7] = (short)(settings.EnergyAlarmLower * 100);
            values[8] = (short)(settings.PresetLaserEnergy * 100);
            values[9] = (short)settings.MaxOutputFrequency;

            for (var index = 0; index < 16; index++)
            {
                values[(index * 2) + 10] = ScaleToShort(settings.Segments[index].Time*100);
                values[(index * 2) + 11] = ScaleToShort(settings.Segments[index].Power*100);
            }
        }
        else
        {
            values[6] = (short)settings.EnergyAlarmUpper;
            values[7] = (short)settings.EnergyAlarmLower;
            values[8] = (short)(settings.PresetLaserEnergy * 10);
            values[9] = (short)(settings.MaxOutputFrequency*10);

            for (var index = 0; index < 16; index++)
            {
                values[(index * 2) + 10] = ScaleToShort(settings.Segments[index].Time*10);
                values[(index * 2) + 11] = ScaleToShort(settings.Segments[index].Power*100);
            }
        }
       

        var bytes = new byte[values.Length * 2];
        for (var index = 0; index < values.Length; index++)
        {
            var valueBytes = BitConverter.GetBytes(values[index]);
            Array.Copy(valueBytes, 0, bytes, index * 2, 2);
        }
        return bytes;
    }

    public static LaserWaveSettings ParseWaveSettings(byte[] waveParameterBytes)
    {
        if (waveParameterBytes.Length < 86)
        {
            throw new ArgumentException("波形参数长度不足。", nameof(waveParameterBytes));
        }

        var settings = new LaserWaveSettings
        {
            WaveNumber = ReadInt16(waveParameterBytes, 4),
            WaveModeIndex = ReadInt16(waveParameterBytes, 6),
            OutputFrequency = ReadInt16(waveParameterBytes, 8),
            MaximumPeakPower = ReadInt16(waveParameterBytes, 10),
            MaximumAveragePower = ReadInt16(waveParameterBytes, 12),
            AverageFrequency = ReadInt16(waveParameterBytes, 14)
            
        };
        if (settings.WaveModeIndex == 0)
        {
           settings.EnergyAlarmUpper = ReadInt16(waveParameterBytes, 16) * 0.01;
           settings.EnergyAlarmLower = ReadInt16(waveParameterBytes, 18) * 0.01;
           settings.PresetLaserEnergy = ReadInt16(waveParameterBytes, 20) * 0.01;
           settings.MaxOutputFrequency = ReadInt16(waveParameterBytes, 22);
            for (var index = 0; index < 16; index++)
            {
                settings.Segments[index].Time = ReadInt16(waveParameterBytes, 24 + (index * 4)) * 0.01;
                settings.Segments[index].Power = ReadInt16(waveParameterBytes, 26 + (index * 4)) * 0.01;
            }
        }
        else
        {
            settings.EnergyAlarmUpper = ReadInt16(waveParameterBytes, 16) ;
            settings.EnergyAlarmLower = ReadInt16(waveParameterBytes, 18) ;
            settings.PresetLaserEnergy = ReadInt16(waveParameterBytes, 20)*0.1;
            settings.MaxOutputFrequency = ReadInt16(waveParameterBytes, 22)*0.1;
            for (var index = 0; index < 16; index++)
            {
                settings.Segments[index].Time = ReadInt16(waveParameterBytes, 24 + (index * 4)) * 0.1;
                settings.Segments[index].Power = ReadInt16(waveParameterBytes, 26 + (index * 4)) * 0.01;
            }
        }
    

        return settings;
    }

    public static LaserSystemSettings ParseSystemSettings(byte[] payload)
    {
        if (payload.Length < 6)
        {
            throw new ArgumentException("系统参数长度不足。", nameof(payload));
        }

        var dataStart = payload[0] == 0x0F ? 2 : 0;
        if (payload.Length < dataStart + 6)
        {
            throw new ArgumentException("系统参数长度不足。", nameof(payload));
        }

        var data5 = payload[dataStart + 4];
        return new LaserSystemSettings
        {
            RedLightExternalEnabled = payload[dataStart] == 0x01,
            WaveExternalEnabled = payload[dataStart + 1] == 0x01,
            LaserTriggerExternalEnabled = payload[dataStart + 2] == 0x01,
            AnalogModulationEnabled = (data5 & 0x01) != 0,
            DigitalModulationEnabled = (data5 & 0x02) != 0,
            LaserTriggerModeIndex = payload[dataStart + 5] == 0x01 ? 1 : 0
        };
    }

    public static int ParsePointCount(byte[] payload)
    {
        var dataStart = GetDataStart(payload, 4, "出光点数");
        return BitConverter.ToInt32([payload[dataStart], payload[dataStart + 1], payload[dataStart + 2], payload[dataStart + 3]]);
    }

    public static void ApplyMachineState(LaserRealtimeStatus status, byte[] payload)
    {
        var dataStart = GetDataStart(payload, 3, "整机状态");
        status.MachineAlarmByte = payload[dataStart];
        status.MachineLightByte = payload[dataStart + 1];
        status.PowerStateByte = payload[dataStart + 2];
    }

    public static void ApplyDb25Info(LaserRealtimeStatus status, byte[] payload)
    {
        var dataStart = GetDataStart(payload, 4, "DB25 信号");
        status.Db25InputIo = payload[dataStart];
        status.Db25InputWave = payload[dataStart + 1];
        status.Db25OutputIo = payload[dataStart + 2];
        status.AnalogInput = payload[dataStart + 3];
    }

    public static void ApplyRealtimeParameter(LaserRealtimeStatus status, byte[] payload)
    {
        var dataStart = GetDataStart(payload, 6, "实时参数");
        status.ErrorLowByte = payload[dataStart];
        status.ErrorHighByte = payload[dataStart + 1];
        status.StateLowByte = payload[dataStart + 2];
        status.StateHighByte = payload[dataStart + 3];
        status.LaserPower = BitConverter.ToInt16([payload[dataStart + 4], payload[dataStart + 5]]);
    }

    public static byte[] ExtractPayload(byte[] encodedResponse, int count, byte expectedCommand)
    {
        var decoded = DecodeFrame(encodedResponse, count);
        if (decoded.Length < 6 || decoded[2] != expectedCommand)
        {
            throw new InvalidOperationException("激光器返回指令与请求不匹配。");
        }

        var payload = decoded.Skip(1).Take(decoded.Length - 4).ToArray();
        var expectedCrc = CalculateFcs(payload);
        var actualCrc = BitConverter.ToUInt16([decoded[^3], decoded[^2]]);
        if (expectedCrc != actualCrc)
        {
            throw new InvalidOperationException("激光器返回数据 CRC 校验失败。");
        }

        return payload;
    }

    private static short ReadInt16(byte[] bytes, int startIndex)
    {
        return BitConverter.ToInt16([bytes[startIndex], bytes[startIndex + 1]]);
    }

    private static int GetDataStart(byte[] payload, int minimumDataLength, string name)
    {
        var dataStart = payload.Length >= 2 && payload[0] == 0x0F ? 2 : 0;
        if (payload.Length < dataStart + minimumDataLength)
        {
            throw new ArgumentException($"{name}长度不足。", nameof(payload));
        }

        return dataStart;
    }
    private static short ScaleToShort(double value)
    {
        return (short)Math.Round(value, MidpointRounding.AwayFromZero);
    }
}
