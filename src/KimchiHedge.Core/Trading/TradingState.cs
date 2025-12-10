namespace KimchiHedge.Core.Trading;

/// <summary>
/// 트레이딩 상태 머신 상태 정의
/// </summary>
public enum TradingState
{
    /// <summary>
    /// 자동매매 OFF, 포지션 없음
    /// </summary>
    Idle,

    /// <summary>
    /// 자동매매 ON, 진입 조건 대기 중
    /// </summary>
    WaitEntry,

    /// <summary>
    /// 진입 중 (업비트 매수 -> BingX 숏)
    /// </summary>
    Entering,

    /// <summary>
    /// 포지션 보유 중 (헷지 완료)
    /// </summary>
    PositionOpen,

    /// <summary>
    /// 청산 중 (양쪽 매도/청산)
    /// </summary>
    Exiting,

    /// <summary>
    /// 청산 후 재진입 금지 기간
    /// </summary>
    Cooldown,

    /// <summary>
    /// 오류 발생, 강제 롤백 진행 중
    /// </summary>
    ErrorRollback
}

/// <summary>
/// 트레이딩 이벤트 정의
/// </summary>
public enum TradingEvent
{
    /// <summary>
    /// 자동매매 시작 버튼
    /// </summary>
    ToggleOn,

    /// <summary>
    /// 자동매매 중지 버튼 (수동 청산 포함)
    /// </summary>
    ToggleOff,

    /// <summary>
    /// 서버에서 김프 값 수신
    /// </summary>
    KimchiUpdate,

    /// <summary>
    /// 업비트 매수 체결 완료
    /// </summary>
    UpbitFilled,

    /// <summary>
    /// BingX 숏 체결 완료
    /// </summary>
    BingXFilled,

    /// <summary>
    /// 업비트 매도 체결 완료
    /// </summary>
    UpbitCloseFilled,

    /// <summary>
    /// BingX 숏 청산 체결 완료
    /// </summary>
    BingXCloseFilled,

    /// <summary>
    /// 타임아웃 (체결 지연, 쿨다운 종료 등)
    /// </summary>
    Timeout,

    /// <summary>
    /// 오류 발생 (주문 실패, 수량 불일치, API 에러)
    /// </summary>
    Error
}

/// <summary>
/// 청산 이유
/// </summary>
public enum CloseReason
{
    /// <summary>
    /// 익절 (kimchi <= tp_k)
    /// </summary>
    TakeProfit,

    /// <summary>
    /// 손절 (kimchi >= sl_k)
    /// </summary>
    StopLoss,

    /// <summary>
    /// 수동 청산
    /// </summary>
    Manual,

    /// <summary>
    /// 롤백 (오류로 인한 강제 청산)
    /// </summary>
    Rollback,

    /// <summary>
    /// 오류
    /// </summary>
    Error
}

/// <summary>
/// 상태 전이 결과
/// </summary>
public class StateTransitionResult
{
    public bool Success { get; set; }
    public TradingState PreviousState { get; set; }
    public TradingState NewState { get; set; }
    public string? ErrorMessage { get; set; }

    public static StateTransitionResult Ok(TradingState prev, TradingState next)
        => new() { Success = true, PreviousState = prev, NewState = next };

    public static StateTransitionResult Fail(TradingState current, string error)
        => new() { Success = false, PreviousState = current, NewState = current, ErrorMessage = error };
}
