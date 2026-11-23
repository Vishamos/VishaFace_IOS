# 작업 목록

> **현재 진행 (2026-11-21):** Mac 빌드 자동화(`tools/build_ios.sh`)로 pull∼유선 설치 확인. 다음은 A2(수치 표시)·A3(FPS).

## 운영 방식
- **해야 할 작업만 적는다.** 끝난 항목은 지운다.
- 사용자가 항목을 고르면 메인 세션이 수정 방향을 제안하고, 승인된 항목만 반영한다.
- 항목 형식: `- [ ] <번호> <내용> — 사양: §<절>`

## 사용자 조치 대기
- [ ] 노션 `VishaFace_IOS` 갱신 (TrueDepth 문구 포함, 나중에). 작성 후 Claude가 spec과 대조해 일치 항목은 spec에서 삭제
- [ ] (VishaSynapse) 프로토콜 §8 용량 표 Face iOS 줄을 `[11, 22, 0x16, …]` 250바이트 × 60회로 갱신(2026-11-22 확인: 아직 88바이트 × 10회, 변경 표의 예 `[11, 22, 15, …]`도 함께) — 그쪽 저장소에서 — 사양: §5.8
- [ ] (Vishamos) `docs/protocol/README.md` v1 기준으로 수신부 제작 — 그쪽 저장소에서

## A. 1단계: 추적 + 수치 표시 (송신 없음)
- [ ] A1c (선택) 무선 설치 재시도. 계속 `RSD` 에러면 유선 전용으로 확정 — 사양: §2.7
- [ ] A1d `Build_iOS.command` 더블클릭 실행 확인 (pull 후 실행 권한 유지 포함) — 사양: §2.7
- [ ] A2 실기기 확인: 혀·시선·방송 조명 품질, 고개를 돌렸을 때 추적이 끊기는 각도. 머리 회전·위치의 축 방향·부호·정면 기준값을 확인해 프로토콜 §5a에 기록 — 사양: §3, §3.3b, §4.1
  - 구현됨(2026-11-23): `FaceTracker`(ARKit → `FaceFrame`, 카메라 기준 머리 자세, 끊기면 마지막 값 유지), `DebugHud`(검은 배경, 머리 자세 3줄 + 블렌드셰이프 52개, 0.1초마다 갱신). 화면 글자는 영어(내장 폰트 한글 표시 미확인)
- [ ] A3 `targetFrameRate = 60` 장시간 실측(발열 포함), 안 되면 30 — 사양: §3.4, §4.2, §4.4
  - 구현됨(2026-11-23): `AppBootstrap`에서 60 설정, `DebugHud` 상단에 FPS(0.5초 평균)·추적 여부(Tracking/Lost) 표시

## B. 2단계: 송신
- [ ] B1 설정 화면: 서버 IP·포트 입력·저장 — 사양: §4.5
  - 구현됨(2026-11-23): `Assets/Scripts/UI/ServerSettingsPanel.cs`. 화면 맨 위에 IP·포트 입력과 Connect 버튼, 그 아래 연결 상태·에러 코드. UI는 코드로 생성(씬 배치 없음). 입력 확인 → 저장 → `Socket.Reconnect()`
  - 남은 것: 실기기에서 키보드 입력·저장 유지 확인
- [ ] B2 WebSocket 송신 계층 — 사양: §4.6, §5.7∼5.7b, §5.8a
  - 구현됨(2026-11-23): `Assets/Scripts/Network/Socket.cs`. 접속·등록(from 22)·`client.registered` 후 송신·최신값 한 칸 송신 루프·수신 루프 상시·에러/종료 코드 보관·3초 재접속·앱 복귀 시 즉시 재접속·64KB 초과 메시지 버림. 주소는 `PlayerPrefs` 키 `Synapse.Host`/`Synapse.Port`(기본 4000). Windows 컴파일만 확인
  - 로컬 네트워크 권한 문구: `Assets/Editor/IosInfoPlistPostprocess.cs`가 빌드 후 `Info.plist`에 넣음(2026-11-23). Mac 빌드 후 `Builds/iOS/Info.plist`에 키가 있는지, 첫 접속 때 권한 창이 뜨는지 확인
  - 남은 것: (씬 배치는 `AppBootstrap`이 코드로 함) iOS 실기기에서 `ClientWebSocket` 동작·서버 접속 확인(접속은 승인 후)
- [ ] B3 실기기에서 서버 경유 송신 확인 (Vishamos 수신부 또는 Synapse 로그로 250바이트·seq 증가 확인, 접속은 승인 후) — 사양: §5.3∼5.5, 프로토콜 v1
  - 구현됨(2026-11-23): `FacePacket`(직렬화), `FacePacketSender`(트래킹 → 소켓 연결). EditMode 테스트 `Assets/Tests/EditMode/FacePacketTests.cs` 20개 통과(구현을 보지 않고 프로토콜 문서만으로 작성)
  - 프로토콜 문서 보완 후보(테스트 작성 중 발견): 블렌드셰이프 0∼1 범위 밖 값 처리(현재 그대로 보냄), 회전 정규화·부호(q/-q) 책임(현재 ARKit 값 그대로)
