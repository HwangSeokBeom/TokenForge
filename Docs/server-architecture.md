# TokenForge Server Architecture

## 문서 목적

이 문서는 TokenForge 서버의 역할, 기술 스택, 모듈 구조, 데이터 원칙, API, 인증, 동기화, Privacy Guard, 테스트, MVP 범위를 정의한다. 서버는 원본 개발 데이터 분석기가 아니라, 클라이언트가 privacy-safe 형태로 만든 집계값을 저장하고 동기화하는 제품 기반 계층이다.

## 확정된 내용과 조사 필요 항목

### 확정된 내용

- 서버는 원본 개발 데이터 분석기가 아니다.
- 서버는 계정, 프로필, 캐릭터 상태 동기화, 세션 요약 저장, 업적/시즌 확장 기반을 담당한다.
- 서버는 안전한 집계값만 저장한다.
- 서버는 원본 프롬프트, 원본 코드, 전체 로그 원문, 터미널 출력, 절대 경로, Git remote URL, 커밋 diff를 받지 않는다.
- 클라이언트가 금지 필드를 보내면 서버는 요청을 거부한다.
- Guest Mode에서는 서버 없이 로컬 전용으로 사용 가능해야 한다.
- 로그인과 클라우드 동기화는 opt-in이다.
- MVP 성장 계산은 클라이언트가 수행하고 서버는 범위 검증과 저장을 담당한다.

### 조사 필요 항목

- 최종 배포 환경: AWS EC2/RDS, Render, Fly.io, Railway 중 선택.
- 이메일/비밀번호 로그인과 소셜 로그인 우선순위.
- Steam 계정 연동 방식.
- sync conflict resolution 상세 정책.
- 리더보드/시즌 경쟁 도입 시 서버 성장 계산 필요 범위.
- token bucket/range 기준값.
- 데이터 export/delete 법적 요구사항 범위.

---

## 1. 서버 역할 정의

TokenForge 서버는 원본 개발 데이터 분석기가 아니다. Git diff, AI Agent 로그, 프롬프트, 코드, 터미널 출력의 분석은 클라이언트 로컬에서만 수행한다. 서버는 클라이언트가 만든 안전한 집계값을 받아 계정과 캐릭터 상태를 동기화하고, 세션 요약과 업적/시즌 확장의 기반 데이터를 관리한다.

서버 핵심 역할:

- 계정과 인증.
- 사용자 프로필.
- 캐릭터 상태 저장과 동기화.
- 세션 요약 저장.
- 업적 평가 기반.
- 시즌/리더보드 확장 기반.
- Privacy Guard를 통한 금지 payload 차단.
- 데이터 export/delete.
- Health check.

서버가 하지 않는 일:

- 원본 개발 로그 분석.
- 원본 프롬프트 저장.
- 원본 코드 저장.
- 터미널 출력 저장.
- 절대 파일 경로 저장.
- Git remote URL 저장.
- 커밋 diff 원문 저장.

Guest Mode에서는 서버 없이 로컬 전용 사용이 가능해야 한다. 로그인/동기화는 사용자가 명시적으로 선택하는 opt-in 기능이다.

## 2. 클라이언트/서버 책임 경계

### Client 책임

- 원본 프로젝트/로그 접근.
- Git/Agent 자동 분석.
- 1차 privacy sanitization.
- `AgentWorkSession` 생성.
- 성장 계산.
- 로컬 저장.
- Safe Sync Payload 생성.
- 자동 분석 결과 확인/보정.
- 미니게임과 캐릭터 UI.

### Server 책임

- 인증/계정.
- 사용자 프로필.
- 캐릭터 snapshot 저장.
- 세션 요약 저장.
- Privacy Guard 2차 검증.
- 클라우드 동기화.
- 업적/시즌 확장 기반.
- 데이터 export/delete.

### Server가 절대 하지 않는 일

- 원본 로그 분석.
- 원본 프롬프트 저장.
- 원본 코드 저장.
- 터미널 출력 원문 저장.
- Git diff/patch 원문 저장.
- 절대 파일 경로 저장.
- Git remote URL 저장.
- 브랜치명/커밋 메시지 원문 저장.
- 회사/팀 생산성 감시 도구 역할.

---

## 3. 서버 기술 스택 추천

### Runtime: Node.js

선택 이유:

- 개발 속도가 빠르고 배포 옵션이 많다.
- NestJS/Prisma/PostgreSQL 조합과 잘 맞는다.
- TypeScript 기반 API 서버 구현 경험을 쌓기 좋다.

대안:

- .NET: C# 생태계와 맞지만 서버 개발 속도와 NestJS 모듈 구조 측면에서 후순위.
- Go: 성능과 단순 배포가 좋지만 초기 제품형 API 개발 생산성은 NestJS가 더 높다.

### Framework: NestJS

선택 이유:

- 모듈, guard, pipe, interceptor 구조가 명확하다.
- Auth, validation, testing, dependency injection 구성이 좋다.
- 클라이언트/서버 문서화와 팀 협업에 적합하다.

대안:

- Express: 단순하지만 구조를 직접 잡아야 한다.
- Fastify: 성능이 좋지만 NestJS 위에서 adapter로도 사용 가능하다.

### Language: TypeScript

선택 이유:

- DTO, API contract, Prisma type과 잘 맞는다.
- 금지 필드 allowlist 설계를 타입으로 강제하기 쉽다.

### Database: PostgreSQL

선택 이유:

- 계정, 캐릭터, 세션 요약, 업적 데이터를 안정적으로 관리할 수 있다.
- JSONB를 제한적으로 사용할 수 있어 stat delta나 설정 일부 저장에 유연하다.

대안:

- SQLite: 로컬/개발용에는 좋지만 서버 운영에는 제한적.
- MySQL: 가능하지만 PostgreSQL의 JSONB와 확장성이 더 적합하다.

### ORM: Prisma

선택 이유:

- TypeScript type generation이 강하다.
- migration 관리가 쉽다.
- raw query를 최소화해 SQL injection 위험을 줄일 수 있다.

대안:

- TypeORM: NestJS와 오래 쓰였지만 Prisma의 개발 경험이 더 단순하다.
- Drizzle: 타입 안전성이 좋지만 팀 숙련도 확인 필요.

### Cache/Queue: Redis, BullMQ later

선택 이유:

- rate limit, background job, achievement evaluation queue에 활용 가능하다.
- MVP에서는 필수는 아니며 later로 둔다.

### Auth: JWT access/refresh token

선택 이유:

- 모바일/데스크톱 클라이언트와 잘 맞는다.
- refresh token rotation으로 세션 보안을 강화할 수 있다.

대안:

- 세션 쿠키: 웹 중심 서비스에는 좋지만 Unity 클라이언트에는 JWT가 단순하다.

### API: REST MVP, WebSocket later

선택 이유:

- REST는 Unity 클라이언트 구현과 테스트가 쉽다.
- 동기화가 request/response 중심인 MVP에 적합하다.

대안:

- GraphQL: 유연하지만 초기 complexity가 크다.
- WebSocket: 실시간 동기화가 필요해질 때 검토한다.

### Validation: class-validator/Zod

선택 이유:

- DTO allowlist와 forbidden field 검사에 필요하다.
- Zod는 runtime schema와 테스트가 명확하다.
- NestJS 기본 흐름은 class-validator가 편하다.

권장:

- NestJS DTO는 class-validator.
- Privacy Guard의 deep payload 검사는 Zod 또는 custom validator 병행 검토.

### Test: Jest/Vitest

선택 이유:

- NestJS 기본은 Jest.
- Vitest는 빠르지만 NestJS ecosystem과의 호환을 확인해야 한다.

### Deploy: AWS EC2/RDS 또는 Render/Fly.io/Railway

선택 이유:

- AWS EC2/RDS는 운영 제어권이 높다.
- Render/Fly.io/Railway는 초기 배포 속도가 빠르다.

권장:

- MVP/포트폴리오: Render/Fly.io/Railway.
- 장기 운영: AWS EC2/RDS 또는 ECS/RDS.

### Logging: structured logging

선택 이유:

- 요청 추적과 오류 분석에 필요하다.
- 단, 원문 payload는 절대 로그에 남기지 않는다.

### Security: rate limit, helmet, CORS, request validation

선택 이유:

- 인증 API와 sync API를 보호해야 한다.
- Privacy Guard만으로는 보안이 충분하지 않다.

---

## 4. 서버 전체 아키텍처

모듈 구조:

- Auth Module
- User Module
- Character Module
- Session Summary Module
- Achievement Module
- Sync Module
- Privacy Guard Module
- Admin/Config Module, later
- Season/Leaderboard Module, later

### Auth Module

책임:

- guest auth, signup, login, refresh, logout, account deletion 인증 흐름.
- JWT access/refresh token 발급.
- refresh token rotation.

클라이언트 관계:

- Guest Mode는 로컬 전용이 기본이지만, 서버 guest 계정 발급도 지원 가능하다.
- 로그인 후 sync opt-in 상태에서 서버 API 사용.

