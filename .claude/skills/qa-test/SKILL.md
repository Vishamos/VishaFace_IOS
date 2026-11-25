---
name: qa-test
description: VishaFaceI의 qa 테스트 도구·위치·실행 명령. qa 에이전트가 명세·프로토콜 기준 자동 테스트를 쓰고 실행할 때, 실기기 사용자 시나리오를 판정할 때 사용한다 (Unity Test Framework EditMode, Assets/Tests/ 폴더와 asmdef, 외부 통신 대체, 시나리오 판정 방식).
---

# qa 테스트

## 1. 도구와 위치
| 항목 | 값 |
|---|---|
| 프레임워크 | Unity Test Framework(NUnit), **EditMode**. 새 패키지 없음 |
| 테스트 위치 | `Assets/Tests/EditMode/<대상>Tests.cs` (커밋 대상, `.meta` 포함) |
| 어셈블리 | `Assets/Tests/EditMode/VishaFace.Protocol.Tests.asmdef` (참조: `VishaFace.Protocol`) |
| 실행 | 아래 명령 (Unity 경로·에디터 닫기 조건은 `simple-test` 0절) |
| 결과 | `.temp/editmode-results.xml` (git 제외) |
| 시나리오 문서 | `docs/qa/<묶음 이름>.md` |
| 시나리오 로그 | **없음(보류)**. 4절 |

```bash
"/c/Program Files/Unity/Hub/Editor/6000.0.85f2/Editor/Unity.exe" -batchmode -nographics -projectPath . -buildTarget iOS -runTests -testPlatform EditMode -testResults .temp/editmode-results.xml -logFile .temp/editmode.log
```
- 특정 테스트만: `-testFilter <클래스 이름>` 추가.
- 판정은 `.temp/editmode-results.xml`의 `total`·`passed`·`failed`와 실패한 `<test-case>`의 `message`로 한다.

## 2. 테스트할 수 있는 것과 없는 것
| 대상 | 자동 테스트 | 방법 |
|---|---|---|
| `Assets/Scripts/Protocol/` (패킷 배치, 블렌드셰이프 순서, 경계값) | 가능 | 기존 asmdef에 추가. 기대값은 `docs/protocol/README.md`에서 가져온다 |
| `Socket`(접속·등록·재접속·포기) | 아직 안 됨 | `Assembly-CSharp`에 있어 테스트 asmdef가 참조할 수 없다. 테스트하려면 `Network`를 asmdef로 나누는 구조 변경이 필요하다 → "구조 변경 제안"으로 보고만 한다 |
| `FaceTracker`, ARKit, 카메라 | 불가 | 에디터에서 얼굴 추적이 실행되지 않는다. 사용자 시나리오로 |
| UI(`ServerSettingsPanel`, `DebugHud`) | 불가(이 단계) | 사용자 시나리오로 |

- 새 asmdef·참조 추가는 구조 변경이므로 qa가 직접 하지 않고 보고한다.
- 실제 중계 서버에 접속하지 않는다. 연결 테스트가 가능해지면 테스트 안에서 `127.0.0.1`·임의 포트의 가짜 서버(`HttpListener` 기반 WebSocket 등)를 띄운다.
- 경계값: 블렌드셰이프 0∼1 범위 밖 값, 회전 q/-q, `seq` 최대값 넘김, 잘못된 길이의 입력. 명세·프로토콜에 처리 방법이 없으면 테스트하지 않고 "명세 보완 필요"로 보고한다(worklist B3의 보완 후보 참고).
- 테스트 이름과 주석에 어느 프로토콜 절을 확인하는지 적는다.

## 3. 프로젝트 사이 확인
- 서버 경유 송신(250바이트, `seq` 증가)과 Vishamos 수신은 Synapse 서버·Vishamos를 함께 띄워야 하므로 "프로젝트 사이 확인 순서표"로 만든다. 서버 접속은 사용자 승인 후.

## 4. 사용자 시나리오 (실기기)
- 앱 안에서 시나리오 로그 파일을 쓰는 코드가 **없고**, 실기기 로그를 Windows로 가져올 경로도 정해지지 않았다(`docs/work/plan_review.md` 보류).
- 정해지기 전까지: 시나리오의 "기대 로그" 대신 **"기대 화면"**(HUD의 상태 문자열·FPS·Tracking/Lost, 설정 화면의 연결 상태·에러 코드)과 시간 조건을 적고, 사용자가 본 결과를 메인 스레드를 통해 받아 판정한다.
- 로그 파일 방식이 필요하면 `[Proposal] Add log points for <시나리오>` 이슈를 올리고 보류 항목도 함께 언급한다.
- 사용자는 실기기 확인 전에 Mac에서 `tools/Build_iOS.command`로 최신 커밋을 빌드·설치한다(push 필요).
