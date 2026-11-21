#!/usr/bin/env bash
# Finder에서 더블클릭하면 터미널 창에서 tools/build_ios.sh를 실행한다(Mac 전용).
# 끝나면 결과를 보여 주고 Enter를 누를 때까지 창을 유지한다.
# 파일 이름을 영문으로 둔 이유: 한글 파일명은 Windows(NFC)와 Mac(NFD)의 유니코드 표현이 달라 git에서 다른 파일로 보일 수 있다.

cd "$(dirname "$0")/.." || exit 1

bash tools/build_ios.sh "$@"
status=$?

echo
if [[ $status -eq 0 ]]; then
  echo "==== 빌드 성공"
else
  echo "==== 빌드 실패 (종료 코드 $status). 위 로그를 확인하세요."
fi
read -r -p "Enter를 누르면 창을 닫습니다..." _
exit $status
