using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HmiTemplate.Core.Data;
using HmiTemplate.Core.Models;
using HmiTemplate.Core.Services;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;

namespace HmiTemplate.UI.ViewModels;

/// <summary>
/// 趨勢圖 ViewModel：
///   - 4 條 LineSeries：溫度、壓力、轉速（右 Y 軸）、流量
///   - X 軸：DateTimeAxis 顯示最近 5 分鐘
///   - 支援暫停/繼續、清除、匯出 CSV
/// </summary>
public partial class TrendViewModel : ObservableObject
{
    private readonly ModbusMasterService _master;
    private readonly TrendBuffer _buffer;

    // LiveCharts 資料序列（使用 DateTimePoint）
    private readonly ObservableCollection<DateTimePoint> _tempPoints = [];
    private readonly ObservableCollection<DateTimePoint> _pressurePoints = [];
    private readonly ObservableCollection<DateTimePoint> _speedPoints = [];
    private readonly ObservableCollection<DateTimePoint> _flowPoints = [];

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private string _pauseButtonText = "暫停";

    public ISeries[] Series { get; }
    public Axis[] XAxes { get; }
    public Axis[] YAxes { get; }

    public TrendViewModel(ModbusMasterService master, TrendBuffer buffer)
    {
        _master = master;
        _buffer = buffer;

        // 定義 4 條趨勢線
        Series =
        [
            new LineSeries<DateTimePoint>
            {
                Name = "溫度 (°C)",
                Values = _tempPoints,
                Stroke = new SolidColorPaint(SKColors.DodgerBlue, 2),
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0.5
            },
            new LineSeries<DateTimePoint>
            {
                Name = "壓力 (kPa)",
                Values = _pressurePoints,
                Stroke = new SolidColorPaint(SKColors.MediumSeaGreen, 2),
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0.5
            },
            new LineSeries<DateTimePoint>
            {
                Name = "轉速 /10 (RPM)",
                Values = _speedPoints,
                Stroke = new SolidColorPaint(SKColors.Orange, 2),
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0.5,
                ScalesYAt = 1  // 右 Y 軸
            },
            new LineSeries<DateTimePoint>
            {
                Name = "流量 (L/min)",
                Values = _flowPoints,
                Stroke = new SolidColorPaint(SKColors.MediumPurple, 2),
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0.5
            }
        ];

        // X 軸：時間軸
        XAxes =
        [
            new DateTimeAxis(TimeSpan.FromSeconds(1), d => d.ToString("HH:mm:ss"))
            {
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#6B7280")),
                SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#1E293B"))
            }
        ];

        // Y 軸：左側（溫度、壓力、流量）+ 右側（轉速）
        YAxes =
        [
            new Axis
            {
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#6B7280")),
                SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#1E293B"))
            },
            new Axis
            {
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#F59E0B")),
                SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#1E293B")),
                Position = LiveChartsCore.Measure.AxisPosition.End
            }
        ];

        _master.DataReceived += OnDataReceived;
    }

    private void OnDataReceived(ProcessData data)
    {
        if (IsPaused) return;

        // 加入緩衝區
        _buffer.Add(new TrendPoint(
            data.Timestamp,
            data.Temperature,
            data.Pressure,
            data.Speed,
            data.Flow));

        var time = data.Timestamp;

        App.Current.Dispatcher.Invoke(() =>
        {
            // 加入 LiveCharts 資料點
            _tempPoints.Add(new DateTimePoint(time, data.Temperature));
            _pressurePoints.Add(new DateTimePoint(time, data.Pressure));
            _speedPoints.Add(new DateTimePoint(time, data.Speed / 10.0));  // 縮小比例顯示
            _flowPoints.Add(new DateTimePoint(time, data.Flow));

            // 保留最近 5 分鐘（600 個點 @ 2Hz）
            var maxPoints = 600;
            TrimSeries(_tempPoints, maxPoints);
            TrimSeries(_pressurePoints, maxPoints);
            TrimSeries(_speedPoints, maxPoints);
            TrimSeries(_flowPoints, maxPoints);
        });
    }

    private static void TrimSeries(ObservableCollection<DateTimePoint> col, int maxCount)
    {
        while (col.Count > maxCount)
            col.RemoveAt(0);
    }

    [RelayCommand]
    private void TogglePause()
    {
        IsPaused = !IsPaused;
        PauseButtonText = IsPaused ? "繼續" : "暫停";
    }

    [RelayCommand]
    private void ClearData()
    {
        App.Current.Dispatcher.Invoke(() =>
        {
            _tempPoints.Clear();
            _pressurePoints.Clear();
            _speedPoints.Clear();
            _flowPoints.Clear();
            _buffer.Clear();
        });
    }

    [RelayCommand]
    private void ExportCsv()
    {
        var points = _buffer.GetAll();
        if (points.Count == 0)
        {
            MessageBox.Show("無資料可匯出", "匯出 CSV", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            FileName = $"trend_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            Filter = "CSV 檔案 (*.csv)|*.csv",
            DefaultExt = ".csv"
        };

        if (dlg.ShowDialog() != true) return;

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("Time,Temperature(°C),Pressure(kPa),Speed(RPM),Flow(L/min)");
            foreach (var p in points)
            {
                sb.AppendLine($"{p.Time:yyyy-MM-dd HH:mm:ss.fff}," +
                              $"{p.Temperature:F2},{p.Pressure:F2}," +
                              $"{p.Speed:F0},{p.Flow:F2}");
            }
            File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
            MessageBox.Show($"匯出成功：{dlg.FileName}", "匯出 CSV",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"匯出失敗：{ex.Message}", "匯出 CSV",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
