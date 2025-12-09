# 보안 요구사항 명세서

> **문서 버전:** v1.0
> **작성일:** 2025-12-09
> **보안 등급:** 중요

---

## 1. 개요

본 문서는 김치프리미엄 헷지 자동매매 시스템의 보안 요구사항을 정의합니다.

### 1.1 보안 원칙

1. **심층 방어 (Defense in Depth)**: 다중 보안 레이어 적용
2. **최소 권한 원칙**: 필요한 최소 권한만 부여
3. **제로 트러스트**: 모든 요청은 검증 필요
4. **암호화 우선**: 민감 데이터는 항상 암호화

---

## 2. 통신 보안

### 2.1 HTTPS 강제

```csharp
// ❌ 금지: HTTP 통신
HttpClient httpClient = new HttpClient();
httpClient.BaseAddress = new Uri("http://api.example.com"); // 차단됨

// ✅ 필수: HTTPS만 허용
HttpClient httpsClient = new HttpClient();
httpsClient.BaseAddress = new Uri("https://api.example.com");
```

**구현 요구사항:**
- 모든 API 통신은 HTTPS only
- TLS 1.2 이상 필수
- HTTP 요청 자동 차단 (redirect 없음)
- 인증서 검증 필수 (SSL pinning 권장)

### 2.2 Certificate Pinning

```csharp
public class CertificatePinningHandler : HttpClientHandler
{
    private readonly string[] _expectedThumbprints;

    public CertificatePinningHandler(params string[] thumbprints)
    {
        _expectedThumbprints = thumbprints;
        ServerCertificateCustomValidationCallback = ValidateCertificate;
    }

    private bool ValidateCertificate(
        HttpRequestMessage request,
        X509Certificate2 cert,
        X509Chain chain,
        SslPolicyErrors errors)
    {
        if (errors != SslPolicyErrors.None)
            return false;

        string thumbprint = cert.GetCertHashString();
        return _expectedThumbprints.Contains(thumbprint);
    }
}
```

---

## 3. 데이터 암호화

### 3.1 API Key 암호화 (AES-256)

```csharp
public class AesEncryptionService
{
    private const int KeySize = 256;
    private const int BlockSize = 128;
    private const int SaltSize = 32;
    private const int Iterations = 100000;

    public string Encrypt(string plainText, string password)
    {
        byte[] salt = GenerateRandomBytes(SaltSize);
        byte[] iv = GenerateRandomBytes(BlockSize / 8);

        using var key = new Rfc2898DeriveBytes(
            password, salt, Iterations, HashAlgorithmName.SHA256);

        using var aes = Aes.Create();
        aes.KeySize = KeySize;
        aes.BlockSize = BlockSize;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key.GetBytes(KeySize / 8);
        aes.IV = iv;

        using var encryptor = aes.CreateEncryptor();
        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] cipherBytes = encryptor.TransformFinalBlock(
            plainBytes, 0, plainBytes.Length);

        // 형식: salt + iv + ciphertext
        byte[] result = new byte[salt.Length + iv.Length + cipherBytes.Length];
        Buffer.BlockCopy(salt, 0, result, 0, salt.Length);
        Buffer.BlockCopy(iv, 0, result, salt.Length, iv.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, salt.Length + iv.Length, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }
}
```

### 3.2 암호화 대상

| 데이터 | 암호화 방식 | 저장 위치 |
|--------|-------------|-----------|
| 업비트 API Key | AES-256 | 로컬 암호화 파일 |
| 업비트 Secret Key | AES-256 | 로컬 암호화 파일 |
| BingX API Key | AES-256 | 로컬 암호화 파일 |
| BingX Secret Key | AES-256 | 로컬 암호화 파일 |
| Bybit API Key | AES-256 | 로컬 암호화 파일 |
| Bybit Secret Key | AES-256 | 로컬 암호화 파일 |
| 설정 파일 | AES-256 | 로컬 암호화 파일 |
| 사용자 비밀번호 | bcrypt | 서버 DB |

### 3.3 평문 저장 금지

