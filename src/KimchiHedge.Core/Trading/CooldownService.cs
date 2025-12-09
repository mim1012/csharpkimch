using Microsoft.Extensions.Logging;

namespace KimchiHedge.Core.Trading;

/// <summary>
/// 쿨다운 서비스 (단일 책임: 쿨다운 타이머 관리만)
/// </summary>
public class CooldownService
{
    private readonly ILogger<CooldownService> _logger;
    private DateTime? _cooldownEndTime;
    private int _cooldownMinutes = 5; // 기본값 5분

    public event EventHandler? CooldownStarted;
    public event EventHandler? CooldownEnded;

    public CooldownService(ILogger<CooldownService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 쿨다운 중 여부
    /// </summary>
    public bool IsInCooldown => _cooldownEndTime.HasValue && DateTime.UtcNow < _cooldownEndTime;

    /// <summary>
    /// 남은 쿨다운 시간
    /// </summary>
    public TimeSpan? RemainingTime => IsInCooldown
        ? _cooldownEndTime!.Value - DateTime.UtcNow
        : null;

    /// <summary>
    /// 남은 쿨다운 시간 (분:초 형식)
    /// </summary>
    public string RemainingTimeFormatted => RemainingTime?.ToString(@"mm\:ss") ?? "00:00";

    /// <summary>
    /// 쿨다운 시간 설정 (분 단위, 1~30분)
    /// </summary>
    public void SetCooldownMinutes(int minutes)
    {
        if (minutes < 1 || minutes > 30)
        {
            throw new ArgumentOutOfRangeException(nameof(minutes), "쿨다운은 1~30분 사이여야 합니다.");
        }
        _cooldownMinutes = minutes;
    }

    /// <summary>
    /// 쿨다운 시작
    /// </summary>
    public void StartCooldown()
    {
        _cooldownEndTime = DateTime.UtcNow.AddMinutes(_cooldownMinutes);
        _logger.LogInformation("쿨다운 시작. {Minutes}분 후 재진입 가능", _cooldownMinutes);

        CooldownStarted?.Invoke(this, EventArgs.Empty);

        // 쿨다운 종료 타이머
        _ = WaitForCooldownEndAsync();
    }

    /// <summary>
    /// 쿨다운 강제 종료
    /// </summary>
    public void CancelCooldown()
    {
        if (IsInCooldown)
        {
            _cooldownEndTime = null;
            _logger.LogInformation("쿨다운 강제 종료");
            CooldownEnded?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// 쿨다운 종료 대기
    /// </summary>
    private async Task WaitForCooldownEndAsync()
    {
        if (!_cooldownEndTime.HasValue) return;

        var delay = _cooldownEndTime.Value - DateTime.UtcNow;
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay);
        }

        // 쿨다운이 취소되지 않았으면 종료 이벤트 발생
        if (_cooldownEndTime.HasValue && DateTime.UtcNow >= _cooldownEndTime.Value)
        {
            _cooldownEndTime = null;
            _logger.LogInformation("쿨다운 종료. 재진입 가능");
            CooldownEnded?.Invoke(this, EventArgs.Empty);
        }
    }
}
