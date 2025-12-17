using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KimchiHedge.Client.Services;

namespace KimchiHedge.Client.ViewModels;

/// <summary>
/// 설정 ViewModel
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;

    #region Settings Properties

    [ObservableProperty]
    private string _entryPremium = "3.50";

    [ObservableProperty]
    private string _takeProfitPremium = "2.00";

    [ObservableProperty]
    private string _stopLossPremium = "5.00";

    [ObservableProperty]
    private string _entryRatio = "50";

    [ObservableProperty]
    private int _selectedLeverageIndex;

    [ObservableProperty]
    private string _cooldownMinutes = "5";

    #endregion

    #region Status Properties

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _hasStatusMessage;

    [ObservableProperty]
    private bool _isStatusSuccess;

    #endregion

    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _ = LoadSettingsAsync();
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            await _settingsService.LoadAsync();
            var settings = _settingsService.Settings;

            EntryPremium = settings.EntryPremium.ToString("0.00");
            TakeProfitPremium = settings.TakeProfitPremium.ToString("0.00");
            StopLossPremium = settings.StopLossPremium.ToString("0.00");
            EntryRatio = settings.EntryRatio.ToString("0");
            SelectedLeverageIndex = settings.Leverage - 1; // 1 -> index 0, 2 -> index 1
            CooldownMinutes = settings.CooldownMinutes.ToString();
        }
        catch
        {
            // 기본값 유지
        }
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        if (!ValidateSettings())
        {
            return;
        }

        IsBusy = true;
        try
        {
            var settings = new TradingSettings
            {
                EntryPremium = decimal.Parse(EntryPremium),
                TakeProfitPremium = decimal.Parse(TakeProfitPremium),
                StopLossPremium = decimal.Parse(StopLossPremium),
                EntryRatio = decimal.Parse(EntryRatio),
                Leverage = SelectedLeverageIndex + 1,
                CooldownMinutes = int.Parse(CooldownMinutes)
            };

            await _settingsService.SaveAsync(settings);
            ShowStatus("설정이 저장되었습니다.", true);
        }
        catch (Exception ex)
        {
            ShowStatus($"저장 실패: {ex.Message}", false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ResetSettings()
    {
        EntryPremium = "3.50";
        TakeProfitPremium = "2.00";
        StopLossPremium = "5.00";
        EntryRatio = "50";
        SelectedLeverageIndex = 0;
        CooldownMinutes = "5";
        ShowStatus("기본값으로 초기화되었습니다.", true);
    }

    private bool ValidateSettings()
    {
        // 진입 김프 검증
        if (!decimal.TryParse(EntryPremium, out var entryPremium) || entryPremium < 0 || entryPremium > 20)
        {
            ShowStatus("진입 김프는 0~20 사이의 숫자여야 합니다.", false);
            return false;
        }

        // 익절 김프 검증
        if (!decimal.TryParse(TakeProfitPremium, out var takeProfit) || takeProfit < 0 || takeProfit > 20)
        {
            ShowStatus("익절 김프는 0~20 사이의 숫자여야 합니다.", false);
            return false;
        }

        // 손절 김프 검증
        if (!decimal.TryParse(StopLossPremium, out var stopLoss) || stopLoss < 0 || stopLoss > 20)
        {
            ShowStatus("손절 김프는 0~20 사이의 숫자여야 합니다.", false);
            return false;
        }

        // 진입 비율 검증
        if (!decimal.TryParse(EntryRatio, out var entryRatio) || entryRatio < 1 || entryRatio > 100)
        {
            ShowStatus("진입 비율은 1~100 사이의 숫자여야 합니다.", false);
            return false;
        }

        // 쿨다운 검증
        if (!int.TryParse(CooldownMinutes, out var cooldown) || cooldown < 1 || cooldown > 30)
        {
            ShowStatus("쿨다운은 1~30 사이의 숫자여야 합니다.", false);
            return false;
        }

        return true;
    }

    private void ShowStatus(string message, bool isSuccess)
    {
        StatusMessage = message;
        HasStatusMessage = true;
        IsStatusSuccess = isSuccess;

        // 3초 후 메시지 숨김
        _ = Task.Delay(3000).ContinueWith(_ =>
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                HasStatusMessage = false;
            });
        });
    }
}
