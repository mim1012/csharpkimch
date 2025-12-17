using KimchiHedge.Core.Exchanges;
using KimchiHedge.Exchanges.BingX;
using KimchiHedge.Exchanges.Upbit;
using Microsoft.Extensions.Logging;

namespace KimchiHedge.Client.Services;

/// <summary>
/// 거래소 인스턴스 생성 팩토리 구현체
/// SecureStorage에서 API 키를 로드하여 거래소 인스턴스 생성
/// </summary>
public class ExchangeFactory : IExchangeFactory
{
    private readonly ISecureStorage _secureStorage;
    private readonly ILoggerFactory _loggerFactory;

    public ExchangeFactory(ISecureStorage secureStorage, ILoggerFactory loggerFactory)
    {
        _secureStorage = secureStorage;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// 현물 거래소(업비트) 생성
    /// </summary>
    public async Task<ISpotExchange?> CreateSpotExchangeAsync()
    {
        var credentials = await _secureStorage.LoadCredentialsAsync();

        if (string.IsNullOrEmpty(credentials.UpbitAccessKey) ||
            string.IsNullOrEmpty(credentials.UpbitSecretKey))
        {
            return null;
        }

        var logger = _loggerFactory.CreateLogger<UpbitSpotExchange>();
        var exchange = new UpbitSpotExchange(credentials.UpbitAccessKey, credentials.UpbitSecretKey, logger);
        await exchange.ConnectAsync();
        return exchange;
    }

    /// <summary>
    /// 선물 거래소(BingX) 생성
    /// </summary>
    public async Task<IFuturesExchange?> CreateFuturesExchangeAsync()
    {
        var credentials = await _secureStorage.LoadCredentialsAsync();

        if (string.IsNullOrEmpty(credentials.BingxApiKey) ||
            string.IsNullOrEmpty(credentials.BingxSecretKey))
        {
            return null;
        }

        var logger = _loggerFactory.CreateLogger<BingXFuturesExchange>();
        var exchange = new BingXFuturesExchange(credentials.BingxApiKey, credentials.BingxSecretKey, logger);
        await exchange.ConnectAsync();
        return exchange;
    }

    /// <summary>
    /// API 키가 설정되어 있는지 확인
    /// </summary>
    public async Task<bool> HasCredentialsAsync()
    {
        var credentials = await _secureStorage.LoadCredentialsAsync();

        bool hasUpbit = !string.IsNullOrEmpty(credentials.UpbitAccessKey) &&
                        !string.IsNullOrEmpty(credentials.UpbitSecretKey);
        bool hasBingx = !string.IsNullOrEmpty(credentials.BingxApiKey) &&
                        !string.IsNullOrEmpty(credentials.BingxSecretKey);

        return hasUpbit && hasBingx;
    }
}