### User Module

책임:

- 사용자 프로필, nickname, 설정 일부 관리.
- 개인정보 export/delete 요청 연결.

### Character Module

책임:

- 캐릭터 snapshot 저장.
- stats, level, evolution, unlocked items 관리.
- 성장 히스토리 조회용 summary 제공.

### Session Summary Module

책임:

- 클라이언트가 보낸 privacy-safe session summary 저장.
- 세션 목록 조회/삭제.
- 중복 sessionId/idempotency 처리.

주의:

- 원본 로그가 아니라 요약만 저장한다.

### Achievement Module

책임:

- 기본 업적 평가.
- 클라이언트 summary와 character snapshot 기반 업적 unlock.

### Sync Module

책임:

- push/pull 동기화.
- syncVersion, updatedAt, tombstone 처리.
- last-write-wins MVP 정책 적용.

### Privacy Guard Module

책임:

- DTO allowlist.
- 금지 필드 탐지.
- 요청 body size 제한.
- suspicious payload 거부.
- 원문 payload logging 금지.

### Admin/Config Module, later

책임:

- 밸런싱 config, 시즌 config, 운영 상태.
- MVP에서는 비활성 또는 skeleton.

### Season/Leaderboard Module, later

책임:

- 시즌 진행도, 개인/친구 리더보드.
- public/team leaderboard는 privacy 리스크 때문에 신중히.

---

## 5. 서버 폴더 구조

NestJS 기준 추천 구조:

```text
TokenForgeCoreServer/
  src/
    main.ts
    app.module.ts
    common/
      decorators/
      filters/
      guards/
      interceptors/
      pipes/
      utils/
    config/
    prisma/
    auth/
    users/
    characters/
    sessions/
    achievements/
    sync/
    privacy/
    health/
  prisma/
    schema.prisma
    migrations/
  test/
  docs/
  .env.example
  README.md
```

폴더 역할:

- `common/`: 공통 guard, filter, pipe, interceptor, result utility.
- `config/`: 환경 변수 schema, app config, CORS config.
- `prisma/`: Prisma service와 DB 접근 유틸리티.
- `auth/`: 인증 controller/service/dto/strategy.
- `users/`: 사용자 프로필과 계정 설정.
- `characters/`: 캐릭터 snapshot, stats, history.
- `sessions/`: session summary upload/list/delete.
- `achievements/`: 업적 정의와 사용자 업적.
- `sync/`: pull/push, syncVersion, tombstone, idempotency.
- `privacy/`: Privacy Guard, forbidden field scanner, DTO allowlist.
- `health/`: health check endpoint.
- `prisma/schema.prisma`: DB schema.
- `prisma/migrations/`: migration 파일.
- `test/`: e2e/integration/unit test.
- `docs/`: API 계약과 privacy policy 문서.

---

## 6. 서버 데이터 원칙

원칙:

- 서버는 집계값만 저장한다.
- 서버는 원본 개발 데이터 저장 금지.
- 클라이언트가 금지 필드를 보내면 서버에서 거부한다.
- Privacy Guard를 API 레벨에서 적용한다.
- 프로젝트 식별은 가능하면 `projectHash` 또는 `localOnlyProjectId` 기반 비식별 값을 사용한다.
- `projectAlias`는 기본적으로 로컬 전용으로 저장한다.
- 서버 동기화가 필요한 경우 사용자가 명시적으로 허용한 alias만 전송한다.
- 자동 추출한 프로젝트명, 폴더명, repository name은 서버로 전송하지 않는다.
- 사용자가 직접 입력한 alias도 민감 정보일 수 있으므로 동기화 전 안내가 필요하다.
- token은 exact raw value 대신 bucket/range 저장을 우선 검토한다.
- `sessionSummary`는 게임 성장과 동기화에 필요한 최소값만 저장한다.

서버 저장 금지:

- 원본 프롬프트.
- 원본 코드.
- 전체 로그 원문.
- 터미널 출력 원문.
- 절대 파일 경로.
- Git remote URL.
- 커밋 diff 원문.
- 브랜치명 원문.
- 커밋 메시지 원문.
- API Key/Secret.

이 설계의 이유는 서버 침해 또는 운영 실수 시 피해 범위를 최소화하기 위해서다. 서버가 민감 원문을 애초에 받지 않으면, 저장/백업/로그/관리자 화면에서 노출될 가능성도 줄어든다.

---

## 7. 핵심 DB 모델 설계

Prisma schema 코드는 별도 구현 단계에서 작성한다. 여기서는 필드 설계 수준으로 정의한다.

