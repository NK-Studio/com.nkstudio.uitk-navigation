# Changelog

## [0.3.0] - 2026-08-03

### Added

- AI 및 에디터 코드 레이어에서 접근하기 쉽도록 `UINavigationPortalNode` 및 `UINavigationGraphBuilder` 코드-퍼스트 오소링 API 지원을 추가했습니다 (`AddPortal`, `PortalNode`, `Connect(PortalNode, ScreenNode)`).
- `UINavigationPortalConditionDrawer` 커스텀 프로퍼티 드로어를 UI 노드 인스펙터 스타일과 동일한 `uinavigation-phase` 카드 디자인(SVG 아이콘, 헤더, 라운딩 박스)으로 구현했습니다.

### Changed

- 패키지 버전을 `0.3.0`으로 올렸습니다.
- Portal 노드의 옵션(`DisplayName`, `Condition`, `History`)에 `.ShowInInspectorOnly()`를 적용하여 캔버스 노드 바디를 콤팩트하게 유지하고, 인스펙터 창에서 조작할 수 있도록 개선했습니다.
- Portal 노드의 Output 포트 표기를 고정 텍스트(`Force Transition`) 대신 설정된 조건에 맞춰 자동 표시되도록 수정했습니다.

## [0.2.0] - 2026-08-02

### Added

- 명시적인 `VisualElement` 루트에 연결하는 순수 C# `UIPopupStack`과 `PanelRenderer` 수명에
  맞춰 스택을 관리하는 `UIPopupHost`를 추가했습니다.
- `VisualTreeAsset + dataSource + configure` 기반 `Show`/`ShowAsync`, LIFO 중첩, 결과 및
  취소 처리, 포커스 이동/복원, Back 우선 처리를 추가했습니다.
- 타입 기반 UXML 마커 `UIPopupView`, `UIPopupBackdrop`, `UIPopupContent`,
  `UIPopupActionButton`과 템플릿 소유 전환 설정을 추가했습니다.
- 동일 메시지 템플릿에 서로 다른 모델을 전달하는 A 샘플과, 독립된 보상 카드/하단 시트
  템플릿을 사용하는 B 샘플을 추가했습니다.

### Changed

- 패키지 버전을 `0.2.0`으로 올렸습니다.
- Popup은 Navigation Graph와 독립된 패널 로컬 스택으로 동작하며, 최상단 Popup의 Back
  정책만 기존 네비게이션보다 먼저 평가합니다.

### Removed

- **Breaking.** 기존 `UIPopupCatalog`, `UIPopupContext`, `UIPopupShowOptions`,
  `UIPopupOpenButton`, 정적 `UIPopup`, `UIPopupService`, `UIPopupPresenter`를 제거했습니다.
- Popup 문자열 Key Catalog, 전역 기본 Host, Queue 기반 표시에 대한 지원을 제거했습니다.

## [Unreleased]

### Changed

- 메뉴/CreateAssetMenu 경로에서 저작사 이름 `NK Studio`를 걷어내고 에셋 정체성인 `UI Navigation`/`UITK Navigation`으로 통일.
  `Tools > NK Studio > UI Navigation > ...`는 `Tools > UI Navigation > ...`로, `Create > NK Studio > UI Navigation Graph`는
  `Create > UI Navigation > UI Navigation Graph`로, `NK Studio/UITK Navigation/...` 형태의 애셋 생성 메뉴는 `UITK Navigation/...`로 바뀌었다.
- 패키지 전역 System.Linq 사용을 ZLinq(`com.cysharp.zlinq`)로 전환. 에디터 툴링(Key Catalog, Navigation Graph
  Compiler/Validator, 프로퍼티 드로어)과 테스트 코드의 모든 LINQ 호출이 `AsValueEnumerable()` 기반 무할당 API를 사용한다.
  ZLinq는 이제 필수 의존성이며 LitMotion과 마찬가지로 git 패키지로 별도 설치해야 한다.

### Added

- `NavElement`의 `Startup` 커스텀 인스펙터 패널. `On Start`(Disabled / Instant Hide / Instant Show /
  Animation Hide / Animation Show), `When Hidden`(Display / Visibility), `Custom Start Position`(Get / Set / Reset)을 제공한다.
- `NavElement`의 `Auto Hide after Show`. 토글로 켜고 `Auto Hide Delay`(float, 초)를 지정하면 Instant Show / Animation Show로
  표시가 끝난 뒤 그 시간이 지날 때 자동으로 Hide가 실행된다. 기본값은 꺼짐 / 3초이며, UXML 속성은
  `auto-hide-after-show` / `auto-hide-delay`다.
