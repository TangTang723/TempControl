using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Prism.Commands;
using Prism.Mvvm;
using PrismTemperatureMonitor.Models;
using PrismTemperatureMonitor.Services;

namespace PrismTemperatureMonitor.ViewModels;

public sealed class RecipeViewModel : BindableBase
{
    private const int RecipeCount = 16;
    private const int RecipeDbNumber = 15;
    private const int RecipeStartByte = 0;
    private const int RecipeStrideBytes = 324;
    private const int WeldPassCount = 6;
    private const int WeldPassStartOffset = 10;
    private const int WeldPassStrideBytes = 42;
    private const string DefaultRecipeType = "厚片工艺";

    private readonly IPlcService _plcService;
    private readonly IRecipeConfigStore _recipeConfigStore;
    private RecipeDefinition? _selectedRecipe;
    private string _plcIpAddress;
    private string _plcState = "未连接";
    private string _editRecipeName = string.Empty;
    private string _editStartByteOffset = string.Empty;
    private string _operationMessage = "请选择左侧配方，连接 PLC 后可读取该配方参数。";
    private bool _isBusy;
    private bool _suppressAutoRead;

    public RecipeViewModel(IPlcService plcService, IRecipeConfigStore recipeConfigStore)
    {
        _plcService = plcService;
        _recipeConfigStore = recipeConfigStore;
        _plcIpAddress = _plcService.IpAddress;

        Recipes = [];
        for (var index = 0; index < RecipeCount; index++)
        {
            Recipes.Add(CreateRecipe(index));
        }

        var configurationMessage = LoadConfiguration();

        ConnectPlcCommand = new DelegateCommand(ConnectPlc, () => !IsBusy)
            .ObservesProperty(() => IsBusy);
        ReadSelectedRecipeCommand = new DelegateCommand(ReadSelectedRecipe, CanReadSelectedRecipe)
            .ObservesProperty(() => SelectedRecipe)
            .ObservesProperty(() => IsBusy);
        ApplySelectedRecipeCommand = new DelegateCommand(ApplySelectedRecipe, CanApplySelectedRecipe)
            .ObservesProperty(() => SelectedRecipe)
            .ObservesProperty(() => EditRecipeName)
            .ObservesProperty(() => EditStartByteOffset);
        ResetSelectedRecipeEditCommand = new DelegateCommand(LoadSelectedRecipeForEdit, () => SelectedRecipe is not null)
            .ObservesProperty(() => SelectedRecipe);

        _suppressAutoRead = true;
        SelectedRecipe = Recipes[0];
        _suppressAutoRead = false;
        if (_plcService.IsConnected)
        {
            ReadSelectedRecipe();
        }

        if (!string.IsNullOrWhiteSpace(configurationMessage))
        {
            OperationMessage = configurationMessage;
        }
    }

    public ObservableCollection<RecipeDefinition> Recipes { get; }

    public DelegateCommand ConnectPlcCommand { get; }

    public DelegateCommand ReadSelectedRecipeCommand { get; }

    public DelegateCommand ApplySelectedRecipeCommand { get; }

    public DelegateCommand ResetSelectedRecipeEditCommand { get; }

    public RecipeDefinition? SelectedRecipe
    {
        get => _selectedRecipe;
        set
        {
            if (SetProperty(ref _selectedRecipe, value))
            {
                LoadSelectedRecipeForEdit();
                OperationMessage = value is null
                    ? "当前没有选中配方。"
                    : $"当前配方：{value.Name}，PLC 起始地址：DB{RecipeDbNumber}.DBB{value.StartByteOffset}。";

                if (value is not null && !_suppressAutoRead && _plcService.IsConnected)
                {
                    ReadSelectedRecipe();
                }
            }
        }
    }

    public string PlcIpAddress
    {
        get => _plcIpAddress;
        set
        {
            var normalizedValue = value?.Trim() ?? string.Empty;
            if (!IsValidIpv4Address(normalizedValue))
            {
                OperationMessage = $"PLC IP 地址格式无效：{value}";
                RaisePropertyChanged();
                return;
            }

            if (SetProperty(ref _plcIpAddress, normalizedValue))
            {
                _plcService.IpAddress = normalizedValue;
                if (SaveConfiguration())
                {
                    OperationMessage = $"PLC IP 已保存：{normalizedValue}";
                }
            }
        }
    }