### `User`

- 역할: 사용자 계정 루트.
- 주요 필드: `id`, `nickname`, `emailNullable`, `createdAt`, `updatedAt`, `deletedAt`, `syncEnabled`.
- 관계: Character, SessionSummary, AuthAccount, Device.
- 인덱스 후보: `email`, `deletedAt`.
- 저장하면 안 되는 데이터: 프로젝트명 자동 추출값, 로그 경로.

### `AuthAccount`

- 역할: 로그인 제공자 연결.
- 주요 필드: `id`, `userId`, `provider`, `providerUserId`, `emailVerified`.
- 관계: User.
- 인덱스 후보: `(provider, providerUserId)`.
- 저장하면 안 되는 데이터: provider access token 원문. 필요 시 암호화 저장 검토.

### `RefreshToken`

- 역할: refresh token rotation.
- 주요 필드: `id`, `userId`, `tokenHash`, `expiresAt`, `revokedAt`, `createdAt`, `deviceId`.
- 관계: User, Device.
- 인덱스 후보: `userId`, `tokenHash`.
- 저장하면 안 되는 데이터: refresh token 원문.

### `Character`

- 역할: 캐릭터 snapshot.
- 주요 필드: `id`, `userId`, `selectedCharacterId`, `level`, `totalExp`, `evolutionType`, `appearanceVariant`, `updatedAt`.
- 관계: User, CharacterStats, CharacterEvolution.
- 인덱스 후보: `userId`.
- 저장하면 안 되는 데이터: 세션 원문.

### `CharacterStats`

- 역할: 캐릭터 스탯.
- 주요 필드: `characterId`, `logic`, `debug`, `architecture`, `design`, `stability`, `velocity`, `creativity`, `efficiency`, `stress`.
- 관계: Character.
- 인덱스 후보: `characterId`.

### `CharacterEvolution`

- 역할: 진화 진행도와 해금 상태.
- 주요 필드: `id`, `characterId`, `evolutionType`, `progress`, `unlockedAt`.
- 관계: Character.
- 인덱스 후보: `(characterId, evolutionType)`.

### `SessionSummary`

- 역할: 원본 로그가 아닌 세션 요약 저장.
- 주요 필드:
  - `sessionId`
  - `userId`
  - `agentType`
  - `workType`
  - `startedAt` / `endedAt`
  - `durationBucket`
  - `tokenBucket` 또는 `tokenRange`
  - `changedFileCountBucket`
  - `addedLineBucket`
  - `deletedLineBucket`
  - `testRunCount`
  - `buildRunCount`
  - `resultStatus`
  - `expGained`
  - `statDeltas`
  - `evolutionProgressDelta`
  - `confidence`
  - `sourceProvider`
  - `parserVersion`
  - `projectHash` 또는 `localOnlyProjectId`
  - `projectLocalAlias`, 단 사용자가 명시적으로 동기화를 허용한 alias만 저장
- 관계: User.
- 인덱스 후보: `userId`, `sessionId`, `startedAt`, `(userId, workType)`.
- 저장하면 안 되는 데이터: prompt, code, log, terminal output, file path, remote URL, branch raw, commit raw, diff raw.

### `WorkTypeDistribution`

- 역할: 일/주 단위 작업 유형 분포 캐시.
- 주요 필드: `id`, `userId`, `periodStart`, `periodEnd`, `featureCount`, `bugfixCount`, `refactorCount`, `testCount`, `uiuxCount`, `docsCount`.
- 인덱스 후보: `(userId, periodStart)`.

### `Achievement`

- 역할: 업적 정의.
- 주요 필드: `id`, `code`, `title`, `description`, `criteriaType`, `enabled`.
- 저장하면 안 되는 데이터: 특정 사용자 원문 데이터.

### `UserAchievement`

- 역할: 사용자 업적 해금 상태.
- 주요 필드: `id`, `userId`, `achievementId`, `unlockedAt`, `progress`.
- 인덱스 후보: `(userId, achievementId)`.

### `SyncState`

- 역할: 사용자 sync version과 상태.
- 주요 필드: `userId`, `syncVersion`, `lastPulledAt`, `lastPushedAt`, `updatedAt`.

### `Device`

- 역할: 클라이언트 장치 식별과 sync 상태.
- 주요 필드: `id`, `userId`, `deviceName`, `platform`, `lastSeenAt`.
- 저장하면 안 되는 데이터: 로컬 절대 경로.

### `PrivacyConsent`

- 역할: 동기화와 telemetry 동의 이력.
- 주요 필드: `id`, `userId`, `consentType`, `granted`, `createdAt`, `revokedAt`.

