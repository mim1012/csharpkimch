using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KimchiHedge.Client.Services;
using KimchiHedge.Exchanges.BingX;
using KimchiHedge.Exchanges.Upbit;
using Microsoft.Extensions.Logging;

namespace KimchiHedge.Client.ViewModels;

/// <summary>
/// API 키 관리 ViewModel
/// </summary>
public partial class ApiKeysViewModel : ViewModelBase
{
    private readonly ISecureStorage _secureStorage;
    private readonly ILoggerFactory _loggerFactory;

    #region Upbit Properties

    [ObservableProperty]
    private string _upbitAccessKey = string.Empty;

    [ObservableProperty]
    private string _upbitStatusText = "미연결";

    [ObservableProperty]
    private bool _isUpbitConnected;

    [ObservableProperty]
    private bool _isUpbitTesting;

    [ObservableProperty]
    private string _upbitBalance = "---";

    #endregion

    #region BingX Properties

    [ObservableProperty]
    private string _bingxApiKey = string.Empty;

    [ObservableProperty]
    private string _bingxStatusText = "미연결";

    [ObservableProperty]
    private bool _isBingxConnected;

    [ObservableProperty]
    private bool _isBingxTesting;

    [ObservableProperty]
    private string _bingxBalance = "---";

    #endregion

    // PasswordBox 참조 저장 (바인딩 불가로 인해)
    private string _upbitSecretKey = string.Empty;
    private string _bingxSecretKey = string.Empty;

    public ApiKeysViewModel(ISecureStorage secureStorage, ILoggerFactory loggerFactory)
    {
        _secureStorage = secureStorage;
        _loggerFactory = loggerFactory;
        _ = LoadCredentialsAsync();
    }

    private async Task LoadCredentialsAsync()
    {
        try
        {
            var credentials = await _secureStorage.LoadCredentialsAsync();
            UpbitAccessKey = credentials.UpbitAccessKey;
            _upbitSecretKey = credentials.UpbitSecretKey;
            BingxApiKey = credentials.BingxApiKey;
            _bingxSecretKey = credentials.BingxSecretKey;

            // 키가 있으면 연결됨으로 표시
            if (!string.IsNullOrEmpty(UpbitAccessKey) && !string.IsNullOrEmpty(_upbitSecretKey))
            {
                UpbitStatusText = "연결됨";
                IsUpbitConnected = true;
            }

            if (!string.IsNullOrEmpty(BingxApiKey) && !string.IsNullOrEmpty(_bingxSecretKey))
            {
                BingxStatusText = "연결됨";
                IsBingxConnected = true;
            }
        }
        catch
        {
            // 기본값 유지
        }
    }

    #region Upbit Commands

