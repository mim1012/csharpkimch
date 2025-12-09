using KimchiHedge.Core.Models;
using Microsoft.Extensions.Logging;

namespace KimchiHedge.Core.Trading;

/// <summary>
/// 시스템 상태
/// </summary>
public enum SystemState
{
    Initializing,     // 초기화 중
    Authenticating,   // 인증 중
    Connecting,       // 서버 연결 중
    Ready,            // 준비 완료 (대기 상태)
    Trading,          // 트레이딩 활성화
    InPosition,       // 포지션 보유 중
    Closing,          // 청산 중
    Cooldown,         // 쿨다운 중
    Error,            // 오류 상태
    Offline           // 오프라인
}

/// <summary>
/// 자동매매 엔진 (단일 책임: 오케스트레이션만)
/// 각 서비스를 '연결'하고 '조율'하는 역할만 수행
/// - 언제 조건을 평가할지
/// - 언제 주문을 실행할지
/// - 언제 롤백할지
/// 이런 '흐름'만 관리하고, 실제 로직은 각 서비스에 위임
/// </summary>
public class TradingEngine
{
    private readonly ConditionEvaluator _conditionEvaluator;
    private readonly OrderExecutor _orderExecutor;
    private readonly PositionManager _positionManager;
    private readonly RollbackService _rollbackService;
    private readonly CooldownService _cooldownService;
    private readonly ILogger<TradingEngine> _logger;

    private SystemState _state = SystemState.Initializing;
    private TradingSettings _settings = new();

    // 외부로 이벤트 전달
    public event EventHandler<SystemState>? StateChanged;
    public event EventHandler<Position>? PositionOpened;
    public event EventHandler<Position>? PositionClosed;
    public event EventHandler<string>? ErrorOccurred;

    // 상태 조회 (읽기 전용)
    public SystemState State => _state;
    public Position? CurrentPosition => _positionManager.CurrentPosition;
    public bool HasPosition => _positionManager.HasPosition;
    public bool IsInCooldown => _cooldownService.IsInCooldown;
    public string RemainingCooldown => _cooldownService.RemainingTimeFormatted;

    public TradingEngine(
        ConditionEvaluator conditionEvaluator,
        OrderExecutor orderExecutor,
        PositionManager positionManager,
        RollbackService rollbackService,
        CooldownService cooldownService,
        ILogger<TradingEngine> logger)
    {
        _conditionEvaluator = conditionEvaluator;
        _orderExecutor = orderExecutor;
        _positionManager = positionManager;
        _rollbackService = rollbackService;
        _cooldownService = cooldownService;
        _logger = logger;

        // 서비스 이벤트 연결
        SubscribeToServiceEvents();
    }

    /// <summary>
    /// 서비스 이벤트 구독 (이벤트 전달)
    /// </summary>
    private void SubscribeToServiceEvents()
    {
        _positionManager.PositionOpened += (s, position) =>
        {
            PositionOpened?.Invoke(this, position);
        };

        _positionManager.PositionClosed += (s, position) =>
        {
            PositionClosed?.Invoke(this, position);
        };

        _cooldownService.CooldownStarted += (s, e) =>
        {
            ChangeState(SystemState.Cooldown);
        };

        _cooldownService.CooldownEnded += (s, e) =>
        {
            if (_state == SystemState.Cooldown)
            {
                ChangeState(SystemState.Trading);
            }
        };

        _rollbackService.RollbackFailed += (s, errorMessage) =>
        {
            ErrorOccurred?.Invoke(this, errorMessage);
        };
    }

    /// <summary>
    /// 설정 업데이트
    /// </summary>
    public void UpdateSettings(TradingSettings settings)
    {
        if (!settings.Validate(out var errorMessage))
        {
            throw new ArgumentException(errorMessage);
        }

        _settings = settings;
        _conditionEvaluator.UpdateSettings(settings);
        _cooldownService.SetCooldownMinutes(settings.CooldownMinutes);

        _logger.LogInformation("트레이딩 설정 업데이트 완료");
    }

    /// <summary>
    /// 김프 데이터 수신 시 호출 (오케스트레이션 핵심)
    /// </summary>
    public async Task OnKimchiDataReceivedAsync(KimchiPremiumData data)
    {
        // 1. 처리 불가 상태 확인
        if (_state == SystemState.Error || _state == SystemState.Offline)
        {
            return;
        }

        // 2. 쿨다운 확인 → CooldownService에 위임
        if (_cooldownService.IsInCooldown)
        {
            _logger.LogDebug("쿨다운 중... 남은 시간: {Remaining}", _cooldownService.RemainingTimeFormatted);
            return;
        }

        try
        {
            // 3. 조건 평가 → ConditionEvaluator에 위임
            var action = _conditionEvaluator.Evaluate(data.Kimchi, _positionManager.HasPosition);

            // 4. 행동 결정 및 실행
            switch (action)
            {
                case TradingAction.Enter:
                    await HandleEntryAsync(data);
                    break;

                case TradingAction.TakeProfit:
                    await HandleCloseAsync(data, CloseReason.TakeProfit);
                    break;

                case TradingAction.StopLoss:
                    await HandleCloseAsync(data, CloseReason.StopLoss);
                    break;

                case TradingAction.None:
                default:
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "트레이딩 로직 오류");
            await HandleErrorAsync(ex);
        }
    }

