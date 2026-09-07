using CommunityToolkit.Mvvm.ComponentModel;

namespace HmiTemplate.Core.Models;

/// <summary>
/// 製程配方，儲存一組操作參數設定
/// 繼承 ObservableObject 以支援 UI 雙向綁定
/// </summary>
public partial class RecipeModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>溫度設定值（°C）</summary>
    [ObservableProperty]
    private double _temperatureSetpoint;

    /// <summary>轉速設定值（RPM）</summary>
    [ObservableProperty]
    private double _speedSetpoint;

    /// <summary>壓力上限（kPa）</summary>
    [ObservableProperty]
    private double _pressureLimit;

    /// <summary>配方說明（選填）</summary>
    [ObservableProperty]
    private string _description = string.Empty;

    /// <summary>建立時間</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>最後修改時間</summary>
    public DateTime ModifiedAt { get; set; } = DateTime.Now;

    public override string ToString() => Name;
}
