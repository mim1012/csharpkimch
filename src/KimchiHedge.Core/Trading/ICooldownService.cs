namespace KimchiHedge.Core.Trading;

/// <summary>
/// 쿨다운 서비스 인터페이스
/// </summary>
public interface ICooldownService
{
    /// <summary>
    /// 쿨다운 종료 이벤트
    /// </summary>
    event EventHandler? CooldownEnded;

    /// <summary>
    /// 쿨다운 시간 설정 (초 단위)
    /// </summary>
    void SetCooldownSeconds(int seconds);

    /// <summary>
    /// 쿨다운 시작
    /// </summary>
    void Start();

    /// <summary>
    /// 쿨다운 취소
    /// </summary>
    void Cancel();

    /// <summary>
    /// 남은 쿨다운 시간 (초)
    /// </summary>
    int RemainingSeconds { get; }
}
