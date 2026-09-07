using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HmiTemplate.Core.Data;
using HmiTemplate.Core.Services;
using System.Collections.ObjectModel;
using System.Windows;

namespace HmiTemplate.UI.ViewModels;

/// <summary>
/// 設定頁面 ViewModel：
///   - 連線設定（IP、Port、讀取間隔）
///   - Slave 模擬器控制（Start/Stop）
///   - 系統資訊顯示
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ModbusMasterService _master;
    private readonly ModbusSlaveService _slave;
    private readonly ConfigStore _configStore;

    // ── 連線設定 ──────────────────────────────────

    [ObservableProperty]
    private string _host = "127.0.0.1";

    [ObservableProperty]
    private int _port = 502;

    [ObservableProperty]
    private int _selectedReadInterval = 500;

    /// <summary>可選的讀取間隔清單（ms）</summary>
    public ObservableCollection<int> ReadIntervalOptions { get; } = [100, 500, 1000, 2000];

    // ── Slave 狀態 ──────────────────────────────────

    [ObservableProperty]
    private string _slaveStatus = "Stopped";

    [ObservableProperty]
    private string _slaveStatusColor = "#6B7280";

    [ObservableProperty]
    private bool _isSlaveRunning;

    // ── 連線狀態 ──────────────────────────────────

    [ObservableProperty]
    private string _connectionStatus = "未連線";

    // ── 系統資訊 ──────────────────────────────────

    public string AppVersion => "v1.0.0";
    public string DotNetVersion => System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
    public string BuildDate => "2026-04-28";

    public SettingsViewModel(
        ModbusMasterService master,
        ModbusSlaveService slave,
        ConfigStore configStore)
    {
        _master = master;
        _slave = slave;
        _configStore = configStore;

        // 從 ConfigStore 載入設定
        Host = configStore.Config.Connection.Host;
        Port = configStore.Config.Connection.Port;
        SelectedReadInterval = configStore.Config.Connection.ReadIntervalMs;

        // 監聽連線狀態
        _master.StatusChanged += status =>
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                ConnectionStatus = status.ToString();
            });
        };

        // 初始化 Slave 狀態
        UpdateSlaveStatus();
    }

    /// <summary>套用新設定並重新連線</summary>
    [RelayCommand]
    private async Task ApplyAndReconnect()
    {
        try
        {
            // 儲存設定
            _configStore.Config.Connection.Host = Host;
            _configStore.Config.Connection.Port = Port;
            _configStore.Config.Connection.ReadIntervalMs = SelectedReadInterval;
            await _configStore.SaveAsync();

            // 重新啟動 Master 使用新設定
            await _master.StopAsync();
            _master.SetReadInterval(SelectedReadInterval);
            await _master.StartAsync(Host, Port);

            MessageBox.Show("設定套用成功，已重新連線", "設定",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"重新連線失敗：{ex.Message}", "設定",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>啟動 Slave 模擬器</summary>
    [RelayCommand]
    private async Task StartSlave()
    {
        try
        {
            await _slave.StartAsync(Port);
            UpdateSlaveStatus();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Slave 啟動失敗：{ex.Message}", "錯誤",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>停止 Slave 模擬器</summary>
    [RelayCommand]
    private async Task StopSlave()
    {
        await _slave.StopAsync();
        UpdateSlaveStatus();
    }

    private void UpdateSlaveStatus()
    {
        IsSlaveRunning = _slave.IsRunning;
        if (IsSlaveRunning)
        {
            SlaveStatus = "Running";
            SlaveStatusColor = "#22C55E";
        }
        else
        {
            SlaveStatus = "Stopped";
            SlaveStatusColor = "#6B7280";
        }
    }
}
