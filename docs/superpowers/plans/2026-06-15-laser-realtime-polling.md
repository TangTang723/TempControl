# 激光器应用级实时轮询 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 软件启动后持续更新所有已连接 HWQ/HWD 激光器的实时缓存，使温控页无需进入激光管理页即可显示实时功率和出光点数。

**Architecture:** 新增应用级单例 `LaserRealtimePollingService`，后台线程按型号调度共享 `ILaserDeviceService`。HWD 每 50ms 调用快速读取、每 1s 调用完整状态读取；HWQ 每 200ms调用完整状态读取。页面只读取缓存，激光参数 ViewModel 删除自己的通讯轮询。

**Tech Stack:** .NET 8、WPF、Prism、xUnit

---

### Task 1: 应用级轮询服务

**Files:**
- Create: `PrismTemperatureMonitor/Services/LaserRealtimePollingService.cs`
- Test: `PrismTemperatureMonitor.Tests/LaserRealtimePollingServiceTests.cs`

- [ ] 编写失败测试，验证 HWQ/HWD 调用对应读取接口、未连接设备跳过、单台异常隔离。
- [ ] 运行目标测试并确认因服务不存在而失败。
- [ ] 实现后台轮询服务及可测试的单周期调度。
- [ ] 运行目标测试并确认通过。

### Task 2: 应用启动与页面缓存刷新

**Files:**
- Modify: `PrismTemperatureMonitor/App.xaml.cs`
- Modify: `PrismTemperatureMonitor/Services/LaserDeviceStartupInitializer.cs`
- Modify: `PrismTemperatureMonitor/ViewModels/LaserParametersViewModel.cs`
- Test: `PrismTemperatureMonitor.Tests/LaserDeviceStartupInitializerTests.cs`
- Test: `PrismTemperatureMonitor.Tests/LaserParametersViewModelTests.cs`

- [ ] 测试应用初始化会启动轮询一次。
- [ ] 注册并启动单例轮询服务。
- [ ] 删除激光参数页自身的串口轮询，仅使用共享缓存定时刷新显示。
- [ ] 运行相关测试确认无重复通讯轮询。

### Task 3: 验证

**Files:**
- Verify: `PrismTemperatureMonitor.sln`

- [ ] 运行激光器、温度页相关测试。
- [ ] 运行完整测试并记录既有失败。
- [ ] 使用独立输出目录构建解决方案，确认 0 编译错误。
