using HmiTemplate.Core.Data;
using HmiTemplate.Core.Models;
using Serilog;

namespace HmiTemplate.Core.Services;

/// <summary>
/// 警報引擎：根據製程數據即時評估警報規則
///
/// 特性：
///   - 支援 Hysteresis 防止邊界值抖動觸發/清除
///   - 警報觸發後保持 Active 直到條件低於 Hysteresis 值
///   - AcknowledgeAlarm：把 Active 改為 Acknowledged
///   - 警報清除時自動記錄到 AlarmHistory
/// </summary>
public class AlarmEngine
{
    private readonly List<AlarmRule> _rules;
    private readonly AlarmHistory _history;
    private readonly ModbusSlaveService? _slaveService;

    // 目前活躍的警報（Active + Acknowledged）
    private readonly List<AlarmRecord> _activeAlarms = [];
    private readonly object _lock = new();

    // Hysteresis 設錯的規則只警告一次，避免每 500ms 洗版
    private readonly HashSet<string> _hysteresisWarned = [];

    /// <summary>新警報觸發時通知</summary>
    public event Action<AlarmRecord>? AlarmTriggered;

    /// <summary>警報清除時通知</summary>
    public event Action<AlarmRecord>? AlarmCleared;

    /// <summary>目前活躍警報清單（唯讀）</summary>
    public IReadOnlyList<AlarmRecord> ActiveAlarms
    {
        get { lock (_lock) { return _activeAlarms.AsReadOnly(); } }
    }

    public AlarmEngine(
        List<AlarmRule> rules,
        AlarmHistory history,
        ModbusSlaveService? slaveService = null)
    {
        _rules = rules;
        _history = history;
        _slaveService = slaveService;
    }

    /// <summary>
    /// 用新的製程數據評估所有警報規則（每次 DataReceived 時呼叫）
    /// </summary>
    public void Update(ProcessData data)
    {
        foreach (var rule in _rules)
        {
            // 取得該規則對應的原始暫存器值
            var rawValue = GetRawValue(rule.Register, data);

            lock (_lock)
            {
                if (!rule.IsTriggered)
                {
                    // 尚未觸發 → 判斷是否超過閾值
                    if (rawValue >= rule.Threshold)
                    {
                        TriggerAlarm(rule, data.Timestamp);
                    }
                }
                else
                {
                    // 已觸發 → 要降到清除閾值（Hysteresis）以下才清除。
                    // Threshold 與 Hysteresis 之間是不動作的防抖帶
                    if (rawValue < GetClearLevel(rule))
                    {
                        ClearAlarm(rule, data.Timestamp);
                    }
                }
            }
        }

        // 同步更新 Slave Coil1（警報信號）
        var hasActive = false;
        lock (_lock) { hasActive = _activeAlarms.Any(a => a.Status == AlarmStatus.Active); }
        _slaveService?.WriteCoil(1, hasActive);
    }

    /// <summary>將指定警報標記為「已確認」</summary>
    public void AcknowledgeAlarm(string alarmId)
    {
        lock (_lock)
        {
            var alarm = _activeAlarms.FirstOrDefault(a => a.AlarmId == alarmId);
            if (alarm == null)
            {
                Log.Warning("找不到警報 {AlarmId}", alarmId);
                return;
            }

            alarm.Status = AlarmStatus.Acknowledged;
            alarm.AcknowledgedAt = DateTime.Now;
            Log.Information("警報已確認：{AlarmId}", alarmId);
        }
    }

    // ── 私有輔助方法 ──────────────────────────────────────────────

    /// <summary>觸發警報：建立 AlarmRecord 並加入活躍清單</summary>
    private void TriggerAlarm(AlarmRule rule, DateTime timestamp)
    {
        // 避免重複觸發同一規則
        if (_activeAlarms.Any(a => a.AlarmId == rule.Id))
            return;

        rule.IsTriggered = true;

        var record = new AlarmRecord
        {
            AlarmId = rule.Id,
            Message = rule.Description,
            TriggeredAt = timestamp,
            Status = AlarmStatus.Active
        };

        _activeAlarms.Add(record);
        _history.Add(record); // 也記入歷史
        Log.Warning("警報觸發：{AlarmId} - {Message}", rule.Id, rule.Description);

        // 事件通知（在 lock 外部觸發，避免死鎖）
        Task.Run(() => AlarmTriggered?.Invoke(record));
    }

    /// <summary>清除警報：從活躍清單移除並更新歷史</summary>
    private void ClearAlarm(AlarmRule rule, DateTime timestamp)
    {
        var record = _activeAlarms.FirstOrDefault(a => a.AlarmId == rule.Id);
        if (record == null) return;

        rule.IsTriggered = false;
        record.Status = AlarmStatus.Cleared;
        record.ClearedAt = timestamp;
        _activeAlarms.Remove(record);

        Log.Information("警報清除：{AlarmId}，持續 {Duration}",
            rule.Id, record.Duration?.ToString(@"hh\:mm\:ss") ?? "N/A");

        Task.Run(() => AlarmCleared?.Invoke(record));
    }

    /// <summary>
    /// 取得清除閾值。Hysteresis 必須落在 (0, Threshold) 才構成防抖帶；
    /// 設錯（≥ Threshold 或 ≤ 0）就退回用 Threshold 清除，並只警告一次
    /// </summary>
    private int GetClearLevel(AlarmRule rule)
    {
        if (rule.Hysteresis > 0 && rule.Hysteresis < rule.Threshold)
            return rule.Hysteresis;

        if (_hysteresisWarned.Add(rule.Id))
        {
            Log.Warning("規則 {Id} 的 Hysteresis={Hyst} 未低於 Threshold={Thr}，改用 Threshold 清除",
                rule.Id, rule.Hysteresis, rule.Threshold);
        }
        return rule.Threshold;
    }

    /// <summary>
    /// 根據規則的 Register 欄位取得對應的原始暫存器值
    /// </summary>
    private static int GetRawValue(string register, ProcessData data) =>
        register switch
        {
            "HR0" => (int)(data.Temperature * 10),
            "HR1" => (int)(data.Pressure * 10),
            "HR2" => (int)data.Speed,
            "HR3" => (int)(data.Flow * 10),
            _ => 0
        };
}
