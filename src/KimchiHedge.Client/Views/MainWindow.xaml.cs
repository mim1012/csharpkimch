using System.Windows;
using System.Windows.Controls;

namespace KimchiHedge.Client.Views;

public partial class MainWindow : Window
{
    private readonly DashboardView _dashboardView;
    private readonly SettingsView _settingsView;
    private readonly ApiKeysView _apiKeysView;
    private readonly HistoryView _historyView;

    public MainWindow()
    {
        InitializeComponent();

        // 뷰 인스턴스 생성
        _dashboardView = new DashboardView();
        _settingsView = new SettingsView();
        _apiKeysView = new ApiKeysView();
        _historyView = new HistoryView();

        // 초기 뷰 설정
        MainContent.Content = _dashboardView;
    }

    private void NavDashboard_Checked(object sender, RoutedEventArgs e)
    {
        if (MainContent != null)
        {
            MainContent.Content = _dashboardView;
            PageTitle.Text = "대시보드";
        }
    }

    private void NavSettings_Checked(object sender, RoutedEventArgs e)
    {
        if (MainContent != null)
        {
            MainContent.Content = _settingsView;
            PageTitle.Text = "설정";
        }
    }

    private void NavApiKeys_Checked(object sender, RoutedEventArgs e)
    {
        if (MainContent != null)
        {
            MainContent.Content = _apiKeysView;
            PageTitle.Text = "API 키 관리";
        }
    }

    private void NavHistory_Checked(object sender, RoutedEventArgs e)
    {
        if (MainContent != null)
        {
            MainContent.Content = _historyView;
            PageTitle.Text = "거래 내역";
        }
    }

    private void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "로그아웃 하시겠습니까?",
            "로그아웃",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            // 로그인 창으로 전환
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }
    }
}
