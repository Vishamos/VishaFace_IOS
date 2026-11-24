# VishaFaceI 얼굴 데이터 프로토콜

Face iOS 앱(시그니처 22)이 VishaSynapse를 거쳐 Vishamos(11)로 보내는 얼굴 데이터의 형식이다. **받는 쪽(Vishamos)은 이 문서를 보고 수신부를 만든다.**

- 판: v1 (2026-11-22. 2026-11-23 머리 회전·위치 추가, 2026-11-24 좌우 기준 설명 정정. 2026-11-24부터 판 변경 규칙 적용, §6)
- 접속·등록·헤더·에러 등 전송 규칙은 VishaSynapse 클라이언트 프로토콜을 따른다: https://github.com/Vishamos/VishaSynapse/blob/main/docs/protocol/README.md
- 이 문서는 그 규칙의 **바이너리 데이터 부분**만 정한다.
- 공개 저장소이므로 서버 주소·내부 IP는 적지 않는다.

---

## 1. 메시지 요약

| 항목 | 값 |
|---|---|
| 방향 | Face iOS (22) → Vishamos (11) |
| 형식 | WebSocket 바이너리 메시지 1개 = 1프레임 |
| 크기 | **250바이트 고정** (Synapse 헤더 2 + 데이터 248) |
| 빈도 | 초당 최대 60회 (ARKit 갱신마다 1회). 네트워크가 밀리면 오래된 프레임은 버리고 최신 프레임만 보내므로 실제 횟수는 더 적을 수 있다 |
| 숫자 | 리틀 엔디언. 실수는 IEEE 754 float32 |

---

## 2. 바이트 배치

메시지 맨 앞에서부터의 위치(오프셋)다.

| 오프셋 | 크기 | 형식 | 이름 | 내용 |
|---|---|---|---|---|
| 0 | 1 | uint8 | to | `11` (Vishamos). Synapse 헤더 |
| 1 | 1 | uint8 | from | `22` (Face iOS). Synapse 헤더 |
| 2 | 1 | uint8 | type | 데이터 종류. **`0x16`** = 얼굴 프레임 (§3) |
| 3 | 1 | uint8 | version | 이 형식의 판. 지금은 `1` |
| 4 | 1 | uint8 | flags | bit0: 추적 중(1) / 추적 끊김(0). 나머지 비트는 0 (예약) |
| 5 | 1 | uint8 | reserved | 0 |
| 6 | 4 | uint32 | seq | 프레임 번호. 앱이 연결(등록)할 때마다 0부터 1씩 늘린다 |
| 10 | 4 | uint32 | timeMs | 앱 시작 후 경과 시간(ms). 프레임 간격 계산용 |
| 14 | 208 | float32 × 52 | blendShapes | 블렌드셰이프 값 52개, 각 0.0∼1.0. 순서는 §4 표 |
| 222 | 16 | float32 × 4 | headRotation | 머리 회전 쿼터니언 x, y, z, w (§5a) |
| 238 | 12 | float32 × 3 | headPosition | 머리 위치 x, y, z, 단위 m (§5a) |

합계 250바이트.

C# 수신 예 (`data`는 받은 바이트 배열):
```csharp
if (data.Length != 250 || data[2] != 0x16 || data[3] != 1) return; // 형식이 다르면 버린다
bool tracking = (data[4] & 0x01) != 0;
uint seq = BitConverter.ToUInt32(data, 6);
float jawOpen = BitConverter.ToSingle(data, 14 + 24 * 4); // 인덱스 24 = jawOpen
var rot = new Quaternion(
    BitConverter.ToSingle(data, 222), BitConverter.ToSingle(data, 226),
    BitConverter.ToSingle(data, 230), BitConverter.ToSingle(data, 234));
var pos = new Vector3(
    BitConverter.ToSingle(data, 238), BitConverter.ToSingle(data, 242),
    BitConverter.ToSingle(data, 246));
```
`BitConverter`는 기기 기본 바이트 순서를 쓴다. Windows·iOS·Android 기기는 모두 리틀 엔디언이라 그대로 맞는다.

