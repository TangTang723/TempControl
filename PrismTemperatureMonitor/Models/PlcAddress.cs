namespace PrismTemperatureMonitor.Models;

public sealed record PlcAddress(int DbNumber, int ByteOffset, PlcValueType ValueType, byte BitOffset = 0)
{
    public string ToS7Address()
    {
        return ValueType switch
        {
            PlcValueType.Bool => $"DB{DbNumber}.DBX{ByteOffset}.{BitOffset}",
            PlcValueType.Int => $"DB{DbNumber}.DBW{ByteOffset}",
            PlcValueType.DInt => $"DB{DbNumber}.DBD{ByteOffset}",
            PlcValueType.Float => $"DB{DbNumber}.DBD{ByteOffset}",
            _ => throw new InvalidOperationException($"不支持的 PLC 数据类型：{ValueType}")
        };
    }
}
