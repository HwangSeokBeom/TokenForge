# TokenForge Client Architecture

## 문서 목적

이 문서는 TokenForge Unity/macOS 클라이언트의 책임, 계층 구조, 도메인 모델, 로컬 분석, 저장, 동기화, 테스트, MVP 범위를 정의한다. 기존 `Docs/technical-design.md`의 방향과 동일하게 TokenForge는 수동 기록 앱이 아니라 **자동 분석 우선 + 수동 보정 fallback** 구조의 macOS 개발자용 Companion RPG다.

## 확정된 내용과 조사 필요 항목

### 확정된 내용

- 클라이언트는 Unity 2D 기반 macOS 앱이다.
- 클라이언트는 원본 개발 데이터에 접근할 수 있는 유일한 계층이다.
- 클라이언트는 사용자가 명시적으로 연결한 로컬 프로젝트 폴더와 AI Agent 로그 위치만 분석한다.
- Git 변경량과 AI Agent 로그는 가능한 범위에서 자동 분석한다.
- 모든 분석 결과는 `AgentWorkSession` 공통 모델로 정규화한다.
- 사용자는 자동 분석이 부족하거나 불확실한 항목만 보정한다.
- 성장 계산은 MVP에서 클라이언트가 수행한다.
- 클라이언트는 원본 프롬프트, 원본 코드, 전체 로그 원문, 터미널 출력 원문, 절대 파일 경로, Git remote URL, 커밋 diff 원문을 저장하지 않고 서버로 보내지 않는다.
- Guest Mode와 로컬 전용 사용을 반드시 지원한다.

### 조사 필요 항목

- Claude Code / Codex 로그 경로, 포맷, 버전별 안정성.
- Cursor/Windsurf 로그 접근 가능성과 MVP 베타 Provider 범위.
- GitHub Copilot, Continue, Aider, Roo Code, Cline, JetBrains AI Assistant의 로컬 로그 접근 가능성.
- Unity macOS 빌드의 폴더 권한 UX.
- macOS sandbox 배포 시 security-scoped bookmarks 필요 여부.
- Swift helper, CLI helper, 메뉴바 앱, 항상 위 창 구현 방식.
- `System.Text.Json` Unity 호환성, Newtonsoft Json for Unity 빌드 영향.
- SQLite/LiteDB Unity/macOS 빌드 호환성.

---

## 1. 클라이언트 역할 정의

TokenForge 클라이언트는 게임 플레이와 로컬 개발 활동 분석의 중심이다. 서버가 원본 개발 데이터를 분석하지 않기 때문에, 클라이언트가 사용자가 허용한 로컬 범위 안에서 Git과 AI Agent 로그를 분석하고 안전한 집계값으로 변환해야 한다.

핵심 역할:

- Unity 2D 기반 macOS 앱.
- 게임 화면과 캐릭터 성장 UI 담당.
- 로컬 프로젝트 폴더 연결.
- Git 자동 분석.
- AI Agent 로그 자동 분석.
- 자동 분석 결과 확인/보정.
- `AgentWorkSession` 생성.
- 성장 계산.
- 캐릭터 상태 반영.
- 미니게임 실행.
- 로컬 저장.
- 서버 동기화 요청.
- 원본 데이터 privacy 보호.

중요한 원칙:

- 클라이언트는 원본 개발 데이터에 접근할 수 있는 유일한 계층이다.
- 원본 프롬프트, 원본 코드, 전체 로그 원문, 터미널 출력은 분석 과정에서만 일시적으로 읽는다.
- 원본 데이터는 저장, 동기화, 디버그 로그, 서버 요청에 남기지 않는다.
- 분석 이후에는 집계값과 confidence/warning만 유지한다.
- 서버에는 안전한 집계값만 보낸다.
- 클라이언트는 오프라인/Guest Mode에서도 동작해야 한다.
- 서버 장애나 네트워크 단절이 게임 플레이와 로컬 성장 루프를 막아서는 안 된다.

이렇게 설계하는 이유는 개발자의 로컬 프로젝트와 AI Agent 로그가 매우 민감하기 때문이다. TokenForge의 제품 신뢰는 "자동 분석"과 "원본 데이터 비저장"을 동시에 만족할 때 생긴다.

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

## 3. 클라이언트 기술 스택

### Engine: Unity 2D

선택 이유:

- macOS standalone 앱을 빠르게 만들 수 있다.
- 캐릭터 룸, 성장 연출, 2D 미니게임, UI를 한 프로젝트에서 구현하기 쉽다.
- Unity Test Framework, ScriptableObject, Sprite/Animation 파이프라인을 활용할 수 있다.

대안:

- Godot: 2D에 강하지만 팀 숙련도와 macOS 네이티브 연동 조사가 필요하다.
- SwiftUI + SpriteKit: macOS 네이티브 UX는 좋지만 Unity 기반 게임 포트폴리오 방향과 다르다.

### Language: C#

선택 이유:

- Unity의 기본 언어다.
- 도메인 모델, Provider 인터페이스, 성장 계산을 타입 안정성 있게 구성할 수 있다.

대안:

- Swift: macOS helper, 메뉴바 앱, 파일 권한 처리에 적합하다.
- Python/Node.js: 분석 프로토타입에는 적합하지만 Unity 런타임 핵심 로직에는 쓰지 않는다.

### Target: macOS

선택 이유:

- Claude Code, Codex, Cursor, Windsurf 등 개발자 도구 사용 환경과 잘 맞는다.
- 로컬 Git repository, 로그 폴더, 파일 시스템 접근이 핵심 기능이다.

대안:

- Windows/Linux: 추후 확장 가능하지만 권한, 경로, 로그 위치, Git 실행 환경이 달라진다.
- iOS: companion 앱 후보지만 로컬 개발 데이터 접근성이 낮다.