    public string PlcState
    {
        get => _plcState;
        private set => SetProperty(ref _plcState, value);
    }

    public string EditRecipeName
    {
        get => _editRecipeName;
        set => SetProperty(ref _editRecipeName, value);
    }

    public string EditStartByteOffset
    {
        get => _editStartByteOffset;
        set => SetProperty(ref _editStartByteOffset, value);
    }

    public string OperationMessage
    {
        get => _operationMessage;
        private set => SetProperty(ref _operationMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    private void ConnectPlc()
    {
        try
        {
            RunPlcOperation(() =>
            {
                _plcService.IpAddress = PlcIpAddress;
                _plcService.Connect();
                PlcState = _plcService.IsConnected ? "已连接" : "未连接";
                OperationMessage = $"PLC 已连接：{PlcIpAddress}";
            });

            _plcService.ReadBool(new PlcAddress(6, 96, PlcValueType.Bool, 4));
        }
        catch (Exception ex)
        {
            OperationMessage = $"PLC 连接失败：{ex.Message}";
        }
    }

    private bool CanReadSelectedRecipe()
    {
        return SelectedRecipe is not null && !IsBusy;
    }

    private void ReadSelectedRecipe()
    {
        if (SelectedRecipe is null)
        {
            return;
        }

        RunPlcOperation(() =>
        {
            foreach (var parameter in SelectedRecipe.ParameterGroups.SelectMany(group => group.Parameters))
            {
                parameter.Value = ReadParameterValue(parameter);
            }

            PlcState = _plcService.IsConnected ? "已连接" : "未连接";
            OperationMessage = $"已从 PLC 读取 {SelectedRecipe.Name} 的参数。";
        });
    }

    private void RunPlcOperation(Action operation)
    {
        IsBusy = true;
        try
        {
            operation();
        }
        catch (Exception ex)
        {
            PlcState = "连接异常";
            OperationMessage = $"PLC 操作失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private string ReadParameterValue(RecipeParameter parameter)
    {
        return parameter.Address.ValueType switch
        {
            PlcValueType.Bool => _plcService.ReadBool(parameter.Address).ToString(),
            PlcValueType.Int => _plcService.ReadInt(parameter.Address).ToString(CultureInfo.InvariantCulture),
            PlcValueType.Float => _plcService.ReadFloat(parameter.Address).ToString("0.###", CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException($"不支持的 PLC 数据类型：{parameter.Address.ValueType}")
        };
    }

    private bool CanApplySelectedRecipe()
    {
        return SelectedRecipe is not null
            && !string.IsNullOrWhiteSpace(EditRecipeName)
            && int.TryParse(EditStartByteOffset, NumberStyles.Integer, CultureInfo.InvariantCulture, out var startByte)
            && startByte >= 0;
    }

    private void ApplySelectedRecipe()
    {
        if (SelectedRecipe is null
            || !int.TryParse(EditStartByteOffset, NumberStyles.Integer, CultureInfo.InvariantCulture, out var startByte))
        {
            return;
        }

        SelectedRecipe.Name = EditRecipeName.Trim();
        SelectedRecipe.StartByteOffset = startByte;
        RefreshParameterAddresses(SelectedRecipe);
        if (SaveConfiguration())
        {
            OperationMessage = $"已修改选中配方：{SelectedRecipe.Code} / {SelectedRecipe.Name} / DB{RecipeDbNumber}.DBB{SelectedRecipe.StartByteOffset}";
        }
    }

    private string? LoadConfiguration()
    {
        try
        {
            var settings = _recipeConfigStore.Load();
            string? configurationMessage = null;
            if (!string.IsNullOrWhiteSpace(settings.PlcIpAddress))
            {
                if (IsValidIpv4Address(settings.PlcIpAddress))
                {
                    _plcIpAddress = settings.PlcIpAddress.Trim();
                    _plcService.IpAddress = _plcIpAddress;
                }
                else
                {
                    configurationMessage = $"本地配置中的 PLC IP 地址无效，已使用默认地址：{_plcIpAddress}";
                }
            }

            var savedRecipes = (settings.Recipes ?? [])
                .Where(metadata => !string.IsNullOrWhiteSpace(metadata.Code))
                .GroupBy(metadata => metadata.Code, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);

            foreach (var recipe in Recipes)
            {
                if (!savedRecipes.TryGetValue(recipe.Code, out var metadata))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(metadata.Name))
                {
                    recipe.Name = metadata.Name.Trim();
                }

                if (metadata.StartByteOffset >= 0)
                {
                    recipe.StartByteOffset = metadata.StartByteOffset;
                    RefreshParameterAddresses(recipe);
                }
            }

            return configurationMessage;
        }
        catch (Exception ex)
        {
            return $"配方配置读取失败，已使用默认配置：{ex.Message}";
        }
    }

    private bool SaveConfiguration()
    {
        try
        {
            _recipeConfigStore.Save(new RecipeSettings
            {
                PlcIpAddress = _plcIpAddress,
                Recipes = Recipes.Select(recipe => new RecipeMetadata
                {
                    Code = recipe.Code,
                    Name = recipe.Name,
                    StartByteOffset = recipe.StartByteOffset
                }).ToList()
            });
            return true;
        }
        catch (Exception ex)
        {
            OperationMessage = $"配方配置保存失败：{ex.Message}";
            return false;
        }
    }

    private static bool IsValidIpv4Address(string value)
    {
        return IPAddress.TryParse(value, out var address)
            && address.AddressFamily == AddressFamily.InterNetwork;
    }

    private void LoadSelectedRecipeForEdit()
    {
        EditRecipeName = SelectedRecipe?.Name ?? string.Empty;
        EditStartByteOffset = SelectedRecipe?.StartByteOffset.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static RecipeDefinition CreateRecipe(int index)
    {
        var startByte = RecipeStartByte + (index * RecipeStrideBytes);
        var recipe = new RecipeDefinition
        {
            Code = $"R{index + 1:000}",
            Name = index < 3 ? new[] { "150H-B001", "150H-B002", "180H-C001" }[index] : $"厚片配方-{index + 1:00}",
            Type = DefaultRecipeType,
            Description = "厚片材料焊道工艺参数",
            StartByteOffset = startByte
        };

        recipe.ParameterGroups.Add(CreateGroup("配方参数",
            ("YAbsolutePositionSpeed", "Y轴绝对定位速度", 0, PlcValueType.Float, "mm/s", "Y轴定位速度"),
            ("ZAbsolutePositionSpeed", "Z轴绝对定位速度", 4, PlcValueType.Float, "mm/s", "Z轴定位速度"),
            ("WeldPassCount", "焊道数量选择", 8, PlcValueType.Int, "道", "当前配方启用的焊道数量")));

        for (var weldIndex = 0; weldIndex < WeldPassCount; weldIndex++)
        {
            recipe.ParameterGroups.Add(CreateWeldPassGroup(weldIndex));
        }

        RefreshParameterAddresses(recipe);
        return recipe;
    }

    private static RecipeParameterGroup CreateWeldPassGroup(int weldIndex)
    {
        var weldNumber = weldIndex + 1;
        var baseOffset = WeldPassStartOffset + (weldIndex * WeldPassStrideBytes);
        return CreateGroup($"焊道{weldNumber}",
            ($"Weld{weldNumber}WaveNumber", "焊接波形", baseOffset, PlcValueType.Int, "", "焊接使用的激光波形编号"),
            ($"Weld{weldNumber}RSpeed", "R轴速度", baseOffset + 2, PlcValueType.Float, "deg/s", "R轴旋转速度"),
            ($"Weld{weldNumber}YPosition", "Y轴位置", baseOffset + 6, PlcValueType.Float, "mm", "焊接 Y 轴位置"),
            ($"Weld{weldNumber}ZPosition", "Z轴位置", baseOffset + 10, PlcValueType.Float, "mm", "焊接 Z 轴位置"),
            ($"Weld{weldNumber}RPreAngle", "R轴焊前预留角度", baseOffset + 14, PlcValueType.Float, "deg", "焊接前预留角度"),
            ($"Weld{weldNumber}RPosition", "R轴位置", baseOffset + 18, PlcValueType.Float, "deg", "焊接 R 轴位置"),
            ($"Weld{weldNumber}RPostAngle", "R轴焊后预留角度", baseOffset + 22, PlcValueType.Float, "deg", "焊接后预留角度"),
            ($"Weld{weldNumber}TemperatureUpperLimit", "焊接温度上限", baseOffset + 26, PlcValueType.Float, "℃", "焊接温度上限"),
            ($"Weld{weldNumber}TemperatureLowerLimit", "焊接温度下限", baseOffset + 30, PlcValueType.Float, "℃", "焊接温度下限"),
            ($"Weld{weldNumber}LaserPowerUpperLimit", "激光功率上限", baseOffset + 34, PlcValueType.Float, "W", "激光功率上限"),
            ($"Weld{weldNumber}LaserPowerLowerLimit", "激光功率下限", baseOffset + 38, PlcValueType.Float, "W", "激光功率下限"));
    }

    private static RecipeParameterGroup CreateGroup(
        string name,
        params (string Key, string Name, int RelativeOffset, PlcValueType ValueType, string Unit, string Remark)[] parameters)
    {
        var group = new RecipeParameterGroup { Name = name };
        foreach (var parameter in parameters)
        {
            group.Parameters.Add(new RecipeParameter
            {
                Key = parameter.Key,
                Name = parameter.Name,
                Value = "-",
                Unit = parameter.Unit,
                Remark = parameter.Remark,
                RelativeOffset = parameter.RelativeOffset,
                ValueType = parameter.ValueType
            });
        }

        return group;
    }

    private static void RefreshParameterAddresses(RecipeDefinition recipe)
    {
        foreach (var parameter in recipe.ParameterGroups.SelectMany(group => group.Parameters))
        {
            var address = new PlcAddress(
                RecipeDbNumber,
                recipe.StartByteOffset + parameter.RelativeOffset,
                parameter.ValueType);
            parameter.Address = address;
            parameter.AddressText = address.ToS7Address();
        }
    }
}

public sealed class RecipeDefinition : BindableBase
{
    private string _code = string.Empty;
    private string _name = string.Empty;
    private string _type = string.Empty;
    private string _description = string.Empty;
    private int _startByteOffset;

    public string Code
    {
        get => _code;
        set => SetProperty(ref _code, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Type
    {
        get => _type;
        set => SetProperty(ref _type, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public int StartByteOffset
    {
        get => _startByteOffset;
        set => SetProperty(ref _startByteOffset, value);
    }

    public ObservableCollection<RecipeParameterGroup> ParameterGroups { get; } = [];
}

public sealed class RecipeParameterGroup : BindableBase
{
    private string _name = string.Empty;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public ObservableCollection<RecipeParameter> Parameters { get; } = [];
}

public sealed class RecipeParameter : BindableBase
{
    private string _key = string.Empty;
    private string _name = string.Empty;
    private string _value = string.Empty;
    private string _unit = string.Empty;
    private string _remark = string.Empty;
    private string _addressText = string.Empty;
    private PlcAddress _address = new(1, 0, PlcValueType.Float);
    private int _relativeOffset;
    private PlcValueType _valueType;

    public string Key
    {
        get => _key;
        set => SetProperty(ref _key, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    public string Unit
    {
        get => _unit;
        set => SetProperty(ref _unit, value);
    }

    public string Remark
    {
        get => _remark;
        set => SetProperty(ref _remark, value);
    }

    public string AddressText
    {
        get => _addressText;
        set => SetProperty(ref _addressText, value);
    }

    public PlcAddress Address
    {
        get => _address;
        set => SetProperty(ref _address, value);
    }

    public int RelativeOffset
    {
        get => _relativeOffset;
        set => SetProperty(ref _relativeOffset, value);
    }

    public PlcValueType ValueType
    {
        get => _valueType;
        set => SetProperty(ref _valueType, value);
    }
}
