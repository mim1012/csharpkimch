namespace KimchiHedge.Core.Models;

/// <summary>
/// 주문 상태
/// </summary>
public enum OrderStatus
{
    Pending,        // 대기 중
    Filled,         // 체결 완료
    PartiallyFilled,// 부분 체결
    Cancelled,      // 취소됨
    Failed          // 실패
}

/// <summary>
/// 주문 방향
/// </summary>
public enum OrderSide
{
    Buy,
    Sell
}

/// <summary>
/// 주문 결과
/// </summary>
public class OrderResult
{
    /// <summary>
    /// 주문 ID
    /// </summary>
    public string OrderId { get; set; } = string.Empty;

    /// <summary>
    /// 거래소명
    /// </summary>
    public string Exchange { get; set; } = string.Empty;

    /// <summary>
    /// 심볼 (예: BTC/KRW, BTCUSDT)
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// 주문 방향
    /// </summary>
    public OrderSide Side { get; set; }

    /// <summary>
    /// 주문 상태
    /// </summary>
    public OrderStatus Status { get; set; }

    /// <summary>
    /// 체결 수량
    /// </summary>
    public decimal ExecutedQuantity { get; set; }

    /// <summary>
    /// 평균 체결가
    /// </summary>
    public decimal AveragePrice { get; set; }

    /// <summary>
    /// 체결 금액
    /// </summary>
    public decimal ExecutedValue => ExecutedQuantity * AveragePrice;

    /// <summary>
    /// 수수료
    /// </summary>
    public decimal Fee { get; set; }

    /// <summary>
    /// 수수료 통화
    /// </summary>
    public string FeeCurrency { get; set; } = string.Empty;

    /// <summary>
    /// 주문 시간
    /// </summary>
    public DateTime OrderTime { get; set; }

    /// <summary>
    /// 체결 시간
    /// </summary>
    public DateTime? FilledTime { get; set; }

    /// <summary>
    /// 에러 메시지
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 성공 여부 (기본값: Status가 Filled인 경우 true)
    /// </summary>
    private bool? _isSuccess;
    public bool IsSuccess
    {
        get => _isSuccess ?? Status == OrderStatus.Filled;
        set => _isSuccess = value;
    }

    /// <summary>
    /// 실패 결과 생성
    /// </summary>
    public static OrderResult Failure(string exchange, string symbol, string errorMessage)
    {
        return new OrderResult
        {
            Exchange = exchange,
            Symbol = symbol,
            Status = OrderStatus.Failed,
            ErrorMessage = errorMessage,
            OrderTime = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 실패 결과 생성 (에러 메시지만)
    /// </summary>
    public static OrderResult Failure(string errorMessage)
    {
        return new OrderResult
        {
            Status = OrderStatus.Failed,
            ErrorMessage = errorMessage,
            OrderTime = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 실패 결과 생성 (거래소 + 에러 메시지)
    /// </summary>
    public static OrderResult Failure(string exchange, string errorMessage)
    {
        return new OrderResult
        {
            Exchange = exchange,
            Status = OrderStatus.Failed,
            ErrorMessage = errorMessage,
            OrderTime = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 성공 결과 생성
    /// </summary>
    public static OrderResult Success(decimal quantity, decimal price, decimal fee = 0)
    {
        return new OrderResult
        {
            Status = OrderStatus.Filled,
            ExecutedQuantity = quantity,
            AveragePrice = price,
            Fee = fee,
            OrderTime = DateTime.UtcNow,
            FilledTime = DateTime.UtcNow
        };
    }
}
