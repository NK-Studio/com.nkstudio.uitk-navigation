# Pivot 노드 회전 — 포트 이름 고정 방식으로 재설계 (작업 지시서)

Unity 6.6 (6000.6.0b5) + `com.unity.graphtoolkit` (실험적) 환경입니다.
GraphToolkit은 노드 뷰를 직접 만들 수 없어서, 이 기능은 **모델(공개 API) + USS + 노드 뷰 조작**의 조합으로 되어 있습니다.

---

## 1. 목표

Pivot 노드가 아이콘 버튼 클릭으로 `Right → Down → Left → Up → Right`(시계방향) 4단 회전을 하되,
**회전할 때 연결된 와이어가 절대 끊기지 않게** 만듭니다.

| 상태 | 입력 위치 | 출력 위치 |
|---|---|---|
| `Right` | 왼쪽 | 오른쪽 |
| `Down` | 위 | 아래 |
| `Left` | 오른쪽 | 왼쪽 |
| `Up` | 아래 | 위 |

---

## 2. 왜 다시 짜야 하는가

지금은 세로 배치(`Down`/`Up`)에 `IPortBuilder.AsVertical()`을 씁니다. 그런데 **GraphToolkit은 노드를 다시 정의할 때
같은 이름의 포트 모델을 재사용하면서 바뀐 `AsVertical()`을 무시합니다.** (같은 증상이
`UINavigationRandomPortPreview.cs` 상단 주석에 `DisplayName` 기준으로 이미 기록돼 있습니다.)

그래서 현재 코드는 방향마다 포트 **이름을 다르게** 줘서 새 포트 모델이 만들어지게 우회합니다:

```csharp
string enterId = vertical ? "enter_v" : "enter";
```

이름이 바뀌면 그 포트를 참조하던 와이어가 사라집니다. 실측으로 확인했습니다 — 회전 직후 `enter_v connected=0`.

**새 방식: `AsVertical()`을 아예 쓰지 않습니다.** 포트는 언제나 `enter` / `exit` 한 쌍의 가로 포트로 고정하고,
네 가지 배치를 전부 뷰(USS + 와이어 보정)에서 만듭니다. 포트 이름이 영원히 안 바뀌므로 와이어가 끊길 일이 없습니다.

---

## 3. 지금 있는 파일

| 파일 | 역할 |
|---|---|
| `Editor/Navigation/UINavigationPivotPrototypeNode.cs` | 노드 모델. `UINavigationPivotRotation` enum, `rotation` 옵션, 포트 정의 |
| `Editor/Navigation/UINavigationPivotViewStyler.cs` | 노드 뷰에 USS 클래스 부착, 회전 아이콘 버튼 주입, 와이어 방향 보정 |
| `Editor/Styles/UINavigationPivotPrototypeNode.uss` | `[Node(..., stylesheet)]`로 이 노드 타입에만 붙는 USS |

`Editor/Assets/RefreshIcon.svg` 를 회전 버튼 아이콘으로 씁니다 (`svgType: 3` = VectorImage).

---

## 4. 새 설계

### 4.1 모델 (`UINavigationPivotPrototypeNode.cs`)

- `OnDefinePorts`에서 **`AsVertical()` 호출을 전부 제거**합니다.
- 포트 이름은 항상 `EnterPort` / `ExitPort` 상수 하나씩. `EnterPortVertical` / `ExitPortVertical` 및
  `GetEnterPort()` / `GetExitPort()`의 fallback 분기는 삭제합니다.
- `rotation` 옵션, `IsVertical()`, `IsReversed()`, `PortCapacity.Single`,
  `PortConnectorUI.Arrowhead`는 그대로 둡니다.
- 결과적으로 회전을 바꿔도 포트 모델이 그대로 재사용됩니다. **이게 목적입니다.**

### 4.2 뷰 클래스 (`UINavigationPivotViewStyler.cs`)

노드 뷰에 상태 클래스를 하나 붙입니다. 지금의 `pivot--flip-h` / `pivot--flip-v` 2종 대신
**4상태를 명시적으로** 나누는 편이 USS가 훨씬 읽기 쉽습니다:

```
pivot--right   (기본, 클래스 없어도 됨)
pivot--down
pivot--left
pivot--up
```

회전 값은 지금처럼 **노드 본문에 그려진 `EnumField`의 `value`** 에서 읽습니다.
(`ge-node__node-options` 안에 있고, USS로 숨겨져 있지만 트리에는 남아 있습니다.
이게 모델과 값을 주고받는 통로라 지우면 안 됩니다.)

### 4.3 USS 배치

포트는 언제나 `ge-node__port-container` 안의 `ge-node__inputs` / `ge-node__outputs` 두 컨테이너에 들어 있습니다.
(`AsVertical()`을 안 쓰므로 `ge-node__top-vertical-port-container` /
`ge-node__bottom-vertical-port-container`는 **항상 비어 있습니다. 더 이상 쓰지 않습니다.**)

- **Right** — 기본. 손대지 않음.
- **Left** — 이미 동작하는 규칙 그대로:
  ```css
  .pivot--left .ge-node__port-container      { flex-direction: row-reverse; }
  .pivot--left .ge-port__connector-container { flex-direction: row-reverse; }
  .pivot--left .ge-port__connector           { rotate: 180deg; }
  ```
- **Down / Up — 여기가 새로 짜야 하는 부분입니다.**
  `ge-node__inputs`를 노드 위쪽 가장자리 가운데로, `ge-node__outputs`를 아래쪽 가장자리 가운데로 보냅니다
  (`Up`은 그 반대). 절대 위치 + 가운데 정렬이 기본 방향이 될 겁니다:
  ```css
  .pivot--down .ge-node__inputs {
      position: absolute;
      top: 0; left: 0; right: 0;
      align-items: center;
  }
  ```
  커넥터 화살표는 가로 기준으로 그려져 있으므로 `rotate: 90deg` / `270deg`로 세워야 합니다.

### 4.4 와이어 각도

`WireControl`에 **쓰기 가능한** 속성이 있습니다 (실측 확인):

```
PortOrientation FromOrientation / ToOrientation   set=True
PortDirection   FromDirection   / ToDirection     set=True
void            UpdateLayout()                    (인자 없음, 곡선 재계산)
```

모델은 언제나 가로 포트이므로 GraphToolkit은 와이어를 좌우로 그리려 합니다.
`Down`/`Up`에서는 해당 끝점의 **Orientation을 `Vertical`로** 바꿔야 위/아래로 들어옵니다.
`Left`/`Up`처럼 입출력 자리가 뒤집힌 상태는 **Direction도 뒤집습니다** (이미 구현돼 있는 로직).

끝점이 우리 Pivot 포트인지는 `WireView`의 `m_LastUsedFromPort` / `m_LastUsedToPort`
(포트 뷰 그 자체)로 판별합니다. 지금 코드에 이미 있습니다.

---

## 5. 이미 검증된 사실 — 다시 조사하지 마세요

전부 이 프로젝트의 Unity 에디터에서 실측한 것입니다.

**노드 뷰 구조** (`CollapsibleInOutNodeView`, 자식 순서대로)
```
ge-node                                     (루트)
├ ge-node__color-line                       accent bar
├ ge-node__top-vertical-port-container      AsVertical 전용 (새 설계에선 항상 빔)
├ ge-node-title-part                        아이콘 + title/subtitle + collapse 버튼
├ ge-node__node-options                     노드 본문 옵션 (EnumField가 여기 있음)
├ ge-node__port-container
│   ├ ge-node__inputs  > ge-port > ge-port__connector-container > ge-port__connector
│   └ ge-node__outputs > (같은 구조)
├ ge-node__bottom-vertical-port-container   AsVertical 전용
└ ge-node__cache                            줌아웃 시 대체 렌더
```

- `[Node(categoryPath, iconPath, title, stylesheet)]`의 4번째 인자로 **노드 타입 단위 USS**를 붙일 수 있습니다.
  인스턴스마다 다른 상태는 USS만으로 불가능해서 뷰에 클래스를 붙이는 코드가 필요합니다.
