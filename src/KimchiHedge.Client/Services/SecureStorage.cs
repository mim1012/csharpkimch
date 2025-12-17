using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace KimchiHedge.Client.Services;

/// <summary>
/// 보안 저장소 인터페이스
/// </summary>
public interface ISecureStorage
{
    Task<ApiCredentials> LoadCredentialsAsync();
    Task SaveCredentialsAsync(ApiCredentials credentials);
}

/// <summary>
/// API 자격 증명
/// </summary>
public class ApiCredentials
{
    public string UpbitAccessKey { get; set; } = string.Empty;
    public string UpbitSecretKey { get; set; } = string.Empty;
    public string BingxApiKey { get; set; } = string.Empty;
    public string BingxSecretKey { get; set; } = string.Empty;
}

/// <summary>
/// DPAPI를 사용한 보안 저장소 구현
/// </summary>
public class SecureStorage : ISecureStorage
{
    private static readonly string StorageFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "KimchiHedge");

    private static readonly string CredentialsFile = Path.Combine(StorageFolder, "credentials.enc");

    public async Task<ApiCredentials> LoadCredentialsAsync()
    {
        try
        {
            if (!File.Exists(CredentialsFile))
            {
                return new ApiCredentials();
            }

            var encryptedBytes = await File.ReadAllBytesAsync(CredentialsFile);
            var decryptedBytes = ProtectedData.Unprotect(
                encryptedBytes,
                null,
                DataProtectionScope.CurrentUser);

            var json = Encoding.UTF8.GetString(decryptedBytes);
            return JsonSerializer.Deserialize<ApiCredentials>(json) ?? new ApiCredentials();
        }
        catch
        {
            return new ApiCredentials();
        }
    }

    public async Task SaveCredentialsAsync(ApiCredentials credentials)
    {
        try
        {
            // 폴더 생성
            if (!Directory.Exists(StorageFolder))
            {
                Directory.CreateDirectory(StorageFolder);
            }

            var json = JsonSerializer.Serialize(credentials);
            var jsonBytes = Encoding.UTF8.GetBytes(json);
            var encryptedBytes = ProtectedData.Protect(
                jsonBytes,
                null,
                DataProtectionScope.CurrentUser);

            await File.WriteAllBytesAsync(CredentialsFile, encryptedBytes);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"자격 증명 저장 실패: {ex.Message}", ex);
        }
    }
}
