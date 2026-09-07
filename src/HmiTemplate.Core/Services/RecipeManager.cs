using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using HmiTemplate.Core.Models;
using Serilog;

namespace HmiTemplate.Core.Services;

/// <summary>
/// 配方管理器：管理操作員可套用的製程配方
/// 支援新增、刪除、編輯、套用、JSON 序列化
/// </summary>
public class RecipeManager
{
    private readonly ModbusMasterService _master;
    private readonly string _recipePath;

    /// <summary>配方清單（可觀察，支援 UI 綁定）</summary>
    public ObservableCollection<RecipeModel> Recipes { get; } = [];

    public RecipeManager(ModbusMasterService master, string? recipePath = null)
    {
        _master = master;
        _recipePath = recipePath ?? Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "recipes.json");
    }

    /// <summary>
    /// 套用配方：把設定值寫入 Modbus HR4（溫度）、HR5（轉速）、HR6（壓力上限）
    /// </summary>
    public async Task ApplyRecipeAsync(RecipeModel recipe)
    {
        // HR4：溫度設定值 × 10
        await _master.WriteHoldingRegisterAsync(4, (ushort)(recipe.TemperatureSetpoint * 10));

        // HR5：轉速設定值（直接值）
        await _master.WriteHoldingRegisterAsync(5, (ushort)recipe.SpeedSetpoint);

        // HR6：壓力上限 × 10
        await _master.WriteHoldingRegisterAsync(6, (ushort)(recipe.PressureLimit * 10));

        Log.Information("配方套用：{Name}（Temp={Temp}°C, Speed={Speed}RPM, Pressure={Prs}kPa）",
            recipe.Name,
            recipe.TemperatureSetpoint,
            recipe.SpeedSetpoint,
            recipe.PressureLimit);
    }

    /// <summary>新增配方</summary>
    public void Add(RecipeModel recipe)
    {
        Recipes.Add(recipe);
        Log.Debug("新增配方：{Name}", recipe.Name);
    }

    /// <summary>刪除配方</summary>
    public void Delete(RecipeModel recipe)
    {
        if (Recipes.Remove(recipe))
            Log.Debug("刪除配方：{Name}", recipe.Name);
    }

    /// <summary>將配方清單序列化存入 JSON 檔案</summary>
    public async Task SaveAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(Recipes.ToList(),
                new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_recipePath, json);
            Log.Information("配方已儲存：{Path}", _recipePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "配方儲存失敗");
        }
    }

    /// <summary>從 JSON 檔案載入配方，若不存在則建立預設配方</summary>
    public async Task LoadAsync()
    {
        Recipes.Clear();

        if (File.Exists(_recipePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_recipePath);
                var list = JsonSerializer.Deserialize<List<RecipeModel>>(json);
                if (list != null)
                {
                    foreach (var r in list) Recipes.Add(r);
                    Log.Information("配方載入成功，共 {Count} 筆", Recipes.Count);
                    return;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "配方檔案讀取失敗，改用預設配方");
            }
        }

        // 建立預設配方
        LoadDefaults();
    }

    /// <summary>
    /// 載入 4 個預設配方
    /// </summary>
    private void LoadDefaults()
    {
        Recipes.Add(new RecipeModel
        {
            Name = "Default",
            TemperatureSetpoint = 60,
            SpeedSetpoint = 1500,
            PressureLimit = 150,
            Description = "標準操作配方"
        });

        Recipes.Add(new RecipeModel
        {
            Name = "HighSpeed",
            TemperatureSetpoint = 50,
            SpeedSetpoint = 2000,
            PressureLimit = 120,
            Description = "高速低溫模式"
        });

        Recipes.Add(new RecipeModel
        {
            Name = "Eco",
            TemperatureSetpoint = 40,
            SpeedSetpoint = 1000,
            PressureLimit = 100,
            Description = "節能省電模式"
        });

        Recipes.Add(new RecipeModel
        {
            Name = "Custom",
            TemperatureSetpoint = 65,
            SpeedSetpoint = 1200,
            PressureLimit = 160,
            Description = "自訂配方"
        });

        Log.Information("已載入 {Count} 個預設配方", Recipes.Count);
    }
}
