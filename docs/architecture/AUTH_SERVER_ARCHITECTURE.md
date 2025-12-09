# 인증 서버 아키텍처 설계

> **문서 버전:** v1.0
> **작성일:** 2025-12-09
> **상태:** 확정

---

## 1. 시스템 개요

### 1.1 아키텍처 다이어그램

```
┌─────────────────────────────────────────────────────────────────────┐
│                         전체 시스템 구조                              │
├─────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  ┌──────────────┐     HTTPS      ┌──────────────────────────────┐  │
│  │              │◄──────────────►│                              │  │
│  │   Windows    │                │       인증 서버 (Auth)        │  │
│  │   EXE        │                │  ┌────────────────────────┐  │  │
│  │   (C#)       │                │  │ - 로그인/토큰 발급      │  │  │
│  │              │                │  │ - 라이센스 검증         │  │  │
│  └──────────────┘                │  │ - HWID 매칭            │  │  │
│         │                        │  └────────────────────────┘  │  │
│         │                        └──────────────────────────────┘  │
│         │                                      │                    │
│         │                                      │ DB                 │
│         │                                      ▼                    │
│         │                        ┌──────────────────────────────┐  │
│         │                        │         Database             │  │
│         │                        │  - Users                     │  │
│         │                        │  - Licenses                  │  │
│         │                        │  - Sessions                  │  │
│         │                        │  - AuditLogs                 │  │
│         │                        └──────────────────────────────┘  │
│         │                                                           │
│         │ WebSocket/HTTPS        ┌──────────────────────────────┐  │
│         └───────────────────────►│      김프 서버 (Kimchi)       │  │
│                                  │  - 실시간 김프 계산           │  │
│                                  │  - 시세 수집                  │  │
│                                  └──────────────────────────────┘  │
│                                                                      │
│  ┌──────────────┐                ┌──────────────────────────────┐  │
│  │   관리자      │     HTTPS     │       Admin API              │  │
│  │   웹 페이지   │◄─────────────►│  - 사용자 관리               │  │
│  │              │                │  - 라이센스 관리              │  │
│  └──────────────┘                └──────────────────────────────┘  │
│                                                                      │
└─────────────────────────────────────────────────────────────────────┘
```

### 1.2 컴포넌트 설명

| 컴포넌트 | 기술 스택 | 역할 |
|----------|-----------|------|
| Windows EXE | C# (.NET 8) | 자동매매 클라이언트 |
| 인증 서버 | C# ASP.NET Core / Node.js | 인증, 라이센스 관리 |
| 관리자 페이지 | React / Blazor | 운영자 전용 웹 |
| Database | PostgreSQL / SQLite | 사용자, 라이센스 저장 |
| 김프 서버 | TBD | 실시간 김프 데이터 제공 |

---

## 2. 인증 흐름

### 2.1 로그인 시퀀스

```
┌─────────┐                    ┌─────────────┐                    ┌──────────┐
│   EXE   │                    │  Auth API   │                    │    DB    │
└────┬────┘                    └──────┬──────┘                    └────┬─────┘
     │                                │                                 │
     │  1. POST /auth/login           │                                 │
     │  {email, password, hwid}       │                                 │
     │───────────────────────────────►│                                 │
     │                                │                                 │
     │                                │  2. 사용자 조회                  │
     │                                │────────────────────────────────►│
     │                                │                                 │
     │                                │  3. 사용자 정보 반환             │
     │                                │◄────────────────────────────────│
     │                                │                                 │
     │                                │  4. 비밀번호 검증                │
     │                                │  5. 라이센스 상태 확인            │
     │                                │  6. HWID 검증/등록               │
     │                                │                                 │
     │  7. 인증 결과                   │                                 │
     │  {token, license_status,       │                                 │
     │   expires_at, user_info}       │                                 │
     │◄───────────────────────────────│                                 │
     │                                │                                 │
```

### 2.2 인증 상태별 처리

| 상태 | HTTP 코드 | 클라이언트 동작 |
|------|-----------|-----------------|
| 인증 성공 (Active) | 200 | 정상 실행 |
| 비밀번호 오류 | 401 | 에러 메시지 표시 |
| 라이센스 만료 (Expired) | 403 | "라이센스 만료" 안내 후 종료 |
| 계정 정지 (Suspended) | 403 | "계정 정지됨" 안내 후 종료 |
| HWID 불일치 | 403 | "다른 PC에서 사용 중" 안내 후 종료 |
| 서버 오류 | 500 | 재시도 또는 종료 |

