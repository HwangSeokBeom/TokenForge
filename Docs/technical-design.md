# TokenForge Technical Design

- Working Title: TokenForge
- Alternative Names: PromptPal, CodeFamiliar, CommitPet, BuildBuddy, PromptForge
- 이 문서에서는 프로젝트명을 일단 **TokenForge**로 사용한다.

## 문서 목적

이 문서는 Unity 기반 macOS 앱/게임 프로젝트인 TokenForge의 개발 착수 전 기술 설계 기준을 정의한다. 팀원 또는 미래의 개발자가 기술 스택, 전체 시스템 구조, 도메인 모델, 성장 규칙, 저장 방식, 개인정보 보호 원칙, MVP 범위, 개발 순서를 빠르게 이해하고 바로 구현에 들어갈 수 있도록 작성한다.

이 문서는 실제 Unity 코드 구현이 아니라 설계 문서다. 구현 세부 문법, Unity 컴포넌트 코드, Provider 실제 파서 코드는 별도 개발 단계에서 작성한다.

## 확정된 사실과 조사 필요 항목

### 확정된 사실

- 1차 MVP는 Unity 2D 기반 macOS 앱으로 개발한다.
- MVP의 핵심 방향은 “최대한 자동화된 개발 활동 수집 → AgentWorkSession 정규화 → 캐릭터 성장 반영”이다.
- 수동 입력은 메인 플로우가 아니라, 자동 수집이 실패하거나 데이터가 부족할 때 사용하는 fallback 입력 방식으로 둔다.
- MVP부터 Git 기반 자동 분석을 우선 구현한다.
  - `git status --short`
  - `git diff --stat`
  - `git diff --numstat`
  - 변경 파일 경로 분석
  - 테스트/docs/UI/architecture 관련 파일 변경 여부 분석
  - 커밋 메시지 또는 브랜치명 기반 작업 유형 추론
- Claude Code / Codex 로그 연동은 샘플 조사 후 가능한 범위부터 자동 분석에 포함한다.
- Claude Code / Codex 로그 포맷이 버전별로 바뀔 수 있으므로, 성장 시스템은 특정 로그 포맷에 직접 의존하지 않는다.
- 모든 외부 입력은 `AgentWorkSession` 공통 모델로 정규화한 뒤 게임 시스템에 전달한다.
- 자동 수집 우선순위는 다음과 같다.
  1. Git 변경량/파일 경로 기반 자동 분석
  2. Claude Code / Codex 세션 로그 기반 자동 분석
  3. 사용자가 선택한 프로젝트 폴더의 작업 상태 분석
  4. 부족한 정보만 최소 수동 보정
- 원본 프롬프트, 원본 코드, 전체 로그 원문, API Key, Secret, 민감한 터미널 출력은 저장하지 않는다.
- 저장 대상은 원본 데이터가 아니라 집계 결과와 성장 계산에 필요한 요약값이다.
- Privacy-first 설계를 유지하되, 사용자가 명시적으로 허용한 로컬 프로젝트/로그 폴더 안에서 최대한 자동화된 분석을 제공한다.

### 조사 필요 항목

- Claude Code 로그 경로, 저장 방식, 필드 구성, 포맷 안정성.
- Codex 로그 경로, 저장 방식, 필드 구성, 포맷 안정성.
- Cursor의 Composer/Agent/Chat 관련 로컬 기록 접근 가능성.
- GitHub Copilot / Copilot Chat의 로컬 로그 또는 세션 기록 접근 가능성.
- Windsurf의 Cascade/Agent 작업 로그 접근 가능성.
- Continue, Aider, Roo Code, Cline 등 로컬/오픈소스 에이전트의 로그 구조 및 연동 가능성.
- JetBrains AI Assistant의 로컬 로그 또는 IDE 로그 기반 분석 가능성.
- VS Code Extension 기반 에이전트들의 로그 저장 위치와 접근 가능성.
- 터미널 기반 에이전트와 IDE 내장형 에이전트의 데이터 수집 방식 차이.
- 각 에이전트별로 수집 가능한 데이터 범위:
  - 세션 시작/종료 시간
  - 프롬프트 수
  - 토큰 사용량
  - tool call 수
  - 파일 읽기/수정 기록
  - 명령 실행 기록
  - 테스트/빌드 실행 여부
  - 에러/실패 이벤트
  - 작업 디렉토리
  - 모델명
  - 비용 또는 usage 정보
- 각 에이전트별 로그 포맷 변경 가능성과 버전별 호환성.
- 로그 원문에 프롬프트, 코드, API Key, 터미널 출력 등 민감 정보가 포함될 가능성.
- Unity macOS 앱에서 사용자 지정 프로젝트 폴더 및 로그 폴더 접근 권한 UX.
- macOS 샌드박스 배포 시 Git 실행, 파일 접근, 로그 폴더 접근 제한.
- Git 기반 자동 분석만으로 작업 유형을 어느 정도 정확히 추론할 수 있는지.
- Git 분석 결과와 AI Agent 로그를 병합할 때 중복 세션을 어떻게 식별할지.
- `System.Text.Json`의 Unity 버전별 호환성.
- Newtonsoft Json for Unity 패키지 사용 시 빌드 크기와 AOT 이슈.
- Steam 배포 또는 iOS companion 확장 시 저장/권한 정책 차이.

AI 코딩 에이전트의 실제 로그 경로와 포맷은 제품별, 사용자 환경별, 버전별, 설정별로 달라질 수 있으므로 로컬 샘플 조사 전까지 확정하지 않는다.

TokenForge는 특정 에이전트 전용 게임이 아니라, 여러 AI 코딩 에이전트의 작업 데이터를 공통 모델로 정규화해 캐릭터 성장에 반영하는 구조를 목표로 한다.

따라서 Claude Code와 Codex는 1차 우선 지원 대상일 뿐이며, 이후 Cursor, GitHub Copilot, Windsurf, Continue, Aider, Roo Code, Cline 등 다른 AI 개발 도구도 `AgentLogProvider` 어댑터를 통해 확장 가능하게 설계한다.

---

## 1. 프로젝트 개요

### 프로젝트 한 줄 소개

TokenForge는 Claude Code, Codex, Cursor 같은 AI 코딩 에이전트가 작업하는 동안 발생하는 대기 시간과 작업 로그를 캐릭터 성장 루프로 변환하는 macOS용 Unity 2D 방치형 육성 게임이다.

### 문제 정의

AI 코딩 에이전트를 사용하는 개발자는 프롬프트를 입력한 뒤 에이전트가 코드를 수정하고 명령을 실행하며 테스트를 수행하는 동안 대기 시간이 생긴다. 이 시간은 생산적인 개발 흐름의 일부지만 사용자는 수동적으로 기다리거나, 여러 에이전트 작업을 병렬로 돌리면서 결과 확인 시점을 놓치기 쉽다.

또한 AI 에이전트가 생성하는 토큰 사용량, 파일 변경량, 테스트 성공/실패, 명령 실행, Git diff 같은 작업 데이터는 개발 활동을 잘 보여주는 신호지만 일반적으로 휘발된다. TokenForge는 이 신호를 게임 성장 데이터로 바꾸어 사용자가 자신의 개발 패턴을 가볍게 돌아보고, 대기 시간을 짧은 게임 루프로 즐기도록 만든다.

### 해결 방식

- 사용자가 로컬 개발 프로젝트 폴더를 연결한다.
- 앱은 Git 변경량, 파일 경로 패턴, 브랜치/커밋 키워드, 작업 디렉토리 상태를 자동 분석한다.
- 앱은 사용자가 허용한 범위 안에서 Claude Code, Codex 등 AI 코딩 에이전트의 세션 로그를 자동 분석한다.
- Cursor, GitHub Copilot, Windsurf, Continue, Aider, Roo Code, Cline 등 다른 AI 개발 도구는 `AgentLogProvider` 확장 구조를 통해 단계적으로 지원한다.
- 모든 외부 입력은 `AgentWorkSession` 공통 모델로 정규화한다.
- 자동 분석 결과가 부족하거나 신뢰도가 낮은 경우에만 사용자가 최소한의 보정을 수행한다.
- 성장 시스템은 작업 유형, 토큰 사용량, 파일 변경량, 성공/실패, Git 변경 요약, 에이전트 행동 요약을 기반으로 캐릭터 스탯과 경험치를 계산한다.
- 캐릭터는 사용자의 개발 성향에 따라 Debugger, Guardian, Clean Code Architect, Token Berserker 등으로 진화한다.
- 대기 시간에는 30초~3분짜리 미니게임을 플레이할 수 있다.
- 미니게임 보상은 보조 보상으로 제한하고, 핵심 성장은 실제 개발 작업 요약 데이터에서 나온다.

### 주요 사용자

- Claude Code, Codex, Cursor 등 AI 코딩 에이전트를 자주 사용하는 개인 개발자.
- 여러 작업을 AI에게 맡겨 놓고 결과를 기다리는 시간이 잦은 개발자.
- 개발 작업량과 성향을 가볍게 시각화하고 싶은 개발자.
- 생산성 도구와 게임화 경험을 좋아하는 macOS 사용자.
- 포트폴리오용으로 AI 개발 워크플로우를 게임 시스템으로 해석한 프로젝트를 보고 싶은 평가자.

### 핵심 차별점

- 일반적인 방치형 게임이 아니라 실제 개발 세션 데이터가 성장 재료가 된다.
- 토큰 사용량만 점수화하지 않고 Git 변경량, 테스트 결과, 작업 유형, 파일 경로 패턴을 함께 반영한다.
- AI 에이전트 로그 포맷에 종속되지 않는 Provider 어댑터 구조를 사용한다.
- 원본 로그 저장 없이 집계값만 저장하는 privacy-first 구조를 기본값으로 둔다.
- 개발자의 작업 성향이 캐릭터 진화 타입으로 드러난다.

### MVP 목표

MVP의 목표는 Git과 우선 지원 AI Agent Provider를 통해 실제 개발 활동을 자동 분석하고, 그 결과가 캐릭터 성장으로 자연스럽게 이어지는 제품형 경험을 검증하는 것이다. 수동 입력은 자동 분석의 대체 메인 플로우가 아니라, 자동 분석이 실패하거나 일부 값의 신뢰도가 낮을 때 사용하는 보정/fallback이다.

MVP에서 검증할 질문은 다음과 같다.

- 사용자가 프로젝트 폴더를 연결하면 Git 변경량과 작업 유형을 자동으로 분석할 수 있는가?
- Claude Code / Codex 등 최소 2개 이상의 에이전트 로그를 제한된 범위에서 자동 분석할 수 있는가?
- 자동 분석 결과를 `AgentWorkSession`으로 정규화했을 때 성장 결과가 납득 가능한가?
- 자동 분석이 불완전할 때 사용자가 최소한의 보정만으로 세션을 확정할 수 있는가?
- 토큰 수, 수정 파일 수, 작업 유형, 성공/실패가 캐릭터 성장에 직관적으로 연결되는가?
- 캐릭터 룸과 성장 결과 화면이 다음 개발 세션을 이어가게 만드는가?
- 짧은 토큰 수집 미니게임이 개발 대기 시간을 방해하지 않고 보완하는가?
- 로컬 저장, 계정, 클라우드 동기화, 원격 서버를 포함하면서도 원본 로그/코드/프롬프트를 저장하지 않는 privacy-first 구조를 유지할 수 있는가?

### MVP Scope Policy

TokenForge의 MVP는 단순 수동 입력 기반 프로토타입이 아니라, 실제 개발자가 사용하는 AI 코딩 에이전트와 Git 작업 데이터를 자동으로 수집하고 캐릭터 성장에 반영하는 “자동 분석 우선 제품”을 목표로 한다.

다만 모든 기능을 동일한 완성도로 구현하지 않는다. MVP에서는 핵심 경험을 end-to-end로 연결하는 것을 우선하고, 일부 기능은 제한된 범위 또는 베타 수준으로 포함한다.

MVP의 기준은 다음과 같다.

- 사용자가 실제 프로젝트 폴더를 연결할 수 있다.
- 앱이 Git 변경량과 작업 유형을 자동 분석할 수 있다.
- 최소 2개 이상의 AI 코딩 에이전트 로그를 자동 분석할 수 있다.
- 추가 에이전트는 Provider 구조만 준비하거나 일부 베타 지원한다.
- 자동 분석 결과가 `AgentWorkSession`으로 정규화된다.
- 정규화된 작업 데이터가 캐릭터 성장, 스탯, 진화 성향에 반영된다.
- 사용자는 자동 분석 결과를 확인하고 필요한 경우 보정할 수 있다.
- 로컬 저장, 계정, 클라우드 동기화, 기본 배포 흐름까지 제품 형태로 구성한다.

### MVP에 포함할 것

#### 1. Unity 2D macOS 앱
- Unity 2D 기반 macOS 앱
- 캐릭터 성장 화면
- 성장 결과 화면
- 세션 히스토리
- 설정/Privacy 화면
- 간단한 대기 시간용 미니게임

#### 2. 프로젝트 폴더 연결
- 사용자가 로컬 개발 프로젝트 폴더를 선택
- Git repository 여부 감지
- 프로젝트별 분석 상태 저장
- 여러 프로젝트 연결 지원

