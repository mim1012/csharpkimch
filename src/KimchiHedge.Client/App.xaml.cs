using System.Windows;
using KimchiHedge.Client.Services.Auth;
using KimchiHedge.Client.Services.Http;
using KimchiHedge.Core.Auth.Interfaces;
using KimchiHedge.Core.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KimchiHedge.Client;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 빌드 구성에 따른 API BaseUrl 설정
#if DEBUG
        ApiEndpoints.BaseUrl = "http://localhost:5000";
#else
        ApiEndpoints.BaseUrl = Environment.GetEnvironmentVariable("KIMCHI_API_URL") ?? "https://api.kimchihedge.com";
#endif

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
        Services = _serviceProvider;
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Logging
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // Core Services
        services.AddSingleton<HwidGenerator>();

        // Auth Services
        services.AddSingleton<ITokenStorage, TokenStorage>();

        // HTTP Client with Auth Handler
        services.AddSingleton<Func<Task<bool>>>(sp =>
        {
            return async () =>
            {
                var authService = sp.GetRequiredService<IAuthService>();
                var result = await authService.RefreshTokenAsync();
                return result.Success;
            };
        });

        services.AddTransient<AuthHttpHandler>(sp =>
        {
            var tokenStorage = sp.GetRequiredService<ITokenStorage>();
            var refreshFunc = sp.GetRequiredService<Func<Task<bool>>>();
            return new AuthHttpHandler(tokenStorage, refreshFunc);
        });

        services.AddHttpClient<ApiClient>(client =>
        {
            client.BaseAddress = new Uri(ApiEndpoints.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        })
        .AddHttpMessageHandler<AuthHttpHandler>();

        services.AddSingleton<IAuthService, AuthService>();

        // Settings Service
        services.AddSingleton<Services.ISettingsService, Services.SettingsService>();
        services.AddSingleton<Services.ISecureStorage, Services.SecureStorage>();

        // PriceService용 HttpClient (IHttpClientFactory 사용)
        services.AddHttpClient("PriceService", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddSingleton<Services.IPriceService, Services.PriceService>();

        // Trading Services (Factory 패턴 - API 키 설정 후 Lazy 초기화)
        services.AddSingleton<Services.IExchangeFactory, Services.ExchangeFactory>();
        services.AddSingleton<Services.ITradingEngineFactory, Services.TradingEngineFactory>();

        // ViewModels
        services.AddTransient<ViewModels.LoginViewModel>();
        services.AddTransient<ViewModels.RegisterViewModel>();
        services.AddTransient<ViewModels.MainViewModel>();
        services.AddSingleton<ViewModels.DashboardViewModel>();
        services.AddSingleton<ViewModels.SettingsViewModel>();
        services.AddSingleton<ViewModels.ApiKeysViewModel>();
        services.AddSingleton<ViewModels.HistoryViewModel>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Cleanup
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        base.OnExit(e);
    }
}
