# 기획 검토 (plan_review)

형식은 `visha-common:design-work` 2절을 따른다. 노션 `VishaSTD / VishaFace_IOS`는 사용자가 쓰고 Claude는 읽기만 한다.

## 노션에 적을 것
사용자가 노션에 옮기면 지운다. 옮긴 뒤 Claude가 명세(`docs/tech_spec/01_face_app.md`)와 대조해, 일치하는 항목은 명세 근거를 노션 태그로 바꾼다.

- 노션 `VishaFace_IOS` 전반 갱신 (TrueDepth 문구 포함, 나중에). 명세 01의 `확정` 항목 중 노션에 없는 것 전부

## 결정됨
대화로 정한 세부 결정. 지금까지의 결정은 명세 01 각 항목의 근거 열(사용자 답변·결정 날짜)에 있다. 이후 결정만 여기에 날짜와 함께 적는다.

## 보류
- **qa 사용자 시나리오 로그 위치·수집 방법** (2026-11-26): 실기기(iPhone) 앱 안에서 쓴 로그 파일을 Windows의 qa가 읽을 경로가 없다. 정하려면: 기기 파일 위치(`Application.persistentDataPath` 등), Mac에서 꺼내는 방법(Xcode 컨테이너 다운로드 등), Windows로 옮기는 방법. 정해지기 전까지 실기기 시나리오는 화면 관찰 결과를 사용자가 보고하는 방식으로 판정한다(`qa-test` 4절).
