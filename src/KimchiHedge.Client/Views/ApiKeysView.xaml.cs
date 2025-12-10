using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KimchiHedge.Client.Views;

public partial class ApiKeysView : UserControl
{
    public ApiKeysView()
    {
        InitializeComponent();
    }

    private async void TestUpbitConnection_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(UpbitAccessKey.Text) ||
            string.IsNullOrWhiteSpace(UpbitSecretKey.Password))
        {
            MessageBox.Show("API 키를 입력해주세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SetUpbitStatus("테스트 중...", false);

        try
        {
            // TODO: 실제 연결 테스트
            await Task.Delay(1000);

            SetUpbitStatus("연결됨", true);
            MessageBox.Show("업비트 API 연결 성공!", "연결 테스트", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            SetUpbitStatus("연결 실패", false);
            MessageBox.Show($"연결 실패: {ex.Message}", "연결 테스트", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveUpbitKeys_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(UpbitAccessKey.Text) ||
            string.IsNullOrWhiteSpace(UpbitSecretKey.Password))
        {
            MessageBox.Show("API 키를 입력해주세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // TODO: 실제 암호화 및 저장
        MessageBox.Show("업비트 API 키가 저장되었습니다.", "저장 완료", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void TestBingXConnection_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(BingXApiKey.Text) ||
            string.IsNullOrWhiteSpace(BingXSecretKey.Password))
        {
            MessageBox.Show("API 키를 입력해주세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SetBingXStatus("테스트 중...", false);

        try
        {
            // TODO: 실제 연결 테스트
            await Task.Delay(1000);

            SetBingXStatus("연결됨", true);
            MessageBox.Show("BingX API 연결 성공!", "연결 테스트", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            SetBingXStatus("연결 실패", false);
            MessageBox.Show($"연결 실패: {ex.Message}", "연결 테스트", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveBingXKeys_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(BingXApiKey.Text) ||
            string.IsNullOrWhiteSpace(BingXSecretKey.Password))
        {
            MessageBox.Show("API 키를 입력해주세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // TODO: 실제 암호화 및 저장
        MessageBox.Show("BingX API 키가 저장되었습니다.", "저장 완료", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void RefreshBalance_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // TODO: 실제 잔고 조회
            await Task.Delay(500);

            UpbitBalance.Text = "10,000,000 KRW";
            BingXBalance.Text = "5,000.00 USDT";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"잔고 조회 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SetUpbitStatus(string text, bool isConnected)
    {
        UpbitStatusText.Text = text;
        UpbitStatusBadge.Background = isConnected
            ? FindResource("StatusProfitBgBrush") as Brush
            : FindResource("BackgroundLighterBrush") as Brush;
        UpbitStatusText.Foreground = isConnected
            ? FindResource("StatusProfitLightBrush") as Brush
            : FindResource("TextSecondaryBrush") as Brush;
    }

    private void SetBingXStatus(string text, bool isConnected)
    {
        BingXStatusText.Text = text;
        BingXStatusBadge.Background = isConnected
            ? FindResource("StatusProfitBgBrush") as Brush
            : FindResource("BackgroundLighterBrush") as Brush;
        BingXStatusText.Foreground = isConnected
            ? FindResource("StatusProfitLightBrush") as Brush
            : FindResource("TextSecondaryBrush") as Brush;
    }
}
