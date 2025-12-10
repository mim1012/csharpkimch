using Microsoft.Extensions.Logging;

namespace KimchiHedge.Core.Trading;

/// <summary>
/// 쿨다운 서비스 (단일 책임: 쿨다운 타이머 관리만)
/// </summary>
public class CooldownService : ICooldownService
{
    private readonly ILogger<CooldownService> _logger;
    private DateTime? _cooldownEndTime;
    private int _cooldownSeconds = 300; // 기본값 5분 (300초)

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
    /// 남은 쿨다운 시간 (초)
    /// </summary>
    public int RemainingSeconds => (int)(RemainingTime?.TotalSeconds ?? 0);

    /// <summary>
    /// 남은 쿨다운 시간 (분:초 형식)
    /// </summary>
    public string RemainingTimeFormatted => RemainingTime?.ToString(@"mm\:ss") ?? "00:00";

    /// <summary>
    /// 쿨다운 시간 설정 (초 단위)
    /// </summary>
    public void SetCooldownSeconds(int seconds)
    {
        if (seconds < 60 || seconds > 1800) // 1분 ~ 30분
        {
            throw new ArgumentOutOfRangeException(nameof(seconds), "쿨다운은 60~1800초(1~30분) 사이여야 합니다.");
        }
        _cooldownSeconds = seconds;
    }

    /// <summary>
    /// 쿨다운 시작
    /// </summary>
    public void Start()
    {
        _cooldownEndTime = DateTime.UtcNow.AddSeconds(_cooldownSeconds);
        _logger.LogInformation("쿨다운 시작. {Seconds}초 후 재진입 가능", _cooldownSeconds);

        CooldownStarted?.Invoke(this, EventArgs.Empty);

        // 쿨다운 종료 타이머
        _ = WaitForCooldownEndAsync();
    }

    /// <summary>
    /// 쿨다운 취소
    /// </summary>
    public void Cancel()
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