#### 3. Git 자동 분석
- `git status --short`
- `git diff --stat`
- `git diff --numstat`
- 변경 파일 수 계산
- 추가/삭제 라인 수 계산
- 변경 파일 경로 기반 작업 유형 추론
- 테스트/docs/UI/architecture 관련 변경 감지
- 브랜치명/커밋 메시지 기반 보조 추론

#### 4. AI Agent 로그 자동 분석
- Claude Code Provider
- Codex Provider
- Cursor Provider는 베타 또는 조사 기반 Provider skeleton
- Windsurf Provider는 베타 또는 조사 기반 Provider skeleton
- GitHub Copilot, Continue, Aider, Roo Code, Cline, JetBrains AI Assistant는 Provider 확장 구조 준비
- 각 Provider는 원본 로그를 직접 게임 시스템에 전달하지 않고 `AgentWorkSession`으로 정규화

#### 5. 실시간 또는 준실시간 자동 감시
- MVP에서 완전한 백그라운드 데몬 수준은 아니어도, 앱 실행 중 자동 스캔 제공
- 주기적 polling 기반 분석
- 사용자가 연결한 프로젝트/로그 폴더만 감시
- 중복 세션 방지
- 분석 실패 시 graceful fallback

#### 6. 자동 분석 결과 확인/보정
- 추론된 AgentType 확인
- 추론된 WorkType 확인
- 감지된 토큰 사용량 확인
- 변경 파일 수 확인
- 테스트/빌드 감지 결과 확인
- 불확실한 항목만 사용자가 수정 가능

#### 7. 성장 시스템
- `AgentWorkSession` 기반 EXP 계산
- Logic, Debug, Architecture, Design, Stability, Velocity, Creativity, Efficiency, Stress 스탯 반영
- 작업 유형별 성장 가중치
- 토큰 사용량/변경량/성공 여부 기반 보정
- 성장 로그 저장
- 진화 타입 후보 계산

#### 8. 계정 시스템
- MVP에서는 최소 계정 시스템만 포함
- 이메일/소셜 로그인 중 하나 선택
- 로컬 플레이만으로도 사용 가능하게 Guest Mode 제공
- 계정은 클라우드 동기화와 Steam 확장을 위한 기반으로 설계

#### 9. 클라우드 동기화
- 캐릭터 성장 상태
- 세션 요약값
- 설정 일부
- 프로젝트 식별은 `projectPathHash`, `projectAlias`, `localOnlyProjectId` 중심으로 처리
- 사용자가 명시적으로 입력한 alias 외에는 프로젝트명이나 경로를 서버로 전송하지 않음
- 원본 로그/프롬프트/코드/터미널 출력은 동기화 금지

#### 10. 원격 서버
- 인증
- 유저 프로필
- 캐릭터 저장 데이터 동기화
- 세션 요약 동기화
- 리더보드는 MVP에서는 선택 또는 비활성화
- Privacy-first API 계약

#### 11. Steam 배포 준비
- Steam 출시 품질 완성까지는 아니어도, Steam 배포를 고려한 빌드 구조
- macOS standalone build
- 앱 아이콘, 기본 설정, 로컬 저장 경로 정리
- 추후 Steamworks 연동 가능 구조

---

## 2. 제품 컨셉

### AI 코딩 에이전트 대기 시간의 게임화

TokenForge는 사용자가 AI 에이전트에게 작업을 맡긴 뒤 생기는 대기 시간을 빈 시간으로 보지 않는다. 이 시간은 개발 세션이 진행 중인 상태이며, 게임 안에서는 캐릭터가 "토큰 에너지"를 흡수하고 "빌드 게이지"를 채우는 시간으로 표현된다.

대기 중 사용자는 다음 행동을 할 수 있다.

- 현재 작업 세션에 대한 예상 정보 입력.
- 짧은 미니게임 플레이.
- 이전 세션 성장 결과 확인.
- 캐릭터 스탯과 진화 진행도 확인.
- 작업 완료 후 실제 결과를 입력하여 성장 확정.

### 토큰 주도 개발과 캐릭터 성장 연결

AI 에이전트의 토큰 사용량은 작업 규모와 복잡도의 신호가 될 수 있다. 하지만 토큰이 많다고 항상 좋은 작업은 아니다. 따라서 TokenForge는 토큰 사용량을 단순 점수로 환산하지 않고 다음 맥락과 함께 해석한다.

- 작업 유형: Feature, Bugfix, Refactor, Test, UI/UX, Docs 등.
- 성공 여부: 빌드 성공, 테스트 성공, 실패 후 복구 여부.
- 변경량: 파일 수, 라인 추가/삭제, 경로 분포.
- 효율성: 적은 토큰으로 성공했는지, 큰 토큰을 사용했지만 실패했는지.
- 스트레스: 많은 토큰, 큰 diff, 실패 결과가 겹쳤는지.

이 구조는 대규모 작업도 보상하되, 작은 성공과 좋은 테스트 습관도 성장으로 인정한다.

### 개발 로그 기반 성장 시스템

개발 로그는 게임 시스템에 직접 들어오지 않는다. 로그 Provider는 원본을 읽고 필요한 집계값만 추출하여 `AgentWorkSession`을 만든다. 성장 시스템은 이 공통 모델만 사용한다.

이 설계의 이유는 다음과 같다.

- Claude Code / Codex 로그 포맷 변경에 성장 로직이 흔들리지 않는다.
- Git 분석, Claude/Codex 로그 파서, 추가 에이전트 Provider, 수동 보정 Provider, 테스트용 Mock Provider를 같은 흐름에서 다룰 수 있다.
- 개인정보 보호 정책을 Provider와 Sanitization 계층에서 강제할 수 있다.
- MVP부터 Git/Agent 자동 분석을 핵심 흐름으로 두면서도, 실패하거나 부족한 값만 수동 보정으로 흡수할 수 있다.

### 개발자용 다마고치 + 로그 기반 RPG

제품 포지셔닝은 "개발자용 다마고치 + 로그 기반 RPG"다.

- 다마고치 요소: 캐릭터 상태, 성장, 방치형 보상, 방 꾸미기 가능성.
- RPG 요소: 경험치, 스탯, 진화 타입, 성장 로그, 보상 화면.
- 개발자 도구 요소: Git 분석, 작업 세션 요약, privacy settings, Provider 설정.

게임은 개발을 방해하지 않아야 한다. 따라서 MVP UI는 빠른 입력과 빠른 확인을 우선하고, 미니게임은 언제든 중단 가능한 짧은 세션으로 설계한다.

---

## 3. 기술 스택

### Client / Game Engine

**선택: Unity 2D**

선택 이유:

- macOS 데스크톱 앱 빌드를 빠르게 만들 수 있다.
- 2D 캐릭터, 애니메이션, 미니게임, UI를 한 프로젝트 안에서 개발하기 쉽다.
- Unity Test Framework, ScriptableObject, Addressables 같은 도구를 단계적으로 활용할 수 있다.
- 추후 Steam 배포 또는 iOS companion 앱으로 확장할 가능성이 있다.

대안:

- Godot: 가볍고 2D에 강하지만 macOS 네이티브 연동, 팀 숙련도, 에셋 생태계를 별도로 검토해야 한다.
- SwiftUI + SpriteKit: macOS 네이티브 UX에는 강하지만 게임 루프와 Unity 기반 포트폴리오 요구와 맞지 않는다.
- Electron + Canvas/WebGL: 로그 도구 UI에는 강하지만 게임 개발 경험과 배포 품질 면에서 별도 설계가 필요하다.

### Language

**선택: C#**

선택 이유:

- Unity의 기본 개발 언어다.
- 도메인 모델, 성장 계산, Provider 인터페이스를 타입 안정성 있게 구성할 수 있다.
- Unity Test Framework와 잘 맞는다.

대안:

- Swift: macOS helper 또는 메뉴바 앱 단계에서 사용 가능.
- Python/Node.js: Git/log 분석 프로토타입 도구에는 적합하지만 Unity 런타임 핵심 로직에는 부적합하다.

### Target Platform

**선택: macOS**

선택 이유:

- Claude Code, Codex, Cursor를 사용하는 개발자 워크플로우가 macOS 데스크톱 환경과 잘 맞는다.
- 파일 시스템 접근, Git 실행, 로컬 저장, 개발 프로젝트 선택이 주요 기능이다.
- 1차 제품 컨셉이 "개발 중 옆에 띄워두는 앱"에 가깝다.

대안:

- Windows/Linux: 추후 확장 가능하지만 파일 권한, 로그 경로, Git 실행 환경이 달라진다.
- iOS: companion 앱으로 적합하지만 개발 세션 로그와 Git 접근성이 낮다.

### IDE

**선택: JetBrains Rider 또는 VS Code**

선택 이유:

- Rider는 Unity C# 분석, 리팩터링, 테스트 실행 경험이 좋다.
- VS Code는 가볍고 Codex/Claude Code와 함께 쓰는 개발자에게 친숙하다.

대안:

- Visual Studio for Mac은 지원 종료 흐름을 고려하면 신규 프로젝트 기본 IDE로 권장하지 않는다.

### Version Control

**선택: Git / GitHub**

선택 이유:

- Git diff/stat 분석이 성장 데이터의 주요 입력이 된다.
- GitHub는 포트폴리오, 이슈, PR, 릴리스 관리에 적합하다.

대안:

- Perforce, Plastic SCM: Unity 프로젝트에서 가능하지만 MVP의 개발자 대상 Git 분석 기능과 맞지 않는다.

### Local Persistence

**MVP 선택: JSON file using `Application.persistentDataPath`**

선택 이유:

- 구현이 단순하다.
- SaveData 단일 루트 모델로 버전 관리와 마이그레이션을 시작하기 쉽다.
- 로컬 우선 정책과 잘 맞는다.
- MVP에서 저장 데이터 규모가 작다.

대안:

- SQLite: 세션 수가 많아지고 통계 질의가 복잡해지면 적합하다.
- LiteDB: C# 객체 저장 경험은 좋지만 Unity/macOS 빌드 호환성 검증이 필요하다.
- PlayerPrefs: 설정값에는 가능하지만 세션 히스토리와 성장 로그 저장에는 부적합하다.

### Log Parsing

**MVP 선택: 자동 분석 우선 + 수동 보정 fallback**

선택 이유:

- TokenForge의 핵심 가치는 개발자가 직접 기록하는 수동 일지가 아니라, 실제 개발 활동과 AI 코딩 에이전트의 작업 데이터를 자동으로 수집해 게임 성장으로 변환하는 것이다.
- Git 분석은 대부분의 개발 프로젝트에서 공통적으로 적용 가능하므로 MVP의 1차 자동 데이터 소스로 사용한다.
- Claude Code / Codex는 우선 지원 Provider로 두고, 샘플 로그 조사를 기반으로 가능한 범위부터 자동 분석한다.
- Cursor, GitHub Copilot, Windsurf, Continue, Aider, Roo Code, Cline 등은 Provider 구조를 통해 단계적으로 확장한다.
- 로그 포맷이 불안정하거나 일부 필드를 읽을 수 없는 경우에는 자동 분석 결과 확인/보정 화면에서 사용자가 부족한 값만 수정한다.
- 완전 수동 입력은 구현은 쉽지만 제품 핵심 가치가 약하므로 fallback으로만 둔다.
- 모든 에이전트 완전 자동 지원은 이상적이지만 MVP에서 모든 로그 포맷과 IDE를 100% 지원하는 것은 현실적이지 않다.

대안:

- 실시간 파일 감시/백그라운드 데몬: 권한, 성능, 샌드박스, 개인정보 리스크가 커서 MVP에서는 앱 실행 중 주기적 스캔부터 시작한다.
- CLI hook: 정확도가 높을 수 있지만 각 에이전트별 지원 여부가 다르다.

### Git Analysis

**선택: Git CLI 호출 기반 분석**

선택 이유:

- `git status`, `git diff`, `git log` 결과는 대부분의 Git 프로젝트에서 사용 가능하다.
- libgit2 같은 네이티브 의존성 없이 프로토타입이 가능하다.
- 추후 별도 `Tools/git-analyzer-prototype`에서 파싱 로직을 검증하기 쉽다.

대안:

- LibGit2Sharp: 구조화된 API를 제공하지만 Unity/macOS 빌드 호환성과 네이티브 라이브러리 이슈를 검토해야 한다.
- GitHub API: 로컬 작업 디렉토리 상태를 알기 어렵고 네트워크/인증이 필요하다.

### Testing

**선택: Unity Test Framework**

선택 이유:

- EditMode Test로 도메인 로직, 성장 계산, 파서, 저장 직렬화를 빠르게 검증할 수 있다.
- PlayMode Test로 UI 흐름과 미니게임 세션을 검증할 수 있다.
- Unity 프로젝트 안에서 테스트를 관리할 수 있다.

대안:

- NUnit 단독 프로젝트: 순수 도메인 테스트에는 좋지만 Unity 통합 테스트와 분리 관리가 필요하다.

### Build / Distribution

**MVP 선택: Unity Build Settings / macOS Build**

선택 이유:

- macOS standalone `.app` 산출물을 안정적으로 만들고, 제품형 MVP의 배포 구조를 정리할 수 있다.
- Steam 정식 출시 수준까지 완성하지는 않더라도 앱 아이콘, 저장 경로, 빌드 설정, 릴리스 산출물 관리 기준을 MVP부터 둔다.
- notarization, signing, Steamworks 연동은 단계적으로 검토하되 빌드 구조는 확장 가능하게 유지한다.

대안:

