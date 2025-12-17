using System.IO;
using System.Text.Json;

namespace KimchiHedge.Client.Services;

/// <summary>
/// 설정 서비스 인터페이스
/// </summary>
public interface ISettingsService
{
    TradingSettings Settings { get; }
    Task LoadAsync();
    Task SaveAsync(TradingSettings settings);
}

/// <summary>
/// 트레이딩 설정
/// </summary>
public class TradingSettings
{
    /// <summary>
    /// 진입 김프 (%)
    /// </summary>
    public decimal EntryPremium { get; set; } = 3.50m;

    /// <summary>
    /// 익절 김프 (%)
    /// </summary>
    public decimal TakeProfitPremium { get; set; } = 2.00m;

    /// <summary>
    /// 손절 김프 (%)
    /// </summary>
    public decimal StopLossPremium { get; set; } = 5.00m;

    /// <summary>
    /// 진입 비율 (%)
    /// </summary>
    public decimal EntryRatio { get; set; } = 50m;

    /// <summary>
    /// 레버리지
    /// </summary>
    public int Leverage { get; set; } = 1;

    /// <summary>
    /// 쿨다운 (분)
    /// </summary>
    public int CooldownMinutes { get; set; } = 5;
}

/// <summary>
/// 설정 서비스 구현
/// </summary>
public class SettingsService : ISettingsService
{
    private static readonly string SettingsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "KimchiHedge");

    private static readonly string SettingsFile = Path.Combine(SettingsFolder, "settings.json");

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public TradingSettings Settings { get; private set; } = new();

    public async Task LoadAsync()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                var json = await File.ReadAllTextAsync(SettingsFile);
                Settings = JsonSerializer.Deserialize<TradingSettings>(json, _jsonOptions) ?? new();
            }
        }
        catch
        {
            Settings = new TradingSettings();
        }
    }

    public async Task SaveAsync(TradingSettings settings)
    {
        try
        {
            // 폴더 생성
            if (!Directory.Exists(SettingsFolder))
            {
                Directory.CreateDirectory(SettingsFolder);
            }

            var json = JsonSerializer.Serialize(settings, _jsonOptions);
            await File.WriteAllTextAsync(SettingsFile, json);
            Settings = settings;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"설정 저장 실패: {ex.Message}", ex);
        }
    }
}
