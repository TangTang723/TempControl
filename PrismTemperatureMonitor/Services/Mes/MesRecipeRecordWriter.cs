using System.Globalization;
using System.IO;
using System.Text;
using PrismTemperatureMonitor.Models;

namespace PrismTemperatureMonitor.Services;

public sealed class MesRecipeRecordWriter : IMesRecipeRecordWriter
{
    private const int WeldPassCount = 6;
    private readonly object _syncRoot = new();
    private readonly string _rootDirectory;

    public MesRecipeRecordWriter()
        : this(Path.Combine(AppContext.BaseDirectory, "Data", "MesRecipe"))
    {
    }

    public MesRecipeRecordWriter(string rootDirectory)
    {
        _rootDirectory = rootDirectory;
    }

    public void Append(MesRecipeRecord record)
    {
        lock (_syncRoot)
        {
            Directory.CreateDirectory(_rootDirectory);
            var filePath = Path.Combine(_rootDirectory, $"{record.Timestamp:yyyy-MM-dd}.csv");
            var writeHeader = !File.Exists(filePath);
            using var writer = new StreamWriter(filePath, append: true, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            if (writeHeader)
            {
                writer.WriteLine(string.Join(',', BuildHeader()));
            }

            writer.WriteLine(string.Join(',', BuildRow(record).Select(EscapeCsv)));
        }
    }

    private static IEnumerable<string> BuildHeader()
    {
        yield return "记录时间";
        yield return "Y轴绝对定位速度";
        yield return "Z轴绝对定位速度";
        yield return "焊道数量选择";

        for (var index = 1; index <= WeldPassCount; index++)
        {
            yield return $"焊道{index}实际功率";
            yield return $"焊道{index}焊接波形";
            yield return $"焊道{index}R轴速度";
            yield return $"焊道{index}Y轴位置";
            yield return $"焊道{index}Z轴位置";
            yield return $"焊道{index}R轴焊前预留角度";
            yield return $"焊道{index}R轴位置";
            yield return $"焊道{index}R轴焊后预留角度";
            yield return $"焊道{index}焊接温度上限";
            yield return $"焊道{index}焊接温度下限";
            yield return $"焊道{index}激光功率上限";
            yield return $"焊道{index}激光功率下限";
        }
    }

    private static IEnumerable<string> BuildRow(MesRecipeRecord record)
    {
        yield return record.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        yield return FormatNumber(record.YAbsolutePositionSpeed);
        yield return FormatNumber(record.ZAbsolutePositionSpeed);
        yield return record.WeldPassCount.ToString(CultureInfo.InvariantCulture);

        for (var index = 1; index <= WeldPassCount; index++)
        {
            var weldPass = record.WeldPasses.FirstOrDefault(pass => pass.Index == index) ?? new MesWeldPassRecord { Index = index };
            yield return weldPass.ActualPower.ToString(CultureInfo.InvariantCulture);
            yield return weldPass.WaveNumber.ToString(CultureInfo.InvariantCulture);
            yield return FormatNumber(weldPass.RSpeed);
            yield return FormatNumber(weldPass.YPosition);
            yield return FormatNumber(weldPass.ZPosition);
            yield return FormatNumber(weldPass.RPreAngle);
            yield return FormatNumber(weldPass.RPosition);
            yield return FormatNumber(weldPass.RPostAngle);
            yield return FormatNumber(weldPass.TemperatureUpperLimit);
            yield return FormatNumber(weldPass.TemperatureLowerLimit);
            yield return FormatNumber(weldPass.LaserPowerUpperLimit);
            yield return FormatNumber(weldPass.LaserPowerLowerLimit);
        }
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string EscapeCsv(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\r') && !value.Contains('\n'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