- Steam 정식 배포: MVP에서는 상점 페이지, 업적, 결제, 커뮤니티 기능까지 완성하지 않고 배포 준비 수준으로 제한한다.
- Mac App Store: 샌드박스 권한 정책 때문에 Git/log 분석 기능과 충돌할 수 있어 후순위다.

### Future Native macOS Integration

**후보: Swift/macOS helper app, CLI helper, menu bar app, file system permission handling**

선택 이유:

- Unity만으로는 macOS 메뉴바, 항상 위 작은 창, 네이티브 파일 권한 UX가 제한적일 수 있다.
- Swift helper 또는 CLI helper가 Git 실행, 로그 폴더 접근, 권한 요청을 더 자연스럽게 처리할 수 있다.

대안:

- Unity 단독 유지: 구현 단순성은 높지만 macOS 네이티브 UX가 약하다.
- Electron wrapper: 데스크톱 통합에는 강하지만 Unity View와의 통합 복잡성이 생긴다.

---

## 4. 전체 시스템 아키텍처

TokenForge 앱 내부는 다음 계층으로 나눈다.

```text
Presentation Layer
  -> Game Loop Layer
    -> Domain Layer
      -> Growth System Layer
      -> Agent Data Layer
      -> Git Analysis Layer
      -> Persistence Layer
      -> Privacy / Sanitization Layer
      -> Platform Integration Layer
```

의존 방향은 원칙적으로 UI에서 도메인으로 내려가며, 도메인 핵심 로직은 Unity Scene, MonoBehaviour, 외부 로그 포맷, Git CLI 출력 문자열에 직접 의존하지 않는다.

### Presentation Layer

책임:

- Unity Scene, Canvas, UI Prefab, Animation, Character Room 표시.
- 사용자 입력 수집: 작업 타입, 에이전트 타입, 토큰 수, 파일 수, 성공 여부.
- 성장 결과 화면 표시.
- 미니게임 화면 전환.
- 설정/권한/개인정보 화면 표시.

주의사항:

- 성장 계산을 UI 이벤트 핸들러 안에 직접 구현하지 않는다.
- UI는 `AgentWorkSession` 생성에 필요한 입력을 ViewModel 또는 Controller에 전달한다.
- 개인정보 경고와 저장 정책은 Settings/Privacy 화면에서 명확히 보여준다.

### Game Loop Layer

책임:

- 앱의 주요 상태 흐름 관리.
- Main Dashboard -> Manual Input -> Growth Result -> Character Room 흐름 조정.
- 미니게임 시작/일시정지/종료.
- 자동 저장 타이밍 결정.
- Provider 실행 요청과 결과 반영.

주의사항:

- 게임 루프는 도메인 계산 결과를 조립하지만 계산 규칙 자체를 포함하지 않는다.
- 비동기 작업은 MVP에서 Coroutine으로 시작 가능하되, 추후 UniTask 도입 시 경계를 명확히 둔다.

### Domain Layer

책임:

- `AgentWorkSession`, `CharacterProfile`, `SaveData` 등 핵심 모델 정의.
- WorkType, AgentType, EvolutionType 같은 enum 정의.
- 게임 규칙에서 사용하는 값 객체와 결과 모델 정의.

주의사항:

- UnityEngine 의존성을 최소화한다.
- 직렬화 가능한 단순 구조를 우선한다.
- SaveData 마이그레이션을 고려해 `schemaVersion` 또는 `saveVersion` 필드를 둔다.

### Growth System Layer

책임:

- 작업 세션을 캐릭터 성장 결과로 변환.
- 경험치, 스탯 증가량, 스트레스 증가/감소, 진화 진행도 계산.
- GrowthRule 적용.
- 일일 보상 제한, anti-grinding 정책 적용.

주의사항:

- 특정 Provider나 로그 포맷을 몰라야 한다.
- 입력은 `AgentWorkSession`과 현재 캐릭터 상태로 제한한다.
- MVP에서는 코드 기반 가중치 테이블로 시작하고, 이후 ScriptableObject 또는 JSON 밸런싱 테이블로 확장한다.

### Agent Data Layer

책임:

- 여러 데이터 소스를 `AgentWorkSession`으로 변환한다.
- Git diff, Claude Code 로그, Codex 로그, Cursor/Windsurf 로그, 수동 보정, Mock Provider를 공통 인터페이스로 다룬다.

주의사항:

- Provider는 원본 로그를 저장하지 않는다.
- 실패 시 사용자에게 이해 가능한 오류와 fallback 경로를 제공한다.
- 파싱 결과는 Privacy/Sanitization 계층을 통과한 집계값이어야 한다.

### Git Analysis Layer

책임:

- 선택된 프로젝트 폴더에서 Git 명령을 실행한다.
- `GitChangeSummary`를 생성한다.
- 파일 경로 패턴과 변경량을 바탕으로 WorkType 추론에 필요한 신호를 제공한다.

주의사항:

- Git이 설치되어 있지 않거나 프로젝트가 Git 저장소가 아닐 수 있다.
- 명령 실행 타임아웃과 오류 처리가 필요하다.
- 파일 경로는 저장 전 마스킹 또는 해시 처리한다.

### Persistence Layer

책임:

- `SaveData`를 JSON 파일로 저장/로드한다.
- 자동 저장, 백업, 버전 마이그레이션 준비.
- 설정과 세션 히스토리를 로컬에 저장한다.

주의사항:

- 저장 데이터에 원본 로그, 원본 코드, API Key가 들어가지 않도록 모델 수준에서 막는다.
- 저장 실패 시 UI에 복구 가능한 상태를 제공한다.
- MVP에서는 단일 JSON 파일로 시작하되, 세션이 많아지면 SQLite/LiteDB로 확장한다.

### Privacy / Sanitization Layer

책임:

- 민감 정보를 마스킹, 해시, 제거한다.
- Provider와 Persistence 사이에서 저장 가능한 데이터만 통과시킨다.
- 샘플 로그 fixture가 sanitized 상태인지 검증한다.

주의사항:

- "원문 로그 저장 금지"는 설정 옵션이 아니라 기본 강제 정책이다.
- 사용자가 원하더라도 API Key나 원본 프롬프트를 SaveData에 저장하지 않는다.
- 디버그 로그 출력에도 원문이 남지 않게 한다.

### Platform Integration Layer

책임:

- macOS 파일 선택, 폴더 권한, Git 실행, 향후 Swift helper와의 통신.
- `Application.persistentDataPath` 경로 관리.
- OS별 차이 추상화.

주의사항:

- Unity Editor와 macOS 빌드의 경로/권한 차이를 분리한다.
- 샌드박스 배포 여부에 따라 기능 제한이 달라질 수 있다.

---

## 5. 추천 폴더 구조

기본 구조는 다음과 같다.

```text
TokenForge/
  UnityClient/
    Assets/
      _Project/
        Scripts/
          Domain/
          Growth/
          Agents/
          Git/
          Persistence/
          Privacy/
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
  Docs/
    product-brief.md
    game-design-document.md
    technical-design.md
    privacy-policy-draft.md
    agent-log-research.md
    growth-balancing.md
  Samples/
    sanitized/
      claude/
      codex/
  Tools/
    log-sanitizer/
    git-analyzer-prototype/
  README.md
  .gitignore
```

### `UnityClient/`

Unity 프로젝트 루트다. Unity Hub에서 이 폴더를 열도록 구성한다. 저장소 루트와 Unity 루트를 분리하면 Docs, Tools, Samples를 Unity Asset import 대상에서 제외할 수 있어 프로젝트가 가벼워진다.

### `UnityClient/Assets/_Project/`

프로젝트 전용 에셋 루트다. 외부 패키지, 샘플, Unity 기본 폴더와 구분하기 위해 `_Project`를 사용한다.

### `Scripts/Domain/`

도메인 모델과 enum을 둔다. 예: `AgentWorkSession`, `CharacterProfile`, `SaveData`, `WorkType`, `EvolutionType`. 이 폴더의 코드는 Unity Scene이나 UI에 직접 의존하지 않도록 유지한다.

### `Scripts/Growth/`

성장 계산, 진화 판정, 밸런싱 규칙을 둔다. MVP에서는 단순 C# 규칙으로 시작하고, 이후 ScriptableObject 기반 성장 테이블을 추가할 수 있다.

### `Scripts/Agents/`

`AgentLogProvider` 인터페이스와 구현체를 둔다. `ManualSessionProvider`, `ClaudeCodeLogProvider`, `CodexLogProvider`, `MockAgentLogProvider`가 이 영역에 속한다.

### `Scripts/Git/`

Git CLI 실행, diff/stat/numstat 파싱, 경로 분류, `GitChangeSummary` 생성 로직을 둔다.

### `Scripts/Persistence/`

JSON 저장/로드, SaveData 버전 관리, 자동 저장 서비스를 둔다.

### `Scripts/Privacy/`

파일 경로 마스킹, 해시 처리, secret 패턴 제거, sanitized fixture 검증 로직을 둔다.

### `Scripts/UI/`

Dashboard, Manual Input, Growth Result, Settings, Session History 화면의 Presenter/ViewModel/Controller를 둔다.

### `Scripts/MiniGame/`

토큰 수집 미니게임의 룰, 입력, 스폰, 충돌, 세션 결과 계산을 둔다.

### `Scripts/Character/`

캐릭터 표시, 애니메이션 상태, 진화 외형 변경, 방치형 상태 표현을 둔다.

### `Scripts/Common/`

공통 유틸리티, Result 타입, Clock, ID 생성, 숫자 clamp, 이벤트 버스 등 여러 계층에서 쓰이는 작은 도구를 둔다. 단, 너무 많은 기능이 Common에 쌓이지 않도록 주의한다.

### `Art/`, `Audio/`, `Prefabs/`, `Scenes/`, `Tests/`

- `Art/`: 캐릭터, UI, 미니게임 sprite, Aseprite/Piskel export 결과.
- `Audio/`: 효과음과 배경음. 소리 없이도 플레이 가능해야 하므로 필수 정보 전달에 의존하지 않는다.
- `Prefabs/`: UI panel, character, mini game object, reusable component.
- `Scenes/`: Bootstrap, Main, MiniGame 등.
- `Tests/`: EditMode/PlayMode 테스트. 필요하면 `Tests/EditMode`, `Tests/PlayMode`로 세분화한다.

### `Docs/`

제품/게임/기술/개인정보/로그 조사/밸런싱 문서를 둔다.

- `product-brief.md`: 제품 목표와 사용자 가치.
- `game-design-document.md`: 게임 루프, 캐릭터, 미니게임, UX.
- `technical-design.md`: 현재 문서.
- `privacy-policy-draft.md`: 개인정보 원칙과 사용자 고지 초안.
- `agent-log-research.md`: Claude/Codex/Cursor 로그 샘플 조사 결과.
- `growth-balancing.md`: 성장 공식, 가중치, 밸런싱 변경 이력.

### `Samples/sanitized/`

샘플 로그 fixture를 둔다. Git에 포함되는 모든 샘플은 반드시 민감 정보 제거가 끝난 sanitized 데이터만 허용한다. 원본 로그는 저장소에 포함하지 않는다.

### `Tools/`

Unity 런타임 밖에서 돌리는 보조 도구를 둔다.

- `log-sanitizer/`: 샘플 로그를 sanitized fixture로 변환하는 도구.
- `git-analyzer-prototype/`: Git 명령 파싱을 빠르게 검증하는 CLI 프로토타입.

---

## 6. 핵심 도메인 모델

실제 구현 언어는 C#이지만 이 문서에서는 코드가 아닌 필드 설계 수준으로 정의한다.

### `AgentWorkSession`

역할:

- 한 번의 AI 코딩 에이전트 작업, Git 기반 개발 작업, 또는 사용자가 보정한 개발 작업 세션을 표현한다.
- 성장 시스템의 핵심 입력 모델이다.
- Git 분석, Claude/Codex 로그 Provider, 추가 Agent Provider, 수동 보정 fallback 모두 이 모델로 변환되어야 한다.

필드 예시:

- `sessionId`: 세션 고유 ID.
- `startedAt`, `endedAt`: 세션 시작/종료 시각.
- `agentType`: ClaudeCode, Codex, Cursor, Manual, Unknown.
- `workType`: Feature, Bugfix, Refactor, Test, UIUX, Docs, Chore, Research, Mixed, Unknown.
- `title`: 사용자가 입력한 짧은 제목. 민감 정보가 들어갈 수 있으므로 저장 전 길이 제한과 경고 필요.
- `tokenUsage`: 입력/출력/총 토큰 집계. 모르면 null 또는 0.
- `actionSummary`: 명령 실행, 파일 수정, 프롬프트 수 등 집계.
- `gitChangeSummary`: Git 변경 요약.
- `resultStatus`: Success, FailedBuild, FailedTest, Partial, Canceled, Unknown.
- `sourceProvider`: Manual, GitDiff, ClaudeCodeLog, CodexLog, CursorLog, Mock.
- `parserVersion`: 자동 Provider가 생성한 경우 파서 버전.
- `confidence`: Provider 추론 신뢰도. 자동 추론 결과와 사용자가 보정한 필드를 구분해 표시한다.
- `privacyLevel`: 저장 가능한 수준. AggregatedOnly가 기본.

### `AgentType`

역할:

- 어떤 AI 코딩 에이전트 또는 입력 출처에서 온 세션인지 구분한다.

