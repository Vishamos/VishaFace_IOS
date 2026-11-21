# build_ios.sh 개인 설정 예시.
# 이 파일을 같은 폴더에 build_ios.local.sh로 복사한 뒤 값을 채운다.
#   cp tools/build_ios.local.example.sh tools/build_ios.local.sh
# build_ios.local.sh는 .gitignore로 제외된다. 공개 저장소이므로 이 예시 파일에는 실제 값을 넣지 않는다.

# Apple 개발 팀 ID (영문 대문자·숫자 10자리, 필수). 팀 이름("OOO (Personal Team)")이 아니다.
# 인증서 이름 "Apple Development: 이름 (XXXXXXXXXX)"의 괄호 안 값도 Team ID가 아니다(틀리면 "No Account for Team" 에러).
# Xcode 화면(Signing, Build Settings)에는 팀 이름만 보이므로 아래 중 하나로 확인한다.
#   ① (권장) Xcode로 Builds/iOS/Unity-iPhone.xcodeproj를 열어 Signing & Capabilities에서 Personal Team 선택 → 닫은 뒤
#      grep -m1 DEVELOPMENT_TEAM Builds/iOS/Unity-iPhone.xcodeproj/project.pbxproj
#   ② 키체인 접근 → "Apple Development: ..." 인증서 → 조직 단위(OU) 값
TEAM_ID=""

# 설치할 기기의 UDID 또는 기기 이름 (비우면 설치 단계를 건너뜀)
# 확인: 폰을 연결하고 터미널에서 xcrun devicectl list devices
DEVICE_ID=""

# (선택) Unity 실행 파일 경로. 비우면 Unity Hub 기본 경로 + ProjectVersion.txt의 버전을 쓴다.
# 예: /Applications/Unity/Hub/Editor/6000.0.85f2/Unity.app/Contents/MacOS/Unity
UNITY_PATH=""

# (선택) Release 또는 Debug
CONFIGURATION="Release"
