# UITK Navigation

[![Unity](https://img.shields.io/badge/Unity-6000.6%2B-000000?logo=unity&logoColor=white)](https://unity.com/releases/editor/archive)
[![UPM](https://img.shields.io/badge/UPM-Git%20URL-2ea44f)](https://docs.unity3d.com/Manual/upm-ui-giturl.html)
[![Version](https://img.shields.io/badge/version-0.2.0-blue)](https://github.com/NK-Studio/com.nkstudio.uitk-navigation/releases)
[![Documentation](https://img.shields.io/badge/docs-Korean-4c8bf5)](https://nk-studio.github.io/Packages/com.nkstudio.uitk-navigation@0.2/manual/index.html)
[![License](https://img.shields.io/badge/license-See%20License-lightgrey)](https://nk-studio.github.io/Packages/com.nkstudio.uitk-navigation@0.2/license/License.html)

UITK Navigation은 Unity UI Toolkit용 화면 전환 패키지입니다. Graph Toolkit에서 화면 흐름을 만들고, LitMotion 기반 Show/Hide 애니메이션과 패널 로컬 LIFO 팝업을 함께 구성할 수 있습니다.

<!-- IMAGE NEEDED: Navigation Graph 편집기와 실행 중인 UI 화면을 함께 보여주는 16:9 이미지. Documentation~/Images/uitk-navigation-overview.png -->

## 특징

- `.uinavgraph`를 런타임용 `UINavigationAsset`으로 자동 컴파일합니다.
- `NavElement`, `NavButton`, `NavToggle`을 UI Builder와 UXML에서 사용합니다.
- Move, Rotate, Scale, Fade 채널과 다수의 Show/Hide 프리셋을 제공합니다.
- Back/Forward 기록, 지연, Toggle 분기, Scene 및 애플리케이션 Action을 그래프로 구성합니다.
- Key Catalog에서 프로젝트 전체 View, Signal, Toggle 주소를 검색하고 변경합니다.
- `UIPopupHost`로 데이터 바인딩, 비동기 결과, 포커스 복원을 지원합니다.

## 문서

- [UITK Navigation 공식 문서](https://nk-studio.github.io/Packages/com.nkstudio.uitk-navigation@0.2/manual/index.html)
- [변경 내역](https://nk-studio.github.io/Packages/com.nkstudio.uitk-navigation@0.2/changelog/CHANGELOG.html)
- [라이선스](https://nk-studio.github.io/Packages/com.nkstudio.uitk-navigation@0.2/license/License.html)

## 요구사항

- Unity `6000.6.0b5` 이상 — Unity 6.6 권장
- LitMotion `2.0.0` 이상 — Git URL로 직접 설치
- ZLinq — Git URL로 직접 설치
- Graph Toolkit `0.5.0-exp.1`
- Input System `1.19.0`

## 설치

**Window > Package Manager > + > Install package from git URL...**에서 다음 순서로 설치합니다.

```text
https://github.com/annulusgames/LitMotion.git?path=src/LitMotion/Assets/LitMotion
https://github.com/Cysharp/ZLinq.git?path=src/ZLinq.Unity/Assets/ZLinq.Unity
https://github.com/NK-Studio/com.nkstudio.uitk-navigation.git
```

LitMotion과 ZLinq는 Git 패키지이므로 UITK Navigation의 레지스트리 의존성으로 선언하지 않습니다. 두 패키지를 먼저 설치하지 않으면 컴파일되지 않습니다.

## 시작하기

1. UI Builder에서 화면 루트를 `NavElement`로 만들고 View 주소를 지정합니다.
2. 이동 버튼은 `NavButton`, 값 분기는 `NavToggle`로 만들고 Signal 또는 Toggle 주소를 지정합니다.
3. **Create > UI Navigation > UI Navigation Graph**에서 그래프를 만듭니다.
4. `Start → UI`를 연결하고 UI 노드에 Show/Hide View와 출력 트리거를 설정합니다.
5. 패널 GameObject의 `UINavigatorBehaviour`에 `.uinavgraph`를 연결합니다.

<!-- IMAGE NEEDED: Start → Home → Settings가 연결된 그래프. Documentation~/Images/getting-started-graph.png -->

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements"
         xmlns:nav="NKStudio.UITKNavigation.Elements">
    <nav:NavElement name="home" view-category="Demo" view-key="Home">
        <nav:NavButton text="Settings"
            signal-category="Demo" signal-key="OpenSettings" />
        <startup>
            <nav:UIViewStartupSettings on-start="InstantShow"
                hide-mode="Display" auto-hide-after-show="false" />
        </startup>
    </nav:NavElement>
</ui:UXML>
```

`NavElement`는 패널에 연결되면 `Demo/Home`으로 등록됩니다. `NavButton`은 클릭할 때 `Demo/OpenSettings` 신호를 현재 Navigation Service에 전달합니다.

코드에서는 Doozy 스타일의 정적 API로 같은 Signal 출력을 실행할 수 있습니다.

```csharp
using NKStudio.UITKNavigation.Navigation;

bool handled = Signal.Send("Demo", "OpenSettings");
```

## Navigation Graph

- **Start**는 그래프당 하나인 진입점입니다.
- **UI** 노드는 진입/이탈 시 Show/Hide할 View와 Signal, Toggle, Delay, View 완료 출력을 정의합니다.
- **Use Back**을 켠 UI는 방문 기록에 Push되어 Esc 또는 마우스 뒤로 버튼으로 돌아갈 수 있습니다.
- **Set Time Scale**, **Load Scene**, **Set Active Loaded Scene**, **Unload Scene**, **Application Quit** Action을 연결할 수 있습니다.
- `.uinavgraph`를 저장하면 런타임 에셋으로 자동 컴파일됩니다.

Key는 **Tools > UI Navigation > Key Catalog**에서 관리합니다. **Scan Project**는 UXML과 Graph 사용처를 수집하며 Rename은 변경될 참조를 미리 보여준 뒤 함께 수정합니다.

### 코드 또는 AI로 그래프 생성

Editor 자동화에서는 공개 `UINavigationGraphBuilder` API로 편집 가능한 `.uinavgraph`를 만들 수 있습니다. 경로, 노드 좌표, 출력과 연결을 선언하면 Graph Toolkit 내부 타입이나 리플렉션 없이 저장되며 사용한 Key도 Catalog에 등록됩니다.

```csharp
using NKStudio.UITKNavigation.Editor.Navigation;
using NKStudio.UITKNavigation.Identity;
using UnityEngine;

var graph = UINavigationGraphBuilder.Create(
    "Assets/UI/MainNavigation.uinavgraph",
    overwrite: true);

var start = graph.AddStart(new Vector2(40, 160));
var home = graph.AddScreen("home", "Home", new Vector2(300, 100))
    .WithHistory(clearOnEnter: true)
    .ShowViewsOnEnter(new UIKey("Main", "Home"));
var settings = graph.AddScreen("settings", "Settings", new Vector2(760, 100))
    .WithHistory(useBack: true)
    .ShowViewsOnEnter(new UIKey("Main", "Settings"));

graph.Connect(start, home);
graph.Connect(
    home.AddSignalOutput(new UIKey("Main", "OpenSettings")),
    settings);
graph.Save(openGraph: true);
```

`Create`는 아직 파일을 변경하지 않고, `Save` 시점에 생성 또는 교체합니다. `overwrite: false`가 기본값이므로 기존 그래프를 실수로 덮어쓰지 않습니다.

## 전환 애니메이션

`UIViewVisibility`는 `NotVisible`, `Showing`, `Visible`, `Hiding` 상태를 관리합니다. 진행 중 반대 명령을 받으면 하나의 LitMotion 모션을 반전해 자연스럽게 이어갑니다.

- Move / Rotate / Scale / Fade 채널
- 채널별 Delay, Duration, Ease 또는 Animation Curve, Loop
- Category와 Variant로 선택하는 Show/Hide 프리셋
- 숨김 완료 후 `display: none` 또는 `visibility: hidden` 선택

## 팝업

팝업은 Navigation Graph와 분리된 패널 로컬 LIFO 스택입니다. `PanelRenderer`가 있는 GameObject에 `UIPopupHost`를 추가하고 `VisualTreeAsset`을 전달합니다.

```csharp
[SerializeField] private UIPopupHost popupHost;
[SerializeField] private VisualTreeAsset messagePopup;

UIPopupResult result = await popupHost.ShowAsync(
    messagePopup,
    new MessagePopupModel { Title = "안내" },
    configure: view =>
        view.Q("dynamic-content")?.Add(new Label("Runtime content")),
    cancellationToken);

Debug.Log($"Action={result.ActionId}, Reason={result.Reason}");
```

템플릿에는 `UIPopupView`와 `UIPopupContent`를 하나씩 둡니다. `UIPopupBackdrop`은 선택 사항이며 Back 정책은 `Close`, `Block`, `PassThrough` 중에서 선택합니다.

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements"
         xmlns:popup="NKStudio.UITKNavigation.Popup">
    <popup:UIPopupView close-on-backdrop="false" back-behavior="Close">
        <popup:UIPopupBackdrop />
        <popup:UIPopupContent>
            <ui:Label>
                <Bindings>
                    <ui:DataBinding property="text" data-source-path="Title"
                                    binding-mode="ToTarget" />
                </Bindings>
            </ui:Label>
            <popup:UIPopupActionButton text="확인"
                action-id="confirm" close-popup="true" />
        </popup:UIPopupContent>
    </popup:UIPopupView>
</ui:UXML>
```

## 테스트

Test Runner에 패키지 테스트를 표시하려면 프로젝트 `Packages/manifest.json`에 추가합니다.

```json
"testables": ["com.nkstudio.uitk-navigation"]
```

## License

사용 조건은 [License](https://nk-studio.github.io/Packages/com.nkstudio.uitk-navigation@0.2/license/License.html)를 확인하세요.