```csharp
// ❌ 절대 금지
public class InsecureConfig
{
    public string UpbitApiKey { get; set; } = "평문키저장금지";
    public string UpbitSecretKey { get; set; } = "평문시크릿금지";
}

// ❌ 절대 금지: .env 파일에 평문 저장
// UPBIT_API_KEY=abcd1234...
// UPBIT_SECRET_KEY=efgh5678...

// ✅ 올바른 방법: 암호화 저장
public class SecureConfig
{
    public string EncryptedUpbitApiKey { get; set; }
    public string EncryptedUpbitSecretKey { get; set; }

    public string GetDecryptedApiKey(string masterPassword)
    {
        return _encryptionService.Decrypt(EncryptedUpbitApiKey, masterPassword);
    }
}
```

---

## 4. 설정 파일 보안

### 4.1 설정 파일 암호화 구조

```
config/
├── settings.encrypted     # AES-256 암호화된 설정
├── settings.encrypted.sig # HMAC 서명 (무결성 검증)
└── key.protected          # DPAPI로 보호된 마스터 키
```

### 4.2 Windows DPAPI 활용

```csharp
public class DpapiProtection
{
    public byte[] Protect(byte[] data)
    {
        return ProtectedData.Protect(
            data,
            null, // 추가 엔트로피 (선택)
            DataProtectionScope.CurrentUser // 현재 사용자만 복호화 가능
        );
    }

    public byte[] Unprotect(byte[] protectedData)
    {
        return ProtectedData.Unprotect(
            protectedData,
            null,
            DataProtectionScope.CurrentUser
        );
    }
}
```

### 4.3 설정 파일 무결성 검증

```csharp
public class ConfigIntegrityChecker
{
    public bool VerifyIntegrity(string configPath, string signaturePath, byte[] hmacKey)
    {
        byte[] configData = File.ReadAllBytes(configPath);
        byte[] storedSignature = File.ReadAllBytes(signaturePath);

        using var hmac = new HMACSHA256(hmacKey);
        byte[] computedSignature = hmac.ComputeHash(configData);

        return CryptographicOperations.FixedTimeEquals(
            storedSignature, computedSignature);
    }
}
```

---

## 5. HWID (Hardware ID) 보안

### 5.1 HWID 생성

```csharp
public class HwidGenerator
{
    public string GenerateHwid()
    {
        var components = new StringBuilder();

        // CPU ID
        components.Append(GetCpuId());

        // 메인보드 시리얼
        components.Append(GetMotherboardSerial());

        // 첫 번째 MAC 주소
        components.Append(GetFirstMacAddress());

        // 시스템 드라이브 시리얼
        components.Append(GetSystemDriveSerial());

        // SHA256 해시
        using var sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(
            Encoding.UTF8.GetBytes(components.ToString()));

        return Convert.ToHexString(hash).ToLower();
    }

    private string GetCpuId()
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT ProcessorId FROM Win32_Processor");
        foreach (var obj in searcher.Get())
        {
            return obj["ProcessorId"]?.ToString() ?? "";
        }
        return "";
    }
}
```

### 5.2 HWID 검증 흐름

```
┌─────────────────────────────────────────────────────────────┐
│                    HWID 검증 프로세스                        │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  [클라이언트]                         [서버]                 │
│       │                                  │                   │
│  HWID 생성 ──────────────────────────► HWID 수신            │
│       │                                  │                   │
│       │                                  ▼                   │
│       │                         DB에서 등록 HWID 조회        │
│       │                                  │                   │
│       │                         ┌────────┴────────┐          │
│       │                         │                 │          │
│       │                    미등록              등록됨         │
│       │                         │                 │          │
│       │                    첫 로그인?         일치 여부       │
│       │                         │                 │          │
│       │                    ┌────┴────┐      ┌────┴────┐      │
│       │                  예         아니오  일치      불일치   │
│       │                    │           │      │         │     │
│       │                 등록      거부    허용      거부     │
│       │                    │           │      │         │     │
│       │                    └─────┬─────┘      │         │     │
│       │                          │            │         │     │
│       │◄─────────────────────────┴────────────┴─────────┘     │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

---

## 6. 인증 토큰 보안

### 6.1 토큰 저장 정책

```csharp
// ❌ 금지: 파일/레지스트리 저장
File.WriteAllText("token.txt", accessToken);
Registry.SetValue(@"HKEY_CURRENT_USER\Software\KimchiHedge", "Token", accessToken);

