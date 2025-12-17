# KimchiHedge 시스템 테스트 가이드라인

개발자 관점의 전체 시스템 테스트 가이드입니다.

---

## 1. 테스트 환경 구성

### 1.1 필수 구성요소

| 구성요소 | 버전 | 용도 |
|---------|------|------|
| .NET SDK | 8.0+ | 빌드 및 실행 |
| PostgreSQL | 15+ | Supabase 또는 로컬 DB |
| Visual Studio | 2022+ | 개발 및 디버깅 |

### 1.2 환경변수 설정

```bash
# AuthServer 환경변수
ASPNETCORE_ENVIRONMENT=Development
KIMCHI_DATABASE_URL=Host=localhost;Port=5432;Database=kimchihedge;Username=postgres;Password=yourpassword
KIMCHI_JWT_SECRET=KimchiHedge2024SecretKeyAutoTrading1234567890
KIMCHI_ADMIN_EMAIL=admin@kimchihedge.com
KIMCHI_ADMIN_PASSWORD=Admin123456
```

### 1.3 서버 시작

```batch
# AuthServer 실행 (D:\Project\Csharpkimchi)
run-authserver.bat
```

서버 URL:
- **Swagger UI**: http://localhost:5000
- **Admin UI**: http://localhost:5000/admin
- **Health Check**: http://localhost:5000/health

---

## 2. 테스트 시나리오

### 2.1 인증 플로우 테스트

#### TC-AUTH-001: 회원가입
```http
POST /api/v1/auth/register
Content-Type: application/json

{
  "email": "test@example.com",
  "password": "Test1234!@#",
  "referralUid": null
}
```

**예상 결과:**
- Status: 200 OK
- Response: `{ "success": true, "data": { "uid": "USR-XXXXX", "email": "..." } }`
- DB: users 테이블에 license_status='Pending' 레코드 생성

#### TC-AUTH-002: 로그인 (Pending 상태)
```http
POST /api/v1/auth/login
Content-Type: application/json

{
  "email": "test@example.com",
  "password": "Test1234!@#",
  "hwid": "TEST-HWID-12345678"
}
```

**예상 결과:**
- Status: 403 Forbidden
- Error: LICENSE_001_NOT_ACTIVE