- `UIViewVisibility.HideMode`. 숨김 시 `display: none` 대신 레이아웃을 유지하는 `visibility: hidden`을 선택할 수 있다.
- 활성 채널에 맞춘 `usageHints` 자동 관리. Move/Rotate/Scale이 켜지면 `DynamicTransform`,
  Fade가 켜지면 `DynamicColor`를 애니메이션 대상 요소에 붙이고, 채널을 끄면 우리가 붙인 비트만 뗀다.
  UXML에서 직접 오소링한 비트는 보존한다.

### Changed

- **Breaking.** 요소 타입 이름을 짧게 줄였다. 화면 단위를 가리키는 도메인 용어는 그대로 `View`이며,
  `NavElement`는 그 View를 UXML에 배치하는 **요소**라는 뜻이다(요소 안에 View 주소를 연결한다).
  - `UINavigationView` → `NavElement`, `UINavigationButton` → `NavButton`, `UINavigationToggle` → `NavToggle`
  - USS 클래스: `ui-navigation-view` → `nav-element`
  - 지원 타입(`UIViewRegistry`, `UIViewVisibility`, `UIViewStartupSettings`, `UIViewHideMode`,
    `UIViewStartBehaviour`, `UIViewTransitionMode`, `UIViewOutputCondition`, `IUIVisibleView`,
    `UIViewId`, `UINavigationViewCommand`)과 UXML 속성 `view-category` / `view-key`,
    열거형 멤버 `UIKeyCatalogKind.View` / `UINavigationTriggerKind.UIView`,
    그래프 인스펙터 라벨 `Show Views` / `Hide Views`는 변경되지 않았다.
  - 프로젝트의 기존 `.uxml`은 함께 마이그레이션했다. 외부 프로젝트는 태그 이름과 USS 클래스만 바꾸면 된다.
- **Breaking.** Transition 프리셋을 단일 `preset` 열거형(14종)에서 **카테고리 + 변형 번호** 2단 선택으로 교체.
  UXML 속성은 `preset="Fade"` → `preset-category="Fade" preset-variant="1"`이 된다. 기존 `preset` 속성과
  `UITransitionPreset` 열거형은 제거됐다. `preset="Custom"`을 쓰던 곳은 속성을 지우면 된다(기본값
  `preset-category="None"`이 같은 의미다).
  - 카테고리 21종: Back, Basic1, Basic2, Bounce, Default, Discrete, Drift, Drop, Fade, Flip, Ghost,
    Gradual, Jelly, Organic1, Organic2, Rotate, Shake, Slide1, Slide2, Spin, Zoom
  - 카테고리마다 1~25개의 변형이 있어 Show/Hide 각 351개, 총 702개 프리셋을 제공한다
  - Show는 From쪽(들어오는 방향), Hide는 To쪽(나가는 방향)을 프리셋이 정의한다
  - 인스펙터는 변형을 번호로 저장하되 드롭다운에는 원본 이름을 보여준다(Drift의 `01Left` 등 이름이
    항상 숫자는 아니기 때문)
  - `UITransitionFactory.Build` / `BuildPreset`의 시그니처가 `(카테고리, 변형 번호, ...)`로 바뀌었다
  - 제약: UI Toolkit의 `rotate`는 2D(z축)만 지원하므로 원본의 y축 회전 성분은 반영되지 않는다.
    Doozy의 Strength / Vibration / Elasticity 같은 Shake 세부 파라미터도 대응 항목이 없어 생략된다.
- 트랜지션 프리뷰의 Play / Reset 버튼을 누를 때 에디터가 멈칫하던 문제. 선택된 Element를 찾는 경로가
  `TypeCache.GetTypesDerivedFrom<object>()` 전체를 훑었는데, 타입 62,000개 기준 한 번 찾는 데 약 560ms,
  못 찾고 끝까지 도는 경우 약 1,150ms가 걸렸고 이 순회가 클릭 핸들러 안에서 최대 3회 반복됐다.
  대상 타입이 모두 파생된 좁은 집합(`EditorWindow` / `VisualElement`)에서 찾고 결과를 캐싱하도록 바꿔
  도메인 리로드마다 약 22ms 한 번으로 줄였다. 두 번째 클릭부터는 조회 비용이 없다.
- 인스펙터의 트랜지션 프리뷰 재생이 `EditorApplication.update` 대신 대상 Element가 붙어 있는 패널의 스케줄러로
  구동된다. `EditorApplication.update`는 에디터 유휴 틱이라 실측 ~10fps에 그쳐 프리뷰가 끊겨 보였고,
  틱 간격이 델타 상한(50ms)을 넘겨 재생 속도까지 느려졌다. 같은 조건에서 패널 스케줄러는 ~100fps다.

### Fixed