### IDE: Rider 또는 VS Code

선택 이유:

- Rider는 Unity C# 분석과 테스트 실행이 강하다.
- VS Code는 AI 코딩 에이전트 워크플로우와 친숙하다.

대안:

- Visual Studio for Mac은 신규 프로젝트 기본 IDE로 권장하지 않는다.

### Local Storage: JSON MVP, SQLite/LiteDB 확장

선택 이유:

- MVP는 `SaveData` 단일 루트 JSON으로 충분하다.
- `Application.persistentDataPath`와 잘 맞고 디버깅이 쉽다.
- 세션 수와 통계 질의가 늘어나면 SQLite/LiteDB로 전환할 수 있다.

대안:

- PlayerPrefs: 설정값에는 가능하지만 성장 로그와 세션 히스토리에는 부적합하다.
- SQLite: 통계와 히스토리 질의에 강하지만 초기 구현 비용이 있다.
- LiteDB: C# 객체 저장 경험이 좋지만 Unity 빌드 호환성 검증이 필요하다.

### Async: Coroutine 또는 UniTask

선택 이유:

- Coroutine은 Unity 기본 기능이라 MVP 도입 비용이 낮다.
- UniTask는 Provider 분석, 저장, 서버 동기화 같은 비동기 흐름을 더 명확히 표현할 수 있다.

권장:

- MVP 초반은 Coroutine으로 시작 가능.
- Provider/Sync가 복잡해지면 UniTask 도입을 검토한다.

### JSON: Newtonsoft Json for Unity 또는 System.Text.Json 검토

선택 이유:

- JSON SaveData와 서버 sync payload 직렬화가 필요하다.
- Newtonsoft는 Unity에서 사용 사례가 많다.
- System.Text.Json은 가볍지만 Unity 버전별 호환성 검증이 필요하다.

대안:

- Unity `JsonUtility`: 단순하지만 dictionary, polymorphism, migration에 제약이 있다.

### Testing: Unity Test Framework

선택 이유:

- EditMode에서 성장 계산, Provider 파서, privacy sanitizer를 빠르게 검증할 수 있다.
- PlayMode에서 UI 플로우, 미니게임, 권한/설정 화면을 검증할 수 있다.

### Asset Pipeline: Aseprite, PNG/SpriteSheet, Unity Sprite

선택 이유:

- 2D 캐릭터와 미니게임 sprite 제작에 적합하다.
- 원본 `.aseprite`와 Unity import용 PNG/SpriteSheet를 분리 관리할 수 있다.

대안:

- Piskel, Krita, Photoshop. 팀 작업 방식과 라이선스에 따라 선택한다.

### Build: macOS standalone build

선택 이유:

- MVP에서 실제 macOS 앱 사용 경험을 검증해야 한다.
- Steam 배포 준비를 고려한 빌드 산출물 구조를 초기에 정리할 수 있다.

### Native Integration 후보

후보:

- Swift helper.
- CLI helper.
- 메뉴바 앱.
- 파일 권한 처리.
- 항상 위 작은 창.

이유:

- Unity 단독으로 macOS 권한, 메뉴바, 항상 위 창, sandbox 권한 UX를 완성하기 어려울 수 있다.
- MVP에서는 Unity 단독을 우선하되 Phase 후반에 helper 필요성을 검증한다.

---

## 4. 클라이언트 전체 아키텍처

계층 구조:

```text
Presentation Layer
  -> Game Flow / Application Layer
    -> Domain Layer
    -> Growth System Layer
    -> Infrastructure / Adapter Layer
      - Agent Data Layer
      - Git Analysis Layer
      - Privacy / Sanitization Layer
      - Persistence Layer
      - Sync Client Layer
      - Platform Integration Layer
    -> Game Feature Layer
      - MiniGame Layer
      - Character Rendering Layer
```

의존 방향은 Presentation에서 Application을 거쳐 Domain/Growth로 향한다. Infrastructure/Adapter Layer는 외부 세계와 연결되는 구현 세부를 담당하며 Domain/Growth는 Unity Scene, 외부 로그 포맷, 서버 DTO에 직접 의존하지 않는다. Presentation은 도메인 규칙을 직접 계산하지 않고, Sync Client는 SaveData 전체가 아니라 safe DTO만 전송한다.

### Presentation Layer

책임:

- Main Dashboard, Auto Analysis Dashboard, Session Review, Character Room, Growth Result, Settings 화면 표시.
- 사용자 입력 수집과 화면 상태 표시.
- confidence/warning, privacy 안내, 권한 오류 표시.

주의사항:

- 성장 공식이나 Provider 파싱을 UI에 넣지 않는다.
- 원본 로그나 경로를 화면에 표시하지 않는다.

### Game Flow / Application Layer

책임:

- 앱 상태 전환 관리.
- 프로젝트 연결 -> 자동 분석 -> 검토/보정 -> 성장 반영 -> 저장/동기화 흐름 조정.
- 미니게임 진입과 결과 반영.

주의사항:

- 도메인 계산은 위임하고, 플로우 orchestration만 담당한다.

### Domain Layer

책임:

- `AgentWorkSession`, `CharacterProfile`, `SaveData`, enum, 값 객체 정의.

주의사항:

- Unity Scene과 외부 로그 포맷에 직접 의존하지 않는다.
- 서버 payload와 1:1로 결합하지 않는다.

### Growth System Layer

책임:

- `AgentWorkSession` 기반 EXP, 스탯, Stress, 진화 진행도 계산.
- Daily cap, anti-grinding, 미니게임 보너스 제한 적용.

주의사항:

- 특정 Provider를 몰라야 한다.
- MVP에서는 클라이언트에서 계산한다.

### Infrastructure / Adapter Layer

책임:

