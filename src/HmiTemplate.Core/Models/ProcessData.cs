namespace HmiTemplate.Core.Models;

/// <summary>
/// 從 Modbus Slave 讀取的即時製程數據，對應 HR0~HR6 + Coil0~Coil3
/// </summary>
public class ProcessData
{
    /// <summary>溫度（°C），HR0 / 10.0</summary>
    public double Temperature { get; set; }

    /// <summary>壓力（kPa），HR1 / 10.0</summary>
    public double Pressure { get; set; }

    /// <summary>轉速（RPM），HR2 直接值</summary>
    public double Speed { get; set; }

    /// <summary>流量（L/min），HR3 / 10.0</summary>
    public double Flow { get; set; }

    /// <summary>溫度設定值（°C），HR4 / 10.0</summary>
    public double TemperatureSetpoint { get; set; }

    /// <summary>轉速設定值（RPM），HR5</summary>
    public double SpeedSetpoint { get; set; }

    /// <summary>壓力上限（kPa），HR6 / 10.0</summary>
    public double PressureLimit { get; set; }

    /// <summary>運轉中，Coil0</summary>
    public bool IsRunning { get; set; }

    /// <summary>警報觸發，Coil1</summary>
    public bool HasAlarm { get; set; }

    /// <summary>急停，Coil2</summary>
    public bool IsEmergencyStop { get; set; }

    /// <summary>加熱中（溫度 < 設定值），Coil3</summary>
    public bool IsHeating { get; set; }

    /// <summary>資料時間戳記</summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
