# build_ios.sh 개인 설정 예시.
# 이 파일을 같은 폴더에 build_ios.local.sh로 복사한 뒤 값을 채운다.
#   cp tools/build_ios.local.example.sh tools/build_ios.local.sh
# build_ios.local.sh는 .gitignore로 제외된다. 공개 저장소이므로 이 예시 파일에는 실제 값을 넣지 않는다.

# Apple 개발 팀 ID (영문·숫자 10자리, 필수)
# 확인: Xcode에서 이전 빌드 프로젝트 열기 → Unity-iPhone 타깃 → Build Settings → 검색 "DEVELOPMENT_TEAM"
TEAM_ID=""

# 설치할 기기의 UDID 또는 기기 이름 (비우면 설치 단계를 건너뜀)
# 확인: 폰을 연결하고 터미널에서 xcrun devicectl list devices
DEVICE_ID=""

# (선택) Unity 실행 파일 경로. 비우면 Unity Hub 기본 경로 + ProjectVersion.txt의 버전을 쓴다.
# 예: /Applications/Unity/Hub/Editor/6000.0.85f2/Unity.app/Contents/MacOS/Unity
UNITY_PATH=""

# (선택) Release 또는 Debug
CONFIGURATION="Release"
