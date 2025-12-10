using System.Windows;
using System.Windows.Controls;

namespace KimchiHedge.Client.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        // TODO: 실제 설정 로드
        EntryKimchiInput.Text = "3.50";
        TakeProfitInput.Text = "2.00";
        StopLossInput.Text = "5.00";
        EntryRatioInput.Text = "50";
        LeverageCombo.SelectedIndex = 0;
        CooldownInput.Text = "5";
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        // 입력값 검증
        if (!ValidateInputs(out string? error))
        {
            MessageBox.Show(error, "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // TODO: 실제 설정 저장
        MessageBox.Show("설정이 저장되었습니다.", "저장 완료", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "설정을 기본값으로 초기화하시겠습니까?",
            "초기화",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            EntryKimchiInput.Text = "3.00";
            TakeProfitInput.Text = "1.50";
            StopLossInput.Text = "5.00";
            EntryRatioInput.Text = "50";
            LeverageCombo.SelectedIndex = 0;
            CooldownInput.Text = "5";
        }
    }

    private bool ValidateInputs(out string? error)
    {
        error = null;

        if (!decimal.TryParse(EntryKimchiInput.Text, out var entryKimchi) || entryKimchi <= 0)
        {
            error = "진입 김프값은 0보다 커야 합니다.";
            return false;
        }

        if (!decimal.TryParse(TakeProfitInput.Text, out var takeProfit))
        {
            error = "익절 김프값을 올바르게 입력해주세요.";
            return false;
        }

        if (takeProfit >= entryKimchi)
        {
            error = "익절 김프값은 진입값보다 작아야 합니다.";
            return false;
        }

        if (!decimal.TryParse(StopLossInput.Text, out var stopLoss) || stopLoss <= entryKimchi)
        {
            error = "손절 김프값은 진입값보다 커야 합니다.";
            return false;
        }

        if (!decimal.TryParse(EntryRatioInput.Text, out var entryRatio) ||
            entryRatio <= 0 || entryRatio > 100)
        {
            error = "진입 비율은 0~100% 사이여야 합니다.";
            return false;
        }

        if (!int.TryParse(CooldownInput.Text, out var cooldown) ||
            cooldown < 1 || cooldown > 30)
        {
            error = "쿨다운은 1~30분 사이여야 합니다.";
            return false;
        }

        return true;
    }
}
