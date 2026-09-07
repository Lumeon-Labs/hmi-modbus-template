using Serilog;

namespace HmiTemplate.Core.Services;

/// <summary>
/// 數據模擬服務
/// 每 500ms 更新 Slave 暫存器，模擬真實設備的製程變化
///
/// 模擬規則：
///   溫度：25.0°C 基準 + sin(t/30 * 2π) × 5 + Random(-0.3, 0.3)
///   壓力：101.3 kPa 基準 + Random(-2, 2)
///   轉速：1500 RPM 基準 + Random(-50, 50)
///   流量：10.0 L/min 基準 + Random(-0.5, 0.5)
///   加熱中（Coil3）：溫度 < 設定值 時 true
/// </summary>
public class DataSimulatorService
{
    private readonly ModbusSlaveService _slave;
    private readonly Random _random = new();
    private CancellationTokenSource? _cts;
    private Task? _simulatorTask;
    private bool _isRunning;

    // 模擬開始時間，用於計算 sin 波
    private DateTime _startTime = DateTime.Now;

    /// <summary>模擬器是否執行中</summary>
    public bool IsRunning => _isRunning;

    public DataSimulatorService(ModbusSlaveService slave)
    {
        _slave = slave;
    }

    /// <summary>啟動數據模擬迴圈</summary>
    public Task StartAsync(CancellationToken ct = default)
    {
        if (_isRunning)
        {
            Log.Warning("DataSimulator 已在執行中");
            return Task.CompletedTask;
        }

        _startTime = DateTime.Now;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _simulatorTask = Task.Run(() => SimulateLoopAsync(_cts.Token), _cts.Token);
        _isRunning = true;
        Log.Information("DataSimulator 啟動");
        return Task.CompletedTask;
    }

    /// <summary>停止數據模擬</summary>
    public async Task StopAsync()
    {
        if (_cts != null) await _cts.CancelAsync();

        if (_simulatorTask != null)
        {
            try { await _simulatorTask.WaitAsync(TimeSpan.FromSeconds(3)); }
            catch { }
        }

        _isRunning = false;
        Log.Information("DataSimulator 停止");
    }

    /// <summary>主要模擬迴圈（每 500ms 執行一次）</summary>
    private async Task SimulateLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                UpdateRegisters();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "DataSimulator 更新暫存器時發生錯誤");
            }

            try { await Task.Delay(500, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>
    /// 計算並更新所有 Modbus 暫存器值
    /// </summary>
    private void UpdateRegisters()
    {
        // 計算已執行秒數（用於 sin 波）
        var elapsedSeconds = (DateTime.Now - _startTime).TotalSeconds;

        // === 溫度（HR0）===
        // 基準 25.0°C + sin 波（週期 30s，振幅 5°C）+ 微小隨機雜訊
        var tempValue = 25.0
            + Math.Sin(elapsedSeconds / 30.0 * 2 * Math.PI) * 5.0
            + (_random.NextDouble() - 0.5) * 0.6;  // ±0.3
        _slave.WriteHoldingRegister(0, (ushort)Math.Max(0, tempValue * 10));

        // === 壓力（HR1）===
        // 基準 101.3 kPa + Random(-2, 2)
        var pressureValue = 101.3 + (_random.NextDouble() - 0.5) * 4.0;
        _slave.WriteHoldingRegister(1, (ushort)Math.Max(0, pressureValue * 10));

        // === 轉速（HR2）===
        // 基準 1500 RPM + Random(-50, 50)
        var speedValue = 1500 + (_random.NextDouble() - 0.5) * 100;
        _slave.WriteHoldingRegister(2, (ushort)Math.Max(0, speedValue));

        // === 流量（HR3）===
        // 基準 10.0 L/min + Random(-0.5, 0.5)
        var flowValue = 10.0 + (_random.NextDouble() - 0.5) * 1.0;
        _slave.WriteHoldingRegister(3, (ushort)Math.Max(0, flowValue * 10));

        // === 讀取目前設定值（用於 Coil 邏輯）===
        var tempSetpointRaw = _slave.ReadHoldingRegister(4);

        // === Coil0：運轉中，一直為 true ===
        _slave.WriteCoil(0, true);

        // === Coil1：警報，由 AlarmEngine 控制，Simulator 不干預 ===

        // === Coil2：急停，預設 false ===
        _slave.WriteCoil(2, false);

        // === Coil3：加熱中 = 溫度 < 設定值 ===
        var isHeating = (ushort)(tempValue * 10) < tempSetpointRaw;
        _slave.WriteCoil(3, isHeating);
    }
}