// ✅ 권장: 메모리에만 보관
public class TokenStorage
{
    private SecureString _accessToken;

    public void StoreToken(string token)
    {
        _accessToken = new SecureString();
        foreach (char c in token)
        {
            _accessToken.AppendChar(c);
        }
        _accessToken.MakeReadOnly();
    }

    public string GetToken()
    {
        IntPtr ptr = Marshal.SecureStringToBSTR(_accessToken);
        try
        {
            return Marshal.PtrToStringBSTR(ptr);
        }
        finally
        {
            Marshal.ZeroFreeBSTR(ptr);
        }
    }
}
```

### 6.2 토큰 만료 처리

| 상황 | 처리 |
|------|------|
| Access Token 만료 | Refresh Token으로 자동 갱신 |
| Refresh Token 만료 | 재로그인 요청 |
| 토큰 무효화 (서버 측) | 즉시 프로그램 종료 |

---

## 7. 메모리 보안

### 7.1 민감 데이터 메모리 처리

```csharp
public class SecureMemory
{
    public static void ClearString(ref string sensitiveData)
    {
        if (sensitiveData == null) return;

        // 문자열 내용을 덮어쓰기 (가능한 경우)
        unsafe
        {
            fixed (char* ptr = sensitiveData)
            {
                for (int i = 0; i < sensitiveData.Length; i++)
                {
                    ptr[i] = '\0';
                }
            }
        }

        sensitiveData = null;
    }

    public static void ClearBytes(byte[] data)
    {
        if (data == null) return;
        CryptographicOperations.ZeroMemory(data);
    }
}
```

### 7.2 SecureString 사용

```csharp
public class PasswordHandler
{
    public SecureString ReadPassword()
    {
        var password = new SecureString();
        ConsoleKeyInfo key;

        do
        {
            key = Console.ReadKey(true);
            if (key.Key != ConsoleKey.Enter)
            {
                password.AppendChar(key.KeyChar);
            }
        } while (key.Key != ConsoleKey.Enter);

        password.MakeReadOnly();
        return password;
    }
}
```

---

## 8. 네트워크 보안

### 8.1 허용된 호스트만 통신

```csharp
public class NetworkSecurityPolicy
{
    private static readonly HashSet<string> AllowedHosts = new()
    {
        "api.kimchi-hedge.com",
        "auth.kimchi-hedge.com",
        "api.upbit.com",
        "open-api.bingx.com",
        "api.bybit.com"
    };

    public bool IsAllowedHost(string host)
    {
        return AllowedHosts.Contains(host.ToLower());
    }

    public void ValidateRequest(HttpRequestMessage request)
    {
        if (!IsAllowedHost(request.RequestUri.Host))
        {
            throw new SecurityException(
                $"Blocked request to unauthorized host: {request.RequestUri.Host}");
        }

        if (request.RequestUri.Scheme != "https")
        {
            throw new SecurityException("HTTP connections are not allowed");
        }
    }
}
```

### 8.2 요청 서명

```csharp
public class RequestSigner
{
    public void SignRequest(HttpRequestMessage request, string secretKey)
    {
        string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        string nonce = Guid.NewGuid().ToString("N");

        string payload = $"{timestamp}{nonce}{request.Method}{request.RequestUri.PathAndQuery}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        byte[] signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        string signature = Convert.ToBase64String(signatureBytes);

        request.Headers.Add("X-Timestamp", timestamp);
        request.Headers.Add("X-Nonce", nonce);
        request.Headers.Add("X-Signature", signature);
    }
}
```

---

## 9. 로깅 보안

### 9.1 민감 정보 마스킹

```csharp
public class SecureLogger
{
    public void Log(string message)
    {
        string sanitized = SanitizeMessage(message);
        _logger.Information(sanitized);
    }

