using System.Security;
using KimchiHedge.Core.Auth.Interfaces;

namespace KimchiHedge.Client.Services.Auth;

/// <summary>
/// 토큰 저장소 - SecureString으로 메모리 보안
/// </summary>
public class TokenStorage : ITokenStorage
{
    private SecureString? _accessToken;
    private SecureString? _refreshToken;
    private DateTime _accessTokenExpiresAt;
    private readonly object _lock = new();

    public bool HasAccessToken => !string.IsNullOrEmpty(GetAccessToken());
    public bool HasRefreshToken => !string.IsNullOrEmpty(GetRefreshToken());
    public bool IsAccessTokenExpired => DateTime.UtcNow >= _accessTokenExpiresAt;

    public void StoreTokens(string accessToken, string refreshToken, int expiresInSeconds)
    {
        lock (_lock)
        {
            SetSecureString(ref _accessToken, accessToken);
            SetSecureString(ref _refreshToken, refreshToken);
            _accessTokenExpiresAt = DateTime.UtcNow.AddSeconds(expiresInSeconds);
        }
    }

    public string? GetAccessToken()
    {
        return GetSecureString(_accessToken);
    }

    public string? GetRefreshToken()
    {
        return GetSecureString(_refreshToken);
    }

    public void UpdateAccessToken(string accessToken, int expiresInSeconds)
    {
        lock (_lock)
        {
            SetSecureString(ref _accessToken, accessToken);
            _accessTokenExpiresAt = DateTime.UtcNow.AddSeconds(expiresInSeconds);
        }
    }

    public void ClearTokens()
    {
        lock (_lock)
        {
            _accessToken?.Dispose();
            _refreshToken?.Dispose();
            _accessToken = null;
            _refreshToken = null;
            _accessTokenExpiresAt = DateTime.MinValue;
        }
    }

    private string? GetSecureString(SecureString? secureString)
    {
        if (secureString == null || secureString.Length == 0)
            return null;

        lock (_lock)
        {
            var ptr = System.Runtime.InteropServices.Marshal.SecureStringToBSTR(secureString);
            try
            {
                return System.Runtime.InteropServices.Marshal.PtrToStringBSTR(ptr);
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.ZeroFreeBSTR(ptr);
            }
        }
    }

    private void SetSecureString(ref SecureString? secureString, string? value)
    {
        secureString?.Dispose();

        if (string.IsNullOrEmpty(value))
        {
            secureString = null;
            return;
        }

        secureString = new SecureString();
        foreach (var c in value)
        {
            secureString.AppendChar(c);
        }
        secureString.MakeReadOnly();
    }
}
