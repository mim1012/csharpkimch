namespace KimchiHedge.Client.Services.Http;

/// <summary>
/// API 엔드포인트 상수
/// </summary>
public static class ApiEndpoints
{
    public const string BaseUrl = "http://localhost:5000";

    public static class Auth
    {
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
