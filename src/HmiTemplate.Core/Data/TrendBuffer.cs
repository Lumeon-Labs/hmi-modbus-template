namespace HmiTemplate.Core.Data;

/// <summary>
/// 單筆趨勢資料點，記錄各製程變數的瞬間值
/// </summary>
/// <param name="Time">資料時間戳記</param>
/// <param name="Temperature">溫度（°C）</param>
/// <param name="Pressure">壓力（kPa）</param>
/// <param name="Speed">轉速（RPM）</param>
/// <param name="Flow">流量（L/min）</param>
public record TrendPoint(
    DateTime Time,
    double Temperature,
    double Pressure,
    double Speed,
    double Flow);

/// <summary>
/// 環形趨勢緩衝區，最多保留 600 個樣本（約 5 分鐘 @ 2 Hz）
/// 執行緒安全（lock）
/// </summary>
public class TrendBuffer
{
    // 最大樣本數：5 分鐘 × 60 秒 × 2 samples/s = 600
    private const int MaxCapacity = 600;

    private readonly Queue<TrendPoint> _buffer = new(MaxCapacity);
    private readonly object _lock = new();

    /// <summary>加入新資料點，超過上限時自動移除最舊的</summary>
    public void Add(TrendPoint point)
    {
        lock (_lock)
        {
            if (_buffer.Count >= MaxCapacity)
                _buffer.Dequeue();
            _buffer.Enqueue(point);
        }
    }

    /// <summary>取得最近 N 筆資料</summary>
    public IReadOnlyList<TrendPoint> GetLast(int count)
    {
        lock (_lock)
        {
            var all = _buffer.ToArray();
            var skip = Math.Max(0, all.Length - count);
            return all[skip..];
        }
    }

    /// <summary>取得最近 N 分鐘的資料</summary>
    public IReadOnlyList<TrendPoint> GetLastMinutes(double minutes)
    {
        var cutoff = DateTime.Now.AddMinutes(-minutes);
        lock (_lock)
        {
            return _buffer
                .Where(p => p.Time >= cutoff)
                .ToList()
                .AsReadOnly();
        }
    }

    /// <summary>取得所有資料</summary>
    public IReadOnlyList<TrendPoint> GetAll()
    {
        lock (_lock)
        {
            return _buffer.ToArray();
        }
    }

    /// <summary>清除緩衝區</summary>
    public void Clear()
    {
        lock (_lock)
        {
            _buffer.Clear();
        }
    }

    /// <summary>目前樣本數</summary>
    public int Count
    {
        get { lock (_lock) { return _buffer.Count; } }
    }
}