- 외부 세계와 연결되는 구현 세부를 모은다.
- Agent 로그, Git CLI, 파일 시스템, 로컬 저장, 서버 API, macOS 권한을 도메인 모델로 변환한다.

주의사항:

- Adapter는 도메인 규칙을 계산하지 않는다.
- 원본 데이터는 Adapter 내부에서만 일시적으로 읽고, sanitizer를 통과한 집계값만 상위 계층으로 전달한다.

### Agent Data Layer

책임:

- `AgentLogProvider` 인터페이스와 구현체 관리.
- 원본 로그 또는 Git 분석 결과를 `AgentWorkSession`으로 정규화.
- confidence/warning 제공.

주의사항:

- Provider는 원본 로그를 저장하지 않는다.
- 원본 데이터는 Privacy/Sanitization Layer를 통과해 집계값으로만 남는다.

### Git Analysis Layer

책임:

- Git repository 감지.
- Git 명령 실행과 timeout 처리.
- diff/stat/numstat/log/branch 결과 파싱.
- `GitChangeSummary` 생성.

주의사항:

- 원본 절대 경로, remote URL, diff 원문을 저장하지 않는다.

### Privacy / Sanitization Layer

책임:

- 민감 필드 제거, 마스킹, 해시 처리.
- 저장/동기화 payload 검사.
- forbidden field detection.

주의사항:

- 클라이언트 내부 debug log에도 원문을 남기지 않는다.

### Persistence Layer

책임:

- SaveData JSON 저장/로드.
- saveVersion, migration, 자동 저장, 삭제 처리.

주의사항:

- 금지 데이터가 SaveData 모델에 들어가지 않게 한다.

### Sync Client Layer

책임:

- 서버 인증 상태 관리.
- 클라우드 sync opt-in 처리.
- character snapshot, session summary, settings 일부 동기화.
- offline queue와 retry.
- `SaveData` 전체가 아니라 `SafeSyncPayload`, `SessionSummaryUploadRequest`, `CharacterSnapshotSyncRequest` 같은 명시적 DTO만 전송.

주의사항:

- 원본 데이터를 서버로 보내지 않는다.
- sync payload는 allowlist 기반으로 만든다.
 
### Game Feature Layer

책임:

- 게임으로서의 기능 구현을 담당한다.
- 미니게임, 캐릭터 렌더링, 연출, 입력 반응을 포함한다.

주의사항:

- 게임 기능은 Growth System의 결과를 표시하거나 보조 보상을 생성하지만, Provider 분석과 sync payload 생성에는 관여하지 않는다.

### Platform Integration Layer

책임:

- macOS 파일/폴더 선택.
- Git 실행 가능 여부 확인.
- 향후 Swift helper, CLI helper, 메뉴바 앱 연동.

### MiniGame Layer

책임:

- 토큰 수집 미니게임 실행.
- 점수, 일시정지, 결과 생성.
- 성장 보너스 제한값 반환.

### Character Rendering Layer

책임:

- 캐릭터 sprite, animation, 진화 외형, mood state 표시.
- 성장 결과와 현재 stats에 따른 시각적 반응.

---

## 5. 클라이언트 폴더 구조

권장 구조:

```text
UnityClient/
  Assets/
    _Project/
      Scripts/
        Domain/
        Growth/
        Agents/
        Git/
        Privacy/
        Persistence/
        Sync/
        Platform/
        UI/
        MiniGame/
        Character/
        Common/
      Art/
      Audio/
      Prefabs/
      Scenes/
      Tests/
  ProjectSettings/
  Packages/
```

폴더 역할:

- `Scripts/Domain/`: 핵심 모델, enum, 값 객체. Unity Scene 의존 최소화.
- `Scripts/Growth/`: 성장 계산, 진화 판정, 밸런싱 규칙.
- `Scripts/Agents/`: Agent Provider 인터페이스와 구현체.
- `Scripts/Git/`: Git 명령 실행, 파서, `GitChangeSummary` 생성.
- `Scripts/Privacy/`: sanitizer, hash/masking, sync payload 검사.
- `Scripts/Persistence/`: SaveData 저장/로드, migration, 삭제.
- `Scripts/Sync/`: 서버 API client, auth state, offline queue.
- `Scripts/Platform/`: macOS 권한, 파일 선택, Git 실행 확인, helper 연동.
- `Scripts/UI/`: 화면 Presenter/ViewModel/Controller.
- `Scripts/MiniGame/`: 미니게임 룰, 입력, 결과.
- `Scripts/Character/`: 캐릭터 렌더링, animation, evolution visuals.
- `Scripts/Common/`: 공통 Result 타입, clock, id generator, utility.
- `Art/`: Unity import용 PNG, SpriteSheet, atlas.
- `Audio/`: 효과음, 배경음.
- `Prefabs/`: UI, 캐릭터, 미니게임 object.
- `Scenes/`: Bootstrap, Main, MiniGame 등.
- `Tests/`: EditMode/PlayMode 테스트.

### ArtSource 분리 원칙

원본 아트 파일은 Unity import 리소스와 분리하는 것이 좋다.

후보:

```text
ArtSource/
  Aseprite/
  References/
  Exports/
```

원칙:

- `.aseprite`, PSD 등 원본 파일은 `ArtSource/`에 둔다.
- Unity에서 직접 사용하는 PNG/SpriteSheet는 `UnityClient/Assets/_Project/Art/`에 둔다.
- export 규칙과 파일명 규칙을 문서화한다.
- Unity import 리소스는 빌드 크기와 import 설정 기준으로 관리한다.

---

## 6. 클라이언트 핵심 도메인 모델

실제 C# 코드는 작성하지 않고 필드 설계 수준으로 정의한다.

### `AgentWorkSession`

