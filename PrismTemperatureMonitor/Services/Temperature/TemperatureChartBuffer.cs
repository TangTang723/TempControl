using System.Collections.ObjectModel;
using LiveChartsCore.Defaults;
using PrismTemperatureMonitor.Models;

namespace PrismTemperatureMonitor.Services;

public sealed class TemperatureChartBuffer
{
    private readonly ObservableCollection<ObservablePoint> _values;
    private readonly int _maxRenderedPointCount;

    public TemperatureChartBuffer(ObservableCollection<ObservablePoint> values, int maxRenderedPointCount)
    {
        _values = values;
        _maxRenderedPointCount = maxRenderedPointCount;
    }

    public int RenderedPointCount => _values.Count;

    public void Add(TemperatureSample sample)
    {
        if (_values.Count >= _maxRenderedPointCount)
        {
            DownsampleInPlace();
        }

        _values.Add(new ObservablePoint(sample.Timestamp.ToOADate(), sample.Value));
    }

    public void Clear()
    {
        _values.Clear();
    }

    private void DownsampleInPlace()
    {
        var compacted = new List<ObservablePoint>(_maxRenderedPointCount / 2);
        for (var index = 0; index < _values.Count; index += 2)
        {
            compacted.Add(_values[index]);
        }

        _values.Clear();
        foreach (var point in compacted)
        {
            _values.Add(point);
        }
    }
}
