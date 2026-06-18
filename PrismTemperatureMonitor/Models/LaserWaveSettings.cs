using System.Collections.ObjectModel;
using LiveChartsCore.Defaults;
using Prism.Mvvm;

namespace PrismTemperatureMonitor.Models;

public sealed class LaserWaveSettings : BindableBase
{
    private int _waveNumber = 1;
    private int _waveModeIndex;
    private short _outputFrequency;
    private short _maximumPeakPower;
    private short _maximumAveragePower;
    private short _averageFrequency;
    private double _energyAlarmUpper;
    private double _energyAlarmLower;
    private double _presetLaserEnergy;
    private double _maxOutputFrequency;

    public LaserWaveSettings()
    {
        for (var index = 1; index <= 16; index++)
        {
            Segments.Add(new LaserWaveSegment { Index = index, Time = 0, Power = 0 });
        }
    }

    public int WaveNumber
    {
        get => _waveNumber;

        set {
            SetProperty(ref _waveNumber, value);
        }
    }

    public int WaveModeIndex
    {
        get => _waveModeIndex;
        set => SetProperty(ref _waveModeIndex, value);
    }

    public short OutputFrequency
    {
        get => _outputFrequency;
        set => SetProperty(ref _outputFrequency, value);
    }

    public short MaximumPeakPower
    {
        get => _maximumPeakPower;
        set => SetProperty(ref _maximumPeakPower, value);
    }

    public short MaximumAveragePower
    {
        get => _maximumAveragePower;
        set => SetProperty(ref _maximumAveragePower, value);
    }

    public short AverageFrequency
    {
        get => _averageFrequency;
        set => SetProperty(ref _averageFrequency, value);
    }

    public double EnergyAlarmUpper
    {
        get => _energyAlarmUpper;
        set => SetProperty(ref _energyAlarmUpper, value);
    }

    public double EnergyAlarmLower
    {
        get => _energyAlarmLower;
        set => SetProperty(ref _energyAlarmLower, value);
    }

    public double PresetLaserEnergy
    {
        get => _presetLaserEnergy;
        set => SetProperty(ref _presetLaserEnergy, value);
    }

    public double MaxOutputFrequency
    {
        get => _maxOutputFrequency;
        set => SetProperty(ref _maxOutputFrequency, value);
    }

    public ObservableCollection<LaserWaveSegment> Segments { get; } = [];

    public LaserWaveSettings Clone()
    {
        var clone = new LaserWaveSettings
        {
            WaveNumber = WaveNumber,
            WaveModeIndex = WaveModeIndex,
            OutputFrequency = OutputFrequency,
            MaximumPeakPower = MaximumPeakPower,
            MaximumAveragePower = MaximumAveragePower,
            AverageFrequency = AverageFrequency,
            EnergyAlarmUpper = EnergyAlarmUpper,
            EnergyAlarmLower = EnergyAlarmLower,
            PresetLaserEnergy = PresetLaserEnergy,
            MaxOutputFrequency = MaxOutputFrequency
        };
        clone.Segments.Clear();
        foreach (var segment in Segments)
        {
            clone.Segments.Add(segment.Clone());
        }

        return clone;
    }
}
