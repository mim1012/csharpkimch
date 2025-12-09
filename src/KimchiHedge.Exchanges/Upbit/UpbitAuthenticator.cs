using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace KimchiHedge.Exchanges.Upbit;

/// <summary>
/// 업비트 JWT 인증 토큰 생성기
/// </summary>
public class UpbitAuthenticator
{
    private readonly string _accessKey;
    private readonly string _secretKey;

    public UpbitAuthenticator(string accessKey, string secretKey)
    {
        _accessKey = accessKey;
        _secretKey = secretKey;
    }

    /// <summary>
    /// 쿼리 파라미터 없는 요청용 JWT 토큰 생성
    /// </summary>
    public string CreateToken()
    {
        var payload = new JwtPayload
        {
            { "access_key", _accessKey },
            { "nonce", Guid.NewGuid().ToString() }
        };

        return GenerateToken(payload);
    }

    /// <summary>
    /// 쿼리 파라미터가 있는 요청용 JWT 토큰 생성
    /// </summary>
    public string CreateToken(Dictionary<string, string> queryParams)
    {
        var queryString = string.Join("&", queryParams.Select(p => $"{p.Key}={p.Value}"));
        var queryHash = ComputeSha512Hash(queryString);

        var payload = new JwtPayload
        {
            { "access_key", _accessKey },
            { "nonce", Guid.NewGuid().ToString() },
            { "query_hash", queryHash },
            { "query_hash_alg", "SHA512" }
        };

        return GenerateToken(payload);
    }

    /// <summary>
    /// Body가 있는 POST 요청용 JWT 토큰 생성
    /// </summary>
    public string CreateTokenWithBody(string body)
    {
        var queryHash = ComputeSha512Hash(body);

        var payload = new JwtPayload
        {
            { "access_key", _accessKey },
            { "nonce", Guid.NewGuid().ToString() },
            { "query_hash", queryHash },
            { "query_hash_alg", "SHA512" }
        };

        return GenerateToken(payload);
    }

    private string GenerateToken(JwtPayload payload)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var header = new JwtHeader(credentials);
        var token = new JwtSecurityToken(header, payload);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string ComputeSha512Hash(string input)
    {
        var bytes = SHA512.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
