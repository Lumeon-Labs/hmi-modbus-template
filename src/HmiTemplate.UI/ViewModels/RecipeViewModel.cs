using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HmiTemplate.Core.Models;
using HmiTemplate.Core.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace HmiTemplate.UI.ViewModels;

/// <summary>
/// 配方管理 ViewModel：
///   - 顯示配方 DataGrid
///   - 支援新增、編輯（雙擊 → 對話框）、刪除、套用
///   - Import/Export JSON
/// </summary>
public partial class RecipeViewModel : ObservableObject
{
    private readonly RecipeManager _manager;

    public ObservableCollection<RecipeModel> Recipes => _manager.Recipes;

    [ObservableProperty]
    private RecipeModel? _selectedRecipe;

    [ObservableProperty]
    private bool _isApplying;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public RecipeViewModel(RecipeManager manager)
    {
        _manager = manager;
    }

    [RelayCommand]
    private void AddRecipe()
    {
        var newRecipe = new RecipeModel
        {
            Name = $"New Recipe {Recipes.Count + 1}",
            TemperatureSetpoint = 60,
            SpeedSetpoint = 1500,
            PressureLimit = 150,
            Description = "新建配方"
        };

        // 打開編輯對話框
        var edited = ShowEditDialog(newRecipe);
        if (edited != null)
        {
            _manager.Add(edited);
            SelectedRecipe = edited;
            _ = _manager.SaveAsync();
        }
    }

    [RelayCommand]
    private void EditRecipe()
    {
        if (SelectedRecipe == null) return;

        // 複製一份避免直接修改原始物件（Cancel 時不影響原始）
        var copy = new RecipeModel
        {
            Name = SelectedRecipe.Name,
            TemperatureSetpoint = SelectedRecipe.TemperatureSetpoint,
            SpeedSetpoint = SelectedRecipe.SpeedSetpoint,
            PressureLimit = SelectedRecipe.PressureLimit,
            Description = SelectedRecipe.Description
        };

        var edited = ShowEditDialog(copy);
        if (edited != null)
        {
            SelectedRecipe.Name = edited.Name;
            SelectedRecipe.TemperatureSetpoint = edited.TemperatureSetpoint;
            SelectedRecipe.SpeedSetpoint = edited.SpeedSetpoint;
            SelectedRecipe.PressureLimit = edited.PressureLimit;
            SelectedRecipe.Description = edited.Description;
            SelectedRecipe.ModifiedAt = DateTime.Now;
            _ = _manager.SaveAsync();
        }
    }

    [RelayCommand]
    private void DeleteRecipe()
    {
        if (SelectedRecipe == null) return;

        var result = MessageBox.Show(
            $"確定刪除配方「{SelectedRecipe.Name}」？",
            "刪除確認",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            _manager.Delete(SelectedRecipe);
            SelectedRecipe = null;
            _ = _manager.SaveAsync();
        }
    }

    [RelayCommand]
    private async Task ApplyRecipe()
    {
        if (SelectedRecipe == null)
        {
            StatusMessage = "請先選擇一個配方";
            return;
        }

        IsApplying = true;
        StatusMessage = $"套用中：{SelectedRecipe.Name}...";

        try
        {
            await _manager.ApplyRecipeAsync(SelectedRecipe);
            StatusMessage = $"配方已套用：{SelectedRecipe.Name} ✓";
        }
        catch (Exception ex)
        {
            StatusMessage = $"套用失敗：{ex.Message}";
        }
        finally
        {
            IsApplying = false;
        }
    }

    [RelayCommand]
    private void ImportRecipe()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "JSON 檔案 (*.json)|*.json",
            Title = "匯入配方"
        };

        if (dlg.ShowDialog() != true) return;

        try
        {
            var json = File.ReadAllText(dlg.FileName);
            var list = JsonSerializer.Deserialize<List<RecipeModel>>(json);
            if (list == null) return;

            foreach (var r in list)
            {
                _manager.Add(r);
            }

            _ = _manager.SaveAsync();
            StatusMessage = $"匯入成功，新增 {list.Count} 個配方";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"匯入失敗：{ex.Message}", "匯入配方",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void ExportRecipe()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            FileName = $"recipes_{DateTime.Now:yyyyMMdd}.json",
            Filter = "JSON 檔案 (*.json)|*.json",
            Title = "匯出配方"
        };

        if (dlg.ShowDialog() != true) return;

        try
        {
            var json = JsonSerializer.Serialize(Recipes.ToList(),
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(dlg.FileName, json);
            StatusMessage = $"匯出成功：{dlg.FileName}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"匯出失敗：{ex.Message}", "匯出配方",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 顯示配方編輯對話框，回傳 null 表示取消
    /// </summary>
    private static RecipeModel? ShowEditDialog(RecipeModel recipe)
    {
        var dialog = new Views.RecipeEditDialog(recipe);
        return dialog.ShowDialog() == true ? recipe : null;
    }
}
