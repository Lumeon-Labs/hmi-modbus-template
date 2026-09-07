using HmiTemplate.Core.Models;
using System.Windows;

namespace HmiTemplate.UI.Views;

/// <summary>
/// 配方編輯對話框
/// DataContext 綁定到 RecipeModel，直接對物件做雙向編輯
/// </summary>
public partial class RecipeEditDialog : Window
{
    public RecipeEditDialog(RecipeModel recipe)
    {
        InitializeComponent();
        DataContext = recipe;
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