---

## 3. 데이터 종류 번호 (type)

| 값 | 보내는 쪽 | 내용 |
|---|---|---|
| `0x15` | Face Android (21) | 특징점 좌표 (옛 형식, 이 문서 범위 밖) |
| `0x16` | Face iOS (22) | 얼굴 프레임: 블렌드셰이프 + 머리 회전·위치 (이 문서) |

- Android와 다른 번호를 쓰는 이유: 두 형식이 다른데 같은 번호를 쓰면, `from`을 확인하지 않는 수신 코드가 서로의 데이터를 잘못 해석한다.
- 받는 쪽은 `from`과 `type`을 **둘 다** 확인한다.

---

## 4. 블렌드셰이프 순서

ARKit 블렌드셰이프 이름의 알파벳 순서다. **인덱스는 이 표가 기준**이며, 판(version)이 바뀌지 않는 한 바꾸지 않는다.
`Left`/`Right`는 **거울 기준**이다(아래 §4a).

### 4a. 좌우 기준: 거울

**이 앱이 보내는 값이 기준이다.** 앱은 ARKit 값을 좌우로 바꾸지 않고 그대로 보내며, 그 값은 거울(셀카 화면) 기준으로 나온다. 이름의 `Left`는 사용자 본인의 **오른쪽**, `Right`는 본인의 **왼쪽**이다. 거울을 볼 때 화면에서 왼쪽/오른쪽에 보이는 쪽과 같다.

| 사용자 동작 (본인 기준) | 올라가는 값 | 확인 |
|---|---|---|
| 오른쪽 눈을 감음 | `eyeBlinkLeft` (8) | 실기기 2026-11-24 |
| 입을 오른쪽으로 움직임 | `mouthLeft` (32) | 실기기 2026-11-24 |

- 나머지 `…Left`/`…Right` 쌍(눈썹·볼·눈·입·코), `jawLeft`/`jawRight`도 같은 규칙으로 본다(항목별로 측정하지는 않음).
- `eyeLookIn…`/`eyeLookOut…`의 In/Out은 코 쪽/귀 쪽이라 거울과 무관하다. 어느 눈인지(`Left`/`Right`)만 위 규칙을 따른다.
- 머리 회전도 같은 거울 기준이다(§5a). 블렌드셰이프와 머리 회전은 서로 맞으므로 둘 중 하나만 뒤집으면 안 된다.
- ARKit 문서나 다른 앱의 설명이 이와 다르더라도 이 표가 기준이다.

**받는 쪽에서 쓰는 법**
- **그대로 적용하면 거울처럼 움직인다.** 화면을 마주 보는 아바타의 `eyeBlinkLeft`(아바타 자신의 왼쪽 눈)에 그대로 넣으면, 사용자가 오른쪽 눈을 감을 때 아바타는 화면에서 오른쪽에 보이는 눈을 감는다. 거울을 보는 것과 같은 동작이다.
- 사용자 본인과 같은 쪽으로 움직이게 하려면 받는 쪽에서 한꺼번에 뒤집는다:
  1. 블렌드셰이프: 모든 `…Left` ↔ `…Right` 값을 맞바꾼다(`mouthLeft` ↔ `mouthRight`, `jawLeft` ↔ `jawRight` 포함).
  2. 머리 회전: 쿼터니언 `(x, y, z, w)` → `(x, −y, −z, w)` (좌우 반전).
  3. 머리 위치: `x` → `−x`.
- 거울로 쓸지 뒤집을지는 받는 쪽이 정한다. 앱 쪽 기준은 바꾸지 않으며, 바뀌면 `version`을 올린다(§6).