---

## 3. 토큰 관리

### 3.1 JWT 토큰 구조

```json
{
  "header": {
    "alg": "HS256",
    "typ": "JWT"
  },
  "payload": {
    "sub": "user_uid",
    "email": "user@example.com",
    "license_status": "Active",
    "license_expires": "2025-12-31T23:59:59Z",
    "hwid": "hashed_hwid",
    "iat": 1702123456,
    "exp": 1702209856
  }
}
```

### 3.2 토큰 갱신 정책

| 항목 | 값 |
|------|-----|
| Access Token 유효기간 | 24시간 |
| Refresh Token 유효기간 | 30일 |
| 자동 갱신 시점 | 만료 1시간 전 |
| 최대 동시 세션 | 1개 (HWID 기반) |

---

## 4. 라이센스 관리

### 4.1 라이센스 상태 정의

```csharp
public enum LicenseStatus
{
    Pending,    // 승인 대기 (신규 가입)
    Active,     // 활성 (정상 사용 가능)
    Expired,    // 만료됨
    Suspended   // 정지됨 (관리자 조치)
}
```

### 4.2 라이센스 검증 로직

```
┌─────────────────────────────────────────────────────────────┐
│                    라이센스 검증 플로우                       │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  [시작] ─► 토큰 유효성 검증 ─► 유효하지 않음 ─► [종료]        │
│              │                                               │
│              ▼ 유효함                                        │
│                                                              │
│  라이센스 상태 확인 ─► Pending ─► "승인 대기 중" ─► [종료]    │
│              │                                               │
│              ├──► Expired ─► "라이센스 만료" ─► [종료]        │
│              │                                               │
│              ├──► Suspended ─► "계정 정지" ─► [종료]          │
│              │                                               │
│              ▼ Active                                        │
│                                                              │
│  HWID 검증 ─► 불일치 ─► "다른 PC 사용 중" ─► [종료]           │
│              │                                               │
│              ▼ 일치                                          │
│                                                              │
│  만료일 확인 ─► 만료됨 ─► 상태 Expired로 변경 ─► [종료]       │
│              │                                               │
│              ▼ 유효함                                        │
│                                                              │
│            [인증 성공 - 프로그램 실행]                        │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

---

## 5. HWID (Hardware ID) 관리

### 5.1 HWID 생성 요소

```csharp
// HWID 구성 요소 (조합하여 해시)
public class HardwareInfo
{
    public string CpuId { get; set; }           // CPU 일련번호
    public string MotherboardId { get; set; }   // 메인보드 일련번호
    public string MacAddress { get; set; }      // 첫 번째 네트워크 MAC
    public string DiskSerialNumber { get; set; } // 시스템 드라이브 시리얼
}

// HWID = SHA256(CpuId + MotherboardId + MacAddress + DiskSerialNumber)
```

### 5.2 HWID 정책

| 정책 | 설명 |
|------|------|
| 최초 등록 | 첫 로그인 시 자동 등록 |
| 변경 요청 | 관리자 승인 필요 |
| 초기화 주기 | 수동 또는 라이센스 갱신 시 |
| 다중 PC | 기본 비허용 (1:1 매칭) |

---

## 6. 주기적 검증

### 6.1 하트비트 메커니즘

```
클라이언트 (EXE)                 서버 (Auth API)
     │                                │
     │ ─── 하트비트 (5분 간격) ──────► │
     │     {token, hwid, timestamp}   │
     │                                │
     │ ◄── 검증 결과 ──────────────── │
     │     {valid, remaining_days}    │
     │                                │