#### TC-AUTH-003: 관리자 라이선스 승인
1. Admin UI (http://localhost:5000/admin) 접속
2. 가입 신청 메뉴에서 사용자 승인
3. 라이선스 기간 설정 (30일)

#### TC-AUTH-004: 로그인 (Active 상태)
```http
POST /api/v1/auth/login
Content-Type: application/json

{
  "email": "test@example.com",
  "password": "Test1234!@#",
  "hwid": "TEST-HWID-12345678"
}
```

**예상 결과:**
- Status: 200 OK
- Response: accessToken, refreshToken, expiresIn, user 정보

#### TC-AUTH-005: 토큰 갱신
```http
POST /api/v1/auth/refresh
Content-Type: application/json

{
  "refreshToken": "<이전 응답의 refreshToken>",
  "hwid": "TEST-HWID-12345678"
}
```

**예상 결과:**
- Status: 200 OK
- 새 accessToken 및 refreshToken 발급

#### TC-AUTH-006: HWID 불일치 테스트
```http
POST /api/v1/auth/refresh
Content-Type: application/json

{
  "refreshToken": "<기존 refreshToken>",
  "hwid": "DIFFERENT-HWID-99999"
}
```

**예상 결과:**
- Status: 401 Unauthorized
- Error: HWID_001_MISMATCH
- audit_logs 테이블에 기록

#### TC-AUTH-007: 로그아웃
```http
POST /api/v1/auth/logout
Authorization: Bearer <accessToken>
```

**예상 결과:**
- Status: 200 OK
- sessions 테이블: is_active=false

---

### 2.2 라이선스 검증 테스트

#### TC-LIC-001: 라이선스 상태 조회
```http
GET /api/v1/license/status
Authorization: Bearer <accessToken>
```

**예상 결과:**
- Status: 200 OK
- Response: licenseStatus, expiresAt

#### TC-LIC-002: Heartbeat (주기적 검증)
```http
POST /api/v1/license/heartbeat
Authorization: Bearer <accessToken>
Content-Type: application/json

{
  "hwid": "TEST-HWID-12345678"
}
```

**예상 결과:**
- Status: 200 OK
- HWID 일치 시 라이선스 상태 반환

---

### 2.3 WPF 클라이언트 테스트

#### TC-CLIENT-001: 로그인 화면
1. KimchiHedge.Client.exe 실행
2. 이메일/비밀번호 입력
3. 로그인 버튼 클릭

**확인사항:**
- AuthServer 연결 성공
- 토큰 저장 (Windows Credential Manager)
- MainWindow로 전환

#### TC-CLIENT-002: API 키 등록
1. API 키 탭 이동
2. Upbit API Key/Secret 입력
3. BingX API Key/Secret 입력
4. 저장

**확인사항:**
- SecureStorage에 암호화 저장
- 연결 테스트 버튼으로 검증

#### TC-CLIENT-003: 거래 설정
1. 설정 탭 이동
2. 파라미터 설정:
   - 진입 김프: 3.0%
   - 익절 김프: 1.0%
   - 손절 김프: 5.0%
   - 진입 비율: 50%
   - 레버리지: 1x
   - 쿨다운: 300초

**확인사항:**
- 설정값 범위 검증
- 로컬 JSON 저장

---

### 2.4 자동매매 엔진 테스트

#### TC-TRADE-001: 자동매매 ON
1. 대시보드 탭 이동
2. 자동매매 토글 ON

**확인사항:**
- TradingEngine 상태: IDLE → WAIT_ENTRY
- 실시간 가격 스트리밍 시작
- 김프 모니터링 시작

#### TC-TRADE-002: 진입 시뮬레이션
**조건:** 김프 >= EntryKimchi (3.0%)

**확인사항:**
- 상태: WAIT_ENTRY → ENTERING
- Upbit 시장가 매수 실행
- BingX 숏 포지션 오픈
- 1:1 수량 검증 (QuantityTolerance 이내)
- 상태: ENTERING → POSITION_OPEN

#### TC-TRADE-003: 익절 시뮬레이션
**조건:** 김프 <= TakeProfitKimchi (1.0%)

**확인사항:**
- 상태: POSITION_OPEN → EXITING
- Upbit 전량 매도
- BingX 포지션 청산
- 손익 계산 및 로깅
- 상태: EXITING → COOLDOWN

#### TC-TRADE-004: 손절 시뮬레이션
**조건:** 김프 >= StopLossKimchi (5.0%)

**확인사항:**
- 동일한 청산 플로우
- CloseReason: StopLoss

#### TC-TRADE-005: 쿨다운 테스트
**조건:** 청산 완료 후

**확인사항:**
- 상태: COOLDOWN (CooldownSeconds 동안)
- 타이머 만료 시 → WAIT_ENTRY
- 수동 OFF 시 → IDLE

#### TC-TRADE-006: 롤백 테스트
**시나리오:** Upbit 매수 성공 후 BingX 숏 실패

**확인사항:**
- 상태: ERROR_ROLLBACK
- RollbackService 실행
- Upbit 매수량 자동 매도
- 롤백 완료 후 → COOLDOWN

---

### 2.5 동시성 테스트

#### TC-CONC-001: 빠른 가격 틱 처리
**시나리오:** 100ms 간격으로 가격 업데이트 발생

**확인사항:**
- SemaphoreSlim으로 동시 처리 방지
- 중복 진입 없음
- 이전 처리 완료 전 새 틱 무시

#### TC-CONC-002: 토큰 동시 갱신
**시나리오:** 여러 API 호출이 동시에 401 발생

**확인사항:**
- AuthHttpHandler의 중복 갱신 방지
- 단일 갱신 후 대기 중인 요청 재시도

---

### 2.6 오류 처리 테스트

#### TC-ERR-001: 네트워크 끊김
**시나리오:** 거래 중 인터넷 연결 끊김

**확인사항:**
- 타임아웃 처리
- 재연결 시도
- 포지션 상태 복구

#### TC-ERR-002: 거래소 API 오류
**시나리오:** 거래소 점검 중 주문 실패

**확인사항:**
- 예외 로깅
- 롤백 트리거
- 사용자 알림

#### TC-ERR-003: 잔액 부족
**시나리오:** 진입 시 잔액 부족

**확인사항:**
- 사전 잔액 검증
- 진입 취소 및 알림

---

## 3. API 테스트 (Swagger UI)

### 3.1 Swagger UI 접속
http://localhost:5000 (Development 환경)

### 3.2 인증 헤더 설정
1. 로그인 API 호출하여 accessToken 획득
2. Swagger UI 상단 "Authorize" 버튼 클릭
3. `Bearer <accessToken>` 입력

### 3.3 주요 테스트 엔드포인트

| 메서드 | 경로 | 설명 |
|-------|------|------|
| POST | /api/v1/auth/register | 회원가입 |
| POST | /api/v1/auth/login | 로그인 |
| POST | /api/v1/auth/refresh | 토큰 갱신 |
| POST | /api/v1/auth/logout | 로그아웃 |
| GET | /api/v1/license/status | 라이선스 조회 |
| POST | /api/v1/license/heartbeat | 하트비트 |
| GET | /health | 헬스체크 |

---

## 4. 데이터베이스 검증

### 4.1 Supabase SQL Editor

```sql
-- 사용자 목록 확인
SELECT id, uid, email, license_status, license_expires_at, hwid, created_at
FROM users
ORDER BY created_at DESC;

-- 세션 목록 확인
SELECT s.id, u.email, s.hwid, s.ip_address, s.is_active, s.expires_at
FROM sessions s
JOIN users u ON s.user_id = u.id
ORDER BY s.created_at DESC;

-- 감사 로그 확인
SELECT al.action, al.result, al.ip_address, al.hwid, al.details, al.created_at
FROM audit_logs al
ORDER BY al.created_at DESC
LIMIT 50;

-- 활성 세션 수 확인
SELECT u.email, COUNT(*) as active_sessions
FROM sessions s
JOIN users u ON s.user_id = u.id
WHERE s.is_active = true AND s.expires_at > NOW()
GROUP BY u.email;
```

### 4.2 데이터 정합성 검증

```sql
-- 만료된 세션 확인
SELECT COUNT(*) FROM sessions
WHERE is_active = true AND expires_at < NOW();

-- HWID 없는 활성 사용자 확인
SELECT * FROM users
WHERE license_status = 'Active' AND hwid IS NULL;

-- 중복 HWID 검사
SELECT hwid, COUNT(*) as cnt
FROM users
WHERE hwid IS NOT NULL
GROUP BY hwid
HAVING COUNT(*) > 1;
```

---

## 5. 성능 테스트

### 5.1 부하 테스트 시나리오

#### 동시 로그인 테스트
- 동시 사용자: 100명
- 요청 간격: 100ms
- 예상 응답 시간: < 500ms

#### 가격 업데이트 처리
- 틱 빈도: 10/초
- 처리 지연: < 100ms
- 메모리 누수 없음

### 5.2 모니터링 포인트

| 지표 | 임계값 | 측정 방법 |
|-----|-------|----------|
| API 응답 시간 | < 500ms | Swagger/Postman |
| 메모리 사용량 | < 500MB | Task Manager |
| CPU 사용률 | < 50% | Task Manager |
| DB 연결 수 | < 20 | Supabase Dashboard |

---

## 6. 보안 테스트

### 6.1 인증/인가 테스트

| 테스트 항목 | 방법 | 예상 결과 |
|-----------|------|----------|
| 만료된 토큰 사용 | 24시간 경과 후 API 호출 | 401 Unauthorized |
| 변조된 토큰 사용 | JWT payload 수정 후 호출 | 401 Unauthorized |
| 타인의 토큰 사용 | 다른 사용자 토큰으로 호출 | 권한 검증 실패 |
| HWID 위조 | 다른 HWID로 갱신 요청 | 401 + 감사 로그 |

### 6.2 입력 검증 테스트

| 테스트 항목 | 입력값 | 예상 결과 |
|-----------|-------|----------|
| SQL Injection | `'; DROP TABLE users;--` | 입력 거부 |
| XSS | `<script>alert(1)</script>` | 이스케이프 처리 |
| 비밀번호 강도 | `123` | 검증 실패 |
| 이메일 형식 | `invalid-email` | 검증 실패 |

### 6.3 API 키 보안

| 확인 항목 | 방법 |
|----------|------|
| 암호화 저장 | SecureStorage 사용 확인 |
| 메모리 노출 | 디버거로 평문 노출 검사 |
| 로그 노출 | 로그 파일에 키 미포함 확인 |

---

## 7. 체크리스트

### 7.1 배포 전 필수 확인

- [ ] 모든 환경변수 설정 완료
- [ ] DB 마이그레이션 완료
- [ ] JWT Secret 32자 이상
- [ ] CORS 설정 확인
- [ ] HTTPS 인증서 설정
- [ ] 로그 레벨 조정 (Production: Warning)

### 7.2 기능 테스트 완료 확인

- [ ] TC-AUTH-001 ~ 007 통과
- [ ] TC-LIC-001 ~ 002 통과
- [ ] TC-CLIENT-001 ~ 003 통과
- [ ] TC-TRADE-001 ~ 006 통과
- [ ] TC-CONC-001 ~ 002 통과
- [ ] TC-ERR-001 ~ 003 통과

### 7.3 보안 테스트 완료 확인

- [ ] 토큰 검증 테스트 통과
- [ ] HWID 검증 테스트 통과
- [ ] 입력 검증 테스트 통과
- [ ] API 키 보안 검증 통과

---

## 8. 트러블슈팅

### 8.1 일반적인 문제

| 문제 | 원인 | 해결책 |
|-----|------|-------|
| DB 연결 실패 | 연결 문자열 오류 | KIMCHI_DATABASE_URL 확인 |
| 토큰 검증 실패 | JWT Secret 불일치 | 서버/클라이언트 Secret 동기화 |
| HWID 불일치 | 하드웨어 변경 | 관리자에게 HWID 초기화 요청 |
| 포트 충돌 | 5000 포트 사용 중 | netstat -ano \| findstr :5000 |

### 8.2 로그 확인

```bash
# AuthServer 로그 위치
# Development: 콘솔 출력
# Production: logs/authserver-{date}.log

# 로그 레벨 설정 (appsettings.json)
"Logging": {
  "LogLevel": {
    "Default": "Information",
    "Microsoft.AspNetCore": "Warning"
  }
}
```

### 8.3 디버그 모드 실행

```bash
# Visual Studio에서 F5로 디버그 실행
# 또는 CLI:
dotnet run --project src/KimchiHedge.AuthServer --configuration Debug
dotnet run --project src/KimchiHedge.Client --configuration Debug
```

---

## 9. 연락처

- **기술 지원**: admin@kimchihedge.com
- **버그 리포트**: GitHub Issues
- **문서 업데이트**: 2024-12-15

---

*이 문서는 KimchiHedge 시스템 v1.0 기준으로 작성되었습니다.*