| # | 이름 | # | 이름 | # | 이름 | # | 이름 |
|---|---|---|---|---|---|---|---|
| 0 | browDownLeft | 13 | eyeLookInRight | 26 | mouthClose | 39 | mouthRollLower |
| 1 | browDownRight | 14 | eyeLookOutLeft | 27 | mouthDimpleLeft | 40 | mouthRollUpper |
| 2 | browInnerUp | 15 | eyeLookOutRight | 28 | mouthDimpleRight | 41 | mouthShrugLower |
| 3 | browOuterUpLeft | 16 | eyeLookUpLeft | 29 | mouthFrownLeft | 42 | mouthShrugUpper |
| 4 | browOuterUpRight | 17 | eyeLookUpRight | 30 | mouthFrownRight | 43 | mouthSmileLeft |
| 5 | cheekPuff | 18 | eyeSquintLeft | 31 | mouthFunnel | 44 | mouthSmileRight |
| 6 | cheekSquintLeft | 19 | eyeSquintRight | 32 | mouthLeft | 45 | mouthStretchLeft |
| 7 | cheekSquintRight | 20 | eyeWideLeft | 33 | mouthLowerDownLeft | 46 | mouthStretchRight |
| 8 | eyeBlinkLeft | 21 | eyeWideRight | 34 | mouthLowerDownRight | 47 | mouthUpperUpLeft |
| 9 | eyeBlinkRight | 22 | jawForward | 35 | mouthPressLeft | 48 | mouthUpperUpRight |
| 10 | eyeLookDownLeft | 23 | jawLeft | 36 | mouthPressRight | 49 | noseSneerLeft |
| 11 | eyeLookDownRight | 24 | jawOpen | 37 | mouthPucker | 50 | noseSneerRight |
| 12 | eyeLookInLeft | 25 | jawRight | 38 | mouthRight | 51 | tongueOut |

---

## 5. 값의 성질 (받는 쪽 참고)

| 상황 | 앱이 보내는 값 | 받는 쪽 처리 |
|---|---|---|
| 정상 추적 | flags bit0 = 1, 52개 모두 최신 값 | 그대로 적용 |
| 얼굴 일부가 가려짐·옆으로 돌림 | flags bit0 = 1, 52개 모두 채워짐. 안 보이는 쪽은 ARKit이 추정한 값 | 추정 로직 필요 없음. 그대로 적용 |
| 추적 끊김 (많이 돌림, 화면 밖) | flags bit0 = 0, **마지막으로 추적한 값**을 그대로 보냄. 같은 빈도로 계속 보냄 | 받는 쪽이 정한다: 마지막 표정 유지, 또는 천천히 무표정으로 복귀 |

- 값은 0∼1이지만 사람마다 최대치가 1.0에 못 미치는 항목이 많고(0.6∼0.8), 무표정에서도 0.05∼0.2가 나오는 항목이 있다. 보정(배율·기준값)이 필요하면 받는 쪽에서 한다.
- 작은 떨림이 있다. 스무딩은 받는 쪽에서 한다. 앱은 원본 값을 보낸다.
- `seq`가 건너뛰면 그 사이 프레임은 앱에서 버린 것이다(최신 프레임 우선). 받는 쪽은 재요청하지 않는다.
- 추적이 끊기면 머리 회전·위치도 마지막으로 추적한 값을 보낸다.

---

## 5a. 머리 회전·위치

| 항목 | 값 |
|---|---|
| 기준 | **폰 카메라 기준**(카메라에 대한 얼굴의 상대 자세). 폰을 거치대에 고정해 쓰므로, 폰을 어디에 두든 같은 의미가 된다 |
| 좌표계 | Unity 좌표계(왼손 좌표계, y 위). 받는 쪽 Unity에서 별도 변환 없이 `Quaternion`·`Vector3`로 읽는다 |
| 회전 | 쿼터니언 x, y, z, w. 정규화된 값 |
| 위치 | 미터. 카메라에서 얼굴 중심(코 뒤쪽 머리 안)까지 |
| 정면 | **단위 회전** `(0, 0, 0, 1)`. 얼굴이 카메라를 정면으로 볼 때 이 값 근처가 나온다(실기기 확인 2026-11-23: 예 `(0.060, -0.003, -0.040, 0.997)`) |

