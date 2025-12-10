using KimchiHedge.Core.Models;
using Microsoft.Extensions.Logging;

namespace KimchiHedge.Core.Trading;

/// <summary>
/// 자동매매 엔진 - 상태 머신 기반
///
/// 상태 정의:
/// - IDLE: 자동 OFF, 포지션 없음
/// - WAIT_ENTRY: 자동 ON, 진입 조건 대기
/// - ENTERING: 진입 중 (업비트 매수 -> BingX 숏)
/// - POSITION_OPEN: 포지션 보유 중 (헷지 완료)
/// - EXITING: 청산 중 (양쪽 매도/청산)
/// - COOLDOWN: 청산 후 재진입 금지
/// - ERROR_ROLLBACK: 오류 발생 시 강제 롤백
///
/// 핵심 규칙:
/// - 단일 포지션 원칙 (물타기/불타기 없음)
/// - 업비트 체결량 = BingX 숏 수량 (정확히 1:1)
/// - 수량 불일치 시 즉시 롤백
/// - 김프 값은 서버 전달값만 사용
/// </summary>
public class TradingEngine
{
    private readonly IOrderExecutor _orderExecutor;
    private readonly IPositionManager _positionManager;
    private readonly IRollbackService _rollbackService;
    private readonly ICooldownService _cooldownService;
    private readonly ILogger<TradingEngine> _logger;

    private TradingState _state = TradingState.Idle;
    private TradingSettings _settings = new();
    private readonly object _stateLock = new();

    // 진입 중 임시 저장
    private decimal _pendingUpbitAmount;
    private decimal _pendingUpbitPrice;
    private decimal _lastKimchi;

    // 이벤트
    public event EventHandler<TradingState>? StateChanged;
    public event EventHandler<Position>? PositionOpened;
    public event EventHandler<Position>? PositionClosed;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler<string>? LogMessage;

    // 상태 조회 (읽기 전용)
    public TradingState State => _state;
    public Position? CurrentPosition => _positionManager.CurrentPosition;
    public bool HasPosition => _positionManager.HasPosition;
    public bool IsInCooldown => _state == TradingState.Cooldown;
    public bool IsAutoTradingOn => _state != TradingState.Idle;
    public TradingSettings Settings => _settings;

    public TradingEngine(
        IOrderExecutor orderExecutor,
        IPositionManager positionManager,
        IRollbackService rollbackService,
        ICooldownService cooldownService,
        ILogger<TradingEngine> logger)
    {
        _orderExecutor = orderExecutor;
        _positionManager = positionManager;
        _rollbackService = rollbackService;
        _cooldownService = cooldownService;
        _logger = logger;

        SubscribeToEvents();
    }

    private void SubscribeToEvents()
    {
        _cooldownService.CooldownEnded += OnCooldownEnded;
        _rollbackService.RollbackCompleted += OnRollbackCompleted;
        _rollbackService.RollbackFailed += OnRollbackFailed;
    }

    #region 설정

    /// <summary>
    /// 트레이딩 설정 업데이트
    /// </summary>
    public void UpdateSettings(TradingSettings settings)
    {
        if (!settings.Validate(out var errorMessage))
        {
            throw new ArgumentException(errorMessage);
        }

        _settings = settings;
        _cooldownService.SetCooldownSeconds(settings.CooldownSeconds);
        Log($"설정 업데이트: 진입={settings.EntryKimchi}%, 익절={settings.TakeProfitKimchi}%, 손절={settings.StopLossKimchi}%");
    }

    #endregion

    #region 이벤트 핸들러 (E_*)

    /// <summary>
    /// E_TOGGLE_ON: 자동매매 시작
    /// </summary>
    public void OnToggleOn()
    {
        lock (_stateLock)
        {
            if (_state == TradingState.Idle)
            {
                ChangeState(TradingState.WaitEntry);
                Log("자동매매 시작 - 진입 조건 대기");
            }
            else if (_state == TradingState.Cooldown)
            {
                Log("쿨다운 중 - 자동매매 ON 상태");
            }
        }
    }

    /// <summary>
    /// E_TOGGLE_OFF: 자동매매 중지 (수동 청산 포함)
    /// </summary>
    public async Task OnToggleOffAsync()
    {
        TradingState currentState;
        lock (_stateLock)
        {
            currentState = _state;

            switch (_state)
            {
                case TradingState.WaitEntry:
                    ChangeState(TradingState.Idle);
                    Log("자동매매 중지");
                    return;

                case TradingState.Cooldown:
                    ChangeState(TradingState.Idle);
                    _cooldownService.Cancel();
                    Log("자동매매 중지 (쿨다운 취소)");
                    return;

                case TradingState.PositionOpen:
                    break;

                default:
                    Log($"현재 상태에서는 중지 불가: {_state}");
                    return;
            }
        }

        if (currentState == TradingState.PositionOpen)
        {
            Log("수동 청산 시작");
            await ExecuteExitAsync(CloseReason.Manual);
        }
    }

