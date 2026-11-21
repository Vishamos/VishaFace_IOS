# VishaFaceI 얼굴 데이터 프로토콜

Face iOS 앱(시그니처 22)이 VishaSynapse를 거쳐 Vishamos(11)로 보내는 얼굴 데이터의 형식이다. **받는 쪽(Vishamos)은 이 문서를 보고 수신부를 만든다.**

- 판: v1 (2026-11-22, 초안)
- 접속·등록·헤더·에러 등 전송 규칙은 VishaSynapse 클라이언트 프로토콜을 따른다: https://github.com/Vishamos/VishaSynapse/blob/main/docs/protocol/README.md
- 이 문서는 그 규칙의 **바이너리 데이터 부분**만 정한다.
- 공개 저장소이므로 서버 주소·내부 IP는 적지 않는다.

---

## 1. 메시지 요약

| 항목 | 값 |
|---|---|
| 방향 | Face iOS (22) → Vishamos (11) |
| 형식 | WebSocket 바이너리 메시지 1개 = 1프레임 |
| 크기 | **222바이트 고정** (Synapse 헤더 2 + 데이터 220) |
| 빈도 | 초당 최대 60회 (ARKit 갱신마다 1회). 네트워크가 밀리면 오래된 프레임은 버리고 최신 프레임만 보내므로 실제 횟수는 더 적을 수 있다 |
| 숫자 | 리틀 엔디언. 실수는 IEEE 754 float32 |

---

## 2. 바이트 배치

메시지 맨 앞에서부터의 위치(오프셋)다.

| 오프셋 | 크기 | 형식 | 이름 | 내용 |
|---|---|---|---|---|
| 0 | 1 | uint8 | to | `11` (Vishamos). Synapse 헤더 |
| 1 | 1 | uint8 | from | `22` (Face iOS). Synapse 헤더 |
| 2 | 1 | uint8 | type | 데이터 종류. **`0x16`** = 블렌드셰이프 프레임 (§3) |
| 3 | 1 | uint8 | version | 이 형식의 판. 지금은 `1` |
| 4 | 1 | uint8 | flags | bit0: 추적 중(1) / 추적 끊김(0). 나머지 비트는 0 (예약) |
| 5 | 1 | uint8 | reserved | 0 |
| 6 | 4 | uint32 | seq | 프레임 번호. 앱이 연결(등록)할 때마다 0부터 1씩 늘린다 |
| 10 | 4 | uint32 | timeMs | 앱 시작 후 경과 시간(ms). 프레임 간격 계산용 |
| 14 | 208 | float32 × 52 | blendShapes | 블렌드셰이프 값 52개, 각 0.0∼1.0. 순서는 §4 표 |

합계 222바이트.

C# 수신 예 (`data`는 받은 바이트 배열):
```csharp
if (data.Length != 222 || data[2] != 0x16 || data[3] != 1) return; // 형식이 다르면 버린다
bool tracking = (data[4] & 0x01) != 0;
uint seq = BitConverter.ToUInt32(data, 6);
float jawOpen = BitConverter.ToSingle(data, 14 + 24 * 4); // 인덱스 24 = jawOpen
```
`BitConverter`는 기기 기본 바이트 순서를 쓴다. Windows·iOS·Android 기기는 모두 리틀 엔디언이라 그대로 맞는다.

---

## 3. 데이터 종류 번호 (type)

| 값 | 보내는 쪽 | 내용 |
|---|---|---|
| `0x15` | Face Android (21) | 특징점 좌표 (옛 형식, 이 문서 범위 밖) |
| `0x16` | Face iOS (22) | 블렌드셰이프 프레임 (이 문서) |

- Android와 다른 번호를 쓰는 이유: 두 형식이 다른데 같은 번호를 쓰면, `from`을 확인하지 않는 수신 코드가 서로의 데이터를 잘못 해석한다.
- 받는 쪽은 `from`과 `type`을 **둘 다** 확인한다.

---

## 4. 블렌드셰이프 순서

ARKit 블렌드셰이프 이름의 알파벳 순서다. **인덱스는 이 표가 기준**이며, 판(version)이 바뀌지 않는 한 바꾸지 않는다.
`Left`/`Right`는 **사용자 본인 기준**이다(카메라 화면에서는 반대쪽에 보인다).

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
- 머리 회전·위치는 보내지 않는다(다른 캡처 프로그램 담당).
- `seq`가 건너뛰면 그 사이 프레임은 앱에서 버린 것이다(최신 프레임 우선). 받는 쪽은 재요청하지 않는다.

---

## 6. 판 변경 규칙

- 바이트 배치나 인덱스 순서가 바뀌면 `version`을 올리고, 이 문서에 판별 차이를 적는다.
- 받는 쪽은 모르는 `version`의 메시지를 버린다.
- VishaSynapse 클라이언트 프로토콜 §8 "메시지 용량 표"의 Face iOS 줄을 이 문서와 맞춘다(그쪽 저장소에서 고친다).
