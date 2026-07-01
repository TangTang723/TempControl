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
        Assert.Equal(new PlcAddress(15, 0, PlcValueType.Float), GetParameter(viewModel.Recipes[0], "YAbsolutePositionSpeed").Address);
        Assert.Equal(new PlcAddress(15, 324, PlcValueType.Float), GetParameter(viewModel.Recipes[1], "YAbsolutePositionSpeed").Address);
        Assert.Equal(new PlcAddress(15, 4860, PlcValueType.Float), GetParameter(viewModel.Recipes[15], "YAbsolutePositionSpeed").Address);
    }

    [Fact]
    public void NewViewModel_CreatesSixWeldPassesWithExpectedAddressLayout()
    {
        var viewModel = CreateViewModel();
        var recipe = viewModel.Recipes[0];

        Assert.Equal(new PlcAddress(15, 8, PlcValueType.Int), GetParameter(recipe, "WeldPassCount").Address);
        Assert.Equal(new PlcAddress(15, 10, PlcValueType.Int), GetParameter(recipe, "Weld1WaveNumber").Address);
        Assert.Equal(new PlcAddress(15, 12, PlcValueType.Float), GetParameter(recipe, "Weld1RSpeed").Address);
        Assert.Equal(new PlcAddress(15, 48, PlcValueType.Float), GetParameter(recipe, "Weld1LaserPowerLowerLimit").Address);
        Assert.Equal(new PlcAddress(15, 52, PlcValueType.Int), GetParameter(recipe, "Weld2WaveNumber").Address);
        Assert.Equal(new PlcAddress(15, 90, PlcValueType.Float), GetParameter(recipe, "Weld2LaserPowerLowerLimit").Address);
        Assert.Equal(new PlcAddress(15, 220, PlcValueType.Int), GetParameter(recipe, "Weld6WaveNumber").Address);
        Assert.Equal(new PlcAddress(15, 258, PlcValueType.Float), GetParameter(recipe, "Weld6LaserPowerLowerLimit").Address);
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
        Assert.Equal("DB15.DBD512", GetParameter(selectedRecipe, "YAbsolutePositionSpeed").AddressText);
        Assert.Equal("DB15.DBW520", GetParameter(selectedRecipe, "WeldPassCount").AddressText);
        Assert.Equal("DB15.DBW522", GetParameter(selectedRecipe, "Weld1WaveNumber").AddressText);
        Assert.Equal("DB15.DBW564", GetParameter(selectedRecipe, "Weld2WaveNumber").AddressText);
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
                    new RecipeMetadata { Code = "R002", Name = "已保存配方", StartByteOffset = 640 },
                    new RecipeMetadata { Code = "UNKNOWN", Name = "忽略", StartByteOffset = 999 }
                ]
            }
        };

        var viewModel = CreateViewModel(plc, store);

        Assert.Equal("192.168.1.25", viewModel.PlcIpAddress);
        Assert.Equal("192.168.1.25", plc.IpAddress);
        Assert.Equal("已保存配方", viewModel.Recipes[1].Name);
        Assert.Equal(640, viewModel.Recipes[1].StartByteOffset);
        Assert.Equal("DB15.DBD640", GetParameter(viewModel.Recipes[1], "YAbsolutePositionSpeed").AddressText);
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
    public void ReadSelectedRecipeCommand_LoadsWeldPassValuesDirectlyByPlcAddress()
    {
        var plc = new RecordingPlcService();
        plc.FloatValues[new PlcAddress(15, 0, PlcValueType.Float)] = 20.5f;
        plc.FloatValues[new PlcAddress(15, 4, PlcValueType.Float)] = 21.5f;
        plc.IntValues[new PlcAddress(15, 8, PlcValueType.Int)] = 6;
        plc.IntValues[new PlcAddress(15, 10, PlcValueType.Int)] = 1;
        plc.FloatValues[new PlcAddress(15, 12, PlcValueType.Float)] = 180.5f;
        plc.FloatValues[new PlcAddress(15, 48, PlcValueType.Float)] = 30.5f;
        plc.IntValues[new PlcAddress(15, 52, PlcValueType.Int)] = 2;
        plc.FloatValues[new PlcAddress(15, 90, PlcValueType.Float)] = 40.5f;
        var viewModel = CreateViewModel(plc);

        viewModel.ReadSelectedRecipeCommand.Execute();

        var selectedRecipe = viewModel.SelectedRecipe!;
        Assert.Equal("20.5", GetParameter(selectedRecipe, "YAbsolutePositionSpeed").Value);
        Assert.Equal("21.5", GetParameter(selectedRecipe, "ZAbsolutePositionSpeed").Value);
        Assert.Equal("6", GetParameter(selectedRecipe, "WeldPassCount").Value);
        Assert.Equal("1", GetParameter(selectedRecipe, "Weld1WaveNumber").Value);
        Assert.Equal("180.5", GetParameter(selectedRecipe, "Weld1RSpeed").Value);
        Assert.Equal("30.5", GetParameter(selectedRecipe, "Weld1LaserPowerLowerLimit").Value);
        Assert.Equal("2", GetParameter(selectedRecipe, "Weld2WaveNumber").Value);
        Assert.Equal("40.5", GetParameter(selectedRecipe, "Weld2LaserPowerLowerLimit").Value);
        Assert.Contains(new PlcAddress(15, 0, PlcValueType.Float), plc.ReadAddresses);
        Assert.Contains(new PlcAddress(15, 8, PlcValueType.Int), plc.ReadAddresses);
        Assert.Contains(new PlcAddress(15, 52, PlcValueType.Int), plc.ReadAddresses);
        Assert.Contains(new PlcAddress(15, 90, PlcValueType.Float), plc.ReadAddresses);
    }

    [Fact]
    public void SelectingRecipe_LoadsValuesDirectlyBySelectedRecipeAddress()
    {
        var plc = new RecordingPlcService();
        plc.FloatValues[new PlcAddress(15, 324, PlcValueType.Float)] = 31.2f;
        plc.IntValues[new PlcAddress(15, 332, PlcValueType.Int)] = 4;
        plc.IntValues[new PlcAddress(15, 334, PlcValueType.Int)] = 3;
        plc.FloatValues[new PlcAddress(15, 336, PlcValueType.Float)] = 205.8f;
        plc.FloatValues[new PlcAddress(15, 414, PlcValueType.Float)] = 55.5f;
        var viewModel = CreateViewModel(plc);
        plc.Connect();

        viewModel.SelectedRecipe = viewModel.Recipes[1];

        var selectedRecipe = viewModel.SelectedRecipe!;
        Assert.Equal("31.2", GetParameter(selectedRecipe, "YAbsolutePositionSpeed").Value);
        Assert.Equal("4", GetParameter(selectedRecipe, "WeldPassCount").Value);
        Assert.Equal("3", GetParameter(selectedRecipe, "Weld1WaveNumber").Value);
        Assert.Equal("205.8", GetParameter(selectedRecipe, "Weld1RSpeed").Value);
        Assert.Equal("55.5", GetParameter(selectedRecipe, "Weld2LaserPowerLowerLimit").Value);
        Assert.Contains(new PlcAddress(15, 324, PlcValueType.Float), plc.ReadAddresses);
        Assert.Contains(new PlcAddress(15, 332, PlcValueType.Int), plc.ReadAddresses);
        Assert.Contains(new PlcAddress(15, 334, PlcValueType.Int), plc.ReadAddresses);
        Assert.Contains(new PlcAddress(15, 414, PlcValueType.Float), plc.ReadAddresses);
    }

    [Fact]
    public void NewViewModel_WhenPlcConnectedLoadsFirstRecipeValues()
    {
        var plc = new RecordingPlcService();
        plc.Connect();
        plc.FloatValues[new PlcAddress(15, 0, PlcValueType.Float)] = 12.5f;
        plc.IntValues[new PlcAddress(15, 8, PlcValueType.Int)] = 6;
        plc.IntValues[new PlcAddress(15, 10, PlcValueType.Int)] = 1;

        var viewModel = CreateViewModel(plc);

        Assert.Equal(viewModel.Recipes[0], viewModel.SelectedRecipe);
        Assert.Equal("12.5", GetParameter(viewModel.Recipes[0], "YAbsolutePositionSpeed").Value);
        Assert.Equal("6", GetParameter(viewModel.Recipes[0], "WeldPassCount").Value);
        Assert.Equal("1", GetParameter(viewModel.Recipes[0], "Weld1WaveNumber").Value);
        Assert.Contains(new PlcAddress(15, 0, PlcValueType.Float), plc.ReadAddresses);
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
        public event EventHandler<RecipeSettings>? SettingsSaved;

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
            SettingsSaved?.Invoke(this, settings);
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
