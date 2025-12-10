using System.Windows;
using System.Windows.Controls;

namespace KimchiHedge.Client.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
    }

    private void AutoTradingToggle_Checked(object sender, RoutedEventArgs e)
    {
        AutoTradingToggle.Content = "자동매매 중지";
        StatusText.Text = "진입 대기";
        StatusBadge.Background = FindResource("AccentBlueMutedBrush") as System.Windows.Media.Brush;

        AddLog("자동매매 시작됨", LogLevel.Info);
    }

    private void AutoTradingToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        AutoTradingToggle.Content = "자동매매 시작";
        StatusText.Text = "대기 중";
        StatusBadge.Background = FindResource("BackgroundLighterBrush") as System.Windows.Media.Brush;

        AddLog("자동매매 중지됨", LogLevel.Info);
    }

    private void ManualCloseButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "현재 포지션을 수동 청산하시겠습니까?",
            "수동 청산",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            AddLog("수동 청산 요청됨", LogLevel.Warning);
            // TODO: 실제 청산 로직 호출
        }
    }

    private void ClearLogButton_Click(object sender, RoutedEventArgs e)
    {
        LogTextBlock.Inlines.Clear();
    }

    private void AddLog(string message, LogLevel level)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var color = level switch
        {
            LogLevel.Success => FindResource("StatusProfitBrush"),
            LogLevel.Warning => FindResource("StatusCooldownBrush"),
            LogLevel.Error => FindResource("StatusLossBrush"),
            _ => FindResource("TextSecondaryBrush")
        };

        var run = new System.Windows.Documents.Run($"[{timestamp}] {message}")
        {
            Foreground = color as System.Windows.Media.Brush
        };

        LogTextBlock.Inlines.Add(run);
        LogTextBlock.Inlines.Add(new System.Windows.Documents.LineBreak());
    }

    private enum LogLevel
    {
        Info,
        Success,
        Warning,
        Error
    }
}
