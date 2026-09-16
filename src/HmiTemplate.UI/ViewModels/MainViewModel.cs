using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HmiTemplate.Core.Services;

namespace HmiTemplate.UI.ViewModels;

/// <summary>
/// 主視窗 ViewModel：管理 Sidebar 導覽與連線狀態顯示
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly ModbusMasterService _master;

    [ObservableProperty]
    private ObservableObject? _currentView;

    [ObservableProperty]
    private string _connectionStatusText = "連線中...";

    [ObservableProperty]
    private string _connectionStatusColor = "#F59E0B";

    [ObservableProperty]
    private string _activeNavItem = "Dashboard";

    public DashboardViewModel DashboardVm { get; }
    public TrendViewModel TrendVm { get; }
    public RecipeViewModel RecipeVm { get; }
    public AlarmViewModel AlarmVm { get; }
    public SettingsViewModel SettingsVm { get; }

    public MainViewModel(
        ModbusMasterService master,
        DashboardViewModel dashboardVm,
        TrendViewModel trendVm,
        RecipeViewModel recipeVm,
        AlarmViewModel alarmVm,
        SettingsViewModel settingsVm)
    {
        _master = master;
        DashboardVm = dashboardVm;
        TrendVm = trendVm;
        RecipeVm = recipeVm;
        AlarmVm = alarmVm;
        SettingsVm = settingsVm;

        // 預設顯示 Dashboard
        CurrentView = DashboardVm;

        // 監聽連線狀態變化。Master 在 App 啟動時就已連線（早於本 VM 建立），
        // 所以訂閱後要先同步一次目前狀態，否則 header 會一直停在「連線中...」
        _master.StatusChanged += OnConnectionStatusChanged;
        ApplyStatus(_master.Status);
    }

    private void OnConnectionStatusChanged(ConnectionStatus status)
    {
        App.Current.Dispatcher.Invoke(() => ApplyStatus(status));
    }

    /// <summary>把連線狀態映射成 header 的文字與顏色</summary>
    private void ApplyStatus(ConnectionStatus status)
    {
        (ConnectionStatusText, ConnectionStatusColor) = status switch
        {
            ConnectionStatus.Connected    => ("已連線", "#22C55E"),
            ConnectionStatus.Connecting   => ("連線中...", "#F59E0B"),
            ConnectionStatus.Disconnected => ("未連線", "#6B7280"),
            ConnectionStatus.Error        => ("連線錯誤", "#EF4444"),
            _ => ("未知", "#6B7280")
        };
    }

    [RelayCommand]
    private void NavigateToDashboard()
    {
        CurrentView = DashboardVm;
        ActiveNavItem = "Dashboard";
    }

    [RelayCommand]
    private void NavigateToTrend()
    {
        CurrentView = TrendVm;
        ActiveNavItem = "Trend";
    }

    [RelayCommand]
    private void NavigateToRecipe()
    {
        CurrentView = RecipeVm;
        ActiveNavItem = "Recipe";
    }

    [RelayCommand]
    private void NavigateToAlarm()
    {
        CurrentView = AlarmVm;
        ActiveNavItem = "Alarm";
    }

    [RelayCommand]
    private void NavigateToSettings()
    {
        CurrentView = SettingsVm;
        ActiveNavItem = "Settings";
    }
}
