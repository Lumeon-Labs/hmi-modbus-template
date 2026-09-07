using HmiTemplate.Core.Data;
using HmiTemplate.Core.Services;
using HmiTemplate.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Windows;

namespace HmiTemplate.UI;

/// <summary>
/// WPF 應用程式入口
/// 負責 DI 容器設定、服務啟動順序：
///   1. Slave 啟動 → 2. Simulator 啟動 → 3. 等 500ms → 4. Master 啟動 → 5. 顯示視窗
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 設定 Serilog 日誌（寫入 logs/hmi-.log，每天一個檔案）
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                path: "logs/hmi-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();

        Log.Information("HMI Template 啟動中...");

        // 建立 DI 容器
        var services = new ServiceCollection();
        RegisterServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // 在背景啟動服務，不阻塞 UI 執行緒
        _ = Task.Run(StartServicesAsync);
    }

    /// <summary>
    /// 依序啟動所有後台服務並顯示主視窗
    /// </summary>
    private async Task StartServicesAsync()
    {
        try
        {
            var sp = _serviceProvider!;
            var slave = sp.GetRequiredService<ModbusSlaveService>();
            var simulator = sp.GetRequiredService<DataSimulatorService>();
            var master = sp.GetRequiredService<ModbusMasterService>();
            var configStore = sp.GetRequiredService<ConfigStore>();
            var recipeManager = sp.GetRequiredService<RecipeManager>();

            // 載入設定
            await configStore.LoadAsync();
            await recipeManager.LoadAsync();

            // Step 1：啟動 Slave（讓 TCP Listener 就緒）
            await slave.StartAsync(
                configStore.Config.Connection.Port);
            Log.Information("Modbus Slave 就緒");

            // Step 2：啟動數據模擬器（開始更新 Slave 暫存器）
            await simulator.StartAsync();
            Log.Information("DataSimulator 就緒");

            // Step 3：等待 500ms 確保 Slave 完全就緒
            await Task.Delay(500);

            // Step 4：啟動 Master（連線到本機 Slave）
            var conn = configStore.Config.Connection;
            await master.StartAsync(conn.Host, conn.Port);
            master.SetReadInterval(conn.ReadIntervalMs);
            Log.Information("Modbus Master 就緒");

            // Step 5：在 UI 執行緒顯示主視窗
            await Dispatcher.InvokeAsync(() =>
            {
                var mainVm = sp.GetRequiredService<MainViewModel>();
                var mainWindow = new MainWindow { DataContext = mainVm };
                MainWindow = mainWindow;
                mainWindow.Show();
                Log.Information("主視窗顯示完成");
            });
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "服務啟動失敗");
            await Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show(
                    $"服務啟動失敗：{ex.Message}\n\n請確認 Port 502 未被佔用。",
                    "HMI Template - 啟動錯誤",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(1);
            });
        }
    }

    /// <summary>
    /// 注冊所有服務（全部 Singleton）
    /// </summary>
    private static void RegisterServices(IServiceCollection services)
    {
        // Core Services
        services.AddSingleton<ConfigStore>();
        services.AddSingleton<AlarmHistory>();
        services.AddSingleton<TrendBuffer>();
        services.AddSingleton<ModbusSlaveService>();
        services.AddSingleton<ModbusMasterService>();
        services.AddSingleton<DataSimulatorService>();

        // AlarmEngine 需要從 ConfigStore 取得規則，用工廠建立
        services.AddSingleton<AlarmEngine>(sp =>
        {
            var configStore = sp.GetRequiredService<ConfigStore>();
            var history = sp.GetRequiredService<AlarmHistory>();
            var slave = sp.GetRequiredService<ModbusSlaveService>();
            return new AlarmEngine(configStore.Config.AlarmRules, history, slave);
        });

        services.AddSingleton<RecipeManager>();

        // ViewModels
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<TrendViewModel>();
        services.AddSingleton<RecipeViewModel>();
        services.AddSingleton<AlarmViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainViewModel>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("HMI Template 關閉中...");

        // 停止所有服務
        if (_serviceProvider != null)
        {
            var master = _serviceProvider.GetService<ModbusMasterService>();
            var simulator = _serviceProvider.GetService<DataSimulatorService>();
            var slave = _serviceProvider.GetService<ModbusSlaveService>();

            master?.StopAsync().GetAwaiter().GetResult();
            simulator?.StopAsync().GetAwaiter().GetResult();
            slave?.StopAsync().GetAwaiter().GetResult();

            _serviceProvider.Dispose();
        }

        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
