using System.Net;
using System.Net.Sockets;
using NModbus;
using Serilog;

namespace HmiTemplate.Core.Services;

/// <summary>
/// Modbus TCP Slave（Server）服務
/// 內建模擬器：作為 Slave 接收 Master 的讀取請求
/// DataSimulatorService 會直接寫入此服務的暫存器
/// </summary>
public class ModbusSlaveService : IDisposable
{
    private TcpListener? _listener;
    private IModbusSlave? _slave;
    private IModbusSlaveNetwork? _slaveNetwork;
    private IModbusFactory? _factory;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private bool _disposed;

    // Slave 識別碼（Unit ID）
    private const byte SlaveId = 1;

    // 暫存器數量
    private const ushort RegisterCount = 10;
    private const ushort CoilCount = 8;

    /// <summary>Slave 是否正在執行</summary>
    public bool IsRunning => _listenTask != null && !_listenTask.IsCompleted;

    /// <summary>
    /// 啟動 Modbus TCP Slave，開始接收連線
    /// </summary>
    /// <param name="port">監聽 Port（預設 502）</param>
    /// <param name="ct">外部取消 Token</param>
    public async Task StartAsync(int port = 502, CancellationToken ct = default)
    {
        if (IsRunning)
        {
            Log.Warning("Modbus Slave 已在執行中，忽略重複啟動");
            return;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _factory = new ModbusFactory();

        // 建立 TCP Listener
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        Log.Information("Modbus Slave 啟動，監聽 Port {Port}", port);

        // 建立 Slave 網路與 Slave 裝置
        _slaveNetwork = _factory.CreateSlaveNetwork(_listener);
        _slave = _factory.CreateSlave(SlaveId);
        _slaveNetwork.AddSlave(_slave);

        // 初始化預設值（設定值：溫度60°C, 轉速1500, 壓力上限150kPa）
        WriteHoldingRegister(4, 600);  // HR4: 溫度設定值 60.0°C
        WriteHoldingRegister(5, 1500); // HR5: 轉速設定值 1500 RPM
        WriteHoldingRegister(6, 1500); // HR6: 壓力上限 150.0 kPa

        // 在背景執行監聽（不阻塞呼叫方）
        _listenTask = Task.Run(async () =>
        {
            try
            {
                await _slaveNetwork.ListenAsync(_cts.Token);
            }
            catch (OperationCanceledException)
            {
                Log.Information("Modbus Slave 已停止");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Modbus Slave 異常");
            }
        }, _cts.Token);

        // 稍等確保 Listener 就緒
        await Task.Delay(100, ct);
    }

    /// <summary>停止 Slave</summary>
    public async Task StopAsync()
    {
        if (_cts != null)
        {
            await _cts.CancelAsync();
        }

        _listener?.Stop();

        if (_listenTask != null)
        {
            try { await _listenTask.WaitAsync(TimeSpan.FromSeconds(3)); }
            catch { /* 超時直接忽略 */ }
        }

        Log.Information("Modbus Slave 已停止");
    }

    /// <summary>
    /// 寫入指定 Holding Register 的值
    /// NModbus 3 使用 WritePoints(startAddress, ushort[]) API
    /// </summary>
    public void WriteHoldingRegister(ushort address, ushort value)
    {
        if (_slave == null) return;
        _slave.DataStore.HoldingRegisters.WritePoints(address, [value]);
    }

    /// <summary>
    /// 寫入指定 Coil 的值
    /// NModbus 3 使用 WritePoints(startAddress, bool[]) API
    /// </summary>
    public void WriteCoil(ushort address, bool value)
    {
        if (_slave == null) return;
        _slave.DataStore.CoilDiscretes.WritePoints(address, [value]);
    }

    /// <summary>
    /// 讀取 Holding Register
    /// NModbus 3 使用 ReadPoints(startAddress, count) API
    /// </summary>
    public ushort ReadHoldingRegister(ushort address)
    {
        if (_slave == null) return 0;
        var values = _slave.DataStore.HoldingRegisters.ReadPoints(address, 1);
        return values.Length > 0 ? values[0] : (ushort)0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts?.Cancel();
        _listener?.Stop();
        _cts?.Dispose();
    }
}
