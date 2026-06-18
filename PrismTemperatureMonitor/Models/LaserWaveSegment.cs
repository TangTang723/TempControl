using Prism.Mvvm;

namespace PrismTemperatureMonitor.Models;

public sealed class LaserWaveSegment : BindableBase
{
    private int _index;
    private double _time;
    private double _power;

    public int Index
    {
        get => _index;
        set => SetProperty(ref _index, value);
    }

    public double Time
    {
        get => _time;
        set => SetProperty(ref _time, value);
    }

    public double Power
    {
        get => _power;
        set => SetProperty(ref _power, value);
    }

    public LaserWaveSegment Clone()
    {
        return new LaserWaveSegment
        {
            Index = Index,
            Time = Time,
            Power = Power
        };
    }
}
