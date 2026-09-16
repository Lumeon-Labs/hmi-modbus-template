using System.IO;
using System.Text.Json;
using HmiTemplate.Core.Models;
using Serilog;

namespace HmiTemplate.Core.Data;

/// <summary>
/// 設定儲存，負責讀寫 JSON 設定檔
/// 預設路徑：應用程式目錄下的 config.json
/// </summary>
public class ConfigStore
{
    private readonly string _configPath;
    private AppConfig _config = new();

    public ConfigStore(string? configPath = null)
    {
        _configPath = configPath ?? Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "config.json");
    }

    /// <summary>目前生效的設定</summary>
    public AppConfig Config => _config;

    /// <summary>從磁碟載入設定，若檔案不存在則套用預設值</summary>
    public async Task LoadAsync()
    {
        if (!File.Exists(_configPath))
        {
            Log.Information("設定檔不存在，使用預設值：{Path}", _configPath);
            _config = CreateDefault();
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_configPath);
            _config = JsonSerializer.Deserialize<AppConfig>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? CreateDefault();
            Log.Information("設定載入成功：{Path}", _configPath);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "設定檔讀取失敗，使用預設值");
            _config = CreateDefault();
        }
    }

    /// <summary>將目前設定存到磁碟</summary>
    public async Task SaveAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_config,
                new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_configPath, json);
            Log.Information("設定已儲存：{Path}", _configPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "設定儲存失敗");
        }
    }

    /// <summary>建立預設設定（含預設警報規則）</summary>
    private static AppConfig CreateDefault() => new()
    {
        Connection = new ConnectionConfig
        {
            Host = "127.0.0.1",
            Port = 502,
            ReadIntervalMs = 500
        },
        AlarmRules =
        [
            new AlarmRule
            {
                Id = "ALM-001",
                Description = "高溫警報",
                Register = "HR0",
                Threshold = 800,      // 80.0°C 觸發
                Hysteresis = 780      // 降到 78.0°C 以下才清除（防抖帶 2°C）
            },
            new AlarmRule
            {
                Id = "ALM-002",
                Description = "高壓警報",
                Register = "HR1",
                Threshold = 1500,     // 150.0 kPa 觸發
                Hysteresis = 1480     // 148.0 kPa 以下才清除
            },
            new AlarmRule
            {
                Id = "ALM-003",
                Description = "高轉速警報",
                Register = "HR2",
                Threshold = 2500,     // 2500 RPM 觸發
                Hysteresis = 2450     // 2450 RPM 以下才清除
            }
        ]
    };
}

/// <summary>應用程式設定根物件</summary>
public class AppConfig
{
    public ConnectionConfig Connection { get; set; } = new();
    public List<AlarmRule> AlarmRules { get; set; } = [];
}

/// <summary>Modbus 連線設定</summary>
public class ConnectionConfig
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 502;
    public int ReadIntervalMs { get; set; } = 500;
}
