using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KimchiHedge.Client.Views;
using KimchiHedge.Core.Auth.Interfaces;

namespace KimchiHedge.Client.ViewModels;

/// <summary>
/// 로그인 ViewModel
/// </summary>
public partial class LoginViewModel : ViewModelBase
{
    private readonly IAuthService _authService;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _hasError;

    /// <summary>
    /// 로그인 성공 시 호출되는 액션 (창 전환용)
    /// </summary>
    public Action? OnLoginSuccess { get; set; }

    public LoginViewModel(IAuthService authService)
    {
        _authService = authService;
    }

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync(object? parameter)
    {
        // PasswordBox에서 비밀번호 가져오기
        var passwordBox = parameter as System.Windows.Controls.PasswordBox;
        var password = passwordBox?.Password ?? string.Empty;

        // 입력 검증
        if (string.IsNullOrWhiteSpace(Email))
        {
            SetError("아이디를 입력해주세요.");
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            SetError("비밀번호를 입력해주세요.");
            return;
        }

        ClearError();
        IsBusy = true;

        try
        {
            var result = await _authService.LoginAsync(Email, password);

            if (result.Success)
            {
                _authService.StartHeartbeat();
                OnLoginSuccess?.Invoke();
            }
            else
            {
                SetError(result.Error?.Message ?? "로그인에 실패했습니다.");
            }
        }
        catch (Exception ex)
        {
            SetError($"로그인 중 오류가 발생했습니다: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanLogin(object? parameter) => !IsBusy;

    private void SetError(string message)
    {
        ErrorMessage = message;
        HasError = true;
    }

    private void ClearError()
    {
        ErrorMessage = null;
        HasError = false;
    }
}