- 역할: 자동 분석 또는 fallback 보정으로 확정된 개발 작업 세션.
- 주요 필드: `sessionId`, `agentType`, `workType`, `startedAt`, `endedAt`, `tokenUsageBucket`, `actionSummary`, `gitChangeSummary`, `resultStatus`, `sourceProvider`, `parserVersion`, `confidence`, `warnings`.
- 저장 가능 데이터: 집계된 토큰 range, 파일 변경량 bucket, 테스트/빌드 결과, confidence.
- 저장하면 안 되는 데이터: 프롬프트 원문, 코드, 로그 원문, 터미널 출력, 절대 경로, Git remote URL, diff 원문.
- 서버 동기화 여부: 요약값만 동기화 가능.

### `AgentType`

- 역할: 데이터 출처 또는 AI Agent 종류.
- 값 예시: `ClaudeCode`, `Codex`, `Cursor`, `Windsurf`, `GitHubCopilot`, `Continue`, `Aider`, `RooCode`, `Cline`, `JetBrainsAI`, `GitOnly`, `ManualFallback`, `Unknown`.
- 저장 가능 데이터: enum 값.
- 저장하면 안 되는 데이터: Agent 로그 경로.
- 서버 동기화 여부: 가능.

### `WorkType`

- 역할: 작업 성격과 성장 매핑 기준.
- 값 예시: `Feature`, `Bugfix`, `Refactor`, `Test`, `UIUX`, `Docs`, `Build`, `Chore`, `Research`, `Mixed`, `Unknown`.
- 저장 가능 데이터: enum 값, confidence.
- 저장하면 안 되는 데이터: 커밋 메시지 원문.
- 서버 동기화 여부: 가능.

### `AgentActionSummary`

- 역할: Agent 행동 집계.
- 주요 필드: `promptCount`, `toolCallCount`, `fileEditCount`, `commandRunCount`, `testRunCount`, `buildRunCount`, `failedCommandCount`, `durationBucket`.
- 저장 가능 데이터: count, bucket.
- 저장하면 안 되는 데이터: command 문자열, stdout/stderr 원문.
- 서버 동기화 여부: 제한 가능.

### `GitChangeSummary`

- 역할: Git 변경량과 파일 카테고리 집계.
- 주요 필드: `changedFileCount`, `addedLineBucket`, `deletedLineBucket`, `testFileChanged`, `docsFileChanged`, `uiFileChanged`, `architectureFileChanged`, `fileCategoryCounts`, `workingTreeStatus`, `projectPathHash`.
- 저장 가능 데이터: 집계값, 해시, 사용자가 입력한 alias.
- 저장하면 안 되는 데이터: 절대 경로, remote URL, diff 원문, 브랜치명 원문, 커밋 메시지 원문.
- 서버 동기화 여부: 요약값만 가능.

### `CharacterProfile`

- 역할: 캐릭터 상태.
- 주요 필드: `characterId`, `displayName`, `level`, `totalExp`, `currentEvolutionType`, `stats`, `appearanceVariant`, `moodState`, `unlockedItems`.
- 저장 가능 데이터: 전체.
- 저장하면 안 되는 데이터: 없음. 단 사용자 displayName은 부적절 문자열 검토 필요.
- 서버 동기화 여부: 가능.

### `CharacterStats`

- 역할: 성장 스탯.
- 주요 필드: `Logic`, `Debug`, `Architecture`, `Design`, `Stability`, `Velocity`, `Creativity`, `Efficiency`, `Stress`.
- 저장 가능 데이터: 전체.
- 저장하면 안 되는 데이터: 없음.
- 서버 동기화 여부: 가능.

### `CharacterGrowthResult`

- 역할: 한 세션의 성장 결과.
- 주요 필드: `sessionId`, `expGained`, `levelBefore`, `levelAfter`, `statDeltas`, `stressDelta`, `evolutionProgressDelta`, `rewardTags`, `dailyCapApplied`.
- 저장 가능 데이터: 전체.
- 저장하면 안 되는 데이터: 원본 세션 원문.
- 서버 동기화 여부: 요약 가능.

### `EvolutionType`

- 역할: 캐릭터 진화 성향.
- 값 예시: `Debugger`, `Guardian`, `CleanCodeArchitect`, `TokenBerserker`, `ProductBard`, `EfficiencyNinja`, `PromptSummoner`, `IncidentSurvivor`, `RealtimeRanger`.
- 서버 동기화 여부: 가능.

### `GrowthRule`

- 역할: 성장 가중치 규칙.
- 주요 필드: `ruleId`, `targetWorkType`, `conditions`, `baseExp`, `statWeights`, `tokenMultiplier`, `fileChangeMultiplier`, `successBonus`, `failureCompensation`, `stressPenalty`.
- 저장 가능 데이터: 로컬 밸런싱 설정.
- 저장하면 안 되는 데이터: 없음.
- 서버 동기화 여부: 기본적으로 불필요. 서버 config로 확장 가능.

### `MiniGameSession`

- 역할: 미니게임 결과.
- 주요 필드: `miniGameSessionId`, `durationSeconds`, `score`, `tokensCollected`, `bugsAvoided`, `testShieldsCollected`, `buildGaugeFilled`, `resultGrade`, `growthBonus`.
- 저장 가능 데이터: 요약값.
- 저장하면 안 되는 데이터: 없음.
- 서버 동기화 여부: 보너스 요약만 가능.

### `SaveData`

- 역할: 로컬 저장 루트.
- 주요 필드: `saveVersion`, `characterProfile`, `workSessionSummaries`, `growthHistory`, `connectedProjects`, `providerSettings`, `syncState`, `privacyPreferences`, `dailyProgress`.
- 저장 가능 데이터: privacy-safe 데이터만.
- 저장하면 안 되는 데이터: 원본 프롬프트/코드/로그/터미널 출력/절대 경로/Git remote URL/diff.
- 서버 동기화 여부: 직접 전체 업로드 금지. Sync DTO로 변환 후 일부만 전송.