값 예시:

- `Manual`
- `ClaudeCode`
- `Codex`
- `Cursor`
- `GitOnly`
- `Other`
- `Unknown`

주의사항:

- AgentType은 성장 테마와 통계 필터에 사용할 수 있지만, 특정 에이전트에 과도한 보상 차이를 주지 않는다.
- Provider가 불확실하면 `Unknown`으로 둔다.

### `WorkType`

역할:

- 작업 성격을 표현하고 성장 스탯 매핑의 핵심 기준으로 사용한다.

값 예시:

- `Feature`
- `Bugfix`
- `Refactor`
- `Test`
- `UIUX`
- `Docs`
- `Chore`
- `Research`
- `Build`
- `Mixed`
- `Unknown`

추론 기준:

- Git 분석은 파일 경로, 커밋 메시지, 테스트 파일 변경 여부를 참고한다.
- 로그 Provider는 tool call, 명령 실행, 파일 edit 요약을 참고할 수 있다.
- 자동 추론 confidence가 낮거나 사용자가 명시적으로 수정한 경우에만 수동 보정값을 우선한다.

### `AgentActionSummary`

역할:

- 에이전트가 수행한 행동을 원본 로그 없이 집계한다.

필드 예시:

- `promptCount`: 사용자 프롬프트 또는 요청 횟수.
- `toolCallCount`: tool call 총 횟수.
- `fileEditCount`: 파일 편집 횟수.
- `commandRunCount`: 명령 실행 횟수.
- `testRunCount`: 테스트 실행 추정 횟수.
- `buildRunCount`: 빌드 실행 추정 횟수.
- `failedCommandCount`: 실패한 명령 수.
- `durationSeconds`: 세션 지속 시간.
- `usedAutoProvider`: 자동 Provider 사용 여부.

주의사항:

- 명령 문자열 원문은 저장하지 않는다.
- 필요하면 명령 카테고리만 저장한다. 예: Test, Build, Git, PackageInstall, Unknown.

### `GitChangeSummary`

역할:

- Git 분석 결과를 성장 시스템이 사용할 수 있게 요약한다.

필드 예시:

- `repositoryId`: 프로젝트 경로를 직접 저장하지 않고 해시 또는 사용자 지정 alias 사용.
- `currentBranchName`: 저장 여부 옵션 필요. 민감할 수 있으므로 마스킹 가능.
- `isGitRepository`: Git 저장소 여부.
- `workingTreeStatus`: Clean, Dirty, Unknown.
- `changedFileCount`: 변경 파일 수.
- `addedLines`: 추가 라인 수.
- `deletedLines`: 삭제 라인 수.
- `testFileChanged`: 테스트 파일 변경 여부.
- `docsFileChanged`: 문서 파일 변경 여부.
- `uiFileChanged`: UI 파일 변경 여부.
- `architectureFileChanged`: service/repository/usecase/DI 등 변경 여부.
- `fileCategoryCounts`: UI, Test, Docs, Domain, Infra, Config 등 카테고리별 개수.
- `commitKeywordCounts`: 최근 커밋 메시지 키워드 집계.

주의사항:

- 원본 파일 경로 전체를 저장하지 않는다.
- UI 표시가 필요하면 `src/**/LoginView.cs`처럼 마스킹된 경로 또는 카테고리만 사용한다.

### `CharacterProfile`

역할:

- 플레이어의 캐릭터 상태와 정체성을 나타낸다.

필드 예시:

- `characterId`
- `displayName`
- `level`
- `totalExp`
- `currentEvolutionType`
- `stats`
- `createdAt`
- `lastGrowthAt`
- `appearanceVariant`
- `unlockedEvolutionTypes`
- `moodState`: Calm, Focused, Stressed, Inspired 등.

### `CharacterStats`

역할:

- 개발 작업 성향과 성장 방향을 수치화한다.

스탯 후보:

- `Logic`: 기능 구현, 문제 분해, 로직 작성.
- `Debug`: 버그 수정, 실패 분석, 테스트 실패 복구.
- `Architecture`: 리팩터링, 구조 개선, 문서화.
- `Design`: UI/UX, 시각적 작업, 사용자 흐름 개선.
- `Stability`: 테스트, 빌드 성공, 안정화.
- `Velocity`: 빠른 기능 구현, 반복 속도.
- `Creativity`: 새로운 기능, UI, 실험성.
- `Efficiency`: 적은 토큰/작은 변경으로 성공.
- `Stress`: 실패, 대규모 변경, 긴 세션의 부담.

주의사항:

- `Stress`는 무조건 나쁜 값이 아니라 진화 조건과 회복 루프에 쓰는 압력 지표다.
- 성장 결과 화면에서는 "Stress가 늘었다"를 벌점처럼만 표현하지 않고 "큰 작업의 열기"처럼 맥락화할 수 있다.

### `CharacterGrowthResult`

역할:

- 한 세션의 성장 계산 결과를 UI와 저장 시스템에 전달한다.

필드 예시:

- `sessionId`
- `expGained`
- `levelBefore`, `levelAfter`
- `statDeltas`
- `stressDelta`
- `evolutionProgressDeltas`
- `unlockedEvolutionType`
- `rewardTags`: Efficient, MassiveDiff, TestGreen, BuildFailedButLearned 등.
- `miniGameBonusApplied`
- `dailyCapApplied`
- `messageKey`: UI 문구 키.

### `EvolutionType`

역할:

- 캐릭터의 장기 성장 성향과 외형 방향을 정의한다.

값 예시:

- `Debugger`
- `Guardian`
- `CleanCodeArchitect`
- `TokenBerserker`
- `ProductBard`
- `EfficiencyNinja`
- `PromptSummoner`
- `IncidentSurvivor`
- `RealtimeRanger`

### `GrowthRule`

역할:

- 특정 조건에서 경험치와 스탯 증가량을 계산하는 규칙을 표현한다.

필드 예시:

- `ruleId`
- `targetWorkType`
- `conditions`: token threshold, file count threshold, success/failure, Git category.
- `baseExp`
- `statWeights`
- `tokenMultiplier`
- `fileChangeMultiplier`
- `successBonus`
- `failureCompensation`
- `stressPenalty`
- `dailyCapGroup`
- `priority`

MVP에서는 코드 상수로 시작하고, 추후 ScriptableObject로 전환한다.

### `MiniGameSession`

역할:

- 짧은 미니게임 한 판의 상태와 결과를 표현한다.

필드 예시:

- `miniGameSessionId`
- `startedAt`, `endedAt`
- `durationSeconds`
- `score`
- `tokensCollected`
- `bugsAvoided`
- `testShieldsCollected`
- `buildGaugeFilled`
- `wasPaused`
- `resultGrade`
- `growthBonus`: 제한된 보너스 값.

### `SaveData`

역할:

- 로컬 저장의 단일 루트 모델이다.

필드 예시:

- `saveVersion`
- `createdAt`
- `updatedAt`
- `characterProfile`
- `growthHistory`
- `workSessionSummaries`
- `settings`
- `connectedProjects`
- `privacyPreferences`
- `providerSettings`
- `dailyProgress`

주의사항:

- 원본 프롬프트, 원본 코드, 전체 로그 원문, API Key, Secret은 필드에 포함하지 않는다.
- 저장 가능한 값은 집계값과 마스킹된 메타데이터로 제한한다.

---

## 7. Agent Data Layer 설계

### `AgentLogProvider` 인터페이스 개념

`AgentLogProvider`는 다양한 입력 소스를 `AgentWorkSession`으로 변환하는 어댑터다. 성장 시스템은 Provider 구현체를 몰라야 하며, 오직 정규화된 `AgentWorkSession`만 받는다.

개념적 책임:

- 입력 소스 접근 가능 여부 확인.
- 원본 데이터 읽기 또는 사용자 입력 수집.
- 민감 정보 제거.
- 집계값 생성.
- `AgentWorkSession` 생성.
- 실패 시 fallback 가능한 오류 반환.

공통 출력:

- 성공: `AgentWorkSession`
- 부분 성공: 일부 필드가 Unknown인 `AgentWorkSession` + warning.
- 실패: 사용자에게 보여줄 수 있는 오류 코드 + fallback 제안.

### `ManualSessionProvider`

역할:

- 자동 분석이 실패했거나 일부 데이터가 부족할 때 사용하는 fallback Provider다.
- 사용자가 직접 보정한 작업 타입, 에이전트 타입, 토큰 사용량, 수정 파일 수, 성공 여부를 바탕으로 세션을 보완하거나 생성한다.

입력:

- AgentType 선택.
- WorkType 선택.
- token usage 입력.
- changed file count 입력.
- build/test success 여부.
- 선택 입력: 제목, 짧은 메모.

출력:

- `sourceProvider = Manual`
- 사용자가 직접 확정한 필드는 신뢰도 높음.
- Git/Agent 자동 분석 정보가 있으면 기존 자동 분석 결과를 유지하고 부족한 필드만 보정.
- 자동 분석이 전부 실패한 경우에는 사용자가 입력한 최소 집계값만 포함.

실패 케이스:

- 숫자 입력이 음수.
- 필수 선택값 누락.
- 지나치게 긴 제목 또는 메모.

Fallback:

- 기본값으로 Unknown/0을 넣되 성장 결과가 왜 낮아졌는지 UI에서 설명한다.

### `GitDiffProvider`

역할:

- 선택한 프로젝트 폴더의 Git 상태와 diff를 분석해 세션 요약을 만든다.
- MVP의 1차 자동 데이터 소스이자 가장 먼저 구현할 핵심 Provider다.

입력:

- 사용자가 선택한 프로젝트 폴더.
- 분석 범위: working tree, last commit, branch diff 등. MVP 이후 설정 가능.

출력:

- `GitChangeSummary`
- 추론된 WorkType 후보.
- 파일 카테고리 요약.

실패 케이스:

- Git 미설치.
- 대상 폴더가 Git 저장소가 아님.
- Git 명령 타임아웃.
- 권한 부족.
- diff가 너무 큼.

Fallback:

- Git 분석을 건너뛰지 않고 가능한 범위의 부분 결과를 먼저 표시한다.
- 부족한 changed file count, WorkType, build/test 결과만 Session Review / Correction Screen에서 보정하게 한다.

### `ClaudeCodeLogProvider`

역할:

- Claude Code의 sanitized sample 로그 조사 후 구현할 Provider다.
- 원본 로그 포맷은 확정하지 않는다.

입력:

- 사용자가 지정한 Claude Code 로그 폴더 또는 파일.
- 분석 기간 또는 최근 세션 선택.

출력:

- token usage 집계.
- prompt count 집계.
- tool call count 집계.
- file edit count 집계.
- command run/test/build 추정 집계.
- `AgentWorkSession`

실패 케이스:

- 로그 경로를 찾을 수 없음.
- 포맷 버전 미지원.
- 필수 필드 누락.
- 파일 권한 부족.
- 로그에 민감 정보가 포함되어 파싱 중단 필요.

Fallback:

- 파서가 이해한 필드만 사용하고 나머지는 Unknown으로 둔다.
- 사용자에게 Session Review / Correction Screen에서 부족한 값만 보정하게 한다.
- 원본 저장 없이 오류 코드만 남긴다.

### `CodexLogProvider`

역할:

- Codex의 sanitized sample 로그 조사 후 구현할 Provider다.
- Codex 실행 환경과 로그 저장 위치가 달라질 수 있으므로 설정 가능해야 한다.

입력:

- 사용자가 지정한 Codex 로그/세션 폴더.
- 분석 대상 세션.

출력:

- token usage 또는 추정치.
- command run count.
- patch/file edit count.
- test/build 결과 추정.
- `AgentWorkSession`

실패 케이스와 fallback:

- ClaudeCodeLogProvider와 동일한 원칙을 적용한다.
- 포맷이 바뀌면 graceful degradation으로 일부 집계만 사용한다.

### `CursorLogProvider` future

역할:

- Cursor 또는 IDE 기반 에이전트 로그 연동을 위한 미래 Provider다.

주의사항:

- Cursor의 로컬 저장 구조와 개인정보 정책을 별도 조사해야 한다.
- MVP 범위에서 제외한다.

### `MockAgentLogProvider`

역할:

- 테스트와 데모를 위한 Provider다.
- deterministic한 `AgentWorkSession`을 생성해 성장 계산과 UI를 검증한다.

입력:

- 테스트 fixture 또는 미리 정의된 시나리오.

출력:

- 항상 동일한 세션 데이터.

실패 케이스:

- 테스트에서 의도적으로 실패 Provider를 만들어 fallback 테스트에 사용한다.

---

## 8. AI Agent 로그 연동 전략

TokenForge는 Claude Code / Codex 전용 앱이 아니다. 여러 AI 코딩 에이전트의 작업 데이터를 `AgentLogProvider` 어댑터로 수집하고, 최종적으로 `AgentWorkSession` 공통 모델로 정규화한다.

MVP에서도 자동 연동을 핵심 경험으로 포함한다. 다만 모든 에이전트를 동일한 완성도로 지원하지 않고, 우선 지원 대상과 베타/확장 대상을 나눈다.

### Agent 지원 단계

Tier 1: MVP 우선 지원

- Git 기반 자동 분석.
- Claude Code Provider.
- Codex Provider.

Tier 2: MVP 베타 지원

- Cursor Provider skeleton.
- Windsurf Provider skeleton.

Tier 3: Provider 구조만 준비

