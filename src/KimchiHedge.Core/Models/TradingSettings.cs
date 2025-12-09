namespace KimchiHedge.Core.Models;

/// <summary>
/// 자동매매 설정
/// </summary>
public class TradingSettings
{
    /// <summary>
    /// 진입 김치프리미엄 값 (%)
    /// </summary>
    public decimal EntryKimchi { get; set; }

    /// <summary>
    /// 익절 김치프리미엄 값 (%)
    /// </summary>
    public decimal TakeProfitKimchi { get; set; }

    /// <summary>
    /// 손절 김치프리미엄 값 (%)
    /// </summary>
    public decimal StopLossKimchi { get; set; }

    /// <summary>
    /// 시드 대비 진입 비율 (%)
    /// </summary>
    public decimal EntryRatio { get; set; }

    /// <summary>
    /// 레버리지 배율 (1~2배)
    /// </summary>
    public int Leverage { get; set; } = 1;

    /// <summary>
    /// 쿨다운 시간 (분) - 기본값 5분, 범위 1~30분
    /// </summary>
    public int CooldownMinutes { get; set; } = 5;

    /// <summary>
    /// 쿨다운 시간을 초로 변환
    /// </summary>
    public int CooldownSeconds => CooldownMinutes * 60;

    /// <summary>
    /// 설정 유효성 검증
    /// </summary>
    public bool Validate(out string? errorMessage)
    {
        if (EntryKimchi <= 0)
        {
            errorMessage = "진입 김프값은 0보다 커야 합니다.";
            return false;
        }

        if (TakeProfitKimchi >= EntryKimchi)
        {
            errorMessage = "익절 김프값은 진입값보다 작아야 합니다.";
            return false;
        }

        if (StopLossKimchi <= EntryKimchi)
        {
            errorMessage = "손절 김프값은 진입값보다 커야 합니다.";
            return false;
        }

        if (EntryRatio <= 0 || EntryRatio > 100)
        {
            errorMessage = "진입 비율은 0~100% 사이여야 합니다.";
            return false;
        }

        if (Leverage < 1 || Leverage > 2)
        {
            errorMessage = "레버리지는 1~2배 사이여야 합니다.";
            return false;
        }

        if (CooldownMinutes < 1 || CooldownMinutes > 30)
        {
            errorMessage = "쿨다운 시간은 1~30분 사이여야 합니다.";
            return false;
        }

        errorMessage = null;
        return true;
    }
}
