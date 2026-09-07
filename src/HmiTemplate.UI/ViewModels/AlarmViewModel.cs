using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HmiTemplate.Core.Data;
using HmiTemplate.Core.Models;
using HmiTemplate.Core.Services;
using System.Collections.ObjectModel;

namespace HmiTemplate.UI.ViewModels;

/// <summary>
/// 警報管理 ViewModel：
///   - Active Alarms Tab：目前活躍警報（含確認功能）
///   - Alarm History Tab：所有歷史記錄（最多 1000 筆）
/// </summary>
public partial class AlarmViewModel : ObservableObject
{
    private readonly AlarmEngine _engine;
    private readonly AlarmHistory _history;

    /// <summary>活躍警報清單（Active + Acknowledged）</summary>
    public ObservableCollection<AlarmRecord> ActiveAlarms { get; } = [];

    /// <summary>警報歷史清單</summary>
    public ObservableCollection<AlarmRecord> HistoryAlarms { get; } = [];

    [ObservableProperty]
    private AlarmRecord? _selectedActiveAlarm;

    [ObservableProperty]
    private int _activeAlarmCount;

    public AlarmViewModel(AlarmEngine engine, AlarmHistory history)
    {
        _engine = engine;
        _history = history;

        // 訂閱警報事件
        _engine.AlarmTriggered += OnAlarmTriggered;
        _engine.AlarmCleared += OnAlarmCleared;
    }

    /// <summary>新警報觸發，加入活躍清單並更新計數</summary>
    private void OnAlarmTriggered(AlarmRecord record)
    {
        App.Current.Dispatcher.Invoke(() =>
        {
            ActiveAlarms.Add(record);
            ActiveAlarmCount = ActiveAlarms.Count;
            RefreshHistory();
        });
    }

    /// <summary>警報清除，從活躍清單移除並更新歷史</summary>
    private void OnAlarmCleared(AlarmRecord record)
    {
        App.Current.Dispatcher.Invoke(() =>
        {
            var existing = ActiveAlarms.FirstOrDefault(a => a.AlarmId == record.AlarmId);
            if (existing != null)
                ActiveAlarms.Remove(existing);
            ActiveAlarmCount = ActiveAlarms.Count;
            RefreshHistory();
        });
    }

    /// <summary>確認選中的警報</summary>
    [RelayCommand]
    private void AcknowledgeAlarm()
    {
        if (SelectedActiveAlarm == null) return;
        _engine.AcknowledgeAlarm(SelectedActiveAlarm.AlarmId);
        // 強制刷新以更新 DataGrid 顯示狀態
        OnPropertyChanged(nameof(ActiveAlarms));
    }

    /// <summary>確認所有活躍警報</summary>
    [RelayCommand]
    private void AcknowledgeAll()
    {
        foreach (var alarm in ActiveAlarms.ToList())
        {
            if (alarm.Status == AlarmStatus.Active)
                _engine.AcknowledgeAlarm(alarm.AlarmId);
        }
        OnPropertyChanged(nameof(ActiveAlarms));
    }

    /// <summary>刷新歷史記錄顯示</summary>
    [RelayCommand]
    private void RefreshHistory()
    {
        App.Current.Dispatcher.Invoke(() =>
        {
            HistoryAlarms.Clear();
            foreach (var record in _history.GetAll())
                HistoryAlarms.Add(record);
        });
    }
}
