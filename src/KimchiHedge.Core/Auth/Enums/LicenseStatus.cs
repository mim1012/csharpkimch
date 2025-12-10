namespace KimchiHedge.Core.Auth.Enums;

/// <summary>
/// 라이선스 상태
/// </summary>
public enum LicenseStatus
{
    /// <summary>
    /// 승인 대기 중 (신규 가입)
    /// </summary>
    Pending,

    /// <summary>
    /// 활성 (정상 사용 가능)
    /// </summary>
    Active,

    /// <summary>
    /// 만료됨
    /// </summary>
    Expired,

    /// <summary>
    /// 정지됨 (관리자에 의해)
    /// </summary>
    Suspended
}
