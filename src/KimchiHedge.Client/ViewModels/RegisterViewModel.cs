using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KimchiHedge.Client.Services.Auth;
using Microsoft.Extensions.Logging;

namespace KimchiHedge.Client.ViewModels;

/// <summary>
/// 회원가입 뷰모델
/// </summary>
public partial class RegisterViewModel : ViewModelBase
{
    private readonly AuthService _authService;
    private readonly ILogger<RegisterViewModel> _logger;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _passwordConfirm = string.Empty;

    [ObservableProperty]
    private string? _referralUid;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _successMessage;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isRegistrationComplete;

    /// <summary>
    /// 로그인 창 열기 요청
    /// </summary>
    public event EventHandler? LoginRequested;

    /// <summary>
    /// 창 닫기 요청
    /// </summary>
    public event EventHandler? CloseRequested;

    public RegisterViewModel(
        AuthService authService,
        ILogger<RegisterViewModel> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [RelayCommand(CanExecute = nameof(CanRegister))]
    private async Task RegisterAsync()
    {
        if (!ValidateInput())
            return;

        IsLoading = true;
        ErrorMessage = null;
        SuccessMessage = null;

        try
        {
            var response = await _authService.RegisterAsync(Email, Password, ReferralUid);

            if (response.Success && response.Data != null)
            {
                _logger.LogInformation("Registration successful: {Uid}", response.Data.Uid);
                SuccessMessage = response.Data.Message;
                IsRegistrationComplete = true;
            }
            else
            {
                ErrorMessage = response.Error?.Message ?? "회원가입에 실패했습니다.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registration error");
            ErrorMessage = "회원가입 중 오류가 발생했습니다.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanRegister() => !IsLoading && !IsRegistrationComplete;

    private bool ValidateInput()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Email))
        {
            ErrorMessage = "이메일을 입력해주세요.";
            return false;
        }

        if (!IsValidEmail(Email))
        {
            ErrorMessage = "올바른 이메일 형식이 아닙니다.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "비밀번호를 입력해주세요.";
            return false;
        }

        if (Password.Length < 8)
        {
            ErrorMessage = "비밀번호는 8자 이상이어야 합니다.";
            return false;
        }

        if (Password != PasswordConfirm)
        {
            ErrorMessage = "비밀번호가 일치하지 않습니다.";
            return false;
        }

        return true;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    [RelayCommand]
    private void GoToLogin()
    {
        LoginRequested?.Invoke(this, EventArgs.Empty);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    partial void OnEmailChanged(string value)
    {
        RegisterCommand.NotifyCanExecuteChanged();
    }

    partial void OnPasswordChanged(string value)
    {
        RegisterCommand.NotifyCanExecuteChanged();
    }

    partial void OnPasswordConfirmChanged(string value)
    {
        RegisterCommand.NotifyCanExecuteChanged();
    }
}
