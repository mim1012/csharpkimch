using KimchiHedge.AuthServer.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace KimchiHedge.AuthServer.Hubs;

/// <summary>
/// 김치프리미엄 실시간 데이터 허브
/// </summary>
[Authorize]
public class KimchiPremiumHub : Hub
{
    private readonly ILogger<KimchiPremiumHub> _logger;

    public KimchiPremiumHub(ILogger<KimchiPremiumHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var email = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;

        _logger.LogInformation("Client connected: {ConnectionId}, User: {Email}",
            Context.ConnectionId, email ?? "unknown");

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var email = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;

        _logger.LogInformation("Client disconnected: {ConnectionId}, User: {Email}, Exception: {Exception}",
            Context.ConnectionId, email ?? "unknown", exception?.Message);

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// 클라이언트가 구독 요청 시 호출
    /// </summary>
    public async Task Subscribe()
    {
        var email = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        _logger.LogInformation("Client subscribed to kimchi premium: {Email}", email);

        await Groups.AddToGroupAsync(Context.ConnectionId, "KimchiPremiumSubscribers");
        await Clients.Caller.SendAsync("Subscribed", new { message = "김치프리미엄 데이터 구독 시작" });
    }

    /// <summary>
    /// 구독 해제
    /// </summary>
    public async Task Unsubscribe()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "KimchiPremiumSubscribers");
        await Clients.Caller.SendAsync("Unsubscribed", new { message = "김치프리미엄 데이터 구독 해제" });
    }
}

/// <summary>
/// SignalR 허브를 통해 데이터를 브로드캐스트하는 서비스
/// </summary>
public class KimchiPremiumBroadcaster
{
    private readonly IHubContext<KimchiPremiumHub> _hubContext;
    private readonly ILogger<KimchiPremiumBroadcaster> _logger;

    public KimchiPremiumBroadcaster(
        IHubContext<KimchiPremiumHub> hubContext,
        ILogger<KimchiPremiumBroadcaster> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// 모든 구독자에게 김치프리미엄 데이터 브로드캐스트
    /// </summary>
    public async Task BroadcastAsync(KimchiPremiumData data)
    {
        _logger.LogDebug("Broadcasting kimchi premium: {Kimchi}%", data.Kimchi);

        await _hubContext.Clients.Group("KimchiPremiumSubscribers")
            .SendAsync("ReceiveKimchiPremium", data);
    }

    /// <summary>
    /// 모든 연결된 클라이언트에게 브로드캐스트 (구독 여부 무관)
    /// </summary>
    public async Task BroadcastToAllAsync(KimchiPremiumData data)
    {
        await _hubContext.Clients.All.SendAsync("ReceiveKimchiPremium", data);
    }
}
