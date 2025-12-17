namespace KimchiHedge.Client.Services.Http;

/// <summary>
/// API 엔드포인트 상수
/// </summary>
public static class ApiEndpoints
{
    /// <summary>
    /// API 서버 기본 URL (App.xaml.cs에서 빌드 구성에 따라 설정)
    /// </summary>
    public static string BaseUrl { get; set; } = "http://localhost:5000";

    public static class Auth
    {
        public const string Register = "/api/v1/auth/register";
        public const string Login = "/api/v1/auth/login";
        public const string Refresh = "/api/v1/auth/refresh";
        public const string Logout = "/api/v1/auth/logout";
    }

    public static class License
    {
        public const string Status = "/api/v1/license/status";
        public const string Heartbeat = "/api/v1/license/heartbeat";
    }
}
