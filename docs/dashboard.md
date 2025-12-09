# 프로젝트 대시보드

> **최종 업데이트:** 2025-12-10 10:30:00
> **프로젝트:** 김치프리미엄 기반 1:1 헷지 자동매매 시스템

---

## 프로젝트 목표
서버에서 제공하는 김치프리미엄(kimchi)을 기준으로 업비트 현물과 해외 선물 포지션을 체결 수량 기준 1:1로 자동 헷지하는 C# 기반 Windows 전용 개인 자동매매 시스템 개발

---

## 전체 진행률
```
[██████████░░░░░░░░░░] 50%
```

**현재 단계:** Core 트레이딩 로직 개발 완료 (SRP 리팩토링 완료)

---

## ✅ 확정된 요구사항

### 트레이딩 로직
- **단일 포지션만** - 업비트 현물 1 + BingX 선물 1 쌍
- **수량 허용 오차 = 0** - 100% 일치 필수
- **수량 불일치 시 즉시 롤백**
- **쿨다운** - 1~30분 (기본 5분)
- **진입 1 → 청산 1** 단일 사이클
- 추가 진입/물타기/분할 청산 없음

### 인증 서버 기반 구조
- 이메일/비밀번호 로그인
- 서버에서 인증 후 JWT 토큰 발급
- HWID(PC 고유값) 1:1 매칭
- 라이센스 상태: `Active`, `Expired`, `Suspended`
- 추천인 승인 시스템

### 보안 요구사항
- API Key AES-256 암호화 저장
- HTTPS(SSL) 통신만 허용

---

## 에이전트별 현재 상태

| 에이전트 | 상태 | 현재 작업 | 진행률 |
|----------|------|-----------|--------|
| Orchestrator | ✅ 완료 | 요구사항 정리 및 문서화 | 100% |
| System_Architect | ✅ 완료 | 전체 아키텍처 설계 | 100% |
| Backend_Developer | 🔄 진행중 | Core 트레이딩 로직 SRP 리팩토링 | 100% |
| Frontend_Developer | ⏳ 대기 | WPF UI 개발 | 0% |
| Security_Expert | ✅ 완료 | 보안 서비스 구현 | 100% |
| QA_Tester | ⏳ 대기 | - | 0% |

---

## 최근 활동 로그

| 시간 | 에이전트 | 활동 |
|------|----------|------|
| 2025-12-10 10:30 | Backend_Developer | TradingEngine SRP 리팩토링 완료 |
| 2025-12-10 10:25 | Backend_Developer | CooldownService 구현 |
| 2025-12-10 10:20 | Backend_Developer | RollbackService 구현 |
| 2025-12-10 10:15 | Backend_Developer | PositionManager 구현 |
| 2025-12-10 10:10 | Backend_Developer | OrderExecutor 구현 |
| 2025-12-10 10:05 | Backend_Developer | ConditionEvaluator 구현 |
| 2025-12-10 09:30 | Backend_Developer | 트레이딩 설정/모델 구현 |
| 2025-12-09 01:00 | System_Architect | 시스템 아키텍처 문서 작성 |

---

## 다음 단계

### 🟢 진행 예정
1. **DI 컨테이너 설정** - ServiceCollection 구성
2. **WPF 프로젝트 생성** - MVVM 구조 설정
3. **인증 서버 API 개발** - ASP.NET Core Web API
4. **거래소 어댑터 구현** - 업비트, BingX 실제 연동

---

## 문서 현황

| 문서 | 경로 | 상태 |
|------|------|------|
| PRD | `/docs/prd/PRD_김치프리미엄_헷지_자동매매.md` | ✅ 완료 |
| 명확화 필요 사항 | `/docs/prd/CLARIFICATION_NEEDED.md` | ✅ 완료 |
| 시스템 아키텍처 | `/docs/architecture/SYSTEM_ARCHITECTURE.md` | ✅ 완료 |
| 김프 서버 아키텍처 | `/docs/architecture/KIMCHI_SERVER_ARCHITECTURE.md` | ✅ 완료 |
| 인증 서버 아키텍처 | `/docs/architecture/AUTH_SERVER_ARCHITECTURE.md` | ✅ 완료 |
| 보안 요구사항 | `/docs/architecture/SECURITY_REQUIREMENTS.md` | ✅ 완료 |

---

## 코드 현황 (SRP 리팩토링 완료)

### KimchiHedge.Core/Trading

| 파일 | 역할 | 상태 |
|------|------|------|
| `TradingEngine.cs` | 오케스트레이션만 (서비스 연결/조율) | ✅ 완료 |
| `ConditionEvaluator.cs` | 진입/익절/손절 조건 판단만 | ✅ 완료 |
| `OrderExecutor.cs` | 주문 실행만 (업비트 매수 → BingX 숏) | ✅ 완료 |
| `PositionManager.cs` | 포지션 상태 관리만 | ✅ 완료 |
| `RollbackService.cs` | 롤백 처리만 | ✅ 완료 |
| `CooldownService.cs` | 쿨다운 타이머 관리만 | ✅ 완료 |

### KimchiHedge.Core/Models

| 파일 | 역할 | 상태 |
|------|------|------|
| `TradingSettings.cs` | 트레이딩 설정값 | ✅ 완료 |
| `Position.cs` | 포지션 정보 | ✅ 완료 |
| `OrderResult.cs` | 주문 결과 | ✅ 완료 |
| `KimchiPremiumData.cs` | 김프 데이터 | ✅ 완료 |

### KimchiHedge.Core/Exchanges

| 파일 | 역할 | 상태 |
|------|------|------|
| `ISpotExchange.cs` | 현물 거래소 인터페이스 | ✅ 완료 |
| `IFuturesExchange.cs` | 선물 거래소 인터페이스 | ✅ 완료 |

### KimchiHedge.Core/Security

| 파일 | 역할 | 상태 |
|------|------|------|
| `AesEncryptionService.cs` | AES-256 암호화 | ✅ 완료 |
| `HwidGenerator.cs` | HWID 생성 | ✅ 완료 |

---

## 버전 태그

| 태그 | 설명 | 날짜 |
|------|------|------|
| v0.1.0-planning | 기획 검토 완료 | 2025-12-09 |
| v0.2.0-auth-design | 인증 서버 아키텍처 설계 | 2025-12-09 |
| v0.3.0-core-srp | Core 트레이딩 로직 SRP 리팩토링 완료 | 2025-12-10 |

---

*이 대시보드는 각 에이전트 작업 시 자동으로 업데이트됩니다.*
