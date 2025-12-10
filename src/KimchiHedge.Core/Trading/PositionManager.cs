using KimchiHedge.Core.Models;

namespace KimchiHedge.Core.Trading;

/// <summary>
/// 포지션 관리 (단일 책임: 포지션 상태 관리만)
/// </summary>
public class PositionManager : IPositionManager
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
    public void CreatePosition(decimal entryKimchi)
    {
        _currentPosition = new Position
        {
            Status = PositionStatus.Opening,
            EntryKimchi = entryKimchi,
            EntryTime = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 포지션 진입 완료 처리
    /// </summary>
    public void CompleteEntry(
        decimal upbitAmount,
        decimal upbitPrice,
        decimal bingxAmount,
        decimal bingxPrice,
        decimal upbitFee,
        decimal bingxFee)
    {
        if (_currentPosition == null) return;

        _currentPosition.UpbitBtcAmount = upbitAmount;
        _currentPosition.UpbitEntryPrice = upbitPrice;
        _currentPosition.UpbitFee = upbitFee;
        _currentPosition.FuturesShortAmount = bingxAmount;
        _currentPosition.FuturesEntryPrice = bingxPrice;
        _currentPosition.FuturesFee = bingxFee;
        _currentPosition.Status = PositionStatus.Open;

        PositionOpened?.Invoke(this, _currentPosition);
    }

    /// <summary>
    /// 포지션 청산 완료 처리
    /// </summary>
    public void CompleteClose(
        decimal closeKimchi,
        CloseReason reason,
        decimal upbitSellPrice,
        decimal bingxClosePrice,
        decimal upbitFee,
        decimal bingxFee)
    {
        if (_currentPosition == null) return;

        _currentPosition.Status = PositionStatus.Closed;
        _currentPosition.CloseTime = DateTime.UtcNow;
        _currentPosition.CloseKimchi = closeKimchi;
        _currentPosition.CloseReason = reason;
        _currentPosition.UpbitExitPrice = upbitSellPrice;
        _currentPosition.FuturesExitPrice = bingxClosePrice;
        _currentPosition.UpbitExitFee = upbitFee;
        _currentPosition.FuturesExitFee = bingxFee;

        // 손익 계산
        _currentPosition.RealizedPnL = CalculatePnL();

        var closedPosition = _currentPosition;
        _currentPosition = null;

        PositionClosed?.Invoke(this, closedPosition);
    }

    /// <summary>
    /// 손익 계산
    /// </summary>
    private decimal CalculatePnL()
    {
        if (_currentPosition == null) return 0;

        // 업비트 손익: (매도가 - 매수가) * 수량 - 수수료
        var upbitPnL = (_currentPosition.UpbitExitPrice - _currentPosition.UpbitEntryPrice)
                       * _currentPosition.UpbitBtcAmount
                       - _currentPosition.UpbitFee
                       - _currentPosition.UpbitExitFee;

        // BingX 손익: (진입가 - 청산가) * 수량 - 수수료 (숏이므로 방향 반대)
        // USD로 계산 후 KRW 환산 필요 (간단히 업비트 가격 기준으로 환산)
        var bingxPnLUsd = (_currentPosition.FuturesEntryPrice - _currentPosition.FuturesExitPrice)
                          * _currentPosition.FuturesShortAmount
                          - _currentPosition.FuturesFee
                          - _currentPosition.FuturesExitFee;

        // USD->KRW 환산 (업비트 매도가 기준)
        var usdToKrw = _currentPosition.UpbitExitPrice / (_currentPosition.FuturesExitPrice > 0 ? _currentPosition.FuturesExitPrice : 1);
        var bingxPnLKrw = bingxPnLUsd * usdToKrw;

        return upbitPnL + bingxPnLKrw;
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
