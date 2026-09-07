namespace HmiTemplate.Core.Models;

/// <summary>警報狀態列舉</summary>
public enum AlarmStatus
{
    /// <summary>觸發中，尚未確認</summary>
    Active,
    /// <summary>已被操作員確認</summary>
    Acknowledged,
    /// <summary>已清除（條件恢復正常）</summary>
    Cleared
}

/// <summary>
/// 單筆警報記錄，包含觸發時間、確認、清除資訊
/// </summary>
public class AlarmRecord
{
    /// <summary>警報唯一識別碼（ALM-001 等）</summary>
    public string AlarmId { get; set; } = string.Empty;

    /// <summary>警報描述訊息</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>觸發時間</summary>
    public DateTime TriggeredAt { get; set; }

    /// <summary>確認時間（null = 尚未確認）</summary>
    public DateTime? AcknowledgedAt { get; set; }

    /// <summary>清除時間（null = 尚未清除）</summary>
    public DateTime? ClearedAt { get; set; }

    /// <summary>警報目前狀態</summary>
    public AlarmStatus Status { get; set; } = AlarmStatus.Active;

    /// <summary>持續時間（從觸發到清除）</summary>
    public TimeSpan? Duration =>
        ClearedAt.HasValue ? ClearedAt.Value - TriggeredAt : null;
}