    /// <summary>
    /// E_KIMCHI_UPDATE: 김프 값 수신
    /// </summary>
    public async Task OnKimchiUpdateAsync(KimchiPremiumData data)
    {
        _lastKimchi = data.Kimchi;

        switch (_state)
        {
            case TradingState.WaitEntry:
                // 진입 조건: kimchi >= entry_k
                if (data.Kimchi >= _settings.EntryKimchi)
                {
                    Log($"진입 조건 충족! 김프: {data.Kimchi:F2}% >= {_settings.EntryKimchi:F2}%");
                    await ExecuteEntryAsync(data);
                }
                break;

            case TradingState.PositionOpen:
                // 익절 조건: kimchi <= tp_k
                if (data.Kimchi <= _settings.TakeProfitKimchi)
                {
                    Log($"익절 조건 충족! 김프: {data.Kimchi:F2}% <= {_settings.TakeProfitKimchi:F2}%");
                    await ExecuteExitAsync(CloseReason.TakeProfit);
                }
                // 손절 조건: kimchi >= sl_k
                else if (data.Kimchi >= _settings.StopLossKimchi)
                {
                    Log($"손절 조건 충족! 김프: {data.Kimchi:F2}% >= {_settings.StopLossKimchi:F2}%");
                    await ExecuteExitAsync(CloseReason.StopLoss);
                }
                break;

            default:
                // 다른 상태에서는 무시
                break;
        }
    }

    /// <summary>
    /// E_TIMEOUT: 쿨다운 종료
    /// </summary>
    private void OnCooldownEnded(object? sender, EventArgs e)
    {
        lock (_stateLock)
        {
            if (_state == TradingState.Cooldown)
            {
                ChangeState(TradingState.WaitEntry);
                Log("쿨다운 종료 - 진입 조건 대기");
            }
        }
    }

    /// <summary>
    /// 롤백 완료
    /// </summary>
    private void OnRollbackCompleted(object? sender, EventArgs e)
    {
        lock (_stateLock)
        {
            if (_state == TradingState.ErrorRollback)
            {
                _positionManager.Clear();
                StartCooldown();
                Log("롤백 완료 - 쿨다운 시작");
            }
        }
    }

    /// <summary>
    /// 롤백 실패 - 상태 유지 + UI 경고
    /// </summary>
    private void OnRollbackFailed(object? sender, string errorMessage)
    {
        ErrorOccurred?.Invoke(this, $"롤백 실패: {errorMessage}. 수동 확인 필요!");
        Log($"[경고] 롤백 실패: {errorMessage}");
    }

    #endregion

    #region 진입/청산 실행

