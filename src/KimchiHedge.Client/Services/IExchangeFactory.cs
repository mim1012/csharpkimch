using KimchiHedge.Core.Exchanges;

namespace KimchiHedge.Client.Services;

/// <summary>
/// 거래소 인스턴스 생성 팩토리
/// API 키가 있어야 거래소 인스턴스 생성 가능
/// </summary>
public interface IExchangeFactory
{
    /// <summary>
    /// 현물 거래소(업비트) 생성
    /// </summary>
    Task<ISpotExchange?> CreateSpotExchangeAsync();

    /// <summary>
    /// 선물 거래소(BingX) 생성
    /// </summary>
    Task<IFuturesExchange?> CreateFuturesExchangeAsync();

    /// <summary>
    /// API 키가 설정되어 있는지 확인
    /// </summary>
    Task<bool> HasCredentialsAsync();
}
