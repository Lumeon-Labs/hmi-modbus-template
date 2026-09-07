namespace HmiTemplate.Core.Models;

/// <summary>
/// 警報規則定義，說明哪個暫存器超過閾值時觸發警報
/// </summary>
public class AlarmRule
{
    /// <summary>規則唯一識別碼（如 ALM-001）</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>警報描述</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>對應的 Modbus 暫存器（如 HR0、HR1）</summary>
    public string Register { get; set; } = string.Empty;

    /// <summary>
    /// 觸發閾值（原始暫存器值，未換算）
    /// 例：溫度 80°C = 800
    /// </summary>
    public int Threshold { get; set; }

    /// <summary>
    /// Hysteresis 清除閾值，必須低於此值才清除警報
    /// 避免在邊界值附近抖動
    /// </summary>
    public int Hysteresis { get; set; }

    /// <summary>目前警報是否處於觸發狀態（用於 hysteresis 邏輯）</summary>
    public bool IsTriggered { get; set; }
}