### `ConnectedProject`

- 역할: 사용자가 연결한 로컬 프로젝트.
- 주요 필드: `localOnlyProjectId`, `projectAlias`, `projectPathHash`, `isGitRepository`, `lastAnalyzedAt`, `analysisEnabled`.
- 저장 가능 데이터: 로컬 alias, hash, 상태.
- 저장하면 안 되는 데이터: 절대 경로, remote URL.
- 서버 동기화 여부: 로컬 전용 기본. 서버에는 가능하면 `projectPathHash` 또는 `localOnlyProjectId`만 보낸다.
- `projectAlias`는 기본적으로 로컬 전용이며, 사용자가 명시적으로 허용한 경우에만 동기화한다.
- 자동 추출한 프로젝트명, 폴더명, repository name은 서버로 전송하지 않는다.
- 사용자가 직접 입력한 alias도 민감 정보일 수 있으므로 동기화 전 안내가 필요하다.

### `ProviderSettings`

- 역할: Agent Provider 설정.
- 주요 필드: `providerId`, `enabled`, `logLocationBookmark`, `lastScanAt`, `parserVersion`, `pollingInterval`.
- 저장 가능 데이터: macOS 권한 token/bookmark는 로컬에만 저장.
- 저장하면 안 되는 데이터: 로그 파일 원문.
- 서버 동기화 여부: 일반 설정 일부만 가능. 로컬 경로/권한 정보는 동기화 금지.
- `logLocationBookmark`는 macOS sandbox/security-scoped bookmark 도입 시 사용하는 로컬 전용 권한 참조값이다.
- Unity 단독 MVP에서는 단순 경로 또는 사용자가 재선택하는 방식으로 시작할 수 있다.
- sandbox 배포 단계에서는 Swift helper를 통한 bookmark 저장을 검토한다.
- `logLocationBookmark`는 서버 동기화 금지 데이터다.

### `SyncState`

- 역할: 클라우드 동기화 상태.
- 주요 필드: `isLoggedIn`, `syncEnabled`, `lastSyncAt`, `syncVersion`, `pendingQueueCount`, `lastErrorCode`.
- 저장 가능 데이터: 전체.
- 저장하면 안 되는 데이터: auth refresh token은 별도 안전 저장 검토.
- 서버 동기화 여부: 일부 필요.

### `PrivacyPreferences`

- 역할: 사용자 privacy 설정.
- 주요 필드: `agentLogAnalysisEnabled`, `gitAnalysisEnabled`, `cloudSyncEnabled`, `telemetryOptIn`, `maskProjectAlias`, `deleteDataRequestedAt`.
- 저장 가능 데이터: 전체.
- 저장하면 안 되는 데이터: 민감 원문.
- 서버 동기화 여부: 일부 가능.

---

## 7. Agent Provider 설계

공통 Provider 인터페이스 개념:

- 접근 가능 여부 확인.
- 분석 실행.
- 집계값 추출.
- privacy sanitization.
- `AgentWorkSession` 반환.
- confidence/warning 반환.
- 실패 시 fallback 제공.

Provider는 원본 로그를 성장 시스템에 직접 전달하지 않는다. 모든 Provider는 집계값을 만들고 원본을 폐기한다.

### Tier 1: MVP 우선 지원

#### `GitDiffProvider`

- 입력: 사용자가 선택한 프로젝트 폴더.
- 출력: `GitChangeSummary`, WorkType 후보, `AgentWorkSession`.
- 실패 케이스: Git 미설치, 저장소 아님, 권한 부족, timeout, 대형 diff.
- fallback 전략: 부분 결과 표시, 부족한 값만 Session Review에서 보정.
- 조사 필요 항목: macOS sandbox에서 Git 실행과 폴더 권한 유지 방식.

#### `ClaudeCodeLogProvider`

- 입력: 사용자가 지정한 Claude Code 로그 폴더 또는 세션 파일.
- 출력: token usage bucket, prompt/tool/file edit/command count, test/build 추정값, `AgentWorkSession`.
- 실패 케이스: 경로 미확정, 포맷 변경, 필드 누락, 권한 부족.
- fallback 전략: 파싱 가능한 필드만 사용하고 warning 표시.
- 조사 필요 항목: 로그 위치, schema, 버전별 필드 변화, 민감 정보 포함 가능성.

#### `CodexLogProvider`

- 입력: 사용자가 지정한 Codex 로그 또는 세션 기록 위치.
- 출력: token usage bucket, command run count, patch/file edit count, test/build 추정값, `AgentWorkSession`.
- 실패 케이스: 실행 환경별 로그 위치 차이, 포맷 변경, 권한 부족.
- fallback 전략: GitDiffProvider 결과와 병합하고 부족 값만 보정.
- 조사 필요 항목: Codex 로컬 세션 저장 구조와 parser version 정책.

### Tier 2: MVP 베타/skeleton

#### `CursorLogProvider` skeleton

- 입력: 사용자가 지정한 Cursor 관련 로그 위치.
- 출력: 가능한 경우 AgentType, duration, action count 요약.
- 실패 케이스: 로그 접근 불가, 포맷 불명확, IDE 내부 저장 정책 변경.
- fallback 전략: Provider 비활성화 후 GitDiffProvider 중심 분석.
- 조사 필요 항목: Composer/Agent/Chat 기록 접근 가능성.

#### `WindsurfLogProvider` skeleton

- 입력: 사용자가 지정한 Windsurf 로그 위치.
- 출력: 가능한 경우 AgentType, command/tool/file edit 요약.
- 실패 케이스: Cascade/Agent 로그 경로 불명확, 권한 문제.
- fallback 전략: GitDiffProvider 중심 분석.
- 조사 필요 항목: Windsurf 로컬 로그 구조.