### `DeletedDataAudit`, minimal

- 역할: 삭제 요청 처리 이력의 최소 기록.
- 주요 필드: `id`, `userId`, `deletedAt`, `deleteType`.
- 저장하면 안 되는 데이터: 삭제 대상 원문 payload.

---

## 8. API 설계

REST MVP 기준이다. 모든 API는 DTO allowlist와 Privacy Guard를 통과해야 한다.

API 검증 원칙:

- 모든 요청 DTO는 allowlist 기반이다.
- DTO에 없는 필드는 strip하지 않고 reject하는 것을 기본으로 한다.
- unknown field가 들어온 경우 `VALIDATION_FAILED` 또는 `FORBIDDEN_PAYLOAD`로 거부한다.
- 금지 필드가 들어오면 `FORBIDDEN_PAYLOAD`로 거부한다.
- 에러 응답에는 원본 필드 값이 포함되지 않는다.
- 클라이언트의 `SaveData`나 `AgentWorkSession` 전체를 받는 API는 만들지 않는다.
- 서버는 `SafeSyncPayload`, `SessionSummaryUploadRequest`, `CharacterSnapshotSyncRequest`, `SettingsSyncRequest` 같은 명시적 DTO만 받는다.

Sync DTO 변환/검증 단계:

1. Local model.
2. Privacy sanitizer.
3. Sync DTO allowlist mapping.
4. Client-side forbidden field check.
5. Server Privacy Guard.
6. Server DTO validation.
7. Persistence.

공통 에러 코드:

- `AUTH_REQUIRED`
- `FORBIDDEN_PAYLOAD`
- `VALIDATION_FAILED`
- `RATE_LIMITED`
- `NOT_FOUND`
- `CONFLICT`
- `SERVER_ERROR`

### Auth

#### `POST /auth/guest`

- 목적: guest user 또는 anonymous server identity 발급.
- 요청 필드: `deviceId`, `clientVersion`.
- 응답 필드: `userId`, `accessToken`, `refreshToken`, `guest=true`.
- 서버 검증: deviceId 형식, rate limit.
- 금지 필드 거부: body 전체 Privacy Guard 적용.
- 인증 필요 여부: 없음.

#### `POST /auth/signup`

- 목적: 계정 생성.
- 요청 필드: `email`, `password`, `nickname`.
- 응답 필드: `userId`, `accessToken`, `refreshToken`.
- 서버 검증: email format, password policy, nickname length.
- 인증 필요 여부: 없음.

#### `POST /auth/login`

- 목적: 로그인.
- 요청 필드: `email`, `password`.
- 응답 필드: `accessToken`, `refreshToken`, `user`.
- 서버 검증: credential, rate limit.
- 인증 필요 여부: 없음.

#### `POST /auth/refresh`

- 목적: access token 재발급.
- 요청 필드: `refreshToken`.
- 응답 필드: new `accessToken`, rotated `refreshToken`.
- 서버 검증: token hash, expiry, revoked status.
- 인증 필요 여부: refresh token.

#### `POST /auth/logout`

- 목적: refresh token revoke.
- 요청 필드: `refreshToken` 또는 `deviceId`.
- 응답 필드: `success`.
- 인증 필요 여부: 필요.

#### `DELETE /auth/account`

- 목적: 계정 삭제.
- 요청 필드: confirmation flag.
- 응답 필드: `deletedAt`.
- 서버 검증: 재인증 또는 fresh token.
- 인증 필요 여부: 필요.

### User

#### `GET /users/me`

- 목적: 현재 사용자 프로필 조회.
- 응답 필드: `userId`, `nickname`, `syncEnabled`, `createdAt`.
- 인증 필요 여부: 필요.

#### `PATCH /users/me`

- 목적: 프로필 수정.
- 요청 필드: `nickname`, settings 일부.
- 응답 필드: updated profile.
- 서버 검증: nickname length, allowlist.
- 인증 필요 여부: 필요.

### Character

#### `GET /characters/me`

- 목적: 캐릭터 snapshot 조회.
- 응답 필드: character profile, stats, evolution, unlocked items.
- 인증 필요 여부: 필요.

#### `PUT /characters/me/snapshot`

- 목적: 클라이언트 계산 결과 snapshot 저장.
- 요청 필드: level, totalExp, stats, evolutionType, unlockedItems, syncVersion, updatedAt.
- 응답 필드: saved snapshot, serverUpdatedAt.
- 서버 검증: 값 범위, stat upper bound, Privacy Guard.

