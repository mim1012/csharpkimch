using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KimchiHedge.Client.Services;
using KimchiHedge.Core.Models;
using KimchiHedge.Core.Trading;
using KimchiHedge.Exchanges.BingX;
using KimchiHedge.Exchanges.Upbit;
using Microsoft.Extensions.Logging;

namespace KimchiHedge.Client.ViewModels;

/// <summary>
/// 대시보드 ViewModel
/// </summary>
public partial class DashboardViewModel : ViewModelBase
{
    private readonly IPriceService _priceService;
    private readonly ISecureStorage _secureStorage;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ITradingEngineFactory _engineFactory;
    private bool _isEngineEventSubscribed;
    #region Price Properties

    [ObservableProperty]
    private string _upbitPrice = "0";

    [ObservableProperty]
    private string _bingxPrice = "0.00";

    [ObservableProperty]
    private string _kimchiPremium = "0.00";

    [ObservableProperty]
    private bool _isPremiumPositive = true;

    [ObservableProperty]
    private string _exchangeRate = "1,400.00";

    #endregion

    #region Trading Status

    [ObservableProperty]
    private bool _isTrading;

    [ObservableProperty]
    private string _statusText = "대기 중";

    [ObservableProperty]
    private string _toggleButtonText = "자동매매 시작";

    [ObservableProperty]
    private bool _isCooldownActive;

    [ObservableProperty]
    private string _cooldownTime = "00:00";

    #endregion

    #region Position Properties

    [ObservableProperty]
    private bool _hasPosition;

    [ObservableProperty]
    private string _spotPosition = "0.00000000";

    [ObservableProperty]
    private string _futuresPosition = "0.00000000";

    [ObservableProperty]
    private string _entryPremium = "0.00%";

    [ObservableProperty]
    private string _currentPremium = "0.00%";

    [ObservableProperty]
    private string _unrealizedPnl = "0";

    [ObservableProperty]
    private bool _isPnlPositive;

    #endregion

    #region Logs

    [ObservableProperty]
    private ObservableCollection<LogEntry> _logs = new();

    #endregion

    public DashboardViewModel(
        IPriceService priceService,
        ISecureStorage secureStorage,
        ILoggerFactory loggerFactory,
        ITradingEngineFactory engineFactory)
    {
        _priceService = priceService;
        _secureStorage = secureStorage;
        _loggerFactory = loggerFactory;
        _engineFactory = engineFactory;

        // 가격 업데이트 이벤트 구독
        _priceService.PriceUpdated += OnPriceUpdated;

        // 초기 로그 추가
        AddLog("시스템 시작", LogLevel.Info);

        // 가격 서비스 시작
        _ = _priceService.StartAsync();
    }

    protected override void OnDispose()
    {
        // 이벤트 구독 해제 (메모리 누수 방지)
        _priceService.PriceUpdated -= OnPriceUpdated;
        UnsubscribeFromEngineEvents();
        base.OnDispose();
    }

    private void OnPriceUpdated(object? sender, PriceData e)
    {
        UpdatePrices(e.UpbitPrice, e.BingxPrice, e.ExchangeRate);

        // TradingEngine에 김프 데이터 전달 (자동매매 ON 상태일 때만)
        if (_engineFactory.IsInitialized && _engineFactory.Engine!.IsAutoTradingOn)
        {
            var kimchiPremium = CalculateKimchiPremium(e.UpbitPrice, e.BingxPrice, e.ExchangeRate);
            var kimchiData = new KimchiPremiumData
            {
                Kimchi = kimchiPremium,
                Upbit = e.UpbitPrice,
                Global = e.BingxPrice,
                Rate = e.ExchangeRate,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ReceivedAt = DateTime.UtcNow
            };
            _ = _engineFactory.Engine.OnKimchiUpdateAsync(kimchiData);
        }
    }

    private static decimal CalculateKimchiPremium(decimal upbitPrice, decimal bingxPrice, decimal exchangeRate)
    {
        if (bingxPrice <= 0 || exchangeRate <= 0)
        {
            return 0;
        }

        var bingxKrw = bingxPrice * exchangeRate;
        return ((upbitPrice / bingxKrw) - 1) * 100;
    }

    #region Trading Commands

