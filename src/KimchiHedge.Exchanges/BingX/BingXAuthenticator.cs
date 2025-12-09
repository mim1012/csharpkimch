using System.Security.Cryptography;
using System.Text;

namespace KimchiHedge.Exchanges.BingX;

/// <summary>
/// BingX HMAC SHA256 서명 생성기
/// </summary>
public class BingXAuthenticator
{
    private readonly string _apiKey;
    private readonly string _secretKey;

    public string ApiKey => _apiKey;

    public BingXAuthenticator(string apiKey, string secretKey)
    {
        _apiKey = apiKey;
        _secretKey = secretKey;
    }

    /// <summary>
    /// 현재 타임스탬프 (밀리초)
    /// </summary>
    public static long GetTimestamp() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>
    /// 쿼리 파라미터 서명 생성
    /// </summary>
    public string Sign(string queryString)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(queryString));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// 파라미터에 timestamp와 signature 추가
    /// </summary>
    public string SignParameters(Dictionary<string, string> parameters)
    {
        // timestamp 추가
        parameters["timestamp"] = GetTimestamp().ToString();

        // 쿼리 스트링 생성 (알파벳 순 정렬)
        var sortedParams = parameters.OrderBy(p => p.Key);
        var queryString = string.Join("&", sortedParams.Select(p => $"{p.Key}={p.Value}"));

        // 서명 생성
        var signature = Sign(queryString);

        // signature 추가
        return $"{queryString}&signature={signature}";
    }

    /// <summary>
    /// GET 요청용 서명된 쿼리 스트링 생성
    /// </summary>
    public string CreateSignedQueryString(Dictionary<string, string>? parameters = null)
    {
        parameters ??= new Dictionary<string, string>();
        return SignParameters(parameters);
    }
}
