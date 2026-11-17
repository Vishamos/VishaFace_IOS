# 작업 목록

> **현재 진행 (2026-11-17):** Unity 6.3은 Xcode 15에서 컴파일 실패 확인 → Unity 6.0 LTS로 프로젝트 재구성.

## 운영 방식
- **해야 할 작업만 적는다.** 끝난 항목은 지운다.
- 사용자가 항목을 고르면 메인 세션이 수정 방향을 제안하고, 승인된 항목만 반영한다.
- 항목 형식: `- [ ] <번호> <내용> — 사양: §<절>`

## 사용자 조치 대기
- [ ] `iPhone_Capture_준비사항.md` 남은 빈칸 채우기 (보낼 데이터, FPS — §7 Q3과 같음)
- [ ] `docs/spec.md` §7 질문 Q1~Q5 답
- [ ] (VishaSynapse) plan_review Q3 등록 형식·바이트 순서 결정 → 송신 단계의 전제

## A. 1단계: 추적 + 수치 표시 (송신 없음)
- [ ] A0 Unity 6.0 LTS로 프로젝트 재구성: Mac·Windows에 6000.0.x + iOS Build Support 설치, 새 프로젝트에 AR Foundation·ARKit XR Plugin 6.0.x와 기존 설정(Player Settings, XR, 씬) 다시 적용 — 사양: §2.2a
- [ ] A1 6.0으로 iOS 빌드 → Xcode에서 User Script Sandboxing 끄기·Team 지정 → 폰에 설치·실행, AR Default Face 표시 확인. 결과로 Q7 확정 — 사양: §2.1c, §2.2b, Q7
- [ ] A1a 자동 잠금 끔(`isIdleTimerDisabled`) — 사양: §4.3
- [ ] A2 ARKit 얼굴 추적, 블렌드셰이프·머리 회전 수치 화면 표시 (영상 표시 없음) — 사양: §3, §4.1
- [ ] A3 FPS·추적 여부 표시 — 사양: §4.2

## B. 2단계: 송신 (Synapse Q3 이후)
- [ ] B1 설정 화면: 서버 IP·포트 입력·저장 — 사양: §4.5
- [ ] B2 WebSocket 연결·등록·자동 재접속 — 사양: §4.6, §5.1, §5.7
- [ ] B3 바이너리 패킷 직렬화 (트래킹 코드와 분리) — 사양: §5.3~5.5, §5.9
