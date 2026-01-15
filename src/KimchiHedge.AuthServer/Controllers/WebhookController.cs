using KimchiHedge.AuthServer.Hubs;
using KimchiHedge.AuthServer.Models;
using KimchiHedge.AuthServer.Services;
using KimchiHedge.Core.Auth.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KimchiHedge.AuthServer.Controllers;

/// <summary>
/// 외부 Webhook 수신 컨트롤러
/// </summary>
[ApiController]
[Route("api/v1/webhook")]
public class WebhookController : ControllerBase
{
    private readonly KimchiPremiumBroadcaster _broadcaster;
    private readonly ILogger<WebhookController> _logger;
    private readonly IConfiguration _configuration;

    // 최근 수신된 데이터 저장 (메모리 캐시)
    private static KimchiPremiumData? _latestData;
    private static readonly object _lock = new();

    public WebhookController(
        KimchiPremiumBroadcaster broadcaster,
        ILogger<WebhookController> logger,
        IConfiguration configuration)
    {
        _broadcaster = broadcaster;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// 김치프리미엄 데이터 수신 (외부 서비스에서 호출)
    /// </summary>
    /// <remarks>
    /// 예시 요청:
    /// POST /api/v1/webhook/kimchi
    /// {
    ///   "kimchi": 3.21,
    ///   "upbit": 102300000,
    ///   "global": 70300,
    ///   "timestamp": 1700000000
    /// }
    /// </remarks>
    [HttpPost("kimchi")]
    [AllowAnonymous] // 외부 서비스에서 호출하므로 인증 없음 (API Key로 검증)
    public async Task<ActionResult<ApiResponse<object>>> ReceiveKimchiPremium(
        [FromBody] KimchiPremiumData data,
        [FromHeader(Name = "X-Webhook-Secret")] string? webhookSecret = null)
    {
        // Webhook Secret 검증 (선택적)
        var expectedSecret = _configuration["Webhook:Secret"];
        if (!string.IsNullOrEmpty(expectedSecret) && webhookSecret != expectedSecret)
        {
            _logger.LogWarning("Invalid webhook secret from {IP}", GetClientIpAddress());
            return Unauthorized(ApiResponse<object>.Fail("WEBHOOK_001", "Invalid webhook secret"));
        }

        // 데이터 유효성 검증
        if (data.Kimchi < -100 || data.Kimchi > 100)
        {
            return BadRequest(ApiResponse<object>.Fail("WEBHOOK_002", "Invalid kimchi value"));
        }

        // 수신 시각 설정
        data.ReceivedAt = DateTime.UtcNow;

        // 최신 데이터 저장
        lock (_lock)
        {
            _latestData = data;
        }

        _logger.LogInformation(
            "Received kimchi premium: {Kimchi}%, Upbit: {Upbit}, Global: {Global}, Timestamp: {Timestamp}",
            data.Kimchi, data.Upbit, data.Global, data.Timestamp);

        // 연결된 모든 클라이언트에게 브로드캐스트
        await _broadcaster.BroadcastToAllAsync(data);

        return Ok(ApiResponse<object>.Ok(new
        {
            received = true,
            kimchi = data.Kimchi,
            broadcastedAt = data.ReceivedAt
        }));
    }

    /// <summary>
    /// 최신 김치프리미엄 데이터 조회
    /// </summary>
    [HttpGet("kimchi/latest")]
    [AllowAnonymous]
    public ActionResult<ApiResponse<KimchiPremiumData>> GetLatestKimchiPremium()
    {
        lock (_lock)
        {
            if (_latestData == null)
            {
                return NotFound(ApiResponse<KimchiPremiumData>.Fail("WEBHOOK_003", "No data available"));
            }

            return Ok(ApiResponse<KimchiPremiumData>.Ok(_latestData));
        }
    }

    /// <summary>
    /// Webhook 상태 확인
    /// </summary>
    [HttpGet("health")]
    [AllowAnonymous]
    public ActionResult<object> Health()
    {
        var pollingData = KimchiPremiumPollingService.GetLatestData();

        return Ok(new
        {
            status = "healthy",
            endpoint = "/api/v1/webhook/kimchi",
            hasLatestData = _latestData != null || pollingData != null,
            latestDataTimestamp = pollingData?.ReceivedAt ?? _latestData?.ReceivedAt,
            dataSource = pollingData != null ? "polling" : (_latestData != null ? "webhook" : "none")
        });
    }

    /// <summary>
    /// Lambda Polling 서비스에서 수신한 최신 데이터 조회
    /// </summary>
    [HttpGet("kimchi/polling")]
    [AllowAnonymous]
    public ActionResult<ApiResponse<object>> GetPollingData()
    {
        var data = KimchiPremiumPollingService.GetLatestData();
        var rawData = KimchiPremiumPollingService.GetLatestRawData();

        if (data == null || rawData == null)
        {
            return NotFound(ApiResponse<object>.Fail("POLLING_001", "No polling data available"));
        }

        return Ok(ApiResponse<object>.Ok(new
        {
            kimchiPremiumPercent = rawData.KimchiPremiumPercent,
            upbitBtcKrw = rawData.UpbitBtcKrw,
            bingxBtcUsd = rawData.BingxBtcUsd,
            bingxBtcKrw = rawData.BingxBtcKrw,
            exchangeRateUsdKrw = rawData.ExchangeRateUsdKrw,
            timestamp = rawData.Timestamp,
            receivedAt = data.ReceivedAt,
            source = "lambda-polling"
        }));
    }

    private string? GetClientIpAddress()
    {
        var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',').FirstOrDefault()?.Trim();
        }
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}