    /// <summary>
    /// 진입 처리 오케스트레이션
    /// </summary>
    private async Task HandleEntryAsync(KimchiPremiumData data)
    {
        _logger.LogInformation("진입 조건 충족! 김프: {Kimchi}%", data.Kimchi);

        // 1. 포지션 생성 시작 → PositionManager
        _positionManager.CreatePosition(data.Kimchi);
        ChangeState(SystemState.InPosition);

        // 2. 주문 실행 → OrderExecutor
        var result = await _orderExecutor.ExecuteEntryAsync(_settings.EntryRatio, _settings.Leverage);

        // 3. 결과 처리
        if (result.Success)
        {
            // 성공 → PositionManager에 진입 완료 통보
            _positionManager.CompleteEntry(result);
            _logger.LogInformation("헷지 진입 성공! 1:1 동기화 완료");
        }
        else
        {
            // 실패 처리
            _logger.LogWarning("진입 실패: {Error}", result.ErrorMessage);

            if (result.NeedsRollback)
            {
                // 롤백 필요 → RollbackService
                var rollbackSuccess = await _rollbackService.ExecuteRollbackAsync(CloseReason.Error);
                _positionManager.MarkAsRolledBack(CloseReason.Error);
            }
            else
            {
                _positionManager.Clear();
            }

            // 쿨다운 시작 → CooldownService
            _cooldownService.StartCooldown();
        }
    }

    /// <summary>
    /// 청산 처리 오케스트레이션
    /// </summary>
    private async Task HandleCloseAsync(KimchiPremiumData data, CloseReason reason)
    {
        _logger.LogInformation("{Reason} 조건 충족! 김프: {Kimchi}%", reason, data.Kimchi);

        // 1. 상태 변경
        _positionManager.SetStatus(PositionStatus.Closing);
        ChangeState(SystemState.Closing);

        // 2. 청산 실행 → OrderExecutor
        var result = await _orderExecutor.ExecuteCloseAsync();

        // 3. 결과 처리
        if (result.Success)
        {
            // 성공 → PositionManager에 청산 완료 통보
            _positionManager.CompleteClose(data.Kimchi, reason);
            _logger.LogInformation("{Reason} 완료!", reason);
        }
        else
        {
            _logger.LogError("청산 실패: {Error}", result.ErrorMessage);
            ErrorOccurred?.Invoke(this, $"청산 실패: {result.ErrorMessage}");
        }

        // 4. 쿨다운 시작 → CooldownService
        _cooldownService.StartCooldown();
    }

    /// <summary>
    /// 에러 처리 오케스트레이션
    /// </summary>
    private async Task HandleErrorAsync(Exception ex)
    {
        ChangeState(SystemState.Error);
        ErrorOccurred?.Invoke(this, ex.Message);

        // 포지션이 있으면 롤백 시도
        if (_positionManager.HasPosition)
        {
            await _rollbackService.ExecuteRollbackAsync(CloseReason.Error);
            _positionManager.MarkAsRolledBack(CloseReason.Error);
        }

        _cooldownService.StartCooldown();
    }

    /// <summary>
    /// 상태 변경 (내부용)
    /// </summary>
    private void ChangeState(SystemState newState)
    {
        if (_state != newState)
        {
            _logger.LogInformation("상태 변경: {OldState} -> {NewState}", _state, newState);
            _state = newState;
            StateChanged?.Invoke(this, newState);
        }
    }

    #region 외부 제어 메서드

    /// <summary>
    /// 트레이딩 시작
    /// </summary>
    public void StartTrading()
    {
        if (_state == SystemState.Ready || _state == SystemState.Cooldown)
        {
            ChangeState(SystemState.Trading);
            _logger.LogInformation("자동매매 시작");
        }
    }

    /// <summary>
    /// 트레이딩 중지
    /// </summary>
    public void StopTrading()
    {
        if (_state == SystemState.Trading)
        {
            ChangeState(SystemState.Ready);
            _logger.LogInformation("자동매매 중지");
        }
    }

    /// <summary>
    /// 수동 포지션 청산
    /// </summary>
    public async Task ManualCloseAsync()
    {
        if (_positionManager.HasPosition)
        {
            await HandleCloseAsync(
                new KimchiPremiumData { Kimchi = _positionManager.CurrentPosition?.EntryKimchi ?? 0 },
                CloseReason.Manual);
        }
    }

    /// <summary>
    /// 상태 초기화 (Ready로 전환)
    /// </summary>
    public void SetReady()
    {
        ChangeState(SystemState.Ready);
    }

    #endregion
}
