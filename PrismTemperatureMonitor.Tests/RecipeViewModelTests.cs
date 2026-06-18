using PrismTemperatureMonitor.Models;
using PrismTemperatureMonitor.Services;
using PrismTemperatureMonitor.ViewModels;

namespace PrismTemperatureMonitor.Tests;

public sealed class RecipeViewModelTests
{
    [Fact]
    public void NewViewModel_CreatesSixteenReadonlyThickPlateRecipes()
    {
        var viewModel = CreateViewModel();

        Assert.Equal(16, viewModel.Recipes.Count);
        Assert.All(viewModel.Recipes, recipe => Assert.Equal("厚片工艺", recipe.Type));
        Assert.Equal("R001", viewModel.Recipes[0].Code);
        Assert.Equal("150H-B001", viewModel.Recipes[0].Name);
        Assert.Equal(new PlcAddress(1, 0, PlcValueType.Float), GetParameter(viewModel.Recipes[0], "TargetTemperature").Address);
        Assert.Equal(new PlcAddress(1, 64, PlcValueType.Float), GetParameter(viewModel.Recipes[1], "TargetTemperature").Address);
        Assert.Equal(new PlcAddress(1, 960, PlcValueType.Float), GetParameter(viewModel.Recipes[15], "TargetTemperature").Address);
    }

    [Fact]
    public void EditingRecipeNameAndStartAddress_RefreshesParameterAddressesWithoutRegistration()
    {
        var store = new RecordingRecipeConfigStore();
        var viewModel = CreateViewModel(store: store);
        var selectedRecipe = viewModel.SelectedRecipe!;

        viewModel.EditRecipeName = "200H-D001";
        viewModel.EditStartByteOffset = "512";
        viewModel.ApplySelectedRecipeCommand.Execute();

        Assert.Equal("R001", selectedRecipe.Code);
        Assert.Equal("200H-D001", selectedRecipe.Name);
        Assert.Equal(512, selectedRecipe.StartByteOffset);
        Assert.Equal("DB1.DBD512", GetParameter(selectedRecipe, "TargetTemperature").AddressText);
        Assert.Equal("DB1.DBW524", GetParameter(selectedRecipe, "SegmentCount").AddressText);
        Assert.Equal("200H-D001", store.SavedSettings!.Recipes.Single(recipe => recipe.Code == "R001").Name);
        Assert.Equal(512, store.SavedSettings.Recipes.Single(recipe => recipe.Code == "R001").StartByteOffset);
    }

    [Fact]
    public void NewViewModel_LoadsSavedPlcIpAndRecipeMetadata()
    {
        var plc = new RecordingPlcService();
        var store = new RecordingRecipeConfigStore
        {
            LoadedSettings = new RecipeSettings
            {
                PlcIpAddress = "192.168.1.25",
                Recipes =
                [
                    new RecipeMetadata { Code = "R002", Name = "已保存配方", StartByteOffset = 320 },
                    new RecipeMetadata { Code = "UNKNOWN", Name = "忽略", StartByteOffset = 999 }
                ]
            }
        };

        var viewModel = CreateViewModel(plc, store);

        Assert.Equal("192.168.1.25", viewModel.PlcIpAddress);
        Assert.Equal("192.168.1.25", plc.IpAddress);
        Assert.Equal("已保存配方", viewModel.Recipes[1].Name);
        Assert.Equal(320, viewModel.Recipes[1].StartByteOffset);
        Assert.Equal("DB1.DBD320", GetParameter(viewModel.Recipes[1], "TargetTemperature").AddressText);
        Assert.Equal("150H-B001", viewModel.Recipes[0].Name);
    }

    [Fact]
    public void SettingValidPlcIp_SavesConfiguration()
    {
        var plc = new RecordingPlcService();
        var store = new RecordingRecipeConfigStore();
        var viewModel = CreateViewModel(plc, store);

        viewModel.PlcIpAddress = "192.168.10.8";

        Assert.Equal("192.168.10.8", plc.IpAddress);
        Assert.Equal("192.168.10.8", store.SavedSettings!.PlcIpAddress);
        Assert.Equal(16, store.SavedSettings.Recipes.Count);
    }

    [Fact]
    public void SettingInvalidPlcIp_DoesNotReplaceLastValidConfiguration()
    {
        var plc = new RecordingPlcService();
        var store = new RecordingRecipeConfigStore();
        var viewModel = CreateViewModel(plc, store);

        viewModel.PlcIpAddress = "not-an-ip";

        Assert.Equal("192.168.0.10", viewModel.PlcIpAddress);
        Assert.Equal("192.168.0.10", plc.IpAddress);
        Assert.Null(store.SavedSettings);
        Assert.Contains("IP 地址格式无效", viewModel.OperationMessage);
    }

