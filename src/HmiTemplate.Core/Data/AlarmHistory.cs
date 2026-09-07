using HmiTemplate.Core.Models;

namespace HmiTemplate.Core.Data;

/// <summary>
/// 警報歷史記錄（In-Memory），最多保留 1000 筆
/// 執行緒安全（lock）
/// </summary>
public class AlarmHistory
{
    private const int MaxRecords = 1000;

    private readonly List<AlarmRecord> _records = new(MaxRecords);
    private readonly object _lock = new();

    /// <summary>新增一筆警報記錄，超過上限時移除最舊的</summary>
    public void Add(AlarmRecord record)
    {
        lock (_lock)
        {
            if (_records.Count >= MaxRecords)
                _records.RemoveAt(0);
            _records.Add(record);
        }
    }

    /// <summary>取得所有歷史記錄（由新到舊排列）</summary>
    public IReadOnlyList<AlarmRecord> GetAll()
    {
        lock (_lock)
        {
            // 回傳副本，避免外部直接操作內部 List
            return _records
                .OrderByDescending(r => r.TriggeredAt)
                .ToList()
                .AsReadOnly();
        }
    }

    /// <summary>目前記錄筆數</summary>
    public int Count
    {
        get { lock (_lock) { return _records.Count; } }
    }

    /// <summary>清除所有歷史記錄</summary>
    public void Clear()
    {
        lock (_lock)
        {
            _records.Clear();
        }
    }
}