#### `PATCH /characters/me/stats`, optional/later

- 목적: 작은 stat update.
- 요청 필드: stat deltas, source sessionId.
- 응답 필드: updated stats.
- 서버 검증: delta 상한, sessionId 존재 여부.
- MVP 정책: `PUT /characters/me/snapshot`을 기본 저장 방식으로 사용한다.
- 제외 이유: 부분 stat update는 sync conflict, 부정 업로드 검증, idempotency 처리가 복잡해질 수 있으므로 MVP 필수에서 제외한다.

#### `GET /characters/me/history`

- 목적: 성장 히스토리 조회.
- 응답 필드: session summary 기반 history.

### Session Summary

#### `POST /sessions/summary`

- 목적: privacy-safe session summary 업로드.
- 요청 필드: SessionSummary allowlist.
- 응답 필드: saved summary id, syncVersion.
- 서버 검증: 금지 필드 거부, 값 range, idempotency key, exp/stat delta 상한.
- 인증 필요 여부: 필요.

#### `GET /sessions/summary`

- 목적: 세션 요약 목록 조회.
- 요청 필드: paging, date range, workType optional.
- 응답 필드: summaries.
- 인증 필요 여부: 필요.

#### `DELETE /sessions/summary/:id`

- 목적: 특정 세션 요약 삭제.
- 응답 필드: deleted tombstone.
- 서버 검증: ownership.

### Sync

#### `POST /sync/pull`

- 목적: 서버 상태 가져오기.
- 요청 필드: `lastSyncVersion`, `deviceId`.
- 응답 필드: changed character snapshot, session summaries, settings 일부, tombstones.

#### `POST /sync/push`

- 목적: 클라이언트 pending queue 업로드.
- 요청 필드: `CharacterSnapshotSyncRequest`, `SessionSummaryUploadRequest`, `SettingsSyncRequest`, idempotency keys.
- 응답 필드: accepted items, rejected items, new syncVersion.
- 서버 검증: Privacy Guard, ownership, version.

#### `POST /sync/resolve`, later

- 목적: conflict resolution.
- MVP에서는 last-write-wins로 단순화.

### Achievement

#### `GET /achievements`

- 목적: 업적 정의 조회.

#### `GET /achievements/me`

- 목적: 사용자 업적 상태 조회.

#### `POST /achievements/evaluate`, optional/later

- 목적: 최신 summary 기반 업적 평가.
- 서버 검증: 과도한 호출 rate limit.
- MVP 정책: 업적 평가는 클라이언트 계산 + 서버 저장 방식으로 시작할 수 있다.
- 서버는 기본 업적 검증 또는 저장만 담당한다.
- 복잡한 업적 평가, 시즌 연동, 리더보드 연계는 later로 분리한다.

### Privacy

#### `GET /privacy/export`

- 목적: 사용자 서버 저장 데이터 export.
- 응답 필드: profile, character, session summaries, achievements, consents.
- 원본 개발 데이터는 서버에 없으므로 export에도 없다.

#### `DELETE /privacy/data`

- 목적: 사용자 서버 데이터 삭제.
- 응답 필드: deletion result.

### Health

#### `GET /health`

- 목적: 서버 상태 확인.
- 인증 필요 여부: 없음.

---

## 9. Privacy Guard 설계

Privacy Guard는 서버 API의 핵심 방어선이다. 클라이언트가 버그나 악의적 조작으로 금지 데이터를 보내더라도 서버가 저장하지 않도록 막는다.

거부해야 할 필드/패턴:

- `prompt`
- `rawPrompt`
- `code`
- `rawCode`
- `log`
- `rawLog`
- `terminalOutput`
- `stdout`
- `stderr`
- `diff`
- `patch`
- `filePath`
- `absolutePath`
- `gitRemoteUrl`
- `branchNameRaw`
- `commitMessageRaw`
- `apiKey`
- `secret`
- `password`
- 임의 payload의 `token`, `rawToken`, `apiToken`, `accessToken`, `secretToken` 등. 단 인증 모듈에서 명시적으로 허용한 `refreshToken`, 서버가 정의한 `tokenBucket`, `tokenRange` 같은 allowlist 필드는 예외.
- `Authorization`
- `Bearer`

정책:

- DTO validation 단계에서 reject.
- 요청 body size 제한.
- allowlist 기반 DTO 설계.
- Privacy Guard는 단순 blocklist가 아니라 endpoint별 allowlist를 우선한다.
- `Authorization` 헤더의 Bearer token은 인증 레이어에서만 처리하고 request body에 저장하지 않는다.
- suspicious payload logging 시 원문 로그 금지.
- 에러 메시지에도 원문 반영 금지.
- 테스트로 금지 필드 거부 검증.
- nested object와 array도 재귀적으로 검사.
- Privacy Guard 위반 로그에는 token 원문을 남기지 않는다.

