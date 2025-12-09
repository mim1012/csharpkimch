# 인증 서버 API 명세서

> **문서 버전:** v1.0
> **작성일:** 2025-12-09
> **Base URL:** `https://api.kimchi-hedge.com/v1`

---

## 1. 인증 API (Authentication)

### 1.1 로그인

**Endpoint:** `POST /auth/login`

**Request:**
```json
{
  "email": "user@example.com",
  "password": "SecurePassword123!",
  "hwid": "a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6"
}
```

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "access_token": "eyJhbGciOiJIUzI1NiIs...",
    "refresh_token": "eyJhbGciOiJIUzI1NiIs...",
    "token_type": "Bearer",
    "expires_in": 86400,
    "user": {
      "uid": "USR-001",
      "email": "user@example.com",
      "license_status": "Active",
      "license_expires_at": "2025-12-31T23:59:59Z"
    }
  }
}
```

**Error Responses:**

| HTTP 코드 | 에러 코드 | 설명 |
|-----------|-----------|------|
| 401 | AUTH_001 | 이메일 또는 비밀번호 오류 |
| 403 | LIC_001 | 라이센스 만료됨 |
| 403 | LIC_002 | 라이센스 정지됨 |
| 403 | LIC_003 | 승인 대기 중 |
| 403 | HWID_001 | HWID 불일치 |

```json
{
  "success": false,
  "error": {
    "code": "AUTH_001",
    "message": "이메일 또는 비밀번호가 올바르지 않습니다."
  }
}
```

---

### 1.2 토큰 갱신

**Endpoint:** `POST /auth/refresh`

**Request:**
```json
{
  "refresh_token": "eyJhbGciOiJIUzI1NiIs..."
}
```

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "access_token": "eyJhbGciOiJIUzI1NiIs...",
    "expires_in": 86400
  }
}
```

---

### 1.3 로그아웃

**Endpoint:** `POST /auth/logout`

**Headers:**
```
Authorization: Bearer {access_token}
```

**Response (200 OK):**
```json
{
  "success": true,
  "message": "로그아웃 되었습니다."
}
```

---

### 1.4 회원가입

**Endpoint:** `POST /auth/register`

**Request:**
```json
{
  "email": "newuser@example.com",
  "password": "SecurePassword123!",
  "password_confirm": "SecurePassword123!"
}
```

**Response (201 Created):**
```json
{
  "success": true,
  "data": {
    "uid": "USR-002",
    "email": "newuser@example.com",
    "license_status": "Pending",
    "message": "가입이 완료되었습니다. 관리자 승인을 기다려주세요."
  }
}
```

---

## 2. 라이센스 API (License)

### 2.1 라이센스 상태 확인

**Endpoint:** `GET /license/status`

**Headers:**
```
Authorization: Bearer {access_token}
```

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "status": "Active",
    "expires_at": "2025-12-31T23:59:59Z",
    "remaining_days": 22,
    "hwid_registered": true,
    "hwid_match": true
  }
}
```

---

### 2.2 하트비트 (주기적 검증)

**Endpoint:** `POST /license/heartbeat`

**Headers:**
```
Authorization: Bearer {access_token}
```

**Request:**
```json
{
  "hwid": "a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6",
  "client_version": "1.0.0",
  "timestamp": 1702123456
}
```

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "valid": true,
    "license_status": "Active",
    "remaining_days": 22,
    "server_time": 1702123460,
    "next_check_after": 300
  }
}
```

---

## 3. 관리자 API (Admin)

> **Note:** 모든 관리자 API는 `is_admin: true` 권한 필요

### 3.1 사용자 목록 조회

**Endpoint:** `GET /admin/users`

**Headers:**
```
Authorization: Bearer {admin_access_token}
```

**Query Parameters:**
| 파라미터 | 타입 | 설명 |
|----------|------|------|
| page | int | 페이지 번호 (기본 1) |
| limit | int | 페이지 당 개수 (기본 20) |
| status | string | 필터: Pending, Active, Expired, Suspended |
| search | string | 이메일 또는 UID 검색 |

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "users": [
      {
        "uid": "USR-001",
        "email": "user1@example.com",
        "license_status": "Active",
        "license_expires_at": "2025-12-31T23:59:59Z",
        "hwid_registered": true,
        "last_login_at": "2025-12-09T10:30:00Z",
        "created_at": "2025-01-01T00:00:00Z"
      },
      {
        "uid": "USR-002",
        "email": "user2@example.com",
        "license_status": "Pending",
        "license_expires_at": null,
        "hwid_registered": false,
        "last_login_at": null,
        "created_at": "2025-12-08T15:00:00Z"
      }
    ],
    "pagination": {
      "current_page": 1,
      "total_pages": 5,
      "total_count": 100,
      "per_page": 20
    }
  }
}
```

---

### 3.2 사용자 상세 조회

**Endpoint:** `GET /admin/users/:uid`

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "uid": "USR-001",
    "email": "user1@example.com",
    "license_status": "Active",
    "license_expires_at": "2025-12-31T23:59:59Z",
    "hwid": "a1b2c3...p6",
    "hwid_registered_at": "2025-01-01T12:00:00Z",
    "created_at": "2025-01-01T00:00:00Z",
    "updated_at": "2025-12-01T00:00:00Z",
    "last_login_at": "2025-12-09T10:30:00Z",
    "login_count": 150,
    "recent_sessions": [
      {
        "ip_address": "123.456.789.0",
        "login_at": "2025-12-09T10:30:00Z",
        "user_agent": "KimchiHedge/1.0.0 Windows/10"
      }
    ]
  }
}
```

