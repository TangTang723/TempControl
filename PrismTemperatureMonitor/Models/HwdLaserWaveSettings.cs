using System.Collections.ObjectModel;
using Prism.Mvvm;

namespace PrismTemperatureMonitor.Models;

public enum HwdEmitMode : byte
{
    SAW = 0x01,
    FCW = 0x02,
    SCW = 0x08
}

public enum HwdTriggerMode : byte
{
    电平出光 = 0,
    脉冲出光 = 1
}

public sealed class HwdLaserWaveSettings : BindableBase
{
    private int _currentOutputWaveNumber = 1;
    private int _waveNumber = 1;
    private HwdEmitMode _emitMode = HwdEmitMode.SAW;
    private HwdTriggerMode _triggerMode = HwdTriggerMode.电平出光;
    private double _maximumPower;
    private double _pulseInterval;
    private double _averagePower;
    private double _singlePointEnergy;
    private double _modulationPeriod;
    private double _outputRatio;

    public HwdLaserWaveSettings()
    {
        for (var index = 1; index <= 16; index++)
        {
            Segments.Add(new LaserWaveSegment { Index = index });
        }
    }

    public int CurrentOutputWaveNumber
    {
        get => _currentOutputWaveNumber;
        set => SetProperty(ref _currentOutputWaveNumber, value);
    }

    public int WaveNumber
    {
        get => _waveNumber;
        set => SetProperty(ref _waveNumber, value);
    }

    public HwdEmitMode EmitMode
    {
        get => _emitMode;
        set
        {
            if (SetProperty(ref _emitMode, value) && value == HwdEmitMode.FCW)
            {
                TriggerMode = HwdTriggerMode.电平出光;
            }
        }
    }

    public HwdTriggerMode TriggerMode
    {
        get => _triggerMode;
        set => SetProperty(ref _triggerMode, EmitMode == HwdEmitMode.FCW ? HwdTriggerMode.电平出光 : value);
    }

    public double MaximumPower
    {
        get => _maximumPower;
        set => SetProperty(ref _maximumPower, value);
    }

    public double PulseInterval
    {
        get => _pulseInterval;
        set => SetProperty(ref _pulseInterval, value);
    }

    public double AveragePower
    {
        get => _averagePower;
        set => SetProperty(ref _averagePower, value);
    }

    public double SinglePointEnergy
    {
        get => _singlePointEnergy;
        set => SetProperty(ref _singlePointEnergy, value);
    }

    public double ModulationPeriod
    {
        get => _modulationPeriod;
        set => SetProperty(ref _modulationPeriod, value);
    }

    public double OutputRatio
    {
        get => _outputRatio;
        set => SetProperty(ref _outputRatio, value);
    }

    public double FcwRampUpTime
    {
        get => Segments[0].Time;
        set
        {
            if (Segments[0].Time == value)
            {
                return;
            }

            Segments[0].Time = value;
            RaisePropertyChanged();
        }
    }

    public double FcwContinuousPower
    {
        get => Segments[1].Power;
        set
        {
            if (Segments[1].Power == value)
            {
                return;
            }

            Segments[1].Power = value;
            RaisePropertyChanged();
        }
    }

    public double FcwRampDownTime
    {
        get => Segments[2].Time;
        set
        {
            if (Segments[2].Time == value)
            {
                return;
            }

            Segments[2].Time = value;
            RaisePropertyChanged();
        }
    }

    public ObservableCollection<LaserWaveSegment> Segments { get; } = [];

    public HwdLaserWaveSettings Clone()
    {
        var clone = new HwdLaserWaveSettings
        {
            CurrentOutputWaveNumber = CurrentOutputWaveNumber,
            WaveNumber = WaveNumber,
            EmitMode = EmitMode,
            TriggerMode = TriggerMode,
            MaximumPower = MaximumPower,
            PulseInterval = PulseInterval,
            AveragePower = AveragePower,
            SinglePointEnergy = SinglePointEnergy,
            ModulationPeriod = ModulationPeriod,
            OutputRatio = OutputRatio
        };
        clone.Segments.Clear();
        foreach (var segment in Segments)
        {
            clone.Segments.Add(segment.Clone());
        }

        return clone;
    }
}
