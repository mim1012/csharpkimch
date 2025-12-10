namespace KimchiHedge.Core.Trading;

/// <summary>
/// 개별 주문 실행 결과
/// </summary>
public class OrderResult
{
    public bool IsSuccess { get; set; }
    public decimal ExecutedQuantity { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal Fee { get; set; }
    public string? ErrorMessage { get; set; }

    public static OrderResult Success(decimal quantity, decimal price, decimal fee = 0)
        => new() { IsSuccess = true, ExecutedQuantity = quantity, AveragePrice = price, Fee = fee };

    public static OrderResult Failure(string error)
        => new() { IsSuccess = false, ErrorMessage = error };
}

/// <summary>
/// 주문 실행 인터페이스
/// </summary>
public interface IOrderExecutor
{
    /// <summary>
    /// 업비트 시장가 매수
    /// </summary>
    /// <param name="entryRatio">시드 대비 진입 비율 (%)</param>
    Task<OrderResult> ExecuteUpbitBuyAsync(decimal entryRatio);

    /// <summary>
    /// 레버리지 설정
    /// </summary>
    Task SetLeverageAsync(int leverage);

    /// <summary>
    /// BingX 시장가 숏 진입
    /// </summary>
    /// <param name="quantity">수량 (BTC)</param>
    Task<OrderResult> ExecuteBingXShortAsync(decimal quantity);

    /// <summary>
    /// 업비트 전량 시장가 매도
    /// </summary>
    Task<OrderResult> ExecuteUpbitSellAllAsync();

    /// <summary>
    /// BingX 숏 포지션 전량 청산
    /// </summary>
    Task<OrderResult> ExecuteBingXCloseAsync();
}
