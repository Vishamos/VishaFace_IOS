#!/usr/bin/env bash
# VishaFaceI iOS 빌드 자동화 (Mac 전용)
# 순서: ① git pull → ② Unity로 Xcode 프로젝트 생성 → ③ xcodebuild 빌드·서명 → ④ 연결된 폰에 설치·실행
# 사용법: bash tools/build_ios.sh [--no-pull] [--no-install]
#   --no-pull     pull 없이 현재 상태로 빌드
#   --no-install  빌드까지만 하고 폰 설치는 건너뜀
# 개인 설정(Team ID, 기기)은 tools/build_ios.local.sh에 둔다(git 제외). 예시: tools/build_ios.local.example.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$PROJECT_DIR"

DO_PULL=1
DO_INSTALL=1
for arg in "$@"; do
  case "$arg" in
    --no-pull) DO_PULL=0 ;;
    --no-install) DO_INSTALL=0 ;;
    -h|--help) sed -n '2,7p' "$0"; exit 0 ;;
    *) echo "알 수 없는 옵션: $arg (도움말: --help)"; exit 2 ;;
  esac
done

# ---- 개인 설정 ----
TEAM_ID=""
DEVICE_ID=""
UNITY_PATH=""
CONFIGURATION="Release"
LOCAL_CONF="$SCRIPT_DIR/build_ios.local.sh"
if [[ -f "$LOCAL_CONF" ]]; then
  # shellcheck source=/dev/null
  source "$LOCAL_CONF"
fi

OUTPUT_DIR="$PROJECT_DIR/Builds/iOS"
DERIVED_DIR="$PROJECT_DIR/Builds/DerivedData"
LOG_DIR="$PROJECT_DIR/Builds/Logs"
UNITY_LOG="$LOG_DIR/unity_build.log"
XCODE_LOG="$LOG_DIR/xcodebuild.log"

step() { echo; echo "==== [$1/4] $2"; }
fail() {
  echo
  echo "!!!! 실패: $1"
  if [[ -n "${2:-}" && -f "$2" ]]; then
    echo "---- 로그 마지막 40줄 ($2) ----"
    tail -n 40 "$2"
    echo "---- 전체 로그: $2"
  fi
  exit 1
}

# ---- 사전 확인 ----
[[ "$(uname)" == "Darwin" ]] || fail "Mac에서만 실행할 수 있습니다."
command -v xcodebuild >/dev/null || fail "xcodebuild가 없습니다. Xcode를 설치하세요."
if [[ -z "$TEAM_ID" ]]; then
  fail "TEAM_ID가 비어 있습니다. tools/build_ios.local.example.sh를 build_ios.local.sh로 복사해 채우세요."
fi
if [[ -z "$UNITY_PATH" ]]; then
  UNITY_VERSION="$(sed -n 's/^m_EditorVersion: //p' ProjectSettings/ProjectVersion.txt | tr -d '\r')"
  UNITY_PATH="/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity"
fi
[[ -x "$UNITY_PATH" ]] || fail "Unity 실행 파일이 없습니다: $UNITY_PATH (Unity Hub 설치 버전 확인, 또는 build_ios.local.sh의 UNITY_PATH 지정)"
mkdir -p "$LOG_DIR"
START_TIME=$SECONDS

# ---- ① git pull ----
if (( DO_PULL )); then
  step 1 "git pull"
  # 추적 중인 파일에 Mac 쪽 변경이 있으면 pull이 꼬일 수 있으므로 멈춘다(새 파일은 무시).
  if [[ -n "$(git status --porcelain --untracked-files=no)" ]]; then
    git status --short --untracked-files=no
    fail "Mac 작업 폴더에 커밋되지 않은 변경이 있습니다. 정리한 뒤 다시 실행하거나 --no-pull로 빌드하세요."
  fi
  git pull --ff-only || fail "git pull 실패 (fast-forward 불가). GitHub Desktop에서 상태를 확인하세요."
else
  step 1 "git pull 건너뜀 (--no-pull)"
fi
echo "커밋: $(git log --oneline -1)"

# ---- ② Unity → Xcode 프로젝트 ----
step 2 "Unity 배치 빌드 (Unity 에디터에서 이 프로젝트를 닫아 두세요)"
echo "진행 상황: tail -f \"$UNITY_LOG\""
if ! "$UNITY_PATH" -batchmode -quit -nographics \
    -projectPath "$PROJECT_DIR" \
    -buildTarget iOS \
    -executeMethod VishaFaceI.EditorTools.IosBuildScript.BuildFromCommandLine \
    -outputPath "$OUTPUT_DIR" \
    -logFile "$UNITY_LOG"; then
  fail "Unity 빌드" "$UNITY_LOG"
fi

# ---- ③ xcodebuild ----
step 3 "xcodebuild ($CONFIGURATION)"
echo "진행 상황: tail -f \"$XCODE_LOG\""
if ! xcodebuild \
    -project "$OUTPUT_DIR/Unity-iPhone.xcodeproj" \
    -scheme Unity-iPhone \
    -configuration "$CONFIGURATION" \
    -destination "generic/platform=iOS" \
    -derivedDataPath "$DERIVED_DIR" \
    -allowProvisioningUpdates \
    DEVELOPMENT_TEAM="$TEAM_ID" \
    CODE_SIGN_STYLE=Automatic \
    build > "$XCODE_LOG" 2>&1; then
  echo "---- 에러 줄 ----"
  grep -E "error:|Sandbox:" "$XCODE_LOG" | head -n 20 || true
  fail "xcodebuild" "$XCODE_LOG"
fi

APP_PATH="$(find "$DERIVED_DIR/Build/Products/${CONFIGURATION}-iphoneos" -maxdepth 1 -name '*.app' -print -quit 2>/dev/null || true)"
[[ -n "$APP_PATH" ]] || fail "빌드된 .app을 찾지 못했습니다." "$XCODE_LOG"
echo "앱: $APP_PATH"

# ---- ④ 폰에 설치·실행 ----
if (( DO_INSTALL )); then
  step 4 "폰에 설치·실행"
  if [[ -z "$DEVICE_ID" ]]; then
    echo "DEVICE_ID가 비어 있어 설치를 건너뜁니다. 'xcrun devicectl list devices'로 확인해 build_ios.local.sh에 넣으세요."
  else
    xcrun devicectl device install app --device "$DEVICE_ID" "$APP_PATH" \
      || fail "설치 실패. 폰 연결·잠금 해제·신뢰 여부를 확인하세요."
    BUNDLE_ID="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$APP_PATH/Info.plist")"
    xcrun devicectl device process launch --device "$DEVICE_ID" "$BUNDLE_ID" \
      || echo "자동 실행 실패(폰이 잠겨 있을 수 있음). 설치는 됐으니 폰에서 직접 실행하세요."
  fi
else
  step 4 "설치 건너뜀 (--no-install)"
fi

ELAPSED=$(( SECONDS - START_TIME ))
echo
echo "==== 완료: $(( ELAPSED / 60 ))분 $(( ELAPSED % 60 ))초"