에러 응답:

- `FORBIDDEN_PAYLOAD`
- 사용자 메시지: "요청에 서버가 저장할 수 없는 원본 개발 데이터 형태가 포함되어 있습니다."
- 원본 필드 값은 응답하지 않는다.

---

## 10. 인증 / 계정 설계

### Guest Mode

- 클라이언트는 서버 없이 로컬 전용으로 사용 가능하다.
- 서버 guest 계정 발급은 opt-in sync의 준비 단계로 제공할 수 있다.
- Guest Mode에서도 원본 개발 데이터는 로컬 저장 금지 정책을 따른다.

### 이메일/비밀번호 또는 소셜 로그인 후보

- MVP 우선: 이메일/비밀번호.
- 추후: Google, GitHub, Apple, Steam.
- 소셜 provider token 저장은 최소화하고 필요 시 암호화한다.

### JWT access/refresh

- access token은 짧은 만료.
- refresh token은 rotation.
- refresh token 원문은 DB에 저장하지 않고 hash만 저장한다.

### Account deletion

- 사용자가 서버 저장 데이터를 삭제할 수 있어야 한다.
- 삭제 시 character, session summary, achievements, refresh tokens를 제거하거나 tombstone 처리한다.

### Guest -> registered account migration

- 클라이언트 로컬 SaveData를 로그인 계정에 업로드할 수 있다.
- 업로드 전 Privacy Guard와 sync payload sanitizer를 통과해야 한다.

### Steam 계정 연동 later

- Steamworks 연동은 later.
- Steam ID와 User 연결 모델만 확장 가능하게 둔다.

---

## 11. 동기화 설계

정책:

- 클라이언트 로컬 우선.
- opt-in cloud sync.
- character snapshot sync.
- session summary sync.
- settings partial sync.
- last-write-wins MVP.
- `syncVersion`.
- `updatedAt`.
- `deletedAt` tombstone.
- conflict handling later.
- offline queue.
- idempotency key.
- 중복 세션 방지.

흐름:

1. 클라이언트가 로컬 분석과 성장 계산을 수행한다.
2. privacy-safe DTO를 만든다.
3. Sync Client가 pending queue에 넣는다.
4. 로그인 + sync enabled 상태에서 `/sync/push` 호출.
5. 서버는 Privacy Guard와 값 범위 검증 후 저장한다.
6. 다른 기기에서는 `/sync/pull`로 snapshot과 summary를 받는다.

서버는 SaveData 전체나 AgentWorkSession 전체를 받지 않는다. Sync API는 명시적 DTO만 받으며, DTO에 정의되지 않은 필드는 거부한다.

충돌 정책:

- MVP는 last-write-wins.
- 기준은 `updatedAt`과 `syncVersion`.
- 추후 version vector 또는 field-level merge 검토.

삭제 동기화:

- 삭제 요청은 tombstone으로 전달한다.
- 서버는 삭제된 session summary를 다시 내려주지 않는다.

---

## 12. 성장 계산 서버 검증 정책

MVP에서는 성장 계산이 클라이언트에서 이루어진다.

서버는 MVP에서 원본 Git/Agent 데이터를 받지 않으므로 성장 결과를 완전히 재계산하지 않는다. 서버는 클라이언트가 보낸 성장 결과와 세션 요약의 일관성만 최소 검증한다.

서버 검증:

- 값 범위.
- exp 증가량 상한.
- daily cap 요약.
- stat delta 상한.
- `sessionSummary` 필드 allowlist.
- 비정상적인 반복 업로드 rate limit.
- 동일 `sessionId` 중복 업로드 방지.
- `sessionSummary.workType`과 `statDeltas` 조합이 허용 가능한 범위인지.
- `expGained`가 `tokenBucket`, `changedFileCountBucket`, `resultStatus` 기준 상한을 넘지 않는지.
- 하루 총 EXP 증가량이 soft cap을 과도하게 넘지 않는지.
- `confidence`가 너무 낮은 세션은 시즌/리더보드/공개 통계에 사용하지 않는지.
- 비정상적인 반복 업로드는 rate limit 또는 temporary block 처리하는지.

향후 리더보드/시즌 경쟁 기능이 들어오면:

- 서버 측 성장 계산 또는 부분 재계산 검토.
- 클라이언트 신뢰도 등급.
- signed payload 또는 local proof는 조사 필요.
- anti-cheat는 MVP 핵심이 아님.