    private string SanitizeMessage(string message)
    {
        // API Key 마스킹
        message = Regex.Replace(
            message,
            @"[A-Za-z0-9]{20,}",
            m => MaskValue(m.Value));

        // 이메일 부분 마스킹
        message = Regex.Replace(
            message,
            @"([a-zA-Z0-9_.+-]+)@([a-zA-Z0-9-]+\.[a-zA-Z0-9-.]+)",
            m => MaskEmail(m.Value));

        return message;
    }

    private string MaskValue(string value)
    {
        if (value.Length <= 8) return "****";
        return value.Substring(0, 4) + "****" + value.Substring(value.Length - 4);
    }

    private string MaskEmail(string email)
    {
        int atIndex = email.IndexOf('@');
        if (atIndex <= 2) return "***@" + email.Substring(atIndex + 1);
        return email.Substring(0, 2) + "***@" + email.Substring(atIndex + 1);
    }
}
```

### 9.2 로그 파일 보호

```csharp
public class LogFileProtection
{
    public void SecureLogFile(string logPath)
    {
        var fileInfo = new FileInfo(logPath);
        var fileSecurity = fileInfo.GetAccessControl();

        // 현재 사용자만 읽기/쓰기 권한
        var currentUser = WindowsIdentity.GetCurrent().User;

        fileSecurity.SetAccessRuleProtection(true, false);
        fileSecurity.AddAccessRule(new FileSystemAccessRule(
            currentUser,
            FileSystemRights.Read | FileSystemRights.Write,
            AccessControlType.Allow));

        fileInfo.SetAccessControl(fileSecurity);
    }
}
```

---

## 10. 보안 체크리스트

### 10.1 개발 단계 체크리스트

- [ ] 모든 API 통신 HTTPS 사용
- [ ] HTTP 통신 코드 존재 여부 검사
- [ ] API Key 평문 저장 코드 없음
- [ ] 하드코딩된 비밀키 없음
- [ ] SecureString 사용 여부
- [ ] 민감 정보 로그 마스킹

### 10.2 배포 전 체크리스트

- [ ] 디버그 로그 비활성화
- [ ] 개발용 인증서 제거
- [ ] 테스트 계정/키 제거
- [ ] 난독화 적용
- [ ] 코드 서명

### 10.3 운영 체크리스트

- [ ] 인증서 만료일 모니터링
- [ ] 비정상 로그인 시도 알림
- [ ] 보안 업데이트 정기 적용
- [ ] 침투 테스트 정기 실시

---

## 11. 취약점 대응

### 11.1 OWASP Top 10 대응

| 취약점 | 대응 방안 |
|--------|-----------|
| Injection | 파라미터 바인딩, 입력 검증 |
| Broken Authentication | 강력한 비밀번호 정책, MFA 고려 |
| Sensitive Data Exposure | AES-256 암호화, HTTPS |
| XML External Entities | XML 파싱 비활성화 또는 안전한 파서 사용 |
| Broken Access Control | 서버 측 권한 검증 |
| Security Misconfiguration | 보안 헤더, 에러 메시지 최소화 |
| XSS | 해당 없음 (데스크톱 앱) |
| Insecure Deserialization | JSON 사용, 타입 검증 |
| Known Vulnerabilities | NuGet 패키지 정기 업데이트 |
| Insufficient Logging | 보안 이벤트 로깅 |

---

## 12. 사고 대응

### 12.1 API Key 유출 시

1. 즉시 해당 거래소에서 API Key 삭제
2. 새 API Key 발급
3. 모든 미체결 주문 확인 및 취소
4. 자산 이동 여부 확인
5. 원인 분석 및 재발 방지

### 12.2 인증 서버 침해 시

1. 서버 격리
2. 모든 토큰 무효화
3. 사용자 비밀번호 강제 리셋
4. 침해 범위 분석
5. 사용자 공지
