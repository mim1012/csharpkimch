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
            // 토큰 갱신 시도 (동시 요청 방지)
            await _refreshSemaphore.WaitAsync(cancellationToken);
            try
            {
                // 다른 요청이 이미 토큰을 갱신했을 수 있음
                var currentToken = _tokenStorage.GetAccessToken();
                if (currentToken != accessToken)
                {
                    // 토큰이 갱신됨, 재시도
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", currentToken);
                    return await base.SendAsync(CloneRequest(request), cancellationToken);
                }

                // 토큰 갱신 시도
                var refreshed = await _refreshTokenFunc();
                if (refreshed)
                {
                    // 갱신 성공, 재시도
                    var newRequest = CloneRequest(request);
                    newRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokenStorage.GetAccessToken());
                    return await base.SendAsync(newRequest, cancellationToken);
                }
            }
            finally
            {
                _refreshSemaphore.Release();
            }
        }

        return response;
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);

        if (request.Content != null)
        {
            clone.Content = request.Content;
        }

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
