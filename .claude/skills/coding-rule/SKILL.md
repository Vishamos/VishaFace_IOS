---
name: coding-rule
description: VishaFaceI(Unity 6.0 LTS, C#, iOS ARKit) 코드 작성 표준. C# 코드를 작성·수정·리뷰할 때 visha-common:coding-common-rule과 함께 적용한다 (버전 고정, 계층 분리, 메인 스레드·async 규칙, iOS 네트워크 주의점, 서버 주소·비밀 값, 로그, 카메라 영상 금지, 의존성 점검).
---

# VishaFaceI C# 코딩 표준

공통 원칙(가정 드러내기, 단순하게, 필요한 곳만 고치기)은 `visha-common:coding-common-rule`을 따른다. 이 스킬은 Unity·iOS와 이 프로젝트에만 해당하는 규칙이다. 동작의 기준은 명세 `docs/tech_spec/01_face_app.md`와 프로토콜 `docs/protocol/README.md`다.

## 버전과 의존성
- Unity **6000.0.x(6.0 LTS)**, AR Foundation·Apple ARKit XR Plugin **6.0.x**. Xcode 15.2(Mac Ventura 상한)로 빌드되어야 하므로 Xcode 16이 필요한 Unity·패키지 버전으로 올리지 않는다(명세 01 §2.2a).
- 내장 기능을 우선한다: `System.Net.WebSockets.ClientWebSocket`, `System.Buffers.Binary`, `PlayerPrefs`, uGUI.
- `Packages/manifest.json` 변경(패키지 추가·버전 변경)은 사용자 승인 후. 에셋 스토어·외부 네이티브 플러그인도 같다.
- **의존성 점검 명령** (security가 사용): 없음. npm 같은 취약점 조회 도구가 없으므로 security는 "의존성 점검 명령 없음"으로 보고하고, `Packages/manifest.json`·`packages-lock.json`의 변경(새 패키지, git URL·로컬 경로 패키지)만 확인한다.

## 계층 분리 (CLAUDE.md "트래킹 코드와 송신 코드를 분리")
| 폴더 | 맡는 것 | 참조하면 안 되는 것 |
|---|---|---|
| `Assets/Scripts/Face/` | ARKit → `FaceFrame` | 네트워크(`Socket`, `System.Net.*`) |
| `Assets/Scripts/Network/` | 접속·등록·송신(`byte[]`만 받음) | ARKit·AR Foundation, `FaceFrame` |
| `Assets/Scripts/Protocol/` (asmdef `VishaFace.Protocol`) | `FaceFrame` → 바이트 배치 | ARKit, 네트워크, `MonoBehaviour` |
| `Assets/Scripts/App/` | 둘을 잇는 연결(`FacePacketSender`), 앱 설정 | — |
| `Assets/Scripts/UI/` | 설정 화면·디버그 표시 | 카메라 영상 |
- 프로토콜 값(시그니처 22, 종류 번호, 오프셋, 크기)은 상수로 두고 숫자를 흩어 쓰지 않는다. 바이트 배치를 바꾸면 프로토콜 문서의 `version`을 올린다(CLAUDE.md).
- 메시지 헤더·등록 절차는 VishaSynapse `docs/protocol/README.md`가 기준이다. 이 프로젝트에서 따로 정하지 않는다.

## Unity 메인 스레드와 async
- Unity API(`GameObject`, `Transform`, UI, `PlayerPrefs`, `Debug` 제외 대부분)는 메인 스레드에서만 부른다. `async` 메서드는 `await` 뒤에 다른 스레드에서 이어질 수 있다(`ConfigureAwait(false)`나 `Task.Run` 안). 그 안에서는 값만 바꾸고 화면 반영은 `Update`에서 한다.
- 스레드 사이에 넘기는 값은 `Interlocked`·`volatile`·`lock` 중 하나로 다룬다(C#과 같다. 기존 `Socket`의 최신값 한 칸 방식 참고).
- `async void`는 Unity 이벤트 진입점에만 쓰고, 안에서 예외를 잡는다. 잡지 않은 예외는 조용히 사라진다.
- `OnDestroy`·`OnApplicationQuit`에서 연결·`CancellationTokenSource`·코루틴을 정리한다. `OnApplicationPause(true)`(앱 백그라운드)도 끊김으로 다룬다.

## iOS 네트워크 주의점
- iOS에서 `ClientWebSocket.ConnectAsync`·`ReceiveAsync`가 취소 토큰과 `Abort()`를 무시하고 멈출 수 있다(worklist B2 버그). 외부 작업을 직접 `await`하지 말고 시간 제한·포기 신호와 `Task.WhenAny`로 경쟁시킨다. 버려진 작업이 늦게 끝나도 상태를 바꾸지 못하게 세대 번호를 확인한다.
- 로컬 네트워크 권한 문구는 `Assets/Editor/IosInfoPlistPostprocess.cs`가 빌드 후 넣는다. `Info.plist` 키를 추가할 때도 이 파일에서 한다.
- 수신한 메시지는 길이를 확인한 뒤 읽는다. 64KB(서버 한도)를 넘는 메시지는 버린다.

## 서버 주소·비밀 값
- 서버 IP·포트를 코드·문서 예시에 실제 값으로 쓰지 않는다. 저장은 `PlayerPrefs`(iOS에서 `UserDefaults`), 키는 `Socket.HostKey`/`Socket.PortKey`.
- 비밀 값이 생기면 Keychain에 저장하고 코드·`Info.plist`·xcconfig에 넣지 않는다(CLAUDE.md "비밀 값 취급"). Keychain 접근에 네이티브 플러그인이 필요하면 의존성 추가로 보고 승인을 받는다.

## 로그
- `Debug.Log*`에 `[클래스명]` 접두어를 붙인다(기존 관례).
- 상태 변화·에러 코드·예외 **타입 이름**만 남긴다. 서버 주소, 패킷 내용, 수신 JSON 전체, 예외 객체 전체(`e.ToString()`, `e.Message`에 주소가 들어갈 수 있음)는 출력하지 않는다.
- 매 프레임 로그를 남기지 않는다(60Hz).

## 방송 안전
- 카메라 영상을 화면에 표시·저장·전송하는 코드(`ARCameraBackground`, `TryAcquireLatestCpuImage`, 텍스처 캡처)는 사용자 승인 전에 넣지 않는다(CLAUDE.md 안전 규칙 3).
- 화면 기본은 검은 배경 + 수치(`DebugHud`).

## 스타일
- 네임스페이스 없음, 4칸 들여쓰기, 줄바꿈 LF, 파일 이름 = 클래스 이름(PascalCase). private 필드는 camelCase, 상수는 PascalCase.
- 새 스크립트·폴더를 만들면 Unity가 만드는 `.meta`도 함께 커밋한다. `.meta`를 손으로 만들지 않는다(에디터나 배치 모드 실행으로 생성).
- 씬·프리팹(`.unity`, `.prefab`)은 YAML이라도 손으로 고치지 않는다. 필요한 오브젝트는 코드로 만들거나(기존 UI 방식) 사용자에게 에디터 작업을 요청한다.
- 기존 파일을 지우고 새로 만들지 않는다. 편집으로 수정한다.
- 주석은 한국어로, 명세 절(§)과 "왜"가 코드만으로 드러나지 않을 때 단다.
- ARKit·카메라·실기기 동작에 관한 코드는 보고에 "실기기 확인 필요"를 적는다(에디터에서 얼굴 추적이 실행되지 않음).
