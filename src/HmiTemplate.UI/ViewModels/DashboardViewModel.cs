using CommunityToolkit.Mvvm.ComponentModel;
using HmiTemplate.Core.Models;
using HmiTemplate.Core.Services;
using System.Windows.Threading;

namespace HmiTemplate.UI.ViewModels;

/// <summary>
/// Dashboard ViewModel：即時顯示溫度、壓力、轉速、流量、LED 狀態
/// </summary>
public partial class DashboardViewModel : ObservableObject
{
    private readonly ModbusMasterService _master;
    private readonly DispatcherTimer _clockTimer;

    // 用於計算趨勢箭頭的前次值
    private double _prevTemperature;
    private double _prevPressure;
    private double _prevSpeed;
    private double _prevFlow;

    // 累計運轉計時
    private DateTime _runStartTime = DateTime.Now;
    private readonly DispatcherTimer _uptimeTimer;

    // ── 製程數值 ──────────────────────────────────

    [ObservableProperty] private double _temperature;
    [ObservableProperty] private double _pressure;
    [ObservableProperty] private double _speed;
    [ObservableProperty] private double _flow;
    [ObservableProperty] private double _temperatureSetpoint;
    [ObservableProperty] private double _speedSetpoint;
    [ObservableProperty] private double _pressureLimit;

    // ── 趨勢箭頭（▲/▼/─）──────────────────────────

    [ObservableProperty] private string _tempTrend = "─";
    [ObservableProperty] private string _pressureTrend = "─";
    [ObservableProperty] private string _speedTrend = "─";
    [ObservableProperty] private string _flowTrend = "─";

    [ObservableProperty] private string _tempTrendColor = "#6B7280";
    [ObservableProperty] private string _pressureTrendColor = "#6B7280";
    [ObservableProperty] private string _speedTrendColor = "#6B7280";
    [ObservableProperty] private string _flowTrendColor = "#6B7280";

    // ── LED 狀態 ──────────────────────────────────

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private bool _hasAlarm;
    [ObservableProperty] private bool _isEmergencyStop;
    [ObservableProperty] private bool _isHeating;

    // ── 系統資訊 ──────────────────────────────────

    [ObservableProperty] private string _systemTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    [ObservableProperty] private string _uptimeDisplay = "000:00:00";

    public DashboardViewModel(ModbusMasterService master)
    {
        _master = master;

        // 訂閱 Modbus 數據更新事件
        _master.DataReceived += OnDataReceived;

        // 系統時鐘每秒更新
        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (_, _) =>
        {
            SystemTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        };
        _clockTimer.Start();

        // 運轉時間每秒更新
        _uptimeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _uptimeTimer.Tick += (_, _) =>
        {
            var elapsed = DateTime.Now - _runStartTime;
            UptimeDisplay = elapsed.ToString(@"ddd\:hh\:mm\:ss");
        };
        _uptimeTimer.Start();
    }

    /// <summary>
    /// Modbus Master 每次收到新數據時更新 UI 屬性
    /// 必須切換到 UI Dispatcher 執行緒
    /// </summary>
    private void OnDataReceived(ProcessData data)
    {
        App.Current.Dispatcher.Invoke(() =>
        {
            // 儲存前次值用於計算趨勢
            _prevTemperature = Temperature;
            _prevPressure = Pressure;
            _prevSpeed = Speed;
            _prevFlow = Flow;

            // 更新數值
            Temperature = data.Temperature;
            Pressure = data.Pressure;
            Speed = data.Speed;
            Flow = data.Flow;
            TemperatureSetpoint = data.TemperatureSetpoint;
            SpeedSetpoint = data.SpeedSetpoint;
            PressureLimit = data.PressureLimit;

            // 更新 LED 狀態
            IsRunning = data.IsRunning;
            HasAlarm = data.HasAlarm;
            IsEmergencyStop = data.IsEmergencyStop;
            IsHeating = data.IsHeating;

            // 計算趨勢箭頭
            (TempTrend, TempTrendColor) = GetTrend(Temperature, _prevTemperature);
            (PressureTrend, PressureTrendColor) = GetTrend(Pressure, _prevPressure);
            (SpeedTrend, SpeedTrendColor) = GetTrend(Speed, _prevSpeed);
            (FlowTrend, FlowTrendColor) = GetTrend(Flow, _prevFlow);
        });
    }

    /// <summary>
    /// 根據當前值與前次值，回傳趨勢符號與顏色
    /// 變化 < 0.1 視為持平
    /// </summary>
    private static (string symbol, string color) GetTrend(double current, double previous)
    {
        var delta = current - previous;
        return delta switch
        {
            > 0.1  => ("▲", "#EF4444"),   // 上升 → 紅
            < -0.1 => ("▼", "#22C55E"),   // 下降 → 綠
            _      => ("─", "#6B7280")    // 持平 → 灰
        };
    }
}
