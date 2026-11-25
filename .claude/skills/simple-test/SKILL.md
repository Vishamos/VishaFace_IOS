---
name: simple-test
description: VishaFaceI 최소 실행 테스트. developer(또는 코드를 직접 고친 메인 스레드)가 커밋 전에 에러가 없는지 확인할 때 사용한다. Unity 배치 모드 컴파일(iOS 대상), 기존 EditMode 테스트 실행, 실기기 확인 목록 작성을 다룬다.
---

# 최소 실행 테스트

컴파일 에러 없이 돌아가는지만 본다. 명세 기준 테스트(`Assets/Tests/`)는 qa가 쓴다(`qa-test`). 얼굴 추적·카메라·iOS 네트워크 권한은 Windows에서 확인할 수 없으므로 "실기기 확인 필요"로 넘긴다.

## 0. 준비
- Unity 경로: `/c/Program Files/Unity/Hub/Editor/<버전>/Editor/Unity.exe`. `<버전>`은 `ProjectSettings/ProjectVersion.txt`의 `m_EditorVersion`(2026-11-26 기준 `6000.0.85f2`). 이 버전이 설치되어 있지 않으면 중단하고 보고한다(다른 버전으로 열지 않는다. 6.3으로 열면 프로젝트가 변환된다).
- **Unity 에디터가 이 프로젝트를 열고 있으면 배치 모드가 실패한다**(로그에 `another Unity instance is running`). 에디터를 강제로 끄지 않는다. 중단하고 "에디터를 닫아 달라"고 메인 스레드에 보고한다.
- 결과 파일은 `.temp/`(git 제외)에 쓴다. 명령은 **한 줄에 하나씩** 실행한다. 시간이 걸리므로 Bash `timeout`을 600000으로 준다.

## 1. 컴파일 (바뀐 C# 파일이 있으면 항상)
iOS 대상으로 컴파일한다(`#if UNITY_IOS` 코드 포함). 빌드 대상이 바뀌어 있으면 첫 실행에 재임포트로 몇 분 걸릴 수 있다.
```bash
"/c/Program Files/Unity/Hub/Editor/6000.0.85f2/Editor/Unity.exe" -batchmode -nographics -quit -projectPath . -buildTarget iOS -logFile .temp/compile.log
```
- 통과: 종료 코드 0이고 `.temp/compile.log`에 `error CS`가 없다.
- 실패: `grep -n "error CS" .temp/compile.log`로 파일:줄과 에러 코드만 확인해 고친다. 로그 전체를 보고에 붙이지 않는다.
- 경고(`warning CS`)는 이번에 바꾼 파일에서 새로 생긴 것만 보고에 적는다.

## 2. 기존 EditMode 테스트 (Protocol·패킷 관련 변경일 때)
`Assets/Scripts/Protocol/`이나 `FacePacketSender`를 고쳤으면 qa가 만든 기존 테스트가 그대로 통과하는지 본다. 테스트 파일은 고치지 않는다.
```bash
"/c/Program Files/Unity/Hub/Editor/6000.0.85f2/Editor/Unity.exe" -batchmode -nographics -projectPath . -buildTarget iOS -runTests -testPlatform EditMode -testResults .temp/editmode-results.xml -logFile .temp/editmode.log
```
- `-runTests`에는 `-quit`를 붙이지 않는다(테스트가 끝나면 스스로 종료).
- 통과: 종료 코드 0. 결과는 `.temp/editmode-results.xml`의 `<test-run ... total= passed= failed=>` 한 줄로 확인한다.
- 실패한 테스트가 이번 변경 때문이면 코드를 고치고, 테스트가 명세와 어긋나 보이면 고치지 말고 보고한다.

## 3. 실기기 확인 목록 (ARKit·네트워크·UI 변경일 때)
Windows에서 실행할 수 없는 동작은 확인하지 않고, 사용자가 Mac 빌드(`tools/Build_iOS.command`) 후 확인할 목록을 보고에 적는다.
- 형식: `① <조작> → <기대 화면·상태>` (예: `① 서버를 끈 채 Connect → 바로 Connecting으로 바뀜`)
- 실제 중계 서버 접속이 필요한 확인은 "접속 승인 필요"를 붙인다.

## 4. 정리와 보고
- `.temp/`의 로그·결과 파일은 지우지 않아도 된다(git 제외, 다음 실행이 덮어씀).
- 보고: 1∼3 각각 통과/실패/생략과 이유. "확인한 것(Windows 컴파일·테스트)"과 "사용자가 확인할 것(실기기)"을 나눈다.