```

### 6.2 오프라인 허용 정책

| 항목 | 값 |
|------|-----|
| 오프라인 허용 시간 | 24시간 |
| 마지막 인증 시간 저장 | 로컬 암호화 |
| 오프라인 만료 시 | 재인증 필요 |

---

## 7. 관리자 기능

### 7.1 관리자 페이지 기능 목록

| 기능 | 설명 |
|------|------|
| 사용자 목록 조회 | 전체 사용자 리스트, 검색, 필터 |
| UID 기반 조회 | 특정 사용자 상세 정보 |
| 승인/거절 | Pending 상태 사용자 처리 |
| 라이센스 만료일 설정 | 개별 사용자 만료일 지정 |
| 상태 변경 | Active ↔ Suspended 전환 |
| HWID 초기화 | PC 변경 시 HWID 리셋 |
| 활동 로그 조회 | 로그인/로그아웃 이력 |

### 7.2 관리자 API 엔드포인트

```
GET    /admin/users              # 사용자 목록
GET    /admin/users/:uid         # 사용자 상세
POST   /admin/users/:uid/approve # 사용자 승인
POST   /admin/users/:uid/reject  # 사용자 거절
PATCH  /admin/users/:uid/status  # 상태 변경
PATCH  /admin/users/:uid/license # 라이센스 만료일 설정
POST   /admin/users/:uid/reset-hwid # HWID 초기화
GET    /admin/audit-logs         # 활동 로그
```

---

## 8. 데이터베이스 스키마

### 8.1 Users 테이블

```sql
CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    uid VARCHAR(20) UNIQUE NOT NULL,        -- 표시용 ID (예: USR-001)
    email VARCHAR(255) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    license_status VARCHAR(20) DEFAULT 'Pending',
    license_expires_at TIMESTAMP,
    hwid VARCHAR(64),
    hwid_registered_at TIMESTAMP,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW(),
    last_login_at TIMESTAMP,
    is_admin BOOLEAN DEFAULT FALSE
);
```

### 8.2 Sessions 테이블

```sql
CREATE TABLE sessions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID REFERENCES users(id),
    access_token TEXT NOT NULL,
    refresh_token TEXT NOT NULL,
    hwid VARCHAR(64) NOT NULL,
    ip_address VARCHAR(45),
    user_agent TEXT,
    created_at TIMESTAMP DEFAULT NOW(),
    expires_at TIMESTAMP NOT NULL,
    is_active BOOLEAN DEFAULT TRUE
);
```

### 8.3 AuditLogs 테이블

```sql
CREATE TABLE audit_logs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID REFERENCES users(id),
    action VARCHAR(50) NOT NULL,       -- LOGIN, LOGOUT, LICENSE_CHECK, etc.
    result VARCHAR(20) NOT NULL,       -- SUCCESS, FAILED
    ip_address VARCHAR(45),
    hwid VARCHAR(64),
    details JSONB,
    created_at TIMESTAMP DEFAULT NOW()
);
```

---

## 9. 보안 고려사항

### 9.1 통신 보안

| 항목 | 구현 |
|------|------|
| 프로토콜 | HTTPS only (HTTP 차단) |
| TLS 버전 | 1.2 이상 |
| 인증서 | Let's Encrypt 또는 상용 |
| Certificate Pinning | 클라이언트에서 검증 |

### 9.2 토큰 보안

| 항목 | 구현 |
|------|------|
| 서명 알고리즘 | HS256 (비밀키 256bit 이상) |
| 토큰 저장 | 메모리 (디스크 저장 금지) |
| 갱신 토큰 | HttpOnly, Secure 플래그 |

### 9.3 비밀번호 정책

| 항목 | 요구사항 |
|------|----------|
| 해시 알고리즘 | bcrypt (cost 12) |
| 최소 길이 | 8자 |
| 복잡성 | 영문 + 숫자 + 특수문자 |
| 재사용 금지 | 최근 3개 |

---

## 10. 에러 처리

### 10.1 에러 코드 정의

| 코드 | 설명 |
|------|------|
| AUTH_001 | 이메일 또는 비밀번호 오류 |
| AUTH_002 | 토큰 만료됨 |
| AUTH_003 | 토큰 무효함 |
| LIC_001 | 라이센스 만료됨 |
| LIC_002 | 라이센스 정지됨 |
| LIC_003 | 승인 대기 중 |
| HWID_001 | HWID 불일치 |
| HWID_002 | HWID 미등록 |
| SRV_001 | 서버 내부 오류 |

---

## 다음 단계

1. API 상세 설계 (`/docs/api_design/`)
2. 클라이언트 인증 모듈 개발
3. 관리자 페이지 개발
