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

        // ViewModels
        services.AddTransient<ViewModels.LoginViewModel>();
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
