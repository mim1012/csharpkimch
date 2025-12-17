using KimchiHedge.Core.Exchanges;
using KimchiHedge.Core.Trading;
using Microsoft.Extensions.Logging;

namespace KimchiHedge.Client.Services;

/// <summary>
/// TradingEngine 생성 및 관리 팩토리 구현체
/// </summary>
public class TradingEngineFactory : ITradingEngineFactory, IDisposable
{
    private readonly IExchangeFactory _exchangeFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<TradingEngineFactory> _logger;
    private readonly object _lock = new();

    private TradingEngine? _engine;
    private ISpotExchange? _spotExchange;
    private IFuturesExchange? _futuresExchange;
    private bool _disposed;

    public TradingEngineFactory(
        IExchangeFactory exchangeFactory,
        ILoggerFactory loggerFactory,
        ISettingsService settingsService)
    {
        _exchangeFactory = exchangeFactory;
        _loggerFactory = loggerFactory;
        _settingsService = settingsService;
        _logger = loggerFactory.CreateLogger<TradingEngineFactory>();
    }

    /// <summary>
    /// TradingEngine 인스턴스
    /// </summary>
    public TradingEngine? Engine => _engine;

    /// <summary>
    /// 초기화 여부
    /// </summary>
    public bool IsInitialized => _engine != null;

    /// <summary>
    /// TradingEngine 초기화
    /// </summary>
    public async Task<bool> InitializeAsync()
    {
        if (_engine != null)
        {
            return true;
        }

        lock (_lock)
        {
            if (_engine != null)
            {
                return true;
            }
        }

        // API 키 확인
        if (!await _exchangeFactory.HasCredentialsAsync())
        {
            _logger.LogWarning("API credentials not found");
            return false;
        }

        try
        {
            // 거래소 인스턴스 생성
            _spotExchange = await _exchangeFactory.CreateSpotExchangeAsync();
            _futuresExchange = await _exchangeFactory.CreateFuturesExchangeAsync();

            if (_spotExchange == null || _futuresExchange == null)
            {
                _logger.LogError("Failed to create exchange instances");
                return false;
            }

            // 의존성 생성
            var positionManager = new PositionManager();
            var cooldownService = new CooldownService(_loggerFactory.CreateLogger<CooldownService>());
            var orderExecutor = new OrderExecutor(
                _spotExchange,
                _futuresExchange,
                _loggerFactory.CreateLogger<OrderExecutor>());
            var rollbackService = new RollbackService(
                _spotExchange,
                _futuresExchange,
                _loggerFactory.CreateLogger<RollbackService>());

            // TradingEngine 생성
            TradingEngine engine;
            lock (_lock)
            {
                engine = new TradingEngine(
                    orderExecutor,
                    positionManager,
                    rollbackService,
                    cooldownService,
                    _loggerFactory.CreateLogger<TradingEngine>());
            }

            // 저장된 설정 로드 및 적용
            await _settingsService.LoadAsync();
            var clientSettings = _settingsService.Settings;

            var coreSettings = new KimchiHedge.Core.Models.TradingSettings
            {
                EntryKimchi = clientSettings.EntryPremium,
                TakeProfitKimchi = clientSettings.TakeProfitPremium,
                StopLossKimchi = clientSettings.StopLossPremium,
                EntryRatio = clientSettings.EntryRatio,
                Leverage = clientSettings.Leverage,
                CooldownSeconds = clientSettings.CooldownMinutes * 60,
                QuantityTolerance = 0.00000001m
            };

            try
            {
                engine.UpdateSettings(coreSettings);
                _logger.LogInformation("TradingEngine settings applied: Entry={Entry}%, TP={TP}%, SL={SL}%",
                    coreSettings.EntryKimchi, coreSettings.TakeProfitKimchi, coreSettings.StopLossKimchi);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Invalid settings, using defaults: {Message}", ex.Message);
                // 기본값 설정
                var defaultSettings = new KimchiHedge.Core.Models.TradingSettings
                {
                    EntryKimchi = 3.5m,
                    TakeProfitKimchi = 2.0m,
                    StopLossKimchi = 5.0m,
                    EntryRatio = 50m,
                    Leverage = 1,
                    CooldownSeconds = 300,
                    QuantityTolerance = 0.00000001m
                };
                engine.UpdateSettings(defaultSettings);
            }

            lock (_lock)
            {
                _engine = engine;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TradingEngine initialization failed");
            // 실패 시 리소스 정리
            await ShutdownAsync();
            return false;
        }
    }

    /// <summary>
    /// TradingEngine 종료 및 리소스 해제
    /// </summary>
    public async Task ShutdownAsync()
    {
        lock (_lock)
        {
            _engine = null;
        }

        if (_spotExchange != null)
        {
            try
            {
                await _spotExchange.DisconnectAsync();
            }
            catch
            {
                // 무시
            }

            if (_spotExchange is IDisposable disposableSpot)
            {
                disposableSpot.Dispose();
            }

            _spotExchange = null;
        }

        if (_futuresExchange != null)
        {
            try
            {
                await _futuresExchange.DisconnectAsync();
            }
            catch
            {
                // 무시
            }

            if (_futuresExchange is IDisposable disposableFutures)
            {
                disposableFutures.Dispose();
            }

            _futuresExchange = null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ShutdownAsync().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }
}
