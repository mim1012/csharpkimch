using KimchiHedge.Core.Models;

namespace KimchiHedge.Core.Trading;

/// <summary>
/// 포지션 관리 인터페이스
/// </summary>
public interface IPositionManager
{
    /// <summary>
    /// 현재 포지션
    /// </summary>
    Position? CurrentPosition { get; }

    /// <summary>
    /// 포지션 보유 여부
    /// </summary>
    bool HasPosition { get; }

    /// <summary>
    /// 새 포지션 생성 (진입 시작)
    /// </summary>
    void CreatePosition(decimal entryKimchi);

    /// <summary>
    /// 포지션 진입 완료 처리
    /// </summary>
    void CompleteEntry(
        decimal upbitAmount,
        decimal upbitPrice,
        decimal bingxAmount,
        decimal bingxPrice,
        decimal upbitFee,
        decimal bingxFee);

    /// <summary>
    /// 포지션 청산 완료 처리
    /// </summary>
    void CompleteClose(
        decimal closeKimchi,
        CloseReason reason,
        decimal upbitSellPrice,
        decimal bingxClosePrice,
        decimal upbitFee,
        decimal bingxFee);

    /// <summary>
    /// 포지션 강제 클리어
    /// </summary>
    void Clear();
}