- GitHub Copilot.
- Continue.
- Aider.
- Roo Code.
- Cline.
- JetBrains AI Assistant.
- Unknown Agent.

### 단계별 접근

1. Git 자동 분석
   - 프로젝트 폴더 선택 후 Git repository 여부를 감지한다.
   - `git status --short`, `git diff --stat`, `git diff --numstat`, `git log --oneline`, `git branch --show-current`를 활용해 변경량과 작업 유형을 추론한다.
   - 변경 파일 수, 추가/삭제 라인 수, 파일 카테고리, 브랜치/커밋 키워드, 작업 디렉토리 상태를 `GitChangeSummary`로 정규화한다.

2. Claude Code / Codex 로그 자동 분석
   - 사용자가 로그 폴더 또는 세션 파일 위치를 직접 지정한다.
   - 최근 세션 또는 선택한 세션을 분석한다.
   - token usage, prompt count, tool call count, file edit count, command run count, test/build 추정값을 집계한다.
   - 원본 로그는 저장하지 않고 집계값만 `AgentWorkSession`에 반영한다.

3. 자동 분석 결과 확인/보정
   - 추론된 AgentType, WorkType, token usage, 변경 파일 수, 추가/삭제 라인 수, 테스트/빌드 결과, confidence를 보여준다.
   - 사용자는 불확실하거나 잘못된 항목만 수정한다.
   - 수동 입력은 메인 플로우가 아니라 자동 분석 실패 또는 부족한 데이터 보완용 fallback이다.

4. 추가 에이전트 Provider 확장
   - Cursor/Windsurf는 skeleton 또는 베타 Provider로 시작한다.
   - GitHub Copilot, Continue, Aider, Roo Code, Cline, JetBrains AI Assistant는 로그 접근 가능성과 포맷 안정성을 조사한 뒤 Provider를 추가한다.
   - 모든 Provider는 성장 시스템에 직접 연결되지 않고 `AgentWorkSession`만 반환한다.

### 로그 경로 정책

- 로그 경로는 사용자 환경마다 다를 수 있으므로 앱에서 직접 지정 가능하게 한다.
- 기본 경로 자동 탐색은 "후보 제안" 정도로만 사용한다.
- 사용자가 명시적으로 선택한 폴더만 분석한다.
- 접근 실패 시 권한 요청과 Session Review / Correction Screen의 수동 보정 fallback을 제공한다.
- 경로 후보를 문서나 코드에 확정값처럼 박아두지 않고 Provider 설정으로 관리한다.

### 원본 저장 금지

로그 원문은 저장하지 않는다. Provider는 사용자가 허용한 위치의 원본을 읽은 뒤 다음 집계값만 남긴다.

- token usage.
- tool call count.
- file edit count.
- command run count.
- prompt count.
- test/build success 또는 failure 추정.
- duration.
- file category counts.

프롬프트 전문, 코드 조각, 명령 출력 전문, 파일 경로 원문, Git remote URL, 커밋 diff 원문은 저장하지 않는다.

### 포맷 변경 대응

- Provider마다 `parserVersion`을 둔다.
- 샘플 로그 fixture는 sanitized 형태로 버전별 보관한다.
- schema validation을 통해 필수 필드가 없으면 partial parse로 전환한다.
- 알 수 없는 필드는 무시한다.
- 파싱 실패가 성장 시스템 오류로 번지지 않게 한다.
- Provider 결과에는 confidence와 warning을 포함한다.
- confidence가 낮은 필드는 자동 확정하지 않고 사용자 보정 대상으로 표시한다.

---

## 9. Git Analysis 설계

### Git 분석으로 얻을 수 있는 데이터

- 변경 파일 수.
- 추가/삭제 라인 수.
- 변경 파일 경로.
- 테스트 파일 변경 여부.
- docs 변경 여부.
- UI 파일 변경 여부.
- service/repository/usecase/DI 변경 여부.
- 커밋 메시지 키워드.
- 현재 브랜치.
- 작업 디렉토리 상태.

### 분석 명령 후보

- `git status --short`
- `git diff --stat`
- `git diff --numstat`
- `git log --oneline -n 20`
- `git branch --show-current`

### 파싱 전략

`git status --short`:

- 변경된 파일 수와 상태를 파악한다.
- staged/unstaged/untracked 상태를 구분할 수 있다.
- 원본 경로는 저장하지 않고 카테고리 분류 후 폐기한다.

`git diff --stat`:

- 사람이 읽기 쉬운 요약에 적합하다.
- UI 표시용 요약에 사용할 수 있다.

`git diff --numstat`:

- 추가/삭제 라인 수를 구조적으로 얻기 좋다.
- binary 파일은 `-`로 표시될 수 있으므로 예외 처리한다.

`git log --oneline -n 20`:

- 최근 커밋 메시지 키워드로 작업 성향을 추정한다.
- `fix`, `bug`, `refactor`, `test`, `docs`, `ui`, `style`, `perf` 같은 키워드를 카운트한다.

`git branch --show-current`:

- 현재 브랜치를 가져온다.
- 브랜치명에 민감 정보가 있을 수 있으므로 저장 기본값은 해시 또는 저장 안 함으로 둔다.

### 파일 경로 카테고리 분류

경로 패턴 예시:

- 테스트: `test`, `tests`, `spec`, `__tests__`, `*.Tests.cs`
- 문서: `docs`, `README`, `*.md`
- UI: `ui`, `view`, `screen`, `component`, `prefab`, `ux`
- 아키텍처: `service`, `repository`, `usecase`, `interactor`, `di`, `container`
- 설정: `.github`, `package`, `config`, `settings`, `ci`
- 도메인: `domain`, `model`, `entity`, `valueobject`

### WorkType 매핑 전략

- 테스트 파일 비중이 높으면 `Test`.
- docs 파일 비중이 높으면 `Docs`.
- UI 경로와 asset/prefab 변경이 많으면 `UIUX`.
- service/repository/usecase/DI 변경이 많고 라인 이동이 크면 `Refactor` 또는 `Architecture` 성향.
- 커밋 메시지에 fix/bug/hotfix가 많으면 `Bugfix`.
- 새 파일과 기능 경로 변경이 많으면 `Feature`.
- 신호가 섞이면 `Mixed`.
- 확신이 낮으면 사용자 선택을 우선한다.

### CharacterStats 매핑 전략

- 변경 파일 수와 라인 수는 경험치 규모에 영향을 준다.
- 테스트 파일 변경은 `Stability`, `Debug`를 올린다.
- docs 변경은 `Architecture`, `Efficiency`를 올린다.
- UI 파일 변경은 `Design`, `Creativity`를 올린다.
- service/repository/usecase/DI 변경은 `Architecture`, `Logic`을 올린다.
- 큰 diff와 실패 결과가 겹치면 `Stress`가 증가한다.
- 작은 diff와 성공 결과가 겹치면 `Efficiency`가 증가한다.

---

## 10. 성장 시스템 설계

성장 시스템은 `AgentWorkSession`과 현재 `CharacterProfile`을 입력으로 받아 `CharacterGrowthResult`를 생성한다.

### 스탯 후보

- Logic
- Debug
- Architecture
- Design
- Stability
- Velocity
- Creativity
- Efficiency
- Stress

### 작업 유형별 성장 예시

Feature:

- 주요 성장: Logic, Velocity, Creativity
- 보조 성장: Architecture
- 조건: 성공 시 Velocity 보너스, 큰 diff면 Stress 소폭 증가.

Bugfix:

- 주요 성장: Debug, Stability
- 보조 성장: Logic
- 조건: 실패 후 성공한 세션이면 Debug 추가 보너스.

Refactor:

- 주요 성장: Architecture, Logic
- 보조 성장: Efficiency
- 조건: 라인 삭제가 추가보다 많고 성공하면 Efficiency 보너스.

Test:

- 주요 성장: Stability, Debug
- 보조 성장: Logic
- 조건: 실패 테스트를 통과로 바꾼 경우 큰 Stability 보너스.

UI/UX:

- 주요 성장: Design, Creativity
- 보조 성장: Velocity
- 조건: UI 파일과 asset/prefab 변경이 많으면 Design 보너스.

Docs:

- 주요 성장: Architecture, Efficiency
- 보조 성장: Logic
- 조건: 작은 diff로 문서만 정리한 경우 Stress 감소 가능.

High Token + Large Diff:

- 큰 경험치.
- Stress 증가.
- Token Berserker 진화 진행도 증가.

Small Token + Success:

- Efficiency 증가.
- Stress 감소 또는 증가 없음.
- Efficiency Ninja 진화 진행도 증가.

Failed Build / Failed Test:

- Stress 증가.
- Debug 성장 후보.
- 실패를 입력해도 완전 벌점으로 만들지 않고 "학습 보상"을 준다.

### 성장 계산 방식

MVP 공식은 단순 가중치 기반으로 시작한다.

- Base EXP: WorkType별 기본 경험치.
- Token Multiplier: token usage 구간별 배율.
- File Change Multiplier: 변경 파일 수와 라인 수 구간별 배율.
- Success Bonus: 빌드/테스트 성공 시 보너스.
- Failure Compensation: 실패 시 경험치는 낮추되 Debug/Stability 후보 보상 제공.
- Stress Penalty: Stress가 너무 높으면 일부 보상 효율 감소.
- Daily Cap: 하루 반복 입력으로 과도한 성장을 막는 제한.

개념 공식:

```text
sessionExp = baseExp
  * tokenMultiplier
  * fileChangeMultiplier
  + successBonus
  + failureCompensation
  - stressPenalty
```

MVP 권장값:

- Base EXP는 10~50 범위.
- tokenMultiplier는 0.8~2.0 범위.
- fileChangeMultiplier는 0.8~2.5 범위.
- 성공 보너스는 baseExp의 10~30%.
- 실패 보상은 Debug/Stability에 소량 부여.
- 미니게임 보너스는 최종 보상의 5~15%를 넘지 않는다.

### Daily Cap과 anti-grinding

필요 이유:

- 사용자가 의미 없는 작은 세션을 반복 입력해 성장을 빠르게 올리는 것을 막는다.
- 실제 개발 세션의 의미를 유지한다.

정책 후보:

- 하루 총 EXP soft cap.
- 동일 WorkType 반복 입력 시 점진적 보상 감소.
- token/file count가 0인 세션은 최소 보상만 제공.
- Manual Fallback Input만 사용한 세션은 일정 수 이상부터 soft cap 적용.
- Git/Provider 검증이 있는 세션은 soft cap 완화.

MVP에서는 간단히 "하루 기본 성장량 초과 시 EXP 50% 감소" 정도로 시작한다.

### 밸런싱 확장

MVP에서는 코드 상수로 구현한다. 이후 다음으로 확장한다.

- `GrowthRule` ScriptableObject.
- `growth-balancing.md`에 변경 이력 기록.
- CSV/JSON 기반 밸런싱 테이블.
- 원격 설정은 privacy-first와 오프라인 정책을 해치므로 후순위.

---

## 11. 진화 시스템 설계

진화 시스템은 누적 작업 성향을 반영한다. 특정 스탯이 높거나 특정 패턴이 반복되면 진화 타입이 해금된다.

### 공통 진화 조건

- 최소 레벨 조건.
- 주요 스탯 임계값.
- 특정 WorkType 누적 횟수.
- 특정 reward tag 누적 횟수.
- Stress 상태 또는 회복 이력.

진화는 한 번의 세션으로 즉시 결정되기보다 누적 진행도를 통해 해금되는 방식이 적합하다.

### Debugger

- 조건: Bugfix/Test 세션 누적, Debug 스탯 높음, failed test/build 이후 성공 경험.
- 주요 스탯: Debug, Stability.
- 해금 연출: 오류 흔적을 추적해 빛나는 브레이크포인트를 활성화.
- 외형 방향: 돋보기, 콘솔 라인, 브레이크포인트 장식.

### Guardian

- 조건: Test 세션과 성공한 build/test 결과가 많음.
- 주요 스탯: Stability, Debug.
- 해금 연출: 테스트 방패가 캐릭터 주변에 형성.
- 외형 방향: 방패, 체크마크, 안정적인 색조.

### Clean Code Architect

- 조건: Refactor, Docs, Architecture 관련 파일 변경이 많음.
- 주요 스탯: Architecture, Logic, Efficiency.
- 해금 연출: 복잡한 코드 블록이 정렬된 구조물로 재배치.
- 외형 방향: 청사진, 격자, 설계도구.

### Token Berserker

- 조건: High Token + Large Diff 세션 누적.
- 주요 스탯: Velocity, Creativity, Stress.
- 해금 연출: 토큰 폭풍을 흡수하며 과부하 형태로 변신.
- 외형 방향: 강한 에너지, 큰 무기, 불안정한 이펙트.

### Product Bard

- 조건: Feature, UI/UX, Docs 세션이 균형 있게 많음.
- 주요 스탯: Creativity, Design, Architecture.
- 해금 연출: 기능 노트와 UI 조각이 하나의 이야기로 연결.
- 외형 방향: 악보, 스크롤, 밝은 UI glyph.

### Efficiency Ninja

- 조건: Small Token + Success, 작은 diff 성공, 반복적으로 낮은 Stress.
- 주요 스탯: Efficiency, Velocity.
- 해금 연출: 적은 움직임으로 여러 토큰을 정리.
- 외형 방향: 간결한 실루엣, 빠른 이동 잔상.

