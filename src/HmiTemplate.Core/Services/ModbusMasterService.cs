using System.Net.Sockets;
using HmiTemplate.Core.Models;
using NModbus;
using Serilog;

namespace HmiTemplate.Core.Services;

/// <summary>連線狀態列舉</summary>
public enum ConnectionStatus
{
    Disconnected,
    Connecting,
    Connected,
    Error
}

/// <summary>
/// Modbus TCP Master（Client）服務
/// 定期讀取 Slave 暫存器，並透過 DataReceived 事件推送數據
/// 支援自動重連（異常後 5 秒重試，無限次）
/// </summary>
public class ModbusMasterService : IDisposable
{
    private const byte SlaveId = 1;

    private TcpClient? _client;
    private IModbusMaster? _master;
    private CancellationTokenSource? _cts;
    private Task? _pollingTask;
    private bool _disposed;

    private string _host = "127.0.0.1";
    private int _port = 502;
    private int _readIntervalMs = 500;

    /// <summary>當新的製程數據讀取完成時觸發</summary>
    public event Action<ProcessData>? DataReceived;

    /// <summary>連線狀態改變時觸發</summary>
    public event Action<ConnectionStatus>? StatusChanged;

    private ConnectionStatus _status = ConnectionStatus.Disconnected;

    /// <summary>目前連線狀態</summary>
    public ConnectionStatus Status
    {
        get => _status;
        private set
        {
            if (_status == value) return;
            _status = value;
            StatusChanged?.Invoke(value);
            Log.Information("Modbus Master 狀態：{Status}", value);
        }
    }

    /// <summary>
    /// 啟動 Modbus TCP Master，開始定期輪詢
    /// </summary>
    /// <param name="host">Slave IP（預設 127.0.0.1）</param>
    /// <param name="port">Slave Port（預設 502）</param>
    /// <param name="ct">外部取消 Token</param>
    public Task StartAsync(string host = "127.0.0.1", int port = 502,
        CancellationToken ct = default)
    {
        _host = host;
        _port = port;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        _pollingTask = Task.Run(() => PollLoopAsync(_cts.Token), _cts.Token);
        return Task.CompletedTask;
    }

    /// <summary>設定讀取間隔（毫秒）</summary>
    public void SetReadInterval(int intervalMs)
    {
        _readIntervalMs = Math.Max(100, intervalMs);
    }

    /// <summary>停止輪詢並斷開連線</summary>
    public async Task StopAsync()
    {
        if (_cts != null) await _cts.CancelAsync();

        if (_pollingTask != null)
        {
            try { await _pollingTask.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch { /* 超時直接略過 */ }
        }

        DisconnectInternal();
        Status = ConnectionStatus.Disconnected;
    }

    /// <summary>
    /// 主要輪詢迴圈：連線失敗時自動重試（5 秒間隔），無限次
    /// </summary>
    private async Task PollLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ConnectAsync(ct);

                // 連線成功後，持續讀取直到斷線或取消
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        var data = await ReadAllRegistersAsync(ct);
                        DataReceived?.Invoke(data);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        Log.Warning("Modbus 讀取失敗：{Message}", ex.Message);
                        Status = ConnectionStatus.Error;
                        break; // 跳出內層，回到外層重連
                    }

                    await Task.Delay(_readIntervalMs, ct);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Warning("Modbus 連線失敗（5 秒後重試）：{Message}", ex.Message);
                Status = ConnectionStatus.Error;
                DisconnectInternal();
            }

            if (!ct.IsCancellationRequested)
            {
                // 等待 5 秒後重試連線
                try { await Task.Delay(5000, ct); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    /// <summary>建立 TCP 連線</summary>
    private async Task ConnectAsync(CancellationToken ct)
    {
        Status = ConnectionStatus.Connecting;
        DisconnectInternal();

        _client = new TcpClient();
        await _client.ConnectAsync(_host, _port, ct);

        var factory = new ModbusFactory();
        _master = factory.CreateMaster(_client);
        _master.Transport.ReadTimeout = 3000;
        _master.Transport.WriteTimeout = 3000;

        Status = ConnectionStatus.Connected;
        Log.Information("Modbus Master 連線成功：{Host}:{Port}", _host, _port);
    }

    /// <summary>讀取所有 Modbus 暫存器並組裝成 ProcessData</summary>
    private async Task<ProcessData> ReadAllRegistersAsync(CancellationToken ct)
    {
        // 一次讀取 HR0~HR6（7 個暫存器）
        var holdingRegs = await Task.Run(
            () => _master!.ReadHoldingRegisters(SlaveId, 0, 7), ct);

        // 一次讀取 Coil0~Coil3（4 個 Coil）
        var coils = await Task.Run(
            () => _master!.ReadCoils(SlaveId, 0, 4), ct);

        return new ProcessData
        {
            Temperature = holdingRegs[0] / 10.0,        // HR0: °C × 10
            Pressure = holdingRegs[1] / 10.0,           // HR1: kPa × 10
            Speed = holdingRegs[2],                      // HR2: RPM 直接值
            Flow = holdingRegs[3] / 10.0,               // HR3: L/min × 10
            TemperatureSetpoint = holdingRegs[4] / 10.0, // HR4
            SpeedSetpoint = holdingRegs[5],              // HR5
            PressureLimit = holdingRegs[6] / 10.0,      // HR6
            IsRunning = coils[0],
            HasAlarm = coils[1],
            IsEmergencyStop = coils[2],
            IsHeating = coils[3],
            Timestamp = DateTime.Now
        };
    }

    /// <summary>
    /// 非同步寫入單一 Holding Register
    /// 主要用於套用 Recipe 設定值
    /// </summary>
    public async Task WriteHoldingRegisterAsync(ushort address, ushort value)
    {
        if (_master == null || Status != ConnectionStatus.Connected)
        {
            Log.Warning("Modbus Master 未連線，無法寫入 HR{Address}", address);
            return;
        }

        await Task.Run(() => _master.WriteSingleRegister(SlaveId, address, value));
        Log.Debug("寫入 HR{Address} = {Value}", address, value);
    }

    private void DisconnectInternal()
    {
        try { _master?.Dispose(); } catch { }
        try { _client?.Close(); } catch { }
        _master = null;
        _client = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts?.Cancel();
        DisconnectInternal();
        _cts?.Dispose();
    }
}