### Tier 3: Future Provider

대상:

- `GitHubCopilotLogProvider`
- `ContinueLogProvider`
- `AiderLogProvider`
- `RooCodeLogProvider`
- `ClineLogProvider`
- `JetBrainsAILogProvider`

공통 전략:

- MVP에서는 Provider interface와 enum 확장 지점만 준비한다.
- 로그 접근 가능성, 정책, 포맷 안정성을 조사한 뒤 구현한다.
- 비공개 내부 포맷에 강하게 의존하지 않는다.

### `ManualFallbackProvider`

- 역할: 자동 분석 실패 또는 부족한 데이터 보완용 fallback.
- 입력: 사용자가 수정한 WorkType, token range, 파일 수, 테스트/빌드 결과.
- 출력: 보정된 `AgentWorkSession`.
- 주의사항: 메인 Provider처럼 강조하지 않는다.
- 서버 동기화: 다른 세션과 동일하게 요약만 가능.

---

## 8. Git Analysis 설계

Git 분석은 MVP의 1차 자동 데이터 소스다.

흐름:

1. 사용자가 프로젝트 폴더를 선택한다.
2. 클라이언트가 Git repository 여부를 감지한다.
3. Git 명령을 timeout과 함께 실행한다.
4. 결과를 파싱해 `GitChangeSummary`를 만든다.
5. WorkType 후보와 confidence를 계산한다.
6. `AgentWorkSession`에 병합한다.
7. 사용자는 Session Review에서 부족한 값만 보정한다.

사용 명령:

- `git status --short`
- `git diff --stat`
- `git diff --numstat`
- `git log --oneline -n 20`
- `git branch --show-current`

파일 경로 카테고리 분류:

- Test: `test`, `tests`, `spec`, `*.Tests.cs`
- Docs: `docs`, `README`, `*.md`
- UI: `ui`, `view`, `screen`, `component`, `prefab`
- Architecture: `service`, `repository`, `usecase`, `di`, `container`
- Config/Build: `.github`, `package`, `config`, `settings`, `ci`
- Domain: `domain`, `model`, `entity`

WorkType 추론:

- 테스트 파일 비중이 높으면 `Test`.
- docs 파일 비중이 높으면 `Docs`.
- UI/prefab/asset 변경이 많으면 `UIUX`.
- service/repository/usecase/DI 변경이 많으면 `Refactor` 또는 Architecture 성향.
- 커밋 키워드에 fix/bug/hotfix가 많으면 `Bugfix`.
- 신규 파일과 기능 경로 변경이 많으면 `Feature`.
- 신호가 섞이면 `Mixed`.

실패 처리:

- Git 미설치: Git 분석 비활성화, Agent 로그 분석 또는 Manual Fallback 안내.
- 저장소 아님: 프로젝트 연결 상태에 warning 표시.
- timeout: 부분 결과만 사용.
- 대형 diff: numstat 범위 제한, 전체 diff 원문 읽기 금지.

Privacy:

- 원본 경로 저장 금지.
- 절대 경로는 `projectPathHash` 또는 `localOnlyProjectId`로 대체.
- 파일 경로는 카테고리 분류 후 폐기하거나 마스킹한다.
- Git remote URL, 커밋 diff 원문, 브랜치명 원문, 커밋 메시지 원문은 저장/전송하지 않는다.

---

## 9. Session Merge / Deduplication

GitDiffProvider와 AgentLogProvider는 같은 개발 작업을 서로 다른 데이터 소스에서 감지할 수 있다. 예를 들어 Claude Code 세션 로그가 파일 편집과 명령 실행을 감지하고, 동시에 GitDiffProvider가 같은 프로젝트의 변경 파일 수와 diff stat을 감지할 수 있다. 이 경우 중복 성장 보상이 발생하지 않도록 세션 병합과 deduplication 정책이 필요하다.

### 병합 후보 기준

- 시간 범위가 겹치는가.
- 동일 `projectPathHash` 또는 사용자가 허용한 `projectAlias`에 속하는가.
- 변경 파일 수와 Agent file edit count가 유사한가.
- Agent working directory와 연결 프로젝트가 매칭되는가.
- Git 분석과 Agent 로그의 WorkType 후보가 크게 충돌하지 않는가.
- 사용자가 Session Review 화면에서 병합 여부를 확인했는가.

### MVP 정책

- 자동 병합은 보수적으로 수행한다.
- confidence가 높을 때만 자동 병합한다.
- 확신이 낮으면 별도 세션 후보로 보여주고 사용자가 선택하게 한다.
- 동일 작업이 중복 성장 보상으로 이어지지 않도록 `deduplicationKey` 또는 `sourceSessionIds`를 둔다.
- 병합된 세션은 `sourceProviders: [GitDiffProvider, ClaudeCodeLogProvider]` 같은 provider list 또는 `GitAndAgent` source 형태로 표현한다.
- 병합 결과도 원본 로그/경로/diff 없이 집계값만 유지한다.

---

## 10. 성장 시스템 설계

성장 시스템은 `AgentWorkSession`을 입력으로 받아 `CharacterGrowthResult`를 만든다.

계산 요소:

- Base EXP.
- Token bucket/range multiplier.
- File change multiplier.
- Success bonus.
- Failure compensation.
- Stress penalty.
- Daily cap.
- Anti-grinding.
- MiniGame bonus cap.

스탯:

- Logic
- Debug
- Architecture
- Design
- Stability
- Velocity
- Creativity
- Efficiency
- Stress

작업 유형별 매핑:

