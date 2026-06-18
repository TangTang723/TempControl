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
    private const int RecipeDbNumber = 1;
    private const int RecipeStartByte = 0;
    private const int RecipeStrideBytes = 64;
    private const int SegmentCountOffset = 12;
    private const int SegmentTemperatureStartOffset = 16;
    private const int SegmentTemperatureStrideBytes = 4;
    private const int MaxSegmentCount = 8;
    private const string SegmentTemperatureGroupName = "分段温度";
    private const string SegmentCountKey = "SegmentCount";
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

                if (value is not null && !_suppressAutoRead&&_plcService.IsConnected)
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
        RunPlcOperation(() =>
        {
            _plcService.IpAddress = PlcIpAddress;
            _plcService.Connect();
            PlcState = _plcService.IsConnected ? "已连接" : "未连接";
            OperationMessage = $"PLC 已连接：{PlcIpAddress}";
        });
        bool t = _plcService.ReadBool(new PlcAddress(6, 96, PlcValueType.Bool, 4));
      
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
            foreach (var group in SelectedRecipe.ParameterGroups)
            {
                if (group.Name == SegmentTemperatureGroupName)
                {
                    ReadSegmentTemperatures(SelectedRecipe, group);
                    continue;
                }

                foreach (var parameter in group.Parameters)
                {
                    parameter.Value = ReadParameterValue(parameter);
                }
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

    private void ReadSegmentTemperatures(RecipeDefinition recipe, RecipeParameterGroup group)
    {
        var segmentCountParameter = group.Parameters.Single(parameter => parameter.Key == SegmentCountKey);
        var rawSegmentCount = _plcService.ReadInt(segmentCountParameter.Address);
        var segmentCount = Math.Clamp(rawSegmentCount, 0, MaxSegmentCount);
        segmentCountParameter.Value = segmentCount.ToString(CultureInfo.InvariantCulture);

        RebuildSegmentTemperatureParameters(recipe, group, segmentCount);

        foreach (var parameter in group.Parameters.Where(parameter => parameter.Key != SegmentCountKey))
        {
            parameter.Value = ReadParameterValue(parameter);
        }
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
            Description = "厚片材料分段升温参数",
            StartByteOffset = startByte
        };

        recipe.ParameterGroups.Add(CreateGroup("基础设置",
            ("TargetTemperature", "目标温度", 0, PlcValueType.Float, "°C", "厚片目标温度"),
            ("TemperatureUpperLimit", "温度上限", 4, PlcValueType.Float, "°C", "厚片保护上限"),
            ("TemperatureLowerLimit", "温度下限", 8, PlcValueType.Float, "°C", "厚片有效温区下限")));
        recipe.ParameterGroups.Add(CreateGroup(SegmentTemperatureGroupName,
            (SegmentCountKey, "分段数", SegmentCountOffset, PlcValueType.Int, "段", "PLC 中配置的温度分段数量")));
        recipe.ParameterGroups.Add(CreateGroup("激光参考",
            ("LaserPower", "激光功率", 48, PlcValueType.Float, "%", "厚片参考功率"),
            ("ScanSpeed", "扫描速度", 52, PlcValueType.Float, "mm/s", "厚片参考速度"),
            ("RepeatCount", "重复次数", 56, PlcValueType.Int, "次", "加工循环次数"),
            ("Enabled", "启用配方", 60, PlcValueType.Bool, "", "PLC 中该配方启用标志")));

        RefreshParameterAddresses(recipe);
        return recipe;
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

    private static void RebuildSegmentTemperatureParameters(
        RecipeDefinition recipe,
        RecipeParameterGroup group,
        int segmentCount)
    {
        var segmentCountParameter = group.Parameters.Single(parameter => parameter.Key == SegmentCountKey);
        group.Parameters.Clear();
        group.Parameters.Add(segmentCountParameter);

        for (var index = 0; index < segmentCount; index++)
        {
            group.Parameters.Add(CreateSegmentTemperatureParameter(recipe, index));
        }
    }

    private static RecipeParameter CreateSegmentTemperatureParameter(RecipeDefinition recipe, int index)
    {
        var relativeOffset = SegmentTemperatureStartOffset + (index * SegmentTemperatureStrideBytes);
        var address = new PlcAddress(RecipeDbNumber, recipe.StartByteOffset + relativeOffset, PlcValueType.Float);
        return new RecipeParameter
        {
            Key = $"Stage{index + 1}Temperature",
            Name = $"{index + 1}段温度",
            Value = "-",
            Unit = "°C",
            Remark = $"第 {index + 1} 段目标温度",
            RelativeOffset = relativeOffset,
            ValueType = PlcValueType.Float,
            Address = address,
            AddressText = address.ToS7Address()
        };
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