    /// <summary>
    /// 진입 실행 (WAIT_ENTRY -> ENTERING -> POSITION_OPEN)
    /// </summary>
    private async Task ExecuteEntryAsync(KimchiPremiumData data)
    {
        ChangeState(TradingState.Entering);
        _pendingUpbitAmount = 0;
        _pendingUpbitPrice = 0;

        try
        {
            // 1. 포지션 생성
            _positionManager.CreatePosition(data.Kimchi);

            // 2. 업비트 시장가 매수
            Log("업비트 매수 주문 실행...");
            var upbitResult = await _orderExecutor.ExecuteUpbitBuyAsync(_settings.EntryRatio);

            if (!upbitResult.IsSuccess)
            {
                Log($"업비트 매수 실패: {upbitResult.ErrorMessage}");
                await HandleEntryErrorAsync(upbitResult.ErrorMessage, needsRollback: false);
                return;
            }

            // E_UPBIT_FILLED
            _pendingUpbitAmount = upbitResult.ExecutedQuantity;
            _pendingUpbitPrice = upbitResult.AveragePrice;
            Log($"업비트 매수 체결: {_pendingUpbitAmount:F8} BTC @ {_pendingUpbitPrice:N0} KRW");

            // 3. BingX 시장가 숏 (업비트 체결 수량과 동일)
            Log($"BingX 숏 주문 실행: {_pendingUpbitAmount:F8} BTC");
            await _orderExecutor.SetLeverageAsync(_settings.Leverage);
            var bingxResult = await _orderExecutor.ExecuteBingXShortAsync(_pendingUpbitAmount);

            if (!bingxResult.IsSuccess)
            {
                Log($"BingX 숏 실패: {bingxResult.ErrorMessage}");
                await HandleEntryErrorAsync(bingxResult.ErrorMessage, needsRollback: true);
                return;
            }

            // E_BINGX_FILLED - 수량 검증
            var bingxAmount = bingxResult.ExecutedQuantity;
            Log($"BingX 숏 체결: {bingxAmount:F8} BTC @ ${bingxResult.AveragePrice:F2}");

            // 1:1 수량 검증 (정확히 일치해야 함)
            if (Math.Abs(_pendingUpbitAmount - bingxAmount) > _settings.QuantityTolerance)
            {
                Log($"수량 불일치! 업비트: {_pendingUpbitAmount:F8}, BingX: {bingxAmount:F8}");
                await HandleEntryErrorAsync("수량 불일치 - 롤백 필요", needsRollback: true);
                return;
            }

            // 4. 진입 완료
            _positionManager.CompleteEntry(
                upbitAmount: _pendingUpbitAmount,
                upbitPrice: _pendingUpbitPrice,
                bingxAmount: bingxAmount,
                bingxPrice: bingxResult.AveragePrice,
                upbitFee: upbitResult.Fee,
                bingxFee: bingxResult.Fee);

            ChangeState(TradingState.PositionOpen);
            PositionOpened?.Invoke(this, _positionManager.CurrentPosition!);
            Log($"헷지 진입 완료! 수량: {_pendingUpbitAmount:F8} BTC (1:1 동기화)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "진입 중 예외 발생");
            await HandleEntryErrorAsync(ex.Message, needsRollback: _pendingUpbitAmount > 0);
        }
    }

    /// <summary>
    /// 청산 실행 (POSITION_OPEN -> EXITING -> COOLDOWN)
    /// </summary>
    private async Task ExecuteExitAsync(CloseReason reason)
    {
        ChangeState(TradingState.Exiting);

        try
        {
            var position = _positionManager.CurrentPosition;
            if (position == null)
            {
                Log("청산할 포지션이 없음");
                StartCooldown();
                return;
            }

            // 1. 업비트 전량 시장가 매도
            Log("업비트 매도 주문 실행...");
            var upbitResult = await _orderExecutor.ExecuteUpbitSellAllAsync();

            // 2. BingX 숏 전량 청산
            Log("BingX 숏 청산 주문 실행...");
            var bingxResult = await _orderExecutor.ExecuteBingXCloseAsync();

            // 3. 결과 확인
            if (!upbitResult.IsSuccess || !bingxResult.IsSuccess)
            {
                var error = !upbitResult.IsSuccess ? upbitResult.ErrorMessage : bingxResult.ErrorMessage;
                Log($"청산 중 오류: {error}");
                await HandleExitErrorAsync(error);
                return;
            }

            // E_UPBIT_CLOSE_FILLED & E_BINGX_CLOSE_FILLED
            Log($"업비트 매도 체결: {upbitResult.ExecutedQuantity:F8} BTC @ {upbitResult.AveragePrice:N0} KRW");
            Log($"BingX 청산 체결: {bingxResult.ExecutedQuantity:F8} BTC @ ${bingxResult.AveragePrice:F2}");

            // 4. 청산 완료 처리
            _positionManager.CompleteClose(
                closeKimchi: _lastKimchi,
                reason: reason,
                upbitSellPrice: upbitResult.AveragePrice,
                bingxClosePrice: bingxResult.AveragePrice,
                upbitFee: upbitResult.Fee,
                bingxFee: bingxResult.Fee);

            // 5. 손익 로그
            var pnl = _positionManager.CurrentPosition?.RealizedPnL ?? 0;
            Log($"{reason} 완료! 손익: {pnl:N0} KRW");

            PositionClosed?.Invoke(this, _positionManager.CurrentPosition!);

            // 6. 쿨다운 시작
            StartCooldown();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "청산 중 예외 발생");
            await HandleExitErrorAsync(ex.Message);
        }
    }

    #endregion

    #region 에러 처리 및 롤백

    /// <summary>
    /// 진입 중 에러 처리
    /// </summary>
    private async Task HandleEntryErrorAsync(string? errorMessage, bool needsRollback)
    {
        ErrorOccurred?.Invoke(this, $"진입 실패: {errorMessage}");

        if (needsRollback)
        {
            ChangeState(TradingState.ErrorRollback);
            Log("롤백 시작 (진입 실패)...");
            await _rollbackService.ExecuteRollbackAsync();
        }
        else
        {
            _positionManager.Clear();
            StartCooldown();
        }
    }

    /// <summary>
    /// 청산 중 에러 처리
    /// </summary>
    private async Task HandleExitErrorAsync(string? errorMessage)
    {
        ErrorOccurred?.Invoke(this, $"청산 실패: {errorMessage}");
        ChangeState(TradingState.ErrorRollback);
        Log("롤백 시작 (청산 실패)...");
        await _rollbackService.ExecuteRollbackAsync();
    }

    /// <summary>
    /// 쿨다운 시작
    /// </summary>
    private void StartCooldown()
    {
        ChangeState(TradingState.Cooldown);
        _cooldownService.Start();
        Log($"쿨다운 시작: {_settings.CooldownSeconds}초");
    }

    #endregion

    #region 유틸리티

    /// <summary>
    /// 상태 변경
    /// </summary>
    private void ChangeState(TradingState newState)
    {
        if (_state != newState)
        {
            var oldState = _state;
            _state = newState;
            _logger.LogInformation("상태 변경: {OldState} -> {NewState}", oldState, newState);
            StateChanged?.Invoke(this, newState);
        }
    }

    /// <summary>
    /// 로그 발행
    /// </summary>
    private void Log(string message)
    {
        _logger.LogInformation(message);
        LogMessage?.Invoke(this, $"[{DateTime.Now:HH:mm:ss}] {message}");
    }

    /// <summary>
    /// 상태 확인
    /// </summary>
    public bool IsState(TradingState state) => _state == state;

    /// <summary>
    /// 쿨다운 남은 시간 (초)
    /// </summary>
    public int RemainingCooldownSeconds => _cooldownService.RemainingSeconds;

    #endregion
}