- Feature: Logic, Velocity, Creativity.
- Bugfix: Debug, Stability.
- Refactor: Architecture, Logic, Efficiency.
- Test: Stability, Debug.
- UI/UX: Design, Creativity.
- Docs: Architecture, Efficiency.
- Build/CI: Stability, Debug.
- Large Diff + High Token: EXP 증가, Stress 증가.
- Small Token + Success: Efficiency 증가.
- Failed Build/Test: Stress 증가, Debug 성장 후보.

진화 타입 후보:

- Debugger: Debug와 Bugfix/Test 비중 높음.
- Guardian: Stability와 테스트 성공 비중 높음.
- Clean Code Architect: Refactor/Docs/Architecture 변경 비중 높음.
- Token Berserker: high token + large diff 누적.
- Efficiency Ninja: small token + success 누적.

미니게임 보너스 제한:

- 미니게임은 핵심 성장 루프를 압도하지 않는다.
- 보너스는 최종 보상의 5~15% 이내로 제한한다.
- 작업 세션 없이 미니게임만 반복하는 경우 soft cap을 적용한다.

확장:

- MVP는 코드 상수 기반 GrowthRule로 시작.
- 이후 ScriptableObject 또는 JSON 밸런싱 테이블로 전환.
- 서버 리더보드가 생기면 서버 검증 정책과 성장 계산 책임 재검토.

---

## 11. UI/UX 설계

### Main Dashboard

- 목적: 캐릭터 상태, 최근 자동 분석 세션, sync 상태, 미니게임 진입점 표시.
- 주요 컴포넌트: 캐릭터 카드, Level/EXP, 스탯 요약, `Analyze Now`, `Review Detected Session`, sync badge.
- 오류/권한 상태: Git/log 권한 오류 표시.
- privacy 안내: 원본 데이터는 서버로 전송하지 않는다는 요약 표시.

### Auto Analysis Dashboard

- 목적: 연결된 프로젝트와 Agent 로그에서 분석 가능한 세션 감지.
- 주요 컴포넌트: 프로젝트 목록, Git 변경 카드, Agent 세션 카드, confidence, warning.
- 사용자 플로우: 감지 -> 분석 실행 -> Session Review 이동.
- 오류/권한 상태: 권한 없음, Git 없음, Provider 실패.

### Project Connection Screen

- 목적: 로컬 프로젝트 폴더 연결.
- 주요 컴포넌트: 폴더 선택, Git 감지 결과, project alias, 분석 ON/OFF.
- privacy 안내: 절대 경로/remote URL 저장 금지 안내.

### Agent Log Connection Screen

- 목적: Claude/Codex 등 로그 위치 연결.
- 주요 컴포넌트: Provider 목록, 로그 폴더 선택, parser version, last scan.
- 오류/권한 상태: 경로 미확정, 접근 실패, 포맷 미지원.
- privacy 안내: 로그 원문 저장 금지.

### Session Review / Correction Screen

- 목적: 자동 분석 결과 확인과 부족한 값 보정.
- 주요 컴포넌트: AgentType, WorkType, token range, changed file count, line buckets, build/test result, confidence/warning, `Confirm Growth`.
- 사용자 플로우: 분석 결과 확인 -> 필요한 값만 수정 -> 성장 확정.

### Manual Fallback Input

- 목적: 자동 분석 실패 또는 사용자가 직접 기록하고 싶은 경우의 보조 입력.
- 주의사항: 메인 플로우로 강조하지 않는다.
- privacy 안내: 원문 입력 금지, 집계값만 입력.

### Growth Result Screen

- 목적: EXP, 스탯, Stress, 진화 진행도 결과 표시.
- 주요 컴포넌트: stat delta, reward tag, daily cap 표시.

### Character Room

- 목적: 캐릭터 상태와 진화 시각화.
- 주요 컴포넌트: 캐릭터 sprite, mood, evolution progress, unlocked items.

### Session History

- 목적: 세션 요약과 성장 로그 확인.
- privacy 안내: 원본 프롬프트/경로/로그 표시 금지.

### Settings / Privacy

- 목적: 분석 범위, sync, 삭제, privacy 설정 관리.
- 주요 컴포넌트: Git/Agent 분석 토글, cloud sync opt-in, 데이터 삭제, export.

### Login / Guest Mode Screen

- 목적: Guest Mode와 로그인 선택.
- 사용자 플로우: 기본은 Guest Mode, sync 필요 시 로그인.
- privacy 안내: 로그인해도 원본 데이터는 전송하지 않음.

### Sync Status Screen

- 목적: 동기화 상태와 오류 표시.
- 주요 컴포넌트: lastSyncAt, pending queue, retry, sync off.

### MiniGame Screen

- 목적: 대기 시간용 짧은 게임.
- 주요 컴포넌트: timer, score, token, bug obstacle, shield, pause.

---

## 12. 로컬 저장 설계

MVP 저장 방식:

- SaveData JSON 저장.
- `Application.persistentDataPath`.
- `saveVersion`.
- 자동 저장.
- 임시 파일 후 원자적 교체.
- 저장 데이터 삭제 기능.
- 백업/복원은 추후.

저장 타이밍:

- 앱 시작 로드.
- 분석 결과 확정 후.
- 성장 결과 반영 후.
- 미니게임 종료 후.
- 설정 변경 후.
- sync state 변경 후.

저장 금지:

- 원본 프롬프트.
- 원본 코드.
- 전체 로그 원문.
- 터미널 출력.
- API Key/Secret.
- 절대 파일 경로.
- Git remote URL.
- 커밋 diff 원문.

SQLite/LiteDB 확장 기준:

- 세션 수가 많아져 히스토리 조회가 느려질 때.
- 일/주/월 통계가 핵심 기능이 될 때.
- 프로젝트별/Agent별 쿼리가 복잡해질 때.
- 부분 업데이트와 migration이 중요해질 때.

---

## 13. 서버 동기화 클라이언트 설계

