using KimchiHedge.Core.Models;

namespace KimchiHedge.Core.Trading;

/// <summary>
/// 포지션 관리 (단일 책임: 포지션 상태 관리만)
/// </summary>
public class PositionManager
{
    private Position? _currentPosition;

    public event EventHandler<Position>? PositionOpened;
    public event EventHandler<Position>? PositionClosed;

    /// <summary>
    /// 현재 포지션
    /// </summary>
    public Position? CurrentPosition => _currentPosition;

    /// <summary>
    /// 포지션 보유 여부
    /// </summary>
    public bool HasPosition => _currentPosition?.Status == PositionStatus.Open;

    /// <summary>
    /// 새 포지션 생성 (진입 시작)
    /// </summary>
    public Position CreatePosition(decimal entryKimchi)
    {
        _currentPosition = new Position
        {
            Status = PositionStatus.Opening,
            EntryKimchi = entryKimchi,
            EntryTime = DateTime.UtcNow
        };
        return _currentPosition;
    }

    /// <summary>
    /// 포지션 진입 완료 처리
    /// </summary>
    public void CompleteEntry(ExecutionResult executionResult)
    {
        if (_currentPosition == null) return;

        _currentPosition.UpbitBtcAmount = executionResult.UpbitBtcAmount;
        _currentPosition.UpbitEntryPrice = executionResult.UpbitEntryPrice;
        _currentPosition.UpbitFee = executionResult.UpbitFee;
        _currentPosition.FuturesShortAmount = executionResult.FuturesShortAmount;
        _currentPosition.FuturesEntryPrice = executionResult.FuturesEntryPrice;
        _currentPosition.FuturesFee = executionResult.FuturesFee;
        _currentPosition.Status = PositionStatus.Open;

        PositionOpened?.Invoke(this, _currentPosition);
    }

    /// <summary>
    /// 포지션 청산 완료 처리
    /// </summary>
    public void CompleteClose(decimal closeKimchi, CloseReason reason)
    {
        if (_currentPosition == null) return;

        _currentPosition.Status = PositionStatus.Closed;
        _currentPosition.CloseTime = DateTime.UtcNow;
        _currentPosition.CloseKimchi = closeKimchi;
        _currentPosition.CloseReason = reason;

        // TODO: 손익 계산
        // _currentPosition.RealizedPnL = CalculatePnL();

        var closedPosition = _currentPosition;
        _currentPosition = null;

        PositionClosed?.Invoke(this, closedPosition);
    }

    /// <summary>
    /// 포지션 롤백 처리
    /// </summary>
    public void MarkAsRolledBack(CloseReason reason)
    {
        if (_currentPosition == null) return;

        _currentPosition.Status = PositionStatus.Rollback;
        _currentPosition.CloseTime = DateTime.UtcNow;
        _currentPosition.CloseReason = reason;

        var rolledBackPosition = _currentPosition;
        _currentPosition = null;

        PositionClosed?.Invoke(this, rolledBackPosition);
    }

    /// <summary>
    /// 포지션 상태 변경
    /// </summary>
    public void SetStatus(PositionStatus status)
    {
        if (_currentPosition != null)
        {
            _currentPosition.Status = status;
        }
    }

    /// <summary>
    /// 포지션 강제 클리어 (비상용)
    /// </summary>
    public void Clear()
    {
        _currentPosition = null;
    }
}
