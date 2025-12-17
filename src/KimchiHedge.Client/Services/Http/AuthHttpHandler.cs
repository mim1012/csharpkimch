using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using KimchiHedge.Core.Auth.Interfaces;

namespace KimchiHedge.Client.Services.Http;

/// <summary>
/// HTTP 요청에 인증 헤더 추가 및 토큰 갱신 처리
/// </summary>
public class AuthHttpHandler : DelegatingHandler
{
    private readonly ITokenStorage _tokenStorage;
    private readonly Func<Task<bool>> _refreshTokenFunc;
    private readonly SemaphoreSlim _refreshSemaphore = new(1, 1);

    public AuthHttpHandler(ITokenStorage tokenStorage, Func<Task<bool>> refreshTokenFunc)
    {
        _tokenStorage = tokenStorage;
        _refreshTokenFunc = refreshTokenFunc;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Content가 있으면 재시도를 위해 미리 버퍼링
        byte[]? contentBytes = null;
        string? contentType = null;
        if (request.Content != null)
        {
            contentBytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            contentType = request.Content.Headers.ContentType?.ToString();
        }

        // 인증 헤더 추가
        var accessToken = _tokenStorage.GetAccessToken();
        if (!string.IsNullOrEmpty(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        var response = await base.SendAsync(request, cancellationToken);

        // 401 응답시 토큰 갱신 시도
        if (response.StatusCode == HttpStatusCode.Unauthorized && _tokenStorage.HasRefreshToken)
        {
            await _refreshSemaphore.WaitAsync(cancellationToken);
            try
            {
                // 다른 요청이 이미 토큰을 갱신했을 수 있음
                var currentToken = _tokenStorage.GetAccessToken();
                if (currentToken != accessToken)
                {
                    // 토큰이 갱신됨, 재시도
                    var retryRequest = CloneRequest(request, contentBytes, contentType);
                    retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", currentToken);
                    return await base.SendAsync(retryRequest, cancellationToken);
                }

                // 토큰 갱신 시도
                var refreshed = await _refreshTokenFunc();
                if (refreshed)
                {
                    // 갱신 성공, 재시도
                    var retryRequest = CloneRequest(request, contentBytes, contentType);
                    retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokenStorage.GetAccessToken());
                    return await base.SendAsync(retryRequest, cancellationToken);
                }
            }
            finally
            {
                _refreshSemaphore.Release();
            }
        }

        return response;
    }

    private static HttpRequestMessage CloneRequest(
        HttpRequestMessage request,
        byte[]? contentBytes,
        string? contentType)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);

        // Content 복제 (새 ByteArrayContent 생성)
        if (contentBytes != null)
        {
            clone.Content = new ByteArrayContent(contentBytes);
            if (!string.IsNullOrEmpty(contentType))
            {
                clone.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
            }
        }

        // 헤더 복제 (Authorization 제외 - 호출자가 설정)
        foreach (var header in request.Headers)
        {
            if (!header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }
}