    [RelayCommand]
    private async Task TestUpbitConnectionAsync(object? parameter)
    {
        var passwordBox = parameter as PasswordBox;
        var secretKey = passwordBox?.Password ?? _upbitSecretKey;

        if (string.IsNullOrWhiteSpace(UpbitAccessKey) || string.IsNullOrWhiteSpace(secretKey))
        {
            MessageBox.Show("업비트 API 키를 입력해주세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsUpbitTesting = true;
        UpbitStatusText = "테스트 중...";

        try
        {
            // 실제 업비트 API 호출하여 연결 테스트
            var logger = _loggerFactory.CreateLogger<UpbitSpotExchange>();
            using var exchange = new UpbitSpotExchange(UpbitAccessKey, secretKey, logger);
            await exchange.ConnectAsync();
            var balance = await exchange.GetBalanceAsync("KRW");

            UpbitStatusText = "연결됨";
            IsUpbitConnected = true;
            _upbitSecretKey = secretKey;
            UpbitBalance = $"{balance:N0} KRW";

            MessageBox.Show($"업비트 연결에 성공했습니다.\n잔고: {balance:N0} KRW", "연결 성공", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            UpbitStatusText = "연결 실패";
            IsUpbitConnected = false;
            MessageBox.Show($"업비트 연결 실패: {ex.Message}", "연결 실패", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsUpbitTesting = false;
        }
    }

    [RelayCommand]
    private async Task SaveUpbitKeysAsync(object? parameter)
    {
        var passwordBox = parameter as PasswordBox;
        var secretKey = passwordBox?.Password ?? _upbitSecretKey;

        if (string.IsNullOrWhiteSpace(UpbitAccessKey) || string.IsNullOrWhiteSpace(secretKey))
        {
            MessageBox.Show("업비트 API 키를 입력해주세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var credentials = await _secureStorage.LoadCredentialsAsync();
            credentials.UpbitAccessKey = UpbitAccessKey;
            credentials.UpbitSecretKey = secretKey;
            await _secureStorage.SaveCredentialsAsync(credentials);

            _upbitSecretKey = secretKey;
            UpbitStatusText = "저장됨";
            IsUpbitConnected = true;

            MessageBox.Show("업비트 API 키가 저장되었습니다.", "저장 완료", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"저장 실패: {ex.Message}", "저장 실패", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #endregion

    #region BingX Commands

    [RelayCommand]
    private async Task TestBingxConnectionAsync(object? parameter)
    {
        var passwordBox = parameter as PasswordBox;
        var secretKey = passwordBox?.Password ?? _bingxSecretKey;

        if (string.IsNullOrWhiteSpace(BingxApiKey) || string.IsNullOrWhiteSpace(secretKey))
        {
            MessageBox.Show("BingX API 키를 입력해주세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsBingxTesting = true;
        BingxStatusText = "테스트 중...";

        try
        {
            // 실제 BingX API 호출하여 연결 테스트
            var logger = _loggerFactory.CreateLogger<BingXFuturesExchange>();
            using var exchange = new BingXFuturesExchange(BingxApiKey, secretKey, logger);
            await exchange.ConnectAsync();
            var balance = await exchange.GetBalanceAsync();

            BingxStatusText = "연결됨";
            IsBingxConnected = true;
            _bingxSecretKey = secretKey;
            BingxBalance = $"{balance:N2} USDT";

            MessageBox.Show($"BingX 연결에 성공했습니다.\n잔고: {balance:N2} USDT", "연결 성공", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            BingxStatusText = "연결 실패";
            IsBingxConnected = false;
            MessageBox.Show($"BingX 연결 실패: {ex.Message}", "연결 실패", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBingxTesting = false;
        }
    }

    [RelayCommand]
    private async Task SaveBingxKeysAsync(object? parameter)
    {
        var passwordBox = parameter as PasswordBox;
        var secretKey = passwordBox?.Password ?? _bingxSecretKey;

        if (string.IsNullOrWhiteSpace(BingxApiKey) || string.IsNullOrWhiteSpace(secretKey))
        {
            MessageBox.Show("BingX API 키를 입력해주세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var credentials = await _secureStorage.LoadCredentialsAsync();
            credentials.BingxApiKey = BingxApiKey;
            credentials.BingxSecretKey = secretKey;
            await _secureStorage.SaveCredentialsAsync(credentials);

            _bingxSecretKey = secretKey;
            BingxStatusText = "저장됨";
            IsBingxConnected = true;

            MessageBox.Show("BingX API 키가 저장되었습니다.", "저장 완료", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"저장 실패: {ex.Message}", "저장 실패", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #endregion

    #region Balance Commands

    [RelayCommand]
    private async Task RefreshBalanceAsync()
    {
        IsBusy = true;

        try
        {
            // 업비트 잔고 조회
            if (IsUpbitConnected && !string.IsNullOrEmpty(UpbitAccessKey) && !string.IsNullOrEmpty(_upbitSecretKey))
            {
                try
                {
                    var logger = _loggerFactory.CreateLogger<UpbitSpotExchange>();
                    using var exchange = new UpbitSpotExchange(UpbitAccessKey, _upbitSecretKey, logger);
                    await exchange.ConnectAsync();
                    var balance = await exchange.GetBalanceAsync("KRW");
                    UpbitBalance = $"{balance:N0} KRW";
                }
                catch (Exception ex)
                {
                    UpbitBalance = "조회 실패";
                    System.Diagnostics.Debug.WriteLine($"Upbit balance error: {ex.Message}");
                }
            }

            // BingX 잔고 조회
            if (IsBingxConnected && !string.IsNullOrEmpty(BingxApiKey) && !string.IsNullOrEmpty(_bingxSecretKey))
            {
                try
                {
                    var logger = _loggerFactory.CreateLogger<BingXFuturesExchange>();
                    using var exchange = new BingXFuturesExchange(BingxApiKey, _bingxSecretKey, logger);
                    await exchange.ConnectAsync();
                    var balance = await exchange.GetBalanceAsync();
                    BingxBalance = $"{balance:N2} USDT";
                }
                catch (Exception ex)
                {
                    BingxBalance = "조회 실패";
                    System.Diagnostics.Debug.WriteLine($"BingX balance error: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"잔고 조회에 실패했습니다: {ex.Message}", "조회 실패", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion
}
