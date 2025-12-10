namespace KimchiHedge.Core.Trading;

/// <summary>
/// 롤백 서비스 인터페이스
/// </summary>
public interface IRollbackService
{
    /// <summary>
    /// 롤백 완료 이벤트
    /// </summary>
    event EventHandler? RollbackCompleted;

    /// <summary>
    /// 롤백 실패 이벤트
    /// </summary>
    event EventHandler<string>? RollbackFailed;

    /// <summary>
    /// 롤백 실행 (업비트 매도 + BingX 청산)
    /// </summary>
    Task ExecuteRollbackAsync();
}