### Prompt Summoner

- 조건: prompt count가 많고 다양한 WorkType을 다룸.
- 주요 스탯: Creativity, Logic.
- 해금 연출: 프롬프트 문장이 소환진처럼 펼쳐짐.
- 외형 방향: 마법진, 입력 커서, 말풍선.

### Incident Survivor

- 조건: Failed Build/Failed Test, Stress 증가 세션 이후 회복 성공.
- 주요 스탯: Debug, Stability, Stress resilience.
- 해금 연출: 깨진 빌드 게이지가 복구되며 훈장 획득.
- 외형 방향: 패치 자국, 경고 삼각형, 복구 배지.

### Realtime Ranger

- 조건: 짧은 세션을 자주 처리하고 command/test 실행이 많음.
- 주요 스탯: Velocity, Debug.
- 해금 연출: 실시간 로그 라인을 따라 빠르게 이동.
- 외형 방향: 헤드셋, 속도감 있는 UI 라인.

---

## 12. 미니게임 시스템 설계

미니게임은 대기 시간에 플레이하는 짧은 보조 루프다. 핵심 로그 성장 시스템을 압도하지 않도록 보상 비중을 제한한다.

### MVP 미니게임: 토큰 수집

핵심 규칙:

- 30초~3분 세션.
- 키보드만으로 조작.
- 토큰을 수집하면 점수 증가.
- 버그 몬스터에 닿으면 점수 감소 또는 shield 소모.
- 테스트 방패를 획득하면 일정 시간 보호.
- 빌드 게이지를 채우면 세션 종료 시 보너스.
- 언제든 일시정지 가능.
- 소리 없이도 모든 상태를 시각적으로 이해 가능.

입력:

- 이동: 방향키 또는 WASD.
- 일시정지: Esc 또는 Space.
- 확정: Enter.

### 보상 반영

미니게임 결과는 `MiniGameSession`으로 저장하고 성장 결과에 제한된 보너스로 반영한다.

보상 예시:

- 토큰 수집량: EXP +5% 이내.
- 버그 회피 성공: Debug +1 또는 Stability +1.
- 테스트 방패 획득: Stress 감소 소량.
- 빌드 게이지 완성: Success Bonus에 소량 추가.

제한:

- 미니게임 보너스는 최종 세션 보상의 5~15%를 넘지 않는다.
- 미니게임만 반복해서 캐릭터가 크게 성장하지 않도록 daily cap을 둔다.
- 실제 작업 세션이 없는 미니게임은 cosmetic 또는 아주 작은 보상만 준다.

### 설계 이유

- TokenForge의 핵심 가치는 개발 로그 기반 성장이다.
- 미니게임이 너무 강하면 사용자가 실제 개발 세션보다 게임 반복을 최적화하게 된다.
- 대기 시간에 부담 없이 즐기는 보조 활동으로 유지해야 한다.

---

## 13. 저장 시스템 설계

### MVP 저장 방식

- JSON file.
- `Application.persistentDataPath`.
- `SaveData` 단일 루트 모델.
- 자동 저장.
- 수동 백업/복원은 추후 기능.

### 저장 타이밍

- 앱 시작 시 SaveData 로드.
- 수동 세션 입력 완료 후 저장.
- 성장 결과 반영 후 저장.
- 설정 변경 후 저장.
- 미니게임 종료 후 저장.
- 앱 종료 또는 pause 이벤트에서 저장.

### 저장할 데이터

- 캐릭터 상태.
- 성장 로그.
- 작업 세션 요약.
- 설정.
- 연결 프로젝트 목록.
- Privacy preferences.
- Provider 설정.
- Daily cap 진행도.

### 저장하지 말아야 할 데이터

- 원본 프롬프트.
- 원본 코드.
- 전체 로그 원문.
- API Key.
- Secret.
- 민감한 터미널 출력.
- 원본 파일 경로 전체.
- 회사명/프로젝트명이 포함된 원본 브랜치명.

### JSON 구조 원칙

- 최상위에 `saveVersion`을 둔다.
- 날짜는 ISO 8601 문자열 또는 UTC timestamp로 통일한다.
- 세션 히스토리는 요약만 저장한다.
- 큰 배열이 커지면 pruning 또는 archive 정책을 둔다.
- 저장 실패 시 기존 파일을 즉시 덮어쓰지 않고 임시 파일 후 원자적 교체를 고려한다.

### SQLite/LiteDB 확장 가능성

세션이 많아지고 통계 기능이 늘어나면 JSON 단일 파일은 다음 문제가 생길 수 있다.

- 히스토리 조회 성능 저하.
- 부분 업데이트 어려움.
- 마이그레이션 복잡도 증가.
- 통계 질의 구현이 번거로움.

확장 후보:

- SQLite: 세션 히스토리, 일별 통계, 프로젝트별 통계를 질의하기 좋다.
- LiteDB: C# 객체 저장 경험이 단순하지만 Unity 빌드 호환성 검증 필요.

MVP에서는 JSON으로 시작하고, 세션 수가 수천 개 이상이 되거나 통계 화면이 핵심 기능이 될 때 DB 전환을 검토한다.

---

## 14. Privacy-first 설계

Privacy-first는 TokenForge의 핵심 설계 원칙이다. 이 제품은 개발자의 코드, 프롬프트, 터미널 출력, 프로젝트 경로 등 민감한 정보에 가까운 데이터를 다룰 가능성이 있으므로, 저장/분석/표시 정책을 보수적으로 잡아야 한다.

### 원칙

- 로컬 우선.
- 원본 데이터 외부 전송 금지. 서버 동기화가 켜져도 허용된 집계값만 전송.
- 로그 원문 저장 금지.
- 집계값만 저장.
- 파일 경로는 마스킹 또는 해시 처리.
- 사용자가 직접 로그/프로젝트 접근 권한을 허용.
- 분석 대상 프로젝트를 사용자가 선택.
- 삭제 기능 제공.
- 샘플 로그는 반드시 sanitized fixture만 Git에 포함.

### 민감 정보 예시

- 프롬프트.
- 코드 조각.
- 파일 경로.
- API Key.
- Secret.
- 터미널 출력.
- 에러 로그.
- 프로젝트명.
- 회사/개인 정보.
- 브랜치명.
- 커밋 메시지.
- 로그 파일 내 사용자명 또는 홈 디렉토리 경로.

### 저장 금지 데이터와 허용 데이터

저장 금지:

- "이 코드를 이렇게 수정해줘" 같은 프롬프트 전문.
- 에이전트가 생성/수정한 코드 전문.
- 터미널 출력 전문.
- 전체 로그 파일.
- `.env`, keychain, credential, token 문자열.
- 원본 절대 경로.

저장 허용:

- 총 토큰 수.
- 파일 변경 수.
- 추가/삭제 라인 수.
- 테스트 실행 횟수.
- 빌드 성공/실패 여부.
- 파일 카테고리별 개수.
- 마스킹된 프로젝트 alias.
- 해시 처리된 repository ID.

### Sanitization 정책

- Provider는 원본을 읽고 즉시 집계값으로 변환한다.
- 원본 문자열은 SaveData나 디버그 로그에 넘기지 않는다.
- 파일 경로는 카테고리 분류 후 폐기하거나 해시 처리한다.
- 샘플 로그를 저장소에 넣기 전 `Tools/log-sanitizer`로 검사한다.
- secret 패턴 탐지: `api_key`, `token`, `secret`, `password`, `Authorization`, `Bearer`, `.env` 등.
- sanitization 실패 시 sample import를 중단한다.

### 사용자 제어

- 사용자가 분석할 프로젝트 폴더를 직접 선택한다.
- 사용자가 로그 폴더를 직접 선택한다.
- 로그 분석 ON/OFF 토글 제공.
- 연결된 프로젝트 삭제 기능 제공.
- 세션 히스토리 삭제 기능 제공.
- 모든 분석 데이터 삭제 기능 제공.
- 원문 로그 저장 금지 옵션은 기본값이 아니라 강제 정책으로 둔다.

### 외부 전송 정책

MVP는 계정 시스템, 클라우드 동기화, 원격 서버를 포함할 수 있다. 다만 서버로 전송 가능한 데이터는 캐릭터 성장과 사용자 설정에 필요한 집계값으로 제한한다.

서버 전송 가능 데이터:

- `userId`
- `nickname`
- `selectedCharacterId`
- `characterLevel`
- `characterStats`
- `evolutionType`
- `unlockedItems`
- `sessionSummary`
- `workType distribution`
- `totalToken bucket` 또는 range
- daily/weekly growth summary
- `createdAt` / `updatedAt`
- user settings 일부

서버 전송 금지 데이터:

- 원본 프롬프트.
- 원본 코드.
- 전체 로그 원문.
- 터미널 출력 원문.
- API Key.
- Secret.
- 절대 파일 경로.
- Git remote URL.
- 커밋 diff 원문.
- 회사명/프로젝트명 자동 추출값.
- 브랜치명 원문.
- 커밋 메시지 원문.

프로젝트 식별은 `projectPathHash`, `projectAlias`, `localOnlyProjectId` 중심으로 처리한다. 사용자가 명시적으로 입력한 alias 외에는 프로젝트명이나 경로를 서버로 전송하지 않는다.

telemetry를 도입하더라도 다음 원칙을 지켜야 한다.

- opt-in.
- 익명 집계.
- 원본 로그/코드/프롬프트 전송 금지.
- 전송 전 사용자에게 명확한 설명.
- 팀 단위 랭킹은 개인정보 이슈 때문에 별도 법적/제품 검토 필요.

---

## 15. UI/UX 화면 설계

### Main Dashboard

목적:

- 현재 캐릭터 상태, 최근 자동 분석 세션, 분석 대기 상태, 미니게임 진입점을 보여준다.

주요 컴포넌트:

- 캐릭터 요약 카드.
- Level/EXP bar.
- 주요 스탯 top 3.
- 최근 성장 결과.
- `Analyze Now` 버튼.
- `Review Detected Session` 버튼.
- `Play MiniGame` 버튼.
- Settings/Privacy 진입.

사용자 플로우:

1. 앱 실행.
2. Dashboard에서 캐릭터 상태 확인.
3. 자동 감지된 세션을 검토하거나 즉시 분석을 실행.
4. 대기 중이면 미니게임 시작.

### Auto Analysis Dashboard

목적:

- 연결된 프로젝트와 AI Agent 로그에서 자동 분석 가능한 세션을 탐지하고, 사용자가 성장 반영할 세션을 선택하게 한다.

주요 컴포넌트:

- 연결 프로젝트 목록.
- 최근 Git 변경 감지 카드.
- 최근 Agent 세션 감지 카드.
- 분석 confidence 표시.
- `Analyze Now` 버튼.
- `Review Detected Session` 버튼.
- 분석 실패/권한 오류 안내.

사용자 플로우:

1. 사용자가 연결된 프로젝트와 로그 Provider 상태를 확인한다.
2. 앱이 감지한 Git 변경 또는 Agent 세션을 카드로 보여준다.
3. 사용자는 분석을 실행하거나 감지된 세션을 검토 화면으로 보낸다.

### Session Review / Correction Screen

목적:

- 자동 분석 결과를 확인하고 부족하거나 잘못된 값만 보정한다.

주요 컴포넌트:

- 추론된 AgentType.
- 추론된 WorkType.
- token usage 또는 token range.
- 변경 파일 수.
- 추가/삭제 라인 수.
- 테스트/빌드 감지 결과.
- confidence와 warning.
- 사용자가 수정 가능한 필드.
- `Confirm Growth` 버튼.

사용자 플로우:

1. 자동 분석 결과가 화면에 표시된다.
2. 사용자는 confidence가 낮거나 warning이 있는 필드만 확인한다.
3. 필요한 값만 보정한 뒤 `Confirm Growth`를 실행한다.
4. Growth Result Screen으로 이동한다.

### Character Room

목적:

- 캐릭터의 성장과 진화 상태를 시각적으로 보여준다.

주요 컴포넌트:

- 캐릭터 스프라이트.
- mood state.
- 진화 진행도.
- 해금된 장식 또는 외형 변화.
- 주요 스탯 표시.

사용자 플로우:

- 성장 결과 확인 후 Character Room으로 이동해 변화 확인.
- 진화 해금 시 연출 표시.

### Manual Fallback Input

목적:

- 자동 분석이 실패했거나 사용자가 직접 기록하고 싶은 경우에만 사용하는 보조 입력 화면.

주요 컴포넌트:

- AgentType 선택.
- WorkType 선택.
- token usage 입력.
- 수정 파일 수 입력.
- 빌드 성공 여부.
- 테스트 성공 여부.
- 세션 제목 optional.
- 개인정보 안내 문구.

주의사항:

- 이 화면을 메인 플로우처럼 강조하지 않는다.
- 기본 진입점은 Auto Analysis Dashboard와 Session Review / Correction Screen이다.
- 사용자가 직접 입력한 값도 원본 프롬프트/코드/터미널 출력 없이 집계값만 저장한다.

사용자 플로우:

1. 자동 분석 실패, 권한 오류, 데이터 부족 상황에서 fallback으로 화면 진입.
2. 필요한 최소 작업 정보를 입력.
3. `Confirm Growth` 실행.
4. Growth Result Screen으로 이동.

### Growth Result Screen

목적:

- 한 세션이 캐릭터 성장에 어떻게 반영되었는지 명확히 보여준다.

