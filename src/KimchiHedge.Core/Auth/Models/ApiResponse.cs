using KimchiHedge.Core.Auth.Enums;

namespace KimchiHedge.Core.Auth.Models;

/// <summary>
/// API 응답 래퍼
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public ApiError? Error { get; set; }

    public static ApiResponse<T> Ok(T? data, string? message = null) => new()
    {
        Success = true,
        Data = data
    };

    public static ApiResponse<T> Fail(AuthErrorCode code, string? detail = null) => new()
    {
        Success = false,
        Error = new ApiError
        {
            Code = code.ToString(),
            Message = code.ToMessage(),
            Detail = detail
        }
    };

    public static ApiResponse<T> Fail(string code, string message, string? detail = null) => new()
    {
        Success = false,
        Error = new ApiError
        {
            Code = code,
            Message = message,
            Detail = detail
        }
    };
}

/// <summary>
/// API 에러 정보
/// </summary>
public class ApiError
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Detail { get; set; }
}