- 타이틀·accent bar·포트 라벨을 `display: none` 하면 노드가 **125×93 → 49×36** 까지 줄어듭니다.
- `flex-direction: row-reverse`로 `ge-node__port-container`를 뒤집으면 입력/출력 자리가 실제로 바뀝니다
  (입력 x: 20 → 187).
- 커넥터는 포트 안에서 한쪽 끝에 고정이라, 행만 뒤집으면 커넥터가 노드 한가운데 남습니다.
  `ge-port__connector-container`도 뒤집어야 가장자리로 갑니다 (입력 커넥터 x 88% 지점 확인).
- **`.ge-node`에 `min-width`는 먹지 않습니다.** GraphToolkit이 노드 폭을 인라인으로 잡는 것으로 보입니다.
  폭을 잡아야 하면 흐름에 남아 있는 자식(`ge-node__node-options` 등)에 `min-width`를 주세요.
- **노드 루트에 `flex-direction: column-reverse`는 효과가 없습니다.** `resolvedStyle`은 바뀌는데 레이아웃이
  그대로입니다. 자리를 바꾸려면 `position: absolute` 또는 `translate`를 쓰세요.
- `EnumField.value`에 코드로 값을 넣으면 사람이 고른 것과 똑같이 모델에 반영되고 노드가 재정의됩니다.
- `IPort.GetConnectedPorts` + `Graph.Connect`로 연결을 수동 복구할 수 있습니다 (둘 다 `True` 반환 확인).
  이번 설계에서는 안 쓰지만, 최후의 안전망으로 알아두세요. `Graph.UndoBeginRecordGraph`/`UndoEndRecordGraph`로
  Undo 한 번에 묶을 수 있습니다.

---

## 6. 아직 검증 안 된 것 — 여기부터 확인하세요

1. **`WireControl.FromOrientation/ToOrientation`을 `Vertical`로 바꿨을 때 와이어가 실제로 위/아래로 들어오는가.**
   속성이 쓰기 가능하다는 것만 확인했고, 실제로 세팅해 본 적은 없습니다. **이 설계 전체가 여기에 걸려 있으니
   제일 먼저 시험하세요.** 안 되면 4.3/4.4를 다시 설계해야 합니다.
2. 가로 포트를 절대 위치로 위/아래 가장자리에 붙였을 때 커넥터의 world 좌표가 제대로 따라오는가
   (와이어 끝점이 커넥터 위치를 따라가므로 중요합니다).
3. 회전 아이콘 버튼의 실제 클릭. 합성 `ClickEvent`로는 핸들러가 타지 않아 코드로 검증하지 못했습니다.
   사람이 눌러서 확인해야 합니다.

---

## 7. 작업 순서

1. **먼저 6-1을 시험합니다.** 임시 에디터 스크립트로 열려 있는 그래프의 와이어를 찾아
   `ToOrientation = Vertical` + `UpdateLayout()`을 걸고 눈으로 확인합니다. 실패하면 여기서 멈추고 보고하세요.
2. 모델에서 `AsVertical()`과 `_v` 포트 이름 분기를 제거합니다 (4.1).
3. 스타일러의 클래스 부착을 4상태로 바꿉니다 (4.2).
4. USS에서 `Down`/`Up` 배치를 새로 짭니다 (4.3). `*-vertical-port-container` 규칙은 전부 삭제합니다.
5. 와이어 보정에 Orientation 처리를 추가합니다 (4.4).
6. 4상태를 한 바퀴 돌리며 측정 + 육안 확인.

---

## 8. 검증 방법

Unity MCP로 에디터를 붙여서 확인합니다. 효과적이었던 방식:

- `Assets/Editor` 또는 패키지 안에 `__` 접두사 임시 에디터 스크립트를 만들고 `[MenuItem]`으로 실행 →
  `Debug.Log`로 결과를 찍고 콘솔에서 읽습니다. **확인이 끝나면 반드시 삭제하세요.**