주요 컴포넌트:

- 획득 EXP.
- 레벨 변화.
- 스탯 증가량.
- Stress 변화.
- reward tags.
- 진화 진행도 변화.
- 미니게임 보너스 적용 여부.

사용자 플로우:

- 사용자는 결과를 확인하고 Character Room 또는 Dashboard로 이동한다.

### MiniGame Screen

목적:

- 대기 시간에 짧은 플레이를 제공한다.

주요 컴포넌트:

- 플레이 영역.
- score.
- timer.
- build gauge.
- shield 상태.
- pause overlay.

사용자 플로우:

1. Dashboard 또는 대기 상태에서 미니게임 시작.
2. 30초~3분 플레이.
3. 결과를 세션 보너스로 제한 반영.

### Session History

목적:

- 과거 작업 세션과 성장 로그를 확인한다.

주요 컴포넌트:

- 날짜별 세션 목록.
- WorkType/AgentType 필터.
- EXP/스탯 변화 요약.
- 삭제 버튼.

주의사항:

- 원문 프롬프트나 파일 경로를 표시하지 않는다.
- 민감할 수 있는 제목은 사용자가 직접 삭제/수정 가능하게 한다.

### Settings / Privacy

목적:

- 저장/분석/권한/개인정보 정책을 제어한다.

주요 컴포넌트:

- 로그 분석 ON/OFF.
- 연결 프로젝트 목록.
- 로그 폴더 경로 설정.
- 데이터 삭제.
- privacy policy 요약.
- 원문 저장 금지 안내.
- 계정/클라우드 동기화 상태.

### Project Connection Screen

목적:

- Git 분석 또는 로그 분석 대상 프로젝트를 연결한다.

주요 컴포넌트:

- 프로젝트 폴더 선택.
- Git 감지 결과.
- repository alias 설정.
- 분석 가능한 항목 표시.
- 권한 오류 표시.
- 로그 Provider 연결 상태 표시.

---

## 16. 설정 / 권한 설계

macOS 앱에서 고려해야 할 항목은 다음과 같다.

### 로그 폴더 위치 지정

- 사용자가 직접 로그 폴더 또는 파일을 선택한다.
- 앱은 후보 경로를 제안할 수 있지만 확정 경로로 단정하지 않는다.
- Claude Code / Codex 로그 경로는 조사 필요 항목이다.

### 프로젝트 폴더 선택

- Git 분석 대상 프로젝트는 사용자가 직접 선택한다.
- 선택한 프로젝트에는 alias를 부여한다.
- 저장 시 원본 절대 경로 대신 alias와 해시를 우선 사용한다.

### 파일 접근 권한

- Unity macOS 빌드에서 폴더 접근 권한 UX를 확인해야 한다.
- 샌드박스 배포 시 security-scoped bookmarks 같은 네이티브 처리가 필요할 수 있다.
- 제품형 MVP에서는 Unity file picker 또는 네이티브 helper를 통해 사용자가 명시적으로 선택한 폴더만 접근한다.

### Git 실행 가능 여부 확인

- 앱 시작 또는 프로젝트 연결 시 Git 실행 가능 여부를 확인한다.
- `git --version`으로 확인 가능하다.
- Git이 없으면 Git 분석 기능을 비활성화하고 Agent 로그 분석 또는 Manual Fallback Input을 안내한다.

### 로그 분석 ON/OFF

- 기본값은 사용자가 명시적으로 허용하기 전까지 OFF로 둔다.
- 온보딩에서 자동 분석의 범위와 저장하지 않는 데이터를 명확히 설명한다.
- 사용자가 프로젝트 폴더와 로그 폴더를 직접 선택해야 자동 분석이 활성화된다.
- 자동 분석이 켜져도 원문 저장 금지 정책은 유지한다.
- 수동 fallback은 항상 사용 가능하다.

### 계정 / 클라우드 동기화 설정

- Guest Mode를 기본 지원한다.
- 계정 로그인은 클라우드 동기화와 다중 기기 확장을 위한 선택 기능으로 제공한다.
- 동기화 대상은 캐릭터 상태, 성장 요약, 설정 일부로 제한한다.
- 원본 로그/프롬프트/코드/터미널 출력은 동기화하지 않는다.
- 사용자는 클라우드 동기화를 끄고 로컬 전용으로 사용할 수 있어야 한다.

### 분석 데이터 삭제

삭제 범위:

- 특정 세션 삭제.
- 특정 프로젝트 연결 삭제.
- Provider 설정 삭제.
- 전체 SaveData 초기화.

삭제 UX:

- 삭제 전 어떤 데이터가 지워지는지 설명한다.
- 원본 프로젝트나 원본 로그 파일은 삭제하지 않는다.
- TokenForge가 저장한 집계 데이터만 삭제한다.

---

## 17. 테스트 전략

테스트는 Unity Test Framework 기준으로 EditMode Test와 PlayMode Test를 나눈다.

### EditMode Test

대상:

- GrowthRule 계산 테스트.
- WorkType 추론 테스트.
- GitChangeSummary 파싱 테스트.
- Claude/Codex sanitized sample parsing 테스트.
- SaveData serialization 테스트.
- Privacy sanitizer 테스트.
- Provider fallback 테스트.

예시:

- Feature 세션은 Logic/Velocity/Creativity를 증가시킨다.
- Bugfix 실패 후 성공 세션은 Debug와 Stability를 증가시킨다.
- `git diff --numstat`의 binary file 표기를 안전하게 처리한다.
- docs 경로가 많으면 WorkType이 Docs로 추론된다.
- secret 패턴이 포함된 sample은 import 실패 또는 마스킹된다.
- SaveData에 원본 로그 필드가 존재하지 않는다.
- Claude Provider가 미지원 schema를 만나도 앱이 크래시하지 않는다.

### PlayMode Test

대상:

- Auto Analysis Dashboard와 Session Review / Correction Screen 흐름.
- Manual Fallback Input 화면 흐름.
- Growth Result Screen 표시.
- Character Room stat 반영.
- MiniGame 시작/일시정지/종료.
- Settings/Privacy 토글.
- 자동 저장 트리거.

예시:

- 사용자가 수동 세션을 입력하면 결과 화면으로 이동한다.
- 미니게임을 일시정지하면 timer가 멈춘다.
- 성장 결과 후 캐릭터 레벨 UI가 업데이트된다.
- Privacy settings에서 분석 OFF 시 Provider 자동 실행이 막힌다.

### 테스트 데이터 정책

- Claude/Codex 테스트 fixture는 반드시 sanitized sample만 사용한다.
- 실제 사용자 로그를 테스트 리소스에 넣지 않는다.
- 경로, 프로젝트명, 브랜치명은 가짜 값만 사용한다.

---

## 18. MVP 기능 범위

### MVP에 포함할 것

#### Unity/macOS 앱

- Unity 2D 기반 macOS 앱.
- 캐릭터 1종.
- Main Dashboard.
- Auto Analysis Dashboard.
- Session Review / Correction Screen.
- Manual Fallback Input.
- Character Room.
- Growth Result Screen.
- Session History.
- Settings / Privacy.
- 간단한 토큰 수집 미니게임.

#### 프로젝트 폴더 연결

- 사용자가 로컬 개발 프로젝트 폴더를 선택.
- Git repository 여부 감지.
- 프로젝트별 분석 상태 저장.
- 여러 프로젝트 연결 지원.
- 프로젝트 alias 설정.
- 원본 절대 경로 저장 최소화.

#### Git 자동 분석

- `git status --short`.
- `git diff --stat`.
- `git diff --numstat`.
- `git log --oneline -n 20`.
- `git branch --show-current`.
- 변경 파일 수 계산.
- 추가/삭제 라인 수 계산.
- 파일 경로 기반 WorkType 추론.
- 테스트/docs/UI/architecture 관련 변경 감지.
- Git 실패 시 fallback 처리.

#### AI Agent 로그 자동 분석

- Claude Code Provider.
- Codex Provider.
- Cursor/Windsurf Provider skeleton 또는 베타 Provider.
- GitHub Copilot, Continue, Aider, Roo Code, Cline, JetBrains AI는 Provider 확장 구조만 준비.
- 모든 Provider는 `AgentWorkSession`으로 정규화.
- 원본 로그 저장 금지.
- `parserVersion`, `confidence`, `warning` 제공.

#### 실시간 또는 준실시간 자동 감시

- 앱 실행 중 주기적 polling 기반 자동 스캔.
- 사용자가 연결한 프로젝트/로그 폴더만 분석.
- 중복 세션 방지.
- 분석 실패 시 graceful fallback.
- 완전한 백그라운드 데몬 수준은 MVP 이후 검토.

#### 성장 시스템

- `AgentWorkSession` 기반 EXP 계산.
- Logic, Debug, Architecture, Design, Stability, Velocity, Creativity, Efficiency, Stress 스탯 반영.
- 작업 유형별 성장 가중치.
- 토큰 사용량/변경량/성공 여부 기반 보정.
- 성장 로그 저장.
- 진화 타입 후보 계산.
- Daily cap 또는 anti-grinding 기본 정책.

#### 저장 시스템

- 로컬 JSON 저장.
- SaveData versioning.
- 자동 저장.
- 분석 데이터 삭제 기능.
- 추후 SQLite/LiteDB 전환 가능 구조.

#### 계정 / 클라우드 / 서버

- Guest Mode.
- 최소 계정 시스템.
- 캐릭터 성장 상태 동기화.
- 세션 요약값 동기화.
- 설정 일부 동기화.
- 원본 로그/프롬프트/코드/터미널 출력 동기화 금지.
- Privacy-first API 계약.

#### Steam 배포 준비

- Steam 정식 출시 수준은 아니어도, macOS standalone build 구조를 정리한다.
- 앱 아이콘, 기본 설정, 저장 경로, 빌드 산출물을 관리한다.
- 추후 Steamworks 연동 가능성을 고려한 구조를 둔다.

### MVP에서 fallback으로만 제공할 것

- 완전 수동 작업 세션 등록.
- 토큰 사용량 수동 입력.
- 작업 타입 수동 선택.
- 빌드/테스트 성공 여부 수동 보정.

### MVP에서 제외할 것

- 모든 AI 에이전트와 모든 IDE의 로그를 100% 완전하게 지원하는 것.
- 각 에이전트의 비공개 내부 포맷에 강하게 의존하는 것.
- 원본 프롬프트, 원본 코드, 전체 로그 원문, 터미널 출력 원문을 저장하거나 서버로 전송하는 것.
- 개발자 또는 팀의 생산성을 감시/평가하는 조직용 분석 도구.
- 회사 프로젝트의 민감한 정보를 자동 수집하는 것.
- MMORPG 수준의 복잡한 전투, 경제, 거래 시스템.
- 과금, 랜덤박스, 아이템 거래, 유료 재화 시스템.
- 모바일 앱까지 동시에 완성하는 것.
- Steam 정식 출시 수준의 모든 상점 페이지/업적/결제/커뮤니티 기능을 완성하는 것.

### MVP 성공 기준

- 사용자가 앱을 실행하고 프로젝트 폴더를 연결할 수 있다.
- Git 저장소의 변경 파일 수, 추가/삭제 라인 수, 파일 카테고리를 자동 분석할 수 있다.
- Claude Code / Codex 중 최소 1개 이상의 Provider가 sanitized sample 또는 실제 사용자 지정 로그에서 제한된 자동 분석을 수행할 수 있다.
- 자동 분석 결과가 `AgentWorkSession`으로 정규화된다.
- 사용자가 자동 분석 결과를 확인하고 부족한 항목만 보정할 수 있다.
- 성장 결과가 직관적으로 이해된다.
- SaveData를 삭제하지 않는 한 앱 재실행 후 캐릭터 상태가 유지된다.
- 미니게임을 30초 이상 플레이하고 결과 보너스를 받을 수 있다.
- 저장 파일과 서버 동기화 데이터에 원본 프롬프트/코드/로그가 없다.

---

## 19. 개발 단계별 로드맵

### Phase 0: 기획/문서/샘플 조사

산출물:

- `technical-design.md`
- `game-design-document.md`
- `privacy-policy-draft.md`
- `agent-log-research.md` 초안
- sanitized sample 수집 계획

완료 조건:

- MVP 범위가 명확히 합의된다.
- MVP가 자동 분석 우선 구조임을 문서화한다.
- 수동 입력은 fallback임을 명확히 한다.
- 서버 동기화 가능 데이터와 금지 데이터를 분리한다.
- 개인정보 저장 금지 정책이 확정된다.

### Phase 1: Unity 프로젝트 세팅

산출물:

- `UnityClient/` Unity 프로젝트.
- 기본 Scene.
- `_Project` 폴더 구조.
- 테스트 폴더.
- Git ignore 설정.

완료 조건:

- macOS Editor에서 실행 가능.
- 빈 앱 빌드가 가능.
- EditMode Test 샘플이 통과.

### Phase 2: 도메인 모델/저장 시스템

산출물:

- Domain 모델.
- SaveData 모델.
- JSON 저장/로드 서비스.
- SaveData serialization 테스트.

완료 조건:

- 캐릭터 기본 상태가 저장/로드된다.
- 저장 파일에 금지 필드가 없다.
- saveVersion이 포함된다.

### Phase 3: Git 자동 분석

산출물:

