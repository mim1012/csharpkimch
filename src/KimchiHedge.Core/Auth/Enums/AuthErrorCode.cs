namespace KimchiHedge.Core.Auth.Enums;

/// <summary>
/// 인증 에러 코드
/// </summary>
public enum AuthErrorCode
{
    // 인증 관련 (AUTH_*)
    AUTH_001_INVALID_CREDENTIALS,
    AUTH_002_TOKEN_EXPIRED,
    AUTH_003_TOKEN_INVALID,
    AUTH_004_REFRESH_TOKEN_EXPIRED,
    AUTH_005_REFRESH_TOKEN_INVALID,
    AUTH_006_VALIDATION_ERROR,
    AUTH_007_EMAIL_EXISTS,

    // 라이선스 관련 (LIC_*)
    LIC_001_LICENSE_EXPIRED,
    LIC_002_LICENSE_SUSPENDED,
    LIC_003_APPROVAL_PENDING,

    // HWID 관련 (HWID_*)
    HWID_001_MISMATCH,
    HWID_002_NOT_REGISTERED,

    // 서버 관련 (SRV_*)
    SRV_001_INTERNAL_ERROR,
    SRV_002_SERVICE_UNAVAILABLE,

    // 일반 (GEN_*)
    GEN_001_UNKNOWN_ERROR,
    GEN_002_VALIDATION_ERROR,
    GEN_003_RATE_LIMITED
}

/// <summary>
/// 에러 코드 확장 메서드
/// </summary>
public static class AuthErrorCodeExtensions
{
    public static string ToMessage(this AuthErrorCode code) => code switch
    {
        AuthErrorCode.AUTH_001_INVALID_CREDENTIALS => "아이디 또는 비밀번호가 올바르지 않습니다.",
        AuthErrorCode.AUTH_002_TOKEN_EXPIRED => "토큰이 만료되었습니다.",
        AuthErrorCode.AUTH_003_TOKEN_INVALID => "유효하지 않은 토큰입니다.",
        AuthErrorCode.AUTH_004_REFRESH_TOKEN_EXPIRED => "리프레시 토큰이 만료되었습니다. 다시 로그인해주세요.",
        AuthErrorCode.AUTH_005_REFRESH_TOKEN_INVALID => "유효하지 않은 리프레시 토큰입니다.",
        AuthErrorCode.AUTH_006_VALIDATION_ERROR => "입력값이 올바르지 않습니다.",
        AuthErrorCode.AUTH_007_EMAIL_EXISTS => "이미 등록된 이메일입니다.",
        AuthErrorCode.LIC_001_LICENSE_EXPIRED => "라이선스가 만료되었습니다.",
        AuthErrorCode.LIC_002_LICENSE_SUSPENDED => "라이선스가 정지되었습니다. 관리자에게 문의하세요.",
        AuthErrorCode.LIC_003_APPROVAL_PENDING => "가입 승인 대기 중입니다.",
        AuthErrorCode.HWID_001_MISMATCH => "등록된 PC와 다른 PC에서 접속했습니다.",
        AuthErrorCode.HWID_002_NOT_REGISTERED => "PC가 등록되지 않았습니다.",
        AuthErrorCode.SRV_001_INTERNAL_ERROR => "서버 오류가 발생했습니다.",
        AuthErrorCode.SRV_002_SERVICE_UNAVAILABLE => "서비스를 일시적으로 사용할 수 없습니다.",
        AuthErrorCode.GEN_001_UNKNOWN_ERROR => "알 수 없는 오류가 발생했습니다.",
        AuthErrorCode.GEN_002_VALIDATION_ERROR => "입력값이 올바르지 않습니다.",
        AuthErrorCode.GEN_003_RATE_LIMITED => "요청이 너무 많습니다. 잠시 후 다시 시도해주세요.",
        _ => "오류가 발생했습니다."
    };

    public static int ToHttpStatus(this AuthErrorCode code) => code switch
    {
        AuthErrorCode.AUTH_001_INVALID_CREDENTIALS => 401,
        AuthErrorCode.AUTH_002_TOKEN_EXPIRED => 401,
        AuthErrorCode.AUTH_003_TOKEN_INVALID => 401,
        AuthErrorCode.AUTH_004_REFRESH_TOKEN_EXPIRED => 401,
        AuthErrorCode.AUTH_005_REFRESH_TOKEN_INVALID => 401,
        AuthErrorCode.AUTH_006_VALIDATION_ERROR => 400,
        AuthErrorCode.AUTH_007_EMAIL_EXISTS => 400,
        AuthErrorCode.LIC_001_LICENSE_EXPIRED => 403,
        AuthErrorCode.LIC_002_LICENSE_SUSPENDED => 403,
        AuthErrorCode.LIC_003_APPROVAL_PENDING => 403,
        AuthErrorCode.HWID_001_MISMATCH => 403,
        AuthErrorCode.HWID_002_NOT_REGISTERED => 403,
        AuthErrorCode.SRV_001_INTERNAL_ERROR => 500,
        AuthErrorCode.SRV_002_SERVICE_UNAVAILABLE => 503,
        AuthErrorCode.GEN_003_RATE_LIMITED => 429,
        _ => 500
    };
}