---

### 3.3 사용자 승인

**Endpoint:** `POST /admin/users/:uid/approve`

**Request:**
```json
{
  "license_expires_at": "2025-12-31T23:59:59Z",
  "note": "승인 완료"
}
```

**Response (200 OK):**
```json
{
  "success": true,
  "message": "사용자가 승인되었습니다.",
  "data": {
    "uid": "USR-002",
    "license_status": "Active",
    "license_expires_at": "2025-12-31T23:59:59Z"
  }
}
```

---

### 3.4 사용자 거절

**Endpoint:** `POST /admin/users/:uid/reject`

**Request:**
```json
{
  "reason": "가입 조건 미충족"
}
```

**Response (200 OK):**
```json
{
  "success": true,
  "message": "사용자 가입이 거절되었습니다."
}
```

---

### 3.5 라이센스 상태 변경

**Endpoint:** `PATCH /admin/users/:uid/status`

**Request:**
```json
{
  "license_status": "Suspended",
  "reason": "이용 약관 위반"
}
```

**Response (200 OK):**
```json
{
  "success": true,
  "message": "라이센스 상태가 변경되었습니다.",
  "data": {
    "uid": "USR-001",
    "previous_status": "Active",
    "new_status": "Suspended"
  }
}
```

---

### 3.6 라이센스 만료일 설정

**Endpoint:** `PATCH /admin/users/:uid/license`

**Request:**
```json
{
  "license_expires_at": "2026-06-30T23:59:59Z"
}
```

**Response (200 OK):**
```json
{
  "success": true,
  "message": "라이센스 만료일이 변경되었습니다.",
  "data": {
    "uid": "USR-001",
    "license_expires_at": "2026-06-30T23:59:59Z"
  }
}
```

---

### 3.7 HWID 초기화

**Endpoint:** `POST /admin/users/:uid/reset-hwid`

**Request:**
```json
{
  "reason": "PC 교체 요청"
}
```

**Response (200 OK):**
```json
{
  "success": true,
  "message": "HWID가 초기화되었습니다. 다음 로그인 시 새 HWID가 등록됩니다."
}
```

---

### 3.8 활동 로그 조회

**Endpoint:** `GET /admin/audit-logs`

**Query Parameters:**
| 파라미터 | 타입 | 설명 |
|----------|------|------|
| user_uid | string | 특정 사용자 필터 |
| action | string | 행동 유형 필터 |
| from | datetime | 시작 날짜 |
| to | datetime | 종료 날짜 |
| page | int | 페이지 번호 |
| limit | int | 페이지 당 개수 |

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "logs": [
      {
        "id": "log-001",
        "user_uid": "USR-001",
        "action": "LOGIN",
        "result": "SUCCESS",
        "ip_address": "123.456.789.0",
        "hwid": "a1b2c3...p6",
        "details": {
          "client_version": "1.0.0"
        },
        "created_at": "2025-12-09T10:30:00Z"
      }
    ],
    "pagination": {
      "current_page": 1,
      "total_pages": 10,
      "total_count": 200
    }
  }
}
```

---

## 4. 공통 헤더

### 4.1 요청 헤더

| 헤더 | 필수 | 설명 |
|------|------|------|
| Authorization | 인증 API 외 | `Bearer {access_token}` |
| Content-Type | POST/PATCH | `application/json` |
| X-Client-Version | 권장 | 클라이언트 버전 |
| X-Request-ID | 선택 | 요청 추적 ID |

### 4.2 응답 헤더

| 헤더 | 설명 |
|------|------|
| X-Request-ID | 요청 추적 ID |
| X-RateLimit-Remaining | 남은 요청 횟수 |
| X-RateLimit-Reset | 제한 리셋 시간 |

---

## 5. Rate Limiting

| 엔드포인트 | 제한 |
|------------|------|
| POST /auth/login | 5회/분 |
| POST /auth/register | 3회/시간 |
| POST /license/heartbeat | 20회/분 |
| 기타 | 60회/분 |

**Rate Limit 초과 응답 (429):**
```json
{
  "success": false,
  "error": {
    "code": "RATE_LIMIT",
    "message": "요청이 너무 많습니다. 잠시 후 다시 시도해주세요.",
    "retry_after": 60
  }
}
```

---

## 6. 에러 응답 형식

```json
{
  "success": false,
  "error": {
    "code": "ERROR_CODE",
    "message": "사용자에게 표시할 메시지",
    "details": {
      "field": "추가 정보 (선택)"
    }
  }
}
```

---

## 7. API 버전 관리

- 현재 버전: `v1`
- 버전은 URL 경로에 포함: `/v1/auth/login`
- 주요 변경 시 새 버전 발행
- 이전 버전 지원 기간: 6개월