- GitDiffProvider.
- Git 실행 가능 여부 확인.
- `git status --short`, `git diff --stat`, `git diff --numstat`, `git log --oneline -n 20`, `git branch --show-current` 파서.
- GitChangeSummary 생성 로직.
- 파일 카테고리 분류 테스트.

완료 조건:

- 선택한 Git 저장소에서 변경 파일 수와 추가/삭제 라인 수를 얻는다.
- 파일 경로 기반 WorkType 후보를 추론한다.
- Git 실패 케이스가 graceful fallback으로 처리된다.

### Phase 4: Agent Provider 기반 구조

산출물:

- AgentLogProvider 인터페이스.
- MockAgentLogProvider.
- ClaudeCodeLogProvider 초안.
- CodexLogProvider 초안.
- Cursor/Windsurf Provider skeleton.
- Provider fallback 테스트.

완료 조건:

- 모든 Provider가 공통 `AgentWorkSession`만 반환한다.
- MockAgentLogProvider로 deterministic 테스트가 가능하다.
- Claude/Codex sanitized sample을 제한적으로 파싱할 수 있다.
- 파싱 실패 시 앱이 크래시하지 않는다.

### Phase 5: 자동 분석 결과 확인/보정 UI

산출물:

- Auto Analysis Dashboard.
- Session Review / Correction Screen.
- Manual Fallback Input.
- confidence/warning 표시.
- Confirm Growth 플로우.

완료 조건:

- 자동 분석된 세션 후보가 UI에 표시된다.
- 사용자가 부족한 필드만 수정할 수 있다.
- fallback 수동 입력은 자동 분석 실패 상황에서만 보조 진입점으로 제공된다.

### Phase 6: 성장 계산 + 캐릭터 룸

산출물:

- GrowthCalculator.
- 기본 GrowthRule.
- Character Room.
- Growth Result Screen.
- 성장 결과 애니메이션 초안.

완료 조건:

- 자동 분석 결과가 캐릭터 EXP와 스탯에 반영된다.
- WorkType별 스탯 증가 테스트가 통과한다.
- daily cap 기본 정책이 동작한다.
- 세션 히스토리에 결과가 남는다.

### Phase 7: 계정 / 클라우드 / 서버 최소 구현

산출물:

- Guest Mode.
- 최소 로그인.
- 서버 API 계약.
- 캐릭터 저장 데이터 동기화.
- 세션 요약 동기화.
- 서버 저장 금지 데이터 검증.

완료 조건:

- Guest Mode만으로도 앱을 사용할 수 있다.
- 로그인 사용자는 캐릭터 성장 상태와 세션 요약값을 동기화할 수 있다.
- 서버 payload에 원본 로그/프롬프트/코드/터미널 출력/절대 경로/Git remote URL/커밋 diff 원문이 포함되지 않는다.
- 사용자가 클라우드 동기화를 끄고 로컬 전용으로 사용할 수 있다.

### Phase 8: 미니게임

산출물:

- 토큰 수집 미니게임.
- 일시정지.
- MiniGameSession 결과.
- 제한 보너스 적용.

완료 조건:

- 30초~3분 세션이 가능하다.
- 키보드만으로 조작 가능하다.
- 미니게임 보상이 핵심 성장 보상을 압도하지 않는다.

### Phase 9: macOS UX / Steam 배포 준비

산출물:

- 프로젝트 폴더 선택 UX 개선.
- 로그 폴더 권한 UX 개선.
- 항상 위 작은 창 또는 메뉴바 앱 조사.
- Swift helper 가능성 검토.
- macOS standalone build 구조.
- 앱 아이콘과 기본 설정.
- Steamworks 연동 가능성 조사.

완료 조건:

- 사용자가 프로젝트/로그 폴더 연결 상태를 이해할 수 있다.
- 권한 오류가 명확히 표시된다.
- Steam 정식 출시 기능 없이도 배포 가능한 macOS build 산출물이 정리된다.

### Phase 10: QA / 제한 배포

산출물:

- macOS 빌드.
- 아이콘.
- README.
- privacy policy draft.
- QA checklist.
- 릴리스 노트.

완료 조건:

- 새 사용자 환경에서 실행 가능하다.
- 저장/삭제/권한/오류 흐름이 검증된다.
- 포트폴리오 제출 또는 제한 배포가 가능하다.

---

## 20. 리스크와 대응 전략

### AI Agent 로그 포맷 변경

리스크:

- 로그 경로와 schema가 버전별로 바뀔 수 있다.
- 특정 필드가 사라지거나 의미가 바뀔 수 있다.

대응:

- MVP는 Git 자동 분석을 안정적인 1차 데이터 소스로 두고, Claude Code / Codex는 sanitized sample 기반 제한 파싱부터 시작한다.
- Provider 어댑터 구조를 사용한다.
- parser version, schema validation, graceful degradation을 적용한다.
- 성장 시스템은 `AgentWorkSession`만 의존한다.

### 로그 접근 권한 문제

리스크:

- macOS 권한 또는 앱 샌드박스로 로그 폴더를 읽지 못할 수 있다.

대응:

- 사용자가 직접 폴더를 선택하게 한다.
- 권한 오류를 명확히 표시한다.
- 자동 분석 결과 확인/보정 화면과 Manual Fallback Input을 항상 제공한다.
- 향후 Swift helper와 security-scoped bookmarks를 검토한다.

### 민감 정보 노출 위험

리스크:

- 프롬프트, 코드, 터미널 출력, API Key가 로그에 포함될 수 있다.

대응:

- 원본 로그 저장 금지.
- 집계값만 저장.
- Sanitizer 계층 적용.
- debug log에도 원문 출력 금지.
- sanitized fixture만 Git에 포함.

### Unity macOS 앱의 네이티브 UX 한계

리스크:

- 메뉴바, 항상 위 창, 파일 권한 UX가 Unity만으로 어색할 수 있다.

대응:

- MVP는 Unity 단독 앱으로 검증한다.
- Phase 9에서 Swift helper 또는 native shell을 검토한다.
- 핵심 게임 루프와 네이티브 integration을 분리한다.

### 게임 재미 부족

리스크:

- 자동 분석 결과 확인과 성장 결과가 반복적으로 느껴질 수 있다.

대응:

- 성장 결과 화면의 피드백을 명확히 만든다.
- WorkType별 캐릭터 반응과 reward tag를 제공한다.
- 미니게임은 짧고 즉시 이해 가능하게 만든다.
- MVP 테스트 후 밸런싱을 조정한다.

### 성장 밸런스 붕괴

리스크:

- 토큰 수나 파일 수가 큰 세션만 지나치게 유리해질 수 있다.
- 미니게임 반복이 실제 작업보다 효율적일 수 있다.

대응:

- Daily cap과 soft cap 적용.
- Small Token + Success에 Efficiency 보상.
- 미니게임 보상 비중 제한.
- `growth-balancing.md`에 수치 변경 이력 관리.

### 자동화 범위 과다로 MVP 지연

리스크:

- Git 분석, Claude/Codex Provider, 계정, 클라우드, 서버, Steam 준비까지 동시에 진행하면 MVP 일정이 크게 늘어날 수 있다.

대응:

- 자동 분석은 Git Provider를 1순위로 구현한다.
- Claude Code / Codex는 sanitized sample 기반 제한 파싱부터 시작한다.
- Cursor/Windsurf 등은 Provider skeleton 또는 베타 수준으로 둔다.
- 계정/클라우드는 Guest Mode를 유지한 상태에서 최소 동기화만 구현한다.
- Steam은 정식 출시 기능이 아니라 빌드 구조와 배포 준비 수준으로 제한한다.
- 각 기능은 end-to-end 흐름을 막지 않도록 graceful fallback을 제공한다.

### Git 명령 실패

리스크:

- Git 미설치, 저장소 아님, 권한 부족, command timeout.

대응:

- Git 실행 가능 여부 사전 확인.
- 명령별 timeout 적용.
- 실패 시 ManualSessionProvider fallback.
- 오류 메시지는 사용자 행동 중심으로 제공.

### 대용량 로그 성능 문제

리스크:

- 로그 파일이 크거나 세션이 많으면 앱이 멈출 수 있다.

대응:

- 전체 로그를 한 번에 읽지 않는다.
- 최근 파일/최근 세션만 분석한다.
- 백그라운드 파싱과 progress UI를 제공한다.
- 파싱 결과는 집계값만 저장한다.

---

## 21. 향후 확장 아이디어

- 메뉴바 앱.
- 항상 위 작은 창.
- Swift macOS Shell + Unity View.
- Steam 배포.
- iOS companion.
- 위젯.
- 개발 세션 통계.
- 에이전트별 성장 비교.
- 프로젝트별 캐릭터 성장.
- 시즌/업적.
- GitHub 연동.
- CI 결과 연동.
- OpenTelemetry 기반 usage 수집.
- 팀 단위 성장/랭킹.

팀 단위 성장/랭킹은 개인정보 이슈가 크므로 매우 신중히 접근한다. 조직 단위 통계를 만들 경우에도 원본 코드/프롬프트/로그를 전송하지 않는 익명 집계 정책과 명확한 opt-in이 필요하다.

### 메뉴바 앱

Unity 메인 앱과 별개로 작은 상태 표시, 세션 시작/종료, 빠른 입력을 제공할 수 있다. Swift 네이티브 구현이 적합하다.

### 항상 위 작은 창

AI 에이전트 작업 중 캐릭터와 빌드 게이지를 작은 overlay처럼 보여줄 수 있다. macOS window level 제어가 필요하므로 Unity 단독 구현 가능성을 검토해야 한다.

### GitHub / CI 연동

GitHub PR, Actions 결과, CI 실패/성공을 성장 데이터로 사용할 수 있다. 단, 인증과 외부 API 호출이 필요하므로 로컬 우선 MVP 이후 opt-in 기능으로 둔다.

### OpenTelemetry 기반 usage 수집

에이전트 또는 개발 도구가 OpenTelemetry 이벤트를 제공한다면 구조화된 usage 수집이 가능하다. 그러나 표준화 여부와 개인정보 정책을 먼저 검토해야 한다.

---

## 22. 최종 요약

### 왜 이 구조가 안전한가

TokenForge는 성장 시스템, Provider, 저장, 개인정보 보호를 분리한다. 핵심 게임 로직은 원본 로그나 특정 AI 에이전트 포맷을 몰라도 동작한다. 저장 모델은 집계값만 담도록 설계되어 민감 정보가 앱 내부에 남을 위험을 줄인다.

### 왜 MVP를 자동 분석 우선으로 시작하는가

TokenForge의 핵심 가치는 사용자가 개발 작업을 직접 기록하는 것이 아니라, 실제 개발 프로젝트와 AI 코딩 에이전트의 작업 데이터를 가능한 범위에서 자동 수집해 캐릭터 성장으로 변환하는 데 있다. 따라서 MVP는 Git 분석과 우선 지원 Agent Provider를 포함한 자동 분석 우선 구조로 시작한다.

다만 모든 AI 에이전트와 IDE를 완벽히 지원하는 것은 MVP 목표가 아니다. 자동 분석이 실패하거나 일부 정보가 부족할 수 있으므로, 수동 입력은 메인 플로우가 아니라 결과 확인/보정 및 fallback으로 제공한다.

### 왜 AgentLogProvider 어댑터 구조가 필요한가

Git 분석, Claude Code 로그, Codex 로그, Cursor/Windsurf 로그, 수동 보정은 입력 방식과 실패 케이스가 모두 다르다. Provider 어댑터는 이 차이를 흡수하고 공통 `AgentWorkSession`으로 변환한다. 덕분에 성장 시스템은 안정적으로 유지되고, 새로운 에이전트를 추가할 때도 기존 성장 계산을 크게 바꾸지 않아도 된다.

### 왜 privacy-first가 핵심인가

개발 로그에는 코드, 프롬프트, 경로, 토큰, 에러 출력, 회사 정보가 섞일 수 있다. 이 프로젝트의 신뢰는 "재미있는 성장 시스템"만으로 얻을 수 없고, 사용자의 개발 데이터를 안전하게 다루는 구조에서 나온다. 로컬 우선, 원본 저장 금지, 집계값 저장, 사용자 권한 선택은 제품의 기본 전제다.

### 다음 개발자가 가장 먼저 해야 할 일

1. `UnityClient/` Unity 프로젝트를 생성하고 `_Project` 폴더 구조를 만든다.
2. `AgentWorkSession`, `GitChangeSummary`, `AgentActionSummary`, `CharacterProfile`, `CharacterStats`, `SaveData` 도메인 모델부터 구현한다.
3. JSON 저장/로드와 SaveData serialization 테스트를 작성한다.
4. `AgentLogProvider` 인터페이스와 `MockAgentLogProvider`를 먼저 구현한다.
5. `GitDiffProvider`를 구현해 실제 프로젝트 폴더의 Git 변경량을 자동 분석한다.
6. Auto Analysis Dashboard와 Session Review / Correction Screen을 구현한다.
7. GrowthCalculator를 연결해 자동 분석 결과가 캐릭터 성장으로 이어지게 한다.
8. Claude Code / Codex Provider는 sanitized sample 조사 결과를 기반으로 제한 파싱부터 구현한다.
9. 수동 입력 화면은 자동 분석 실패 또는 부족한 데이터 보정 fallback으로 제공한다.
10. 계정/클라우드/서버는 원본 로그를 절대 저장하지 않는 API 계약을 먼저 정의한 뒤 최소 동기화부터 구현한다.