    [RelayCommand]
    private async Task ToggleTradingAsync()
    {
        IsBusy = true;

        try
        {
            // TradingEngine 초기화 (최초 1회)
            if (!_engineFactory.IsInitialized)
            {
                AddLog("TradingEngine 초기화 중...", LogLevel.Info);
                var success = await _engineFactory.InitializeAsync();
                if (!success)
                {
                    MessageBox.Show(
                        "API 키가 설정되지 않았습니다.\nAPI 키 관리에서 업비트와 BingX API 키를 먼저 설정해주세요.",
                        "자동매매",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    AddLog("TradingEngine 초기화 실패: API 키 미설정", LogLevel.Error);
                    return;
                }

                SubscribeToEngineEvents();
                AddLog("TradingEngine 초기화 완료", LogLevel.Success);
            }

            var engine = _engineFactory.Engine!;

            if (engine.IsAutoTradingOn)
            {
                // 자동매매 중지 (포지션 보유 시 청산)
                await engine.OnToggleOffAsync();
            }
            else
            {
                // 자동매매 시작
                engine.OnToggleOn();
            }
        }
        catch (Exception ex)
        {
            AddLog($"자동매매 토글 오류: {ex.Message}", LogLevel.Error);
            MessageBox.Show($"오류 발생: {ex.Message}", "자동매매", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    #region TradingEngine Event Handlers

    private void SubscribeToEngineEvents()
    {
        if (_isEngineEventSubscribed || _engineFactory.Engine == null)
        {
            return;
        }

        var engine = _engineFactory.Engine;
        engine.StateChanged += OnEngineStateChanged;
        engine.PositionOpened += OnEnginePositionOpened;
        engine.PositionClosed += OnEnginePositionClosed;
        engine.LogMessage += OnEngineLogMessage;
        engine.ErrorOccurred += OnEngineError;
        _isEngineEventSubscribed = true;
    }

    private void UnsubscribeFromEngineEvents()
    {
        if (!_isEngineEventSubscribed || _engineFactory.Engine == null)
        {
            return;
        }

        var engine = _engineFactory.Engine;
        engine.StateChanged -= OnEngineStateChanged;
        engine.PositionOpened -= OnEnginePositionOpened;
        engine.PositionClosed -= OnEnginePositionClosed;
        engine.LogMessage -= OnEngineLogMessage;
        engine.ErrorOccurred -= OnEngineError;
        _isEngineEventSubscribed = false;
    }

    private void OnEngineStateChanged(object? sender, TradingState state)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            IsTrading = state != TradingState.Idle;
            IsCooldownActive = state == TradingState.Cooldown;

            StatusText = state switch
            {
                TradingState.Idle => "대기 중",
                TradingState.WaitEntry => "진입 대기",
                TradingState.Entering => "진입 중...",
                TradingState.PositionOpen => "포지션 보유",
                TradingState.Exiting => "청산 중...",
                TradingState.Cooldown => "쿨다운",
                TradingState.ErrorRollback => "에러 복구",
                _ => "알 수 없음"
            };

            ToggleButtonText = IsTrading ? "자동매매 중지" : "자동매매 시작";

            // 쿨다운 타이머 업데이트
            if (state == TradingState.Cooldown && _engineFactory.Engine != null)
            {
                _ = UpdateCooldownTimerAsync();
            }
        });
    }

    private async Task UpdateCooldownTimerAsync()
    {
        while (_engineFactory.Engine?.IsInCooldown == true)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                var remaining = _engineFactory.Engine.RemainingCooldownSeconds;
                CooldownTime = $"{remaining / 60:D2}:{remaining % 60:D2}";
            });
            await Task.Delay(1000);
        }

        Application.Current?.Dispatcher.Invoke(() =>
        {
            CooldownTime = "00:00";
        });
    }

    private void OnEnginePositionOpened(object? sender, Position position)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            HasPosition = true;
            SpotPosition = position.UpbitBtcAmount.ToString("F8");
            FuturesPosition = position.FuturesShortAmount.ToString("F8");
            EntryPremium = $"{position.EntryKimchi:+0.00;-0.00}%";
            AddLog($"포지션 진입: {position.UpbitBtcAmount:F8} BTC @ 김프 {position.EntryKimchi:F2}%", LogLevel.Success);
        });
    }

    private void OnEnginePositionClosed(object? sender, Position position)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            HasPosition = false;
            SpotPosition = "0.00000000";
            FuturesPosition = "0.00000000";
            UnrealizedPnl = position.RealizedPnL?.ToString("N0") ?? "0";
            IsPnlPositive = (position.RealizedPnL ?? 0) >= 0;

            var pnlText = position.RealizedPnL?.ToString("N0") ?? "0";
            AddLog($"포지션 청산: {position.CloseReason}, 손익: {pnlText} KRW", LogLevel.Success);
        });
    }

    private void OnEngineLogMessage(object? sender, string message)
    {
        AddLog(message, LogLevel.Info);
    }

    private void OnEngineError(object? sender, string error)
    {
        AddLog($"[엔진 오류] {error}", LogLevel.Error);
        Application.Current?.Dispatcher.Invoke(() =>
        {
            MessageBox.Show(error, "Trading Engine 오류", MessageBoxButton.OK, MessageBoxImage.Error);
        });
    }

    #endregion

    [RelayCommand]
    private async Task ManualCloseAsync()
    {
        var result = MessageBox.Show(
            "현재 포지션을 수동 청산하시겠습니까?\n\n업비트: 전량 시장가 매도\nBingX: 숏 포지션 청산",
            "수동 청산",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        // TradingEngine이 초기화되어 있고 포지션이 있으면 엔진 통해 청산
        if (_engineFactory.IsInitialized && _engineFactory.Engine!.HasPosition)
        {
            AddLog("TradingEngine을 통한 수동 청산 시작", LogLevel.Warning);
            IsBusy = true;
            try
            {
                await _engineFactory.Engine.OnToggleOffAsync();
                MessageBox.Show("수동 청산이 완료되었습니다.", "청산 완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AddLog($"수동 청산 오류: {ex.Message}", LogLevel.Error);
                MessageBox.Show($"청산 중 오류 발생: {ex.Message}", "청산 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
            return;
        }

        // Fallback: 직접 거래소 API 호출
        AddLog("수동 청산 시작 (직접 모드)", LogLevel.Warning);
        IsBusy = true;

        try
        {
            var credentials = await _secureStorage.LoadCredentialsAsync();

            bool hasUpbitKeys = !string.IsNullOrEmpty(credentials.UpbitAccessKey) &&
                               !string.IsNullOrEmpty(credentials.UpbitSecretKey);
            bool hasBingxKeys = !string.IsNullOrEmpty(credentials.BingxApiKey) &&
                               !string.IsNullOrEmpty(credentials.BingxSecretKey);

            if (!hasUpbitKeys && !hasBingxKeys)
            {
                MessageBox.Show("API 키가 설정되지 않았습니다.\nAPI 키 관리에서 설정해주세요.", "수동 청산", MessageBoxButton.OK, MessageBoxImage.Warning);
                AddLog("수동 청산 실패: API 키 미설정", LogLevel.Error);
                return;
            }

            bool upbitSuccess = true;
            bool bingxSuccess = true;

            // 1. 업비트 전량 매도
            if (hasUpbitKeys)
            {
                try
                {
                    var upbitLogger = _loggerFactory.CreateLogger<UpbitSpotExchange>();
                    using var upbitExchange = new UpbitSpotExchange(credentials.UpbitAccessKey, credentials.UpbitSecretKey, upbitLogger);
                    await upbitExchange.ConnectAsync();

                    var btcBalance = await upbitExchange.GetBalanceAsync("BTC");
                    if (btcBalance > 0.0001m) // 최소 수량 체크
                    {
                        AddLog($"업비트 BTC 잔고: {btcBalance:F8}", LogLevel.Info);
                        var sellResult = await upbitExchange.PlaceMarketSellAllAsync("BTC/KRW");
                        if (sellResult.IsSuccess)
                        {
                            AddLog($"업비트 매도 완료: {sellResult.ExecutedQuantity:F8} BTC @ {sellResult.AveragePrice:N0} KRW", LogLevel.Success);
                        }
                        else
                        {
                            AddLog($"업비트 매도 실패: {sellResult.ErrorMessage}", LogLevel.Error);
                            upbitSuccess = false;
                        }
                    }
                    else
                    {
                        AddLog("업비트: 매도할 BTC 없음", LogLevel.Info);
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"업비트 청산 오류: {ex.Message}", LogLevel.Error);
                    upbitSuccess = false;
                }
            }

            // 2. BingX 숏 포지션 청산
            if (hasBingxKeys)
            {
                try
                {
                    var bingxLogger = _loggerFactory.CreateLogger<BingXFuturesExchange>();
                    using var bingxExchange = new BingXFuturesExchange(credentials.BingxApiKey, credentials.BingxSecretKey, bingxLogger);
                    await bingxExchange.ConnectAsync();

                    var position = await bingxExchange.GetPositionAsync("BTCUSDT");
                    if (position != null && position.Quantity > 0)
                    {
                        AddLog($"BingX 숏 포지션: {position.Quantity:F8} BTC @ ${position.EntryPrice:F2}", LogLevel.Info);
                        var closeResult = await bingxExchange.ClosePositionAsync("BTCUSDT");
                        if (closeResult.IsSuccess)
                        {
                            AddLog($"BingX 청산 완료: {closeResult.ExecutedQuantity:F8} BTC @ ${closeResult.AveragePrice:F2}", LogLevel.Success);
                        }
                        else
                        {
                            AddLog($"BingX 청산 실패: {closeResult.ErrorMessage}", LogLevel.Error);
                            bingxSuccess = false;
                        }
                    }
                    else
                    {
                        AddLog("BingX: 청산할 포지션 없음", LogLevel.Info);
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"BingX 청산 오류: {ex.Message}", LogLevel.Error);
                    bingxSuccess = false;
                }
            }

            // 결과 정리
            if (upbitSuccess && bingxSuccess)
            {
                AddLog("수동 청산 완료", LogLevel.Success);
                HasPosition = false;
                SpotPosition = "0.00000000";
                FuturesPosition = "0.00000000";
                MessageBox.Show("수동 청산이 완료되었습니다.", "청산 완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                AddLog("수동 청산 부분 실패", LogLevel.Warning);
                MessageBox.Show("일부 청산이 실패했습니다.\n로그를 확인해주세요.", "청산 결과", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            AddLog($"수동 청산 예외: {ex.Message}", LogLevel.Error);
            MessageBox.Show($"청산 중 오류 발생: {ex.Message}", "청산 오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ClearLogs()
    {
        Logs.Clear();
    }

    #endregion

    #region Log Methods

    public void AddLog(string message, LogLevel level = LogLevel.Info)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Message = message,
            Level = level
        };

        // UI 스레드에서 실행
        Application.Current?.Dispatcher.Invoke(() =>
        {
            Logs.Add(entry);
        });
    }

    #endregion

    #region Price Update Methods

    /// <summary>
    /// 가격 업데이트 (외부에서 호출)
    /// </summary>
    public void UpdatePrices(decimal upbitPrice, decimal bingxPrice, decimal exchangeRate)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            UpbitPrice = upbitPrice.ToString("N0");
            BingxPrice = bingxPrice.ToString("N2");
            ExchangeRate = exchangeRate.ToString("N2");

            // 김치프리미엄 계산
            if (bingxPrice > 0 && exchangeRate > 0)
            {
                var bingxKrw = bingxPrice * exchangeRate;
                var premium = ((upbitPrice / bingxKrw) - 1) * 100;
                KimchiPremium = premium.ToString("+0.00;-0.00;0.00");
                IsPremiumPositive = premium >= 0;
                CurrentPremium = $"{KimchiPremium}%";
            }
        });
    }

    /// <summary>
    /// 포지션 업데이트 (외부에서 호출)
    /// </summary>
    public void UpdatePosition(decimal spotQty, decimal futuresQty, decimal entryPremium, decimal pnl)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            HasPosition = spotQty != 0 || futuresQty != 0;
            SpotPosition = spotQty.ToString("0.00000000");
            FuturesPosition = futuresQty.ToString("0.00000000");
            EntryPremium = $"{entryPremium:+0.00;-0.00;0.00}%";
            UnrealizedPnl = pnl.ToString("N0");
            IsPnlPositive = pnl >= 0;
        });
    }

    #endregion
}

/// <summary>
/// 로그 레벨
/// </summary>
public enum LogLevel
{
    Info,
    Success,
    Warning,
    Error
}

/// <summary>
/// 로그 항목
/// </summary>
public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public string Message { get; set; } = string.Empty;
    public LogLevel Level { get; set; }

    public string FormattedTime => Timestamp.ToString("HH:mm:ss");
    public string FormattedMessage => $"[{FormattedTime}] {Message}";

    public Brush TextColor => Level switch
    {
        LogLevel.Success => (Brush)Application.Current.FindResource("StatusProfitBrush"),
        LogLevel.Warning => (Brush)Application.Current.FindResource("StatusCooldownBrush"),
        LogLevel.Error => (Brush)Application.Current.FindResource("StatusLossBrush"),
        _ => (Brush)Application.Current.FindResource("TextSecondaryBrush")
    };
}
