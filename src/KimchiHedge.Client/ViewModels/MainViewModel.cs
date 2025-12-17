using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KimchiHedge.Core.Auth.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace KimchiHedge.Client.ViewModels;

/// <summary>
/// 메인 윈도우 ViewModel
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly IAuthService _authService;
    private readonly IServiceProvider _serviceProvider;

    // 각 ViewModel 인스턴스 (Lazy 로딩)
    private DashboardViewModel? _dashboardViewModel;
    private SettingsViewModel? _settingsViewModel;
    private ApiKeysViewModel? _apiKeysViewModel;
    private HistoryViewModel? _historyViewModel;

    [ObservableProperty]
    private ViewModelBase? _currentViewModel;

    [ObservableProperty]
    private string _pageTitle = "대시보드";

    [ObservableProperty]
    private string _userName = "사용자";

    [ObservableProperty]
    private string _connectionStatus = "연결됨";

    [ObservableProperty]
    private bool _isConnected = true;

    [ObservableProperty]
    private string _selectedNav = "Dashboard";

    /// <summary>
    /// 로그아웃 후 호출되는 액션 (창 전환용)
    /// </summary>
    public Action? OnLogout { get; set; }

    public MainViewModel(IAuthService authService, IServiceProvider serviceProvider)
    {
        _authService = authService;
        _serviceProvider = serviceProvider;

        // 초기 ViewModel 설정
        _dashboardViewModel = _serviceProvider.GetRequiredService<DashboardViewModel>();
        CurrentViewModel = _dashboardViewModel;

        // 사용자 정보 설정
        if (_authService.CurrentUser != null)
        {
            UserName = _authService.CurrentUser.Email;
        }
    }

    /// <summary>
    /// 대시보드로 이동
    /// </summary>
    [RelayCommand]
    private void NavigateToDashboard()
    {
        _dashboardViewModel ??= _serviceProvider.GetRequiredService<DashboardViewModel>();
        CurrentViewModel = _dashboardViewModel;
        PageTitle = "대시보드";
        SelectedNav = "Dashboard";
    }

    /// <summary>
    /// 설정으로 이동
    /// </summary>
    [RelayCommand]
    private void NavigateToSettings()
    {
        _settingsViewModel ??= _serviceProvider.GetRequiredService<SettingsViewModel>();
        CurrentViewModel = _settingsViewModel;
        PageTitle = "설정";
        SelectedNav = "Settings";
    }

    /// <summary>
    /// API 키 관리로 이동
    /// </summary>
    [RelayCommand]
    private void NavigateToApiKeys()
    {
        _apiKeysViewModel ??= _serviceProvider.GetRequiredService<ApiKeysViewModel>();
        CurrentViewModel = _apiKeysViewModel;
        PageTitle = "API 키 관리";
        SelectedNav = "ApiKeys";
    }

    /// <summary>
    /// 거래 내역으로 이동
    /// </summary>
    [RelayCommand]
    private void NavigateToHistory()
    {
        _historyViewModel ??= _serviceProvider.GetRequiredService<HistoryViewModel>();
        CurrentViewModel = _historyViewModel;
        PageTitle = "거래 내역";
        SelectedNav = "History";
    }

    /// <summary>
    /// 네비게이션 변경 (RadioButton 바인딩용)
    /// </summary>
    partial void OnSelectedNavChanged(string value)
    {
        switch (value)
        {
            case "Dashboard":
                _dashboardViewModel ??= _serviceProvider.GetRequiredService<DashboardViewModel>();
                CurrentViewModel = _dashboardViewModel;
                PageTitle = "대시보드";
                break;
            case "Settings":
                _settingsViewModel ??= _serviceProvider.GetRequiredService<SettingsViewModel>();
                CurrentViewModel = _settingsViewModel;
                PageTitle = "설정";
                break;
            case "ApiKeys":
                _apiKeysViewModel ??= _serviceProvider.GetRequiredService<ApiKeysViewModel>();
                CurrentViewModel = _apiKeysViewModel;
                PageTitle = "API 키 관리";
                break;
            case "History":
                _historyViewModel ??= _serviceProvider.GetRequiredService<HistoryViewModel>();
                CurrentViewModel = _historyViewModel;
                PageTitle = "거래 내역";
                break;
        }
    }

    /// <summary>
    /// 로그아웃
    /// </summary>
    [RelayCommand]
    private async Task LogoutAsync()
    {
        var result = MessageBox.Show(
            "로그아웃 하시겠습니까?",
            "로그아웃",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                await _authService.LogoutAsync();
            }
            catch
            {
                // 로그아웃 실패해도 창 전환
            }

            OnLogout?.Invoke();
        }
    }
}