**거울 기준이다.** 블렌드셰이프(§4a)와 같이 앱은 값을 그대로 보내며, 그 값은 거울에 비친 자세로 나온다. 화면을 마주 보는 아바타에 그대로 적용하면, 사용자가 본인 오른쪽으로 고개를 돌릴 때 아바타는 화면 오른쪽으로 고개를 돌린다(거울처럼). 본인과 같은 쪽으로 돌게 하는 방법은 §4a "받는 쪽에서 쓰는 법"을 따른다.

**축 방향·부호** (실기기 확인 2026-11-23, Vishamos 쪽 측정 2026-11-24도 같음). 아래 표의 **동작** 칸은 폰을 보는 사람 본인 기준으로 적었고, **부호**는 앱이 보내는 값(거울 기준)이다.

| 동작 | 축 | 부호 |
|---|---|---|
| 고개를 왼쪽으로 돌림(코가 본인 왼쪽으로) | y | + |
| 고개를 오른쪽으로 돌림 | y | − |
| 고개를 듦(위를 봄) | x | + |
| 고개를 오른쪽 어깨로 기울임 | z | − |

- 확인 방법: 앱 화면에 쿼터니언을 Unity `eulerAngles`로 바꿔 −180∼180°로 표시한 값을 봤다. 한 축만 조금 돌렸을 때 오일러 각의 부호와 쿼터니언 x/y/z의 부호는 같다(w > 0일 때).
- 고개를 왼쪽 어깨로 기울일 때(z +)와 고개를 숙일 때(x −)는 위 표의 반대로 본다(따로 측정하지 않음).
- 위치 z가 폰과 얼굴 사이 거리(m)인지, 위치 x의 좌우 부호는 아직 확인하지 않았다.
- 사람마다 정면 자세가 조금씩 다르므로, 기준 자세 보정(캘리브레이션)과 스무딩은 받는 쪽에서 한다.

---

## 6. 판 변경 규칙

- 바이트 배치나 인덱스 순서가 바뀌면 `version`을 올리고, 이 문서에 판별 차이를 적는다.
- **값의 의미가 바뀌어도 `version`을 올린다.** 예: 좌우 기준(거울, §4a), 머리 회전 축·부호(§5a), 정면 기준값이 바뀌는 경우. 바이트 배치가 같아도 받는 쪽 동작이 달라지기 때문이다.
- 설명만 고치고 보내는 값이 그대로면 판을 올리지 않는다. 받는 쪽 동작에 영향이 있는 설명 정정은 아래 이력에 적고 GitHub issue로 알린다.
- v1은 받는 쪽 제작 전까지 초안으로 직접 고쳤으나, 2026-11-24부터 위 규칙을 적용한다(Vishamos가 받은 값 처리 방식을 명세에 정함).
- 고친 이력:
  - 2026-11-23 머리 회전·위치 추가(222 → 250바이트, 사용자 결정. VishasBody의 얼굴 회전 인식이 부정확함).
  - 2026-11-24 §4 좌우 설명 정정: "사용자 본인 기준" → "거울 기준"(§4a). **보내는 값은 바뀌지 않았다.** 처음부터 거울 기준 값이었고 설명이 틀렸다. GitHub issue #1(Vishamos 쪽 실측)에서 발견. 거울 기준을 그대로 유지하기로 함(사용자 결정).
- 받는 쪽은 모르는 `version`의 메시지를 버린다.
- VishaSynapse 클라이언트 프로토콜 §8 "메시지 용량 표"의 Face iOS 줄을 이 문서와 맞춘다(그쪽 저장소에서 고친다).