    [Fact]
    public void ConfigurationLoadFailure_UsesDefaultsWithoutCrashing()
    {
        var store = new RecordingRecipeConfigStore { LoadException = new InvalidDataException("bad json") };

        var viewModel = CreateViewModel(store: store);

        Assert.Equal(16, viewModel.Recipes.Count);
        Assert.Equal("150H-B001", viewModel.Recipes[0].Name);
        Assert.Contains("配置读取失败", viewModel.OperationMessage);
    }

    [Fact]
    public void ConfigurationSaveFailure_DoesNotCrashOrRevertCurrentRecipe()
    {
        var store = new RecordingRecipeConfigStore { SaveException = new IOException("disk unavailable") };
        var viewModel = CreateViewModel(store: store);

        viewModel.EditRecipeName = "内存中的配方";
        viewModel.EditStartByteOffset = "256";
        viewModel.ApplySelectedRecipeCommand.Execute();

        Assert.Equal("内存中的配方", viewModel.SelectedRecipe!.Name);
        Assert.Equal(256, viewModel.SelectedRecipe.StartByteOffset);
        Assert.Contains("配置保存失败", viewModel.OperationMessage);
    }

    [Fact]
    public void ConnectPlcCommand_ConnectsWithoutStartingPolling()
    {
        var plc = new RecordingPlcService();
        var viewModel = CreateViewModel(plc);

        viewModel.ConnectPlcCommand.Execute();

        Assert.True(plc.IsConnected);
        Assert.Equal(1, plc.ConnectCount);
        Assert.Equal("已连接", viewModel.PlcState);
    }

    [Fact]
    public void ReadSelectedRecipeCommand_LoadsValuesDirectlyByPlcAddress()
    {
        var plc = new RecordingPlcService();
        plc.FloatValues[new PlcAddress(1, 0, PlcValueType.Float)] = 221.5f;
        plc.IntValues[new PlcAddress(1, 12, PlcValueType.Int)] = 2;
        plc.FloatValues[new PlcAddress(1, 16, PlcValueType.Float)] = 180.5f;
        plc.FloatValues[new PlcAddress(1, 20, PlcValueType.Float)] = 220.5f;
        plc.BoolValues[new PlcAddress(1, 60, PlcValueType.Bool)] = true;
        var viewModel = CreateViewModel(plc);

        viewModel.ReadSelectedRecipeCommand.Execute();

        var selectedRecipe = viewModel.SelectedRecipe!;
        Assert.Equal("221.5", GetParameter(selectedRecipe, "TargetTemperature").Value);
        Assert.Equal("2", GetParameter(selectedRecipe, "SegmentCount").Value);
        Assert.Equal("180.5", GetParameter(selectedRecipe, "Stage1Temperature").Value);
        Assert.Equal("220.5", GetParameter(selectedRecipe, "Stage2Temperature").Value);
        Assert.DoesNotContain(
            selectedRecipe.ParameterGroups.SelectMany(group => group.Parameters),
            parameter => parameter.Key.Contains("Time", StringComparison.Ordinal));
        Assert.Equal("True", GetParameter(selectedRecipe, "Enabled").Value);
        Assert.Contains(new PlcAddress(1, 0, PlcValueType.Float), plc.ReadAddresses);
        Assert.Contains(new PlcAddress(1, 12, PlcValueType.Int), plc.ReadAddresses);
        Assert.Contains(new PlcAddress(1, 16, PlcValueType.Float), plc.ReadAddresses);
        Assert.Contains(new PlcAddress(1, 20, PlcValueType.Float), plc.ReadAddresses);
        Assert.Contains(new PlcAddress(1, 60, PlcValueType.Bool), plc.ReadAddresses);
    }

