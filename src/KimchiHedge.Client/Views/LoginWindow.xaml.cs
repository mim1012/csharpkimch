using System.Windows;

namespace KimchiHedge.Client.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        UsernameTextBox.Focus();
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        var username = UsernameTextBox.Text.Trim();
        var password = PasswordBox.Password;

        // 입력 검증
        if (string.IsNullOrEmpty(username))
        {
            ShowError("아이디를 입력해주세요.");
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            ShowError("비밀번호를 입력해주세요.");
            return;
        }

        // 로딩 상태 표시
        SetLoading(true);
        ErrorMessage.Visibility = Visibility.Collapsed;

        try
        {
            // TODO: 실제 인증 서버 연동
            await Task.Delay(1000); // 임시 딜레이

            // 임시 로그인 처리 (테스트용)
            if (username == "admin" && password == "admin")
            {
                var mainWindow = new MainWindow();
                mainWindow.Show();
                this.Close();
            }
            else
            {
                ShowError("아이디 또는 비밀번호가 올바르지 않습니다.");
            }
        }
        catch (Exception ex)
        {
            ShowError($"로그인 중 오류가 발생했습니다: {ex.Message}");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void ShowError(string message)
    {
        ErrorMessage.Text = message;
        ErrorMessage.Visibility = Visibility.Visible;
    }

    private void SetLoading(bool isLoading)
    {
        LoginButton.IsEnabled = !isLoading;
        UsernameTextBox.IsEnabled = !isLoading;
        PasswordBox.IsEnabled = !isLoading;
        LoadingPanel.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
    }
}
