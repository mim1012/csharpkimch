using KimchiHedge.Core.Models;

namespace KimchiHedge.Core.Trading;

/// <summary>
/// 조건 평가 결과
/// </summary>
public enum TradingAction
{
    None,           // 아무 행동 안함
    Enter,          // 진입
    TakeProfit,     // 익절
    StopLoss        // 손절
}

/// <summary>
/// 진입/익절/손절 조건 평가 (단일 책임: 조건 판단만)
/// </summary>
public class ConditionEvaluator
{
    private TradingSettings _settings = new();

    /// <summary>
    /// 설정 업데이트
    /// </summary>
    public void UpdateSettings(TradingSettings settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// 현재 김프 값으로 어떤 행동을 취해야 하는지 평가
    /// </summary>
    /// <param name="currentKimchi">현재 김치프리미엄 (%)</param>
    /// <param name="hasPosition">현재 포지션 보유 여부</param>
    /// <returns>취해야 할 행동</returns>
    public TradingAction Evaluate(decimal currentKimchi, bool hasPosition)
    {
        if (!hasPosition)
        {
            // 포지션 없음 → 진입 조건 확인
            if (ShouldEnter(currentKimchi))
            {
                return TradingAction.Enter;
            }
        }
        else
        {
            // 포지션 있음 → 익절/손절 조건 확인
            if (ShouldTakeProfit(currentKimchi))
            {
                return TradingAction.TakeProfit;
            }
            if (ShouldStopLoss(currentKimchi))
            {
                return TradingAction.StopLoss;
            }
        }

        return TradingAction.None;
    }

    /// <summary>
    /// 진입 조건: kimchi >= 진입값
    /// </summary>
    private bool ShouldEnter(decimal currentKimchi)
    {
        return currentKimchi >= _settings.EntryKimchi;
    }

    /// <summary>
    /// 익절 조건: kimchi <= 익절값
    /// </summary>
    private bool ShouldTakeProfit(decimal currentKimchi)
    {
        return currentKimchi <= _settings.TakeProfitKimchi;
    }

    /// <summary>
    /// 손절 조건: kimchi >= 손절값
    /// </summary>
    private bool ShouldStopLoss(decimal currentKimchi)
    {
        return currentKimchi >= _settings.StopLossKimchi;
    }
}