    [Fact]
    public void SelectingRecipe_LoadsValuesDirectlyBySelectedRecipeAddress()
    {
        var plc = new RecordingPlcService();
        plc.FloatValues[new PlcAddress(1, 64, PlcValueType.Float)] = 310.2f;
        plc.IntValues[new PlcAddress(1, 76, PlcValueType.Int)] = 1;
        plc.FloatValues[new PlcAddress(1, 80, PlcValueType.Float)] = 205.8f;
        plc.BoolValues[new PlcAddress(1, 124, PlcValueType.Bool)] = true;
        var viewModel = CreateViewModel(plc);
        plc.Connect();

        viewModel.SelectedRecipe = viewModel.Recipes[1];

        var selectedRecipe = viewModel.SelectedRecipe!;
        Assert.Equal("310.2", GetParameter(selectedRecipe, "TargetTemperature").Value);
        Assert.Equal("1", GetParameter(selectedRecipe, "SegmentCount").Value);
        Assert.Equal("205.8", GetParameter(selectedRecipe, "Stage1Temperature").Value);
        Assert.Equal("True", GetParameter(selectedRecipe, "Enabled").Value);
        Assert.Contains(new PlcAddress(1, 64, PlcValueType.Float), plc.ReadAddresses);
        Assert.Contains(new PlcAddress(1, 76, PlcValueType.Int), plc.ReadAddresses);
        Assert.Contains(new PlcAddress(1, 80, PlcValueType.Float), plc.ReadAddresses);
        Assert.Contains(new PlcAddress(1, 124, PlcValueType.Bool), plc.ReadAddresses);
    }

    [Fact]
    public void ReadSelectedRecipeCommand_ShowsErrorWhenPlcReadFails()
    {
        var plc = new RecordingPlcService { ThrowOnRead = true };
        var viewModel = CreateViewModel(plc);

        viewModel.ReadSelectedRecipeCommand.Execute();

        Assert.Equal("连接异常", viewModel.PlcState);
        Assert.StartsWith("PLC 操作失败：", viewModel.OperationMessage);
        Assert.False(viewModel.IsBusy);
    }

    private static RecipeParameter GetParameter(RecipeDefinition recipe, string key)
    {
        return recipe.ParameterGroups
            .SelectMany(group => group.Parameters)
            .Single(parameter => parameter.Key == key);
    }

    private static RecipeViewModel CreateViewModel(
        RecordingPlcService? plc = null,
        RecordingRecipeConfigStore? store = null)
    {
        return new RecipeViewModel(plc ?? new RecordingPlcService(), store ?? new RecordingRecipeConfigStore());
    }

    private sealed class RecordingRecipeConfigStore : IRecipeConfigStore
    {
        public RecipeSettings LoadedSettings { get; set; } = new();

        public RecipeSettings? SavedSettings { get; private set; }

        public Exception? LoadException { get; set; }

        public Exception? SaveException { get; set; }

        public RecipeSettings Load()
        {
            if (LoadException is not null)
            {
                throw LoadException;
            }

            return LoadedSettings;
        }

        public void Save(RecipeSettings settings)
        {
            if (SaveException is not null)
            {
                throw SaveException;
            }

            SavedSettings = new RecipeSettings
            {
                PlcIpAddress = settings.PlcIpAddress,
                Recipes = settings.Recipes
                    .Select(recipe => new RecipeMetadata
                    {
                        Code = recipe.Code,
                        Name = recipe.Name,
                        StartByteOffset = recipe.StartByteOffset
                    })
                    .ToList()
            };
        }
    }

    private sealed class RecordingPlcService : IPlcService
    {
        public Dictionary<PlcAddress, bool> BoolValues { get; } = [];

        public Dictionary<PlcAddress, int> IntValues { get; } = [];

        public Dictionary<PlcAddress, float> FloatValues { get; } = [];

        public List<PlcAddress> ReadAddresses { get; } = [];

        public string IpAddress { get; set; } = "192.168.0.10";

        public bool IsConnected { get; private set; }

        public int ConnectCount { get; private set; }

        public bool ThrowOnRead { get; set; }

        public void Connect()
        {
            ConnectCount++;
            IsConnected = true;
        }

        public void Disconnect()
        {
            IsConnected = false;
        }

        public bool ReadBool(PlcAddress address)
        {
            ReadAddresses.Add(address);
            ThrowIfNeeded();
            return BoolValues.GetValueOrDefault(address);
        }

        public int ReadInt(PlcAddress address)
        {
            ReadAddresses.Add(address);
            ThrowIfNeeded();
            return IntValues.GetValueOrDefault(address);
        }

        public float ReadFloat(PlcAddress address)
        {
            ReadAddresses.Add(address);
            ThrowIfNeeded();
            return FloatValues.GetValueOrDefault(address);
        }

        public void WriteBool(PlcAddress address, bool value)
        {
            BoolValues[address] = value;
        }

        public void WriteInt(PlcAddress address, int value)
        {
            IntValues[address] = value;
        }

        public void WriteDInt(PlcAddress address, int value)
        {
            IntValues[address] = value;
        }

        public void WriteFloat(PlcAddress address, float value)
        {
            FloatValues[address] = value;
        }

        private void ThrowIfNeeded()
        {
            if (ThrowOnRead)
            {
                throw new InvalidOperationException("Simulated PLC read failure.");
            }
        }
    }
}