- 열려 있는 그래프 창은 `Resources.FindObjectsOfTypeAll<EditorWindow>()`에서 타입 이름에
  `GraphViewEditorWindow`가 들어간 것으로 찾습니다.
- 배치 확인은 **커넥터의 world 좌표를 노드 기준 상대 좌표로 환산**해서 찍는 게 가장 확실합니다.
  `layout`은 줌이 반영되지 않아 헷갈립니다.
- 스크립트를 새로 만들면 컴파일 + 도메인 리로드가 필요합니다. `Assets/Refresh` 메뉴를 실행하고,
  메뉴 항목이 등록될 때까지 한두 번 더 시도하세요.

---

## 9. 완료 기준

- [ ] 4상태 모두 입력/출력이 표에 맞는 위치에 그려진다.
- [ ] **연결된 와이어가 붙은 채로 4상태를 한 바퀴 돌려도 연결이 유지된다** (이번 작업의 핵심).
- [ ] 각 상태에서 와이어가 커넥터 쪽에서 자연스럽게 들어온다 (노드를 가로질러 걸치지 않는다).
- [ ] 노드를 드래그하는 동안 와이어가 떨리지 않는다.
- [ ] 화살촉이 흐름 방향과 일치한다.
- [ ] 컴파일 에러/경고 없음. 임시 스크립트 전부 삭제됨.

---

## 10. 함정 모음 (전부 실제로 밟은 것들)

- **폴링으로 와이어를 고치면 드래그 중에 심하게 떱니다.** GraphToolkit이 매 프레임 방향을 되돌리기 때문입니다.
  `WireControl`에 `GeometryChangedEvent` 콜백을 걸어 **재계산된 그 프레임 안에서** 고쳐야 합니다.
  `UpdateLayout()`이 다시 `GeometryChangedEvent`를 부를 수 있으니 **재진입 가드**가 필수입니다.
  UI Toolkit은 콜백 예외를 삼키므로 콜백 안에서 직접 try/catch 하세요.
- **포트 컨테이너를 절대 위치로 빼면 노드가 무너집니다.** 흐름에서 빠진 만큼 노드 폭/높이가 줄어들고,
  옵션 행이 포트와 겹쳐 노드가 통째로 깨져 보입니다. 흐름에 남은 요소의 `min-width` / `margin`으로
  자리를 미리 비워 두세요.
- **자리를 옮기면 화살촉은 원래 방향 그대로 남습니다.** `rotate`로 따로 돌려야 합니다.
- **가로 배치에서 옵션 행은 포트 행 위(제목 자리)에 있습니다.** 회전 버튼을 가운데에 두려면
  버튼을 `ge-node__port-container`의 입력/출력 **사이로 옮겨야** 합니다(코드로 reparent).
  세로 배치에서는 옵션 행이 이미 위/아래 포트 사이라 그대로 두면 됩니다.
- 줌아웃하면 `ge-node__cache` 라벨 렌더로 대체됩니다. 납작한 노드는 여기서 오히려 커 보일 수 있습니다.

---

## 11. 코드 규칙

- 주석은 한국어. **무엇을 하는지가 아니라 왜 그렇게 했는지**를 적습니다. 기존 파일들의 톤을 따르세요.
- 리플렉션으로 GraphToolkit 내부(`WireView`, `WireControl`)를 건드리는 부분은 실험적 패키지라 깨질 수 있습니다.
  **실패하면 조용히 접고 나머지 기능은 계속 동작**해야 합니다 (현재 `_wireReflectionFailed` 패턴 참고).
- 이 기능과 무관한 파일은 건드리지 마세요. 특히 `Assets/Sample/Sample.uinavgraph`는 테스트용이라
  회전 상태나 연결이 바뀔 수 있는데, 그건 보고만 하고 임의로 되돌리지 마세요.
- 콘솔의 `Pivot은 입력과 출력을 모두 연결해야 합니다` 오류는 `UINavigationGraphCompiler`의 검증 메시지입니다.
  이번 작업과 무관합니다.