이 정책의 목적은 MVP 속도를 유지하면서도 명백히 비정상적인 데이터가 서버에 쌓이는 것을 줄이는 것이다.

---

## 13. 업적 / 시즌 / 리더보드 설계

### MVP

- 업적은 클라이언트 계산 + 서버 저장 방식으로 시작할 수 있다.
- 서버는 기본 업적 검증 또는 저장만 담당한다.
- 시즌/리더보드는 데이터 모델만 고려하거나 비활성화한다.

### 추후

- season progress.
- weekly summary.
- personal leaderboard.
- friends leaderboard.
- public leaderboard는 privacy 리스크로 신중히.
- 팀/조직 랭킹은 기본 제외.

주의:

- 리더보드에는 원본 개발 데이터가 절대 들어가지 않는다.
- 팀/회사 생산성 비교 도구처럼 보이는 기능은 제품 방향과 privacy 원칙을 해칠 수 있다.

---

## 14. 보안 설계

필수 보안 항목:

- rate limit.
- CORS.
- Helmet.
- request validation.
- JWT expiration.
- refresh token rotation.
- password hashing.
- SQL injection은 Prisma로 방어하되 raw query 최소화.
- structured logging.
- secret env 관리.
- `.env.example`.
- audit log는 최소화하고 원문 저장 금지.
- account deletion.
- data export.

로깅 정책:

- request id, user id, endpoint, status, duration 정도만 기록한다.
- request body 원문은 기록하지 않는다.
- Privacy Guard 위반 payload의 원문 값은 로그에 남기지 않는다.

---

## 15. 서버 테스트 전략

테스트 대상:

- Auth flow test.
- Guest migration test.
- Character snapshot test.
- SessionSummary validation test.
- Privacy Guard forbidden payload test.
- Sync conflict basic test.
- Account deletion test.
- Rate limit test.
- DTO validation test.
- Unknown field reject test.
- Optional/later API가 MVP 필수 플로우에 포함되지 않는지 contract test.

Privacy Guard 테스트 예:

- `rawPrompt` 포함 요청은 거부된다.
- `gitRemoteUrl` 포함 요청은 거부된다.
- nested `diff` 필드 포함 요청은 거부된다.
- 에러 응답에 원본 값이 포함되지 않는다.
- 로그 mock에 원본 payload가 남지 않는다.

---

## 16. 서버 MVP 범위

포함:

- NestJS server skeleton.
- Auth guest/login/refresh.
- User profile.
- Character snapshot sync.
- Session summary upload/list/delete.
- Privacy Guard.
- Sync DTO validation.
- Data deletion/export.
- Health check.
- Basic achievement storage 또는 minimal evaluation.
- Minimal sync pull/push.

Optional/Later:

- Partial character stat patch.
- Complex achievement evaluation.
- Season/leaderboard.
- WebSocket sync.
- Steamworks full integration.
- Complex anti-cheat.

제외:

- 원본 로그 저장.
- 원본 코드/프롬프트 분석.
- 팀/회사 생산성 분석.
- public leaderboard.
- 결제/과금.

---

## 17. 서버 리스크와 대응

- 개인정보 유출: 서버가 원본을 받지 않는 allowlist/Privacy Guard 구조로 피해 범위를 줄인다.
- 클라이언트 조작 데이터: 값 범위, rate limit, exp/stat delta 상한 적용.
- 성장값 부정 업로드: MVP는 완전 anti-cheat보다 명백한 이상치 차단 우선.
- 과도한 서버 범위: Auth, Character, SessionSummary, Sync, Privacy Guard만 MVP 필수로 둔다.
- 동기화 충돌: last-write-wins MVP, 추후 conflict resolution.
- 계정 삭제/데이터 삭제 누락: deletion test와 tombstone 정책.
- 로그에 민감 정보 남김: request body logging 금지.
- 토큰/세션 탈취: JWT 만료, refresh rotation, token hash 저장.
- DB 마이그레이션 실수: migration review, staging DB, backup.

---

## 18. 서버 개발 순서

1. NestJS 프로젝트 생성.
2. Prisma/PostgreSQL 설정.
3. User/Auth 모델.
4. Guest auth.
5. JWT refresh flow.
6. Character 모델/API.
7. SessionSummary 모델/API.
8. Privacy Guard DTO validation.
9. Sync push/pull.
10. Basic achievement storage 또는 minimal evaluation.
11. Account deletion/export.
12. Tests.
13. Deploy 준비.