Sync Client는 로컬 데이터를 서버 DTO로 변환할 때 allowlist를 사용한다. SaveData 전체를 그대로 업로드하지 않는다.

### Sync DTO 원칙

- 클라이언트의 `SaveData`나 `AgentWorkSession`을 그대로 서버에 업로드하지 않는다.
- 반드시 `SafeSyncPayload`, `SessionSummaryUploadRequest`, `CharacterSnapshotSyncRequest`, `SettingsSyncRequest` 같은 별도 DTO로 변환한다.
- DTO는 allowlist 기반으로 만든다.
- 금지 필드는 DTO 모델에 존재하지 않아야 한다.
- 전송 전 client-side forbidden field check를 수행한다.
- sync queue에 들어가는 payload도 privacy-safe DTO여야 한다.
- offline queue에 원본 로그나 원본 경로를 넣지 않는다.
- sync 실패 로그에도 원본 payload를 남기지 않는다.

변환 단계:

1. Local model.
2. Privacy sanitizer.
3. Sync DTO allowlist mapping.
4. Client-side forbidden field check.
5. Server Privacy Guard.
6. Server DTO validation.
7. Persistence.

보낼 수 있는 데이터:

- `userId`
- `characterProfile` 요약
- `characterStats`
- `evolutionType`
- `unlockedItems`
- `sessionSummary`
- `workType distribution`
- `totalToken bucket/range`
- daily/weekly growth summary
- settings 일부
- `syncVersion`
- `updatedAt`

보내면 안 되는 데이터:

- 원본 프롬프트.
- 원본 코드.
- 전체 로그 원문.
- 터미널 출력 원문.
- API Key.
- Secret.
- 절대 파일 경로.
- Git remote URL.
- 커밋 diff 원문.
- 브랜치명 원문.
- 커밋 메시지 원문.

동기화 정책:

- Guest Mode에서는 로컬 전용.
- 로그인 후 opt-in sync.
- MVP는 last-write-wins.
- 추후 version vector 또는 conflict resolution.
- 오프라인 큐.
- 재시도 정책.
- 삭제 동기화.
- sync payload sanitizer 통과 후 전송.

---

## 14. macOS 권한 / 플랫폼 통합

고려 사항:

- 프로젝트 폴더 선택 권한.
- 로그 폴더 선택 권한.
- Git 실행 가능 여부 확인.
- 샌드박스 배포 시 제약.
- security-scoped bookmarks 조사 필요.
- Unity 단독 구현 한계.
- Swift helper 또는 CLI helper 후보.
- 메뉴바 앱 후보.
- 항상 위 작은 창 후보.

정책:

- 사용자가 선택한 폴더만 분석한다.
- 기본 경로를 확정값처럼 가정하지 않는다.
- 권한 오류는 Auto Analysis Dashboard와 Settings에서 명확히 표시한다.
- Native helper는 MVP 후반에 필요성을 검증한다.

---

## 15. 클라이언트 테스트 전략

### EditMode Test

- GrowthCalculator.
- WorkType 추론.
- GitChangeSummary parser.
- Provider fallback.
- Privacy sanitizer.
- SaveData serialization.
- Sync payload sanitizer.

### PlayMode Test

- 자동 분석 대시보드.
- 세션 검토/보정.
- 성장 결과.
- 캐릭터 룸.
- 미니게임.
- 설정/권한 흐름.
- Guest/Login/Sync 상태 표시.

테스트 데이터:

- Claude/Codex fixture는 sanitized sample만 사용한다.
- 원본 로그나 실제 프로젝트 경로를 테스트 리소스에 넣지 않는다.

---

## 16. 클라이언트 MVP 범위

### Core MVP

- Unity macOS 앱.
- 프로젝트 폴더 연결.
- Git 자동 분석.
- `AgentWorkSession` 정규화.
- 자동 분석 결과 확인/보정.
- 성장 계산.
- 캐릭터 룸.
- 로컬 JSON 저장.
- Privacy settings.
- Manual Fallback Input.

### Product MVP

- Claude/Codex 제한 Provider.
- Guest Mode.
- 서버 동기화 클라이언트 최소 구현.
- 로그인/클라우드 sync opt-in.
- 미니게임.
- macOS standalone build 정리.

### MVP 제외

- 모든 Agent/IDE 100% 완전 지원.
- 완전 백그라운드 데몬.
- 원본 데이터 업로드.
- 복잡한 전투/경제/과금.
- iOS companion.
- Steamworks full integration.

---

## 17. 클라이언트 리스크와 대응

- 로그 포맷 변경: Provider version, schema validation, graceful degradation.
- 권한 문제: 사용자 선택 폴더만 분석, 권한 오류 UI, fallback 제공.
- Git 명령 실패: timeout, 부분 결과, Manual Fallback.
- 대용량 로그 성능: 최근 세션 제한, streaming/partial parse, progress UI.
- Unity macOS 네이티브 UX 한계: Swift helper/CLI helper/메뉴바 앱 검토.
- 민감 정보 노출: sanitizer, allowlist DTO, debug log 원문 금지.
- 성장 밸런스: daily cap, anti-grinding, balancing table.
- 서버 동기화 충돌: last-write-wins MVP, 추후 conflict resolution.
- 오프라인 상태: Guest/로컬 우선, offline queue, retry.

---

## 18. 클라이언트 개발 순서

1. Unity 프로젝트 세팅.
2. Domain/SaveData.
3. AgentLogProvider 인터페이스.
4. GitDiffProvider.
5. Auto Analysis Dashboard.
6. Session Review / Correction.
7. GrowthCalculator.
8. Character Room.
9. Claude/Codex 제한 Provider.
10. Local persistence.
11. Sync Client.
12. MiniGame.
13. Privacy hardening.
14. macOS build.
