using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace HmiTemplate.UI.Converters;

/// <summary>
/// bool → LED 顏色（根據 parameter 決定用哪套配色）
/// parameter: running / alarm / estop / heating
/// </summary>
public class BoolToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isOn = value is bool b && b;
        var param = parameter as string ?? string.Empty;

        return param switch
        {
            "running" => isOn
                ? Color.FromRgb(0x22, 0xC5, 0x5E)   // #22C55E 綠
                : Color.FromRgb(0x37, 0x41, 0x51),   // #374151 灰
            "alarm" or "estop" => isOn
                ? Color.FromRgb(0xEF, 0x44, 0x44)    // #EF4444 紅
                : Color.FromRgb(0x37, 0x41, 0x51),
            "heating" => isOn
                ? Color.FromRgb(0xF5, 0x9E, 0x0B)    // #F59E0B 琥珀
                : Color.FromRgb(0x37, 0x41, 0x51),
            _ => Color.FromRgb(0x37, 0x41, 0x51)
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

/// <summary>
/// bool → SolidColorBrush（用於文字顏色）
/// parameter: alarm（紅色警報文字）
/// </summary>
public class BoolToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isOn = value is bool b && b;
        var param = parameter as string ?? string.Empty;

        if (param == "alarm" && isOn)
            return new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));

        return new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)); // TextBrush
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

/// <summary>
/// null → bool（null = false，有值 = true）
/// 用於 IsEnabled 綁定（需要選中項目才能啟用的按鈕）
/// </summary>
public class NullToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value != null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

/// <summary>
/// bool → bool（反轉）
/// 用於 IsEnabled="{Binding IsSlaveRunning, Converter=InverseBoolConverter}"
/// </summary>
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : (object)true;
}