- Pivot 노드를 거쳐 연결하면 `A UI output can connect to exactly one UI or Action node.`,
  `UI의 Enter에는 Start, 다른 UI 출력 또는 계속 실행되는 Action만 연결할 수 있습니다.` 같은 오류가 뜨고
  그래프가 컴파일되지 않던 문제. Pivot은 와이어를 그대로 넘기는 중계점이므로 검증과 컴파일 모두에서
  건너뛴다. Pivot 체인은 Start / UI Enter / UI·Random·Portal 출력 / Action의 Next 어디서든 통한다.
  입력만 있고 출력이 비어 있는(또는 그 반대인) Pivot은 Pivot 자신이 오류로 알린다.
- Enter Play Mode Settings가 `Do not reload Domain or Scene`일 때 두 번째 플레이부터 시작 Show 애니메이션이
  생략되던 문제. 도메인 리로드를 끄면 static이 세션을 넘어 살아남아 `UIViewRegistry`에 이전 세션의 인스턴스가
  남고, 첫 프레임의 Show 명령이 아직 다시 붙지도 않은 그 요소로 흘러갔다. 세션 간 유지되면 안 되는 static
  (`UIViewRegistry`의 View 목록, `UIViewVisibility`의 표시 목록, `NavElement`의 표시 상태 캐시)에
  `[AutoStaticsCleanup]`을 붙여 플레이 모드 경계에서 초기화한다.
- Move 채널을 켠 Show가 애니메이션은 재생되는데 요소가 한 픽셀도 움직이지 않아 하드컷으로 보이던 문제.
  스케줄러 콜백은 패널 갱신에서 레이아웃 계산보다 먼저 실행되므로, `display: none`에서 되돌아온 요소는 예약된
  첫 호출에서도 `layout`이 0이었다. 그 상태로 Prepare하면 Move의 From이 fallback 경로를 타 To와 같은 값이 되어
  이동 거리가 0이 됐다. 이제 유효한 레이아웃이 잡힐 때까지 프레임을 넘기며 기다린다(최대 4프레임).
- UI Builder 캔버스나 UI Viewport 윈도우가 만든 `NavElement`가 전역 `UIViewRegistry`에 등록되어,
  플레이 중인 런타임 View를 같은 주소로 덮어쓰던 문제. 오소링 중 UXML을 저장하기만 해도 그래프의 Show/Hide 명령이
  화면에 없는 에디터 요소로 흘러갔다. 이제 런타임(Player) 패널에 붙은 인스턴스만 등록한다.
- 시작 노드의 `Show Views`를 `Animated`로 지정해도 애니메이션 없이 하드컷으로 나타나던 문제.
  `UINavigatorBehaviour`가 `Start()`에서 초기 전환을 돌렸는데, `NavElement`는 같은 프레임의 패널 갱신
  (`UpdatePanels`, Update·LateUpdate 이후)에서야 붙으면서 `UIViewRegistry`에 등록된다. 그래서 시작 노드의 Show 명령이
  등록 전에 디스패치돼 조용히 버려지고, 화면은 `On Start` 값만으로 결정됐다. 이제 View가 등록될 때까지 프레임을
  양보한 뒤 초기화한다(View를 쓰지 않는 그래프를 위해 최대 3프레임까지만 대기).
- View가 패널에 붙을 때마다 인라인 스타일(`translate`/`rotate`/`scale`/`opacity`)이 초기화돼,
  UI Builder에서 오소링한 Translate가 UXML 재빌드 직후 0으로 되돌아가던 문제. 부착 시점에는
  스타일을 건드리지 않고 표시 상태만 맞추는 `UIViewVisibility.InitializeVisible`을 사용한다.

### Removed

- `NavElement`의 `start-hidden` 속성. `startup`의 `on-start`로 대체됐다.

## [0.1.0] - 2026-07-28

### Added

- 패키지 최초 생성. Runtime / Editor / Tests 어셈블리 정의.
- Project 창의 `Create > NK Studio > UI Navigation Graph` 생성 메뉴.
- 새 Navigation Graph 생성 시 별도 진입점인 `Start → Home Screen` 구성 자동 추가.
- Graph Toolkit 기반 Screen / Transition 노드와 `.uinavgraph` ScriptedImporter.
- Screen별 Back fallback 포트와 런타임 Back 입력 연결.
- `UINavigatorBehaviour`의 `Create New` / `Open Graph` 인스펙터 흐름과 그래프 유효성 검사.
- `ProjectSettings` 기반 UI Navigation Key Catalog와 프로젝트 사용처 Scan.
- Graph Toolkit 및 UI Builder 공용 Category/Key 검색형 선택기.
- UXML 문자열 호환성을 유지하는 `UIKeySelectorAttribute`.
- 영향도 확인과 실패 복구를 포함한 Category/Key Rename.
