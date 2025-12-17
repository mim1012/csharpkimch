using KimchiHedge.Core.Trading;

namespace KimchiHedge.Client.Services;

/// <summary>
/// TradingEngine 생성 및 관리 팩토리
/// API 키 설정 후 Lazy 초기화
/// </summary>
public interface ITradingEngineFactory
{
    /// <summary>
    /// TradingEngine 인스턴스 (초기화 전에는 null)
    /// </summary>
    TradingEngine? Engine { get; }

    /// <summary>
    /// 초기화 여부
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// TradingEngine 초기화
    /// API 키가 없으면 false 반환
    /// </summary>
    Task<bool> InitializeAsync();

    /// <summary>
    /// TradingEngine 종료 및 리소스 해제
    /// </summary>
    Task ShutdownAsync();
}
