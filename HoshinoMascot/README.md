# 호시노 데스크톱 마스코트 (Unity)

바탕화면 위에 캐릭터를 띄워 두는 Unity 프로젝트용 스크립트 모음입니다.
배경은 투명하고 항상 맨 위에 떠 있으며, 캐릭터가 없는 곳은 클릭이 아래 창으로 통과합니다.
같은 씬을 Android/iOS로 빌드하면 휴대폰 앱으로도 돌아가고, Android에서는 [홈 화면 위에 띄울](#android-홈-화면에-띄우기-실험) 수도 있습니다. [모바일](#모바일-androidios)을 보세요.

## 할 수 있는 것

| 조작 | 반응 |
|---|---|
| 왼쪽 클릭 | 반응하고 말을 합니다. 자고 있으면 깨어납니다. 연타하면 투덜댑니다. |
| 왼쪽 드래그 | 들어서 옮깁니다. 끌면 진자처럼 흔들리고, 놓으면 떨어집니다. 휙 던질 수도 있습니다. |
| 오른쪽 클릭 | 메뉴: 말 걸기 / 낮잠 재우기 / 앉기 / 크게 / 작게 / 종료 |
| 말풍선 클릭 | 말풍선을 닫습니다. |
| 가만히 두기 | 가끔 혼잣말을 하고, 돌아다니거나 앉아서 쉬고, 3분이 지나면 앉은 채로 꾸벅꾸벅 낮잠을 잡니다. |

모바일에서는 터치로 합니다.

| 조작 | 반응 |
|---|---|
| 탭 | 왼쪽 클릭과 같습니다. |
| 끌기 | 들어서 옮기고, 휙 던질 수 있습니다. |
| 길게 누르기 | 메뉴가 열립니다 (오른쪽 클릭 대신). iOS에는 종료가 없고, Android 앱 화면에는 **홈 화면에 띄우기**가 더 있습니다. |
| 두 손가락 벌리기/오므리기 | 크기를 바꿉니다. |
| 뒤로 가기 (Android) | 메뉴가 열려 있으면 닫고, 아니면 인사하고 종료합니다. |
| 앱으로 돌아오기 | 1분 넘게 나가 있었으면 다시 인사합니다. 3분 넘게 비워 두면 그동안 낮잠이 들어 있습니다. |

- 켤 때 시간대(아침/낮/저녁/밤)에 맞는 인사를 합니다.
- 머리가 마우스 커서를 따라봅니다. 모바일에서는 손가락을 따라보다가, 손을 떼면 화면 앞(선생님)을 봅니다.
- 크기와 위치는 다음에 켤 때도 기억합니다.
- **애니메이션 파일이 없어도 움직입니다.** `MascotMotion`이 뼈를 직접 돌려서 졸리고 느긋한 호시노답게 움직입니다.
  - 서 있을 때: 구부정하게 고개를 갸웃하고 체중을 좌우로 옮기며 흔들흔들
  - 가끔 하는 행동: 기지개 하품, 서서 꾸벅꾸벅 졸다가 화들짝, 흔들흔들, 두리번
  - 느릿느릿 뒤뚱뒤뚱 걷기, 손 짚고 기대앉기, 앉아서 고개 기대고 낮잠, 들리면 다리 대롱대롱, 떨어지면 팔 허우적
- **표정이 상황마다 바뀝니다** (MMD 표정 셰이프 키를 씁니다).
  - 평소엔 졸린 반쯤 감은 눈, 찌르면 "으헤~" 웃음(가끔 윙크·홍조), 연타하면 뾰로통, 들면 당황, 세게 떨어지면 어질어질, 하품할 때 입 크게 벌리고 눈물 찔끔, 잘 때 눈 감기
  - 눈 깜빡임, 말할 때 입 모양(あいうえお)

---

## 준비물

- **Unity 2022.3 LTS 이상** (Unity 6도 됩니다)
- **Windows 10/11** (모바일로 빌드한다면 Android 또는 iOS 기기도)
- 호시노 모델 (FBX, VRM, 또는 PMX를 FBX로 바꾼 것). 팬메이드 모델은 이용 규약을 꼭 확인하세요.
- [UniWindowController](https://github.com/kirurobo/UniWindowController): 투명 창을 만들어 주는 무료 오픈소스 라이브러리 (MIT 라이선스). Windows용이라 모바일에서는 쓰지 않습니다.

---

## 설정 순서

### 1. 프로젝트 만들기
Unity Hub에서 **3D (Built-In Render Pipeline)** 템플릿으로 새 프로젝트를 만드세요.
URP 템플릿도 되지만 설정할 게 늘어납니다. 아래 [문제 해결](#문제-해결)을 보세요.

> Unity 6에는 URP 템플릿(**Universal 3D**)만 기본으로 들어 있습니다. 템플릿 목록에서 **3D (Built-In Render Pipeline)** 를 찾아 고르고, 오른쪽에 내려받기 버튼이 있으면 먼저 내려받으세요.

### 2. UniWindowController 설치
`Window > Package Manager` → 왼쪽 위 `+` → **Add package from git URL...** → 아래 주소를 붙여넣기:

```
https://github.com/kirurobo/UniWindowController.git#upm
```

> PC에 Git이 설치되어 있어야 합니다. Git이 없으면 GitHub의 Releases 페이지에서 `.unitypackage`를 받아 `Assets > Import Package > Custom Package`로 가져와도 됩니다.

### 3. 이 폴더 넣기
`HoshinoMascot` 폴더를 통째로 프로젝트의 `Assets` 폴더 안에 복사하세요.

### 4. 모델 가져오기
- **FBX**: Assets에 드래그 → 모델 선택 → Inspector의 `Rig` 탭 → `Animation Type: Humanoid` → Apply
- **VRM**: [UniVRM](https://github.com/vrm-c/UniVRM)을 설치하고 `.vrm` 파일을 Assets에 드래그
- **PMX (MMD)**: Blender + mmd_tools 애드온으로 불러온 뒤 FBX로 내보내서 FBX처럼 처리

애니메이션 느낌을 내려면 툰 셰이더 **lilToon**(무료, MIT)을 먼저 설치하세요. 아래 5-3 메뉴가 lilToon이 있으면 알아서 lilToon으로 맞춥니다.
- `Window > Package Manager` → `+` → **Add package from git URL...** →
  ```
  https://github.com/lilxyzw/lilToon.git?path=Assets/lilToon#2.3.4
  ```

### 5. 마스코트로 만들기
1. 모델을 Hierarchy(씬)에 끌어다 놓습니다.
2. 모델의 **가장 위 오브젝트**를 선택합니다.
3. (MMD에서 바꾼 FBX라면) `Tools > Hoshino Mascot > 3. Set Up Selected Model Materials`
   - FBX 안의 텍스처와 머티리얼을 `모델이름_Materials` 폴더로 꺼내고, 속눈썹 같은 투명한 부분이 제대로 보이게(Cutout) 맞춥니다.
   - lilToon이 있으면 lilToon으로, 없으면 Standard로 만듭니다. lilToon일 때 머리카락·옷·피부에는 윤곽선과 그림자를 넣습니다.
   - lilToon을 나중에 설치했으면 이 메뉴를 한 번 더 누르세요.
4. 메뉴 `Tools > Hoshino Mascot > 2. Make Selected Model a Mascot` 을 누릅니다.
   - 마스코트 스크립트 6개(말풍선, 행동, 메뉴, 동작, 모바일 화면, 홈 화면 띄우기)가 붙고, 카메라가 투명 배경용으로 설정됩니다.
   - 캐릭터 키가 화면 높이의 1/4쯤 되게 카메라 크기를 맞춥니다.

### 6. 투명 창 켜기
1. Project 창에서 `Packages > UniWindowController > Runtime > Prefabs` 안의 **UniWindowController** 프리팹을 씬에 끌어다 놓습니다.
2. 그 오브젝트를 선택하고 Inspector에서 체크합니다:
   - ✅ **Is Transparent**
   - ✅ **Is Topmost** (항상 위)
   - ✅ **Should Fit Monitor** (창을 모니터 전체 크기로)
   - **Hit Test Type: Opacity** (기본값 그대로. 말풍선과 메뉴도 클릭되게 하려면 Opacity여야 합니다)
3. Inspector의 **초록색 버튼**을 눌러 Player Settings를 맞춥니다.
   (또는 `Tools > Hoshino Mascot > 1. Apply Player Settings`)

> `DragMoveCanvas` 프리팹은 넣지 마세요. 캐릭터 드래그와 겹칩니다.

### 7. 빌드해서 실행
`File > Build Settings` (Unity 6은 `File > Build Profiles`) → **Windows** → **Build** → 만들어진 `.exe` 실행.

> **에디터의 Play 모드에서는 배경이 투명해지지 않습니다.** 클릭, 드래그, 말풍선, 메뉴 같은 동작 확인은 에디터에서 하고, 투명 창은 빌드해서 확인하세요.

---

## 모바일 (Android/iOS)

휴대폰 앱은 보통 다른 앱이나 홈 화면 위에 투명하게 떠 있을 수 없습니다.
그래서 모바일에서는 기본으로 **앱 화면에 배경을 깔고 그 위에서** 호시노가 돌아다닙니다. 행동, 말풍선, 표정, 낮잠은 데스크톱과 같습니다.
Android는 "다른 앱 위에 표시" 권한으로 [홈 화면에 띄울](#android-홈-화면에-띄우기-실험) 수도 있습니다. iOS는 OS가 막고 있어서 안 됩니다.

### 빌드
1. 위 1~5단계는 그대로 합니다. 6단계(UniWindowController)는 모바일만 쓴다면 건너뛰어도 됩니다.
   - 씬에 UniWindowController가 있어도 됩니다. 모바일 빌드에서는 그 스크립트가 빠지고, 실행할 때 로그에 스크립트가 없다는 경고만 뜹니다.
2. Unity Hub에서 쓰는 Unity 버전에 **Android Build Support** (또는 **iOS Build Support**) 모듈을 추가합니다.
3. `File > Build Profiles` (Unity 2022는 `File > Build Settings`) → **Android** (또는 **iOS**) → **Switch Platform**
4. Android: 휴대폰의 개발자 옵션에서 USB 디버깅을 켜고 연결한 뒤 **Build And Run**. 또는 **Build**로 만든 `.apk`를 휴대폰에 옮겨 설치합니다.
   iOS: **Build**로 Xcode 프로젝트를 만들고, Mac의 Xcode에서 서명한 뒤 기기에 설치합니다.

> 에디터에서 터치를 미리 보려면 Game 창 왼쪽 위 드롭다운에서 **Simulator**를 고르세요 (Device Simulator). 마우스가 터치처럼 동작해서 탭, 끌기, 길게 누르기, 노치를 피하는 배치를 확인할 수 있습니다. 두 손가락 핀치는 실제 기기에서 확인하세요.

### 화면 설정 (Mascot Mobile)
모바일에서만 동작하고, 데스크톱에서는 아무 일도 하지 않습니다. 컴포넌트가 없으면 모바일에서 기본값으로 저절로 붙습니다.
- `Background Color`: 배경색 (기본은 연한 하늘색)
- `Background Image`: 배경 그림. 그림 파일을 Assets에 드래그해서 여기에 넣으면 화면을 꽉 채우게 깔립니다. 화면을 돌려도 맞춰집니다.
- `Keep Screen On`: 화면이 저절로 꺼지지 않게 합니다 (배터리를 더 씁니다).

### Android 홈 화면에 띄우기 (실험)
호시노가 앱 밖으로 나와서 홈 화면과 다른 앱 위에 떠 있습니다. 데스크톱 마스코트처럼 돌아다니고, 찌르고, 들어서 던질 수 있습니다.

> 아직 실제 기기에서 확인하지 못한 기능입니다. 안 되면 아래 [문제 해결](#문제-해결)을 보세요.

**준비 (한 번만)**
1. `Tools > Hoshino Mascot > 4. Apply Android Settings`
   - **Preserve Framebuffer Alpha**를 켭니다. 꺼져 있으면 캐릭터 주변이 검게 나옵니다.
   - Unity 2023 이상은 **Application Entry Point**를 **Activity**로 바꿉니다 (홈 화면 띄우기가 쓰는 방식).
2. (권장) `Player Settings > Splash Image > Show Splash Screen`을 끄세요. 켜 두면 홈 화면에 띄울 때마다 Unity 로고가 잠깐 홈 화면 위에 나옵니다. (Unity 2022 Personal은 끌 수 없습니다)
3. 그대로 Android로 빌드합니다. "다른 앱 위에 표시" 권한과 홈 화면 서비스는 빌드할 때 매니페스트에 저절로 들어갑니다 (`Editor/MascotAndroidBuild.cs`).

**쓰는 법**
- 앱에서 호시노를 길게 눌러 **홈 화면에 띄우기**
- 처음에는 설정 화면이 열립니다. 이 앱의 **다른 앱 위에 표시**를 허용하고 뒤로 돌아오면, 앱이 닫히고 호시노가 홈 화면에 나타납니다.
- 내리기: 호시노를 길게 눌러 **종료**, 알림의 **끄기**, 또는 앱을 다시 열면 호시노가 앱 화면으로 돌아옵니다.
- 알림은 Android 13부터 알림 권한을 켜야 보입니다 (`설정 > 애플리케이션 > 이 앱 > 알림`). 알림이 안 보여도 호시노는 떠 있습니다.

**알아 둘 것**
- Android 12부터는 다른 앱 위의 창이 터치를 아래로 넘길 수 없게 막혀 있습니다. 그래서 호시노 주변(머리 위 말풍선 자리 포함)만 작은 창으로 띄우고 호시노를 따라 옮깁니다. **그 창 안은 아래 아이콘이 눌리지 않으니**, 가리면 호시노를 다른 곳으로 옮기세요. 말풍선 너비(`Mascot Overlay > Bubble Max Width`)를 줄이면 창도 좁아집니다.
- 들고 있을 때, 떨어지는 동안, 메뉴가 열려 있을 때, 두 손가락으로 크기를 바꿀 때는 잠깐 화면 전체를 씁니다. 이때는 다른 앱을 누를 수 없습니다.
- 캐릭터 크기는 기기 화면 짧은 변의 1/4 정도로 시작합니다. 메뉴의 크게/작게로 바꾸세요.
- 홈 화면의 호시노는 앱과 다른 프로세스에서 Unity를 하나 더 띄운 것입니다. 배터리를 아끼려고 가만히 있을 때는 초당 30프레임으로 그리고, 화면이 꺼지면 멈춥니다.
- 창이 호시노를 따라 움직일 때 한 프레임 늦어서 걸을 때 살짝 떨려 보일 수 있습니다.

---

## 애니메이션 붙이기 (선택)

`MascotMotion`이 기본 동작을 다 만들어 주므로 꼭 필요하지는 않습니다. 더 자연스러운 동작을 원할 때 진짜 애니메이션을 붙이세요.
Animator에 Animator Controller를 넣으면 `MascotMotion`은 저절로 꺼집니다.

1. [Mixamo](https://www.mixamo.com)에서 대기(Idle), 걷기(Walking), 매달림(Hanging 등), 잠(Sleeping 등) 애니메이션을 받습니다.
   걷기는 **In Place**에 체크하고 받으세요.
2. 받은 FBX도 `Rig: Humanoid`로 바꿉니다.
3. Animator Controller를 만들어서 모델의 Animator에 넣고, 아래 파라미터를 추가합니다. **이름과 타입이 정확해야** 합니다.

| 파라미터 | 타입 | 스크립트가 넣는 값 | 연결 예시 |
|---|---|---|---|
| `Speed` | Float | 걸을 때 1, 아니면 0 | Idle ↔ Walk (Speed > 0.5) |
| `Grabbed` | Bool | 들려 있거나 떨어지는 중이면 true | Any State → Hanging |
| `Sleeping` | Bool | 낮잠 중이면 true | Idle ↔ Sleep |
| `Sitting` | Bool | 앉아 있으면 true | Idle ↔ Sit |
| `Poke` | Trigger | 클릭했을 때 | Idle → 놀라는 동작 → Idle |

- Animator Controller를 쓸 때 `Speed` 파라미터가 없으면 돌아다니지 않고, `Sitting`이 없으면 앉지 않습니다. 미끄러지듯 걷는 걸 막기 위해서입니다.
- 머리가 커서를 따라보게 하려면 Animator 창의 레이어 톱니바퀴에서 **IK Pass**를 켜세요.

---

## 바꿔 볼 만한 설정

모델에 붙은 컴포넌트의 Inspector에서 바꿀 수 있습니다.

**Mascot Controller**
- `Ground Offset Pixels`: 바닥 높이. 작업 표시줄 위에 서게 하려면 작업 표시줄 높이(보통 48)로.
- `Mobile Ground Offset Pixels`: 모바일의 바닥 높이. 홈 표시줄·내비게이션 바 위에서부터 잽니다 (단위 dp).
- `Long Press Seconds`: 모바일에서 메뉴가 열리기까지 누르고 있는 시간
- `Pinch To Resize`: 모바일에서 두 손가락으로 크기 바꾸기
- `Model Yaw Offset`: 모델이 뒤돌아 있으면 `180`
- `Sleep After Seconds`: 몇 초 동안 안 건드리면 잘지 (0이면 안 잠)
- `Chatter Seconds Range`: 혼잣말 간격
- `Walk Speed`, `Gravity`: 속도는 "초당 캐릭터 키" 단위라서 모델 크기와 상관없이 비슷하게 움직입니다.
- `Sit Chance`, `Sit Seconds Range`: 서 있다가 앉을 확률과 앉아 있는 시간

**Mascot Motion** (뼈를 직접 움직이는 동작)
- `Steps Per Second`, `Leg Swing`, `Knee Bend`, `Arm Swing`: 걸음 빠르기와 다리·팔 흔드는 각도
- `Sit Depth`: 앉을 때 엉덩이를 내리는 정도. 바닥에 파묻히거나 떠 있으면 조절하세요.
- `Head Tilt`, `Slouch`: 고개 갸웃하는 각도, 구부정한 정도
- `Idle Action Interval`: 하품·졸기 같은 행동 사이 간격(초)
- `Blink Shapes`, `Talk Shapes`: 눈 감기, 말할 때 입 모양 셰이프 키 이름 (모델마다 다르면 여기에 적으세요)
- `Faces`: 상황별 표정. `셰이프키이름:세기` 를 쉼표로 적습니다 (예: `笑い:1, ω:0.7`). 이름(normal, relaxed, happy, wink, blush, surprised, annoyed, dizzy, held, yawn, doze, sleep)은 바꾸지 마세요.
- `Look At Cursor`: 커서 쳐다보기

**Mascot Speech**
- 상황별 대사 목록을 마음대로 고치거나 늘릴 수 있습니다.
- 말풍선 색, 글자 크기, 표시 시간
- `Font`: 말풍선과 메뉴 글꼴(TTF). 비워 두면 OS 글꼴(맑은 고딕 등)을 씁니다.

**Mascot Menu**
- 메뉴 색, 크기, 크게/작게 배율
- `Touch Item Height`: 모바일에서 메뉴 항목 높이 (손가락으로 누르기 좋게 더 높습니다)

**Mascot Mobile**: 위 [화면 설정](#화면-설정-mascot-mobile)을 보세요.

**Mascot Overlay** (Android 홈 화면에 띄웠을 때)
- `Bubble Max Width`: 말풍선 최대 너비(dp). 홈 화면 창이 이만큼 넓어집니다.
- `Idle Frame Rate`, `Active Frame Rate`: 가만히 있을 때, 들고 있거나 떨어질 때 초당 프레임

> 대사 기본값은 컴포넌트를 처음 붙일 때만 들어갑니다. 코드를 고친 뒤 새 기본값을 쓰고 싶으면 컴포넌트 이름 옆 ⋮ 메뉴에서 **Reset**을 누르세요.

---

## 문제 해결

**UniWindowController를 넣었더니 `'EventSystems' does not exist` 에러가 나요**
- **Unity UI** 패키지가 없어서 그렇습니다. `Window > Package Manager` → `Unity Registry` → **Unity UI** (`com.unity.ugui`) 설치. 템플릿으로 만든 프로젝트에는 보통 들어 있습니다.

**배경이 검은색으로 나와요**
- Player Settings에서 `Use DXGI flip model swapchain for D3D11`이 꺼져 있는지 확인하세요.
- Graphics API가 **Direct3D11**인지 확인하세요. Direct3D12에서는 투명 창이 안 됩니다.
- 카메라의 Background 알파가 0인지, HDR이 꺼져 있는지 확인하세요.
- **URP**를 쓴다면: 카메라의 `Output > HDR`을 끄고, URP 에셋의 Post-processing에서 `Alpha Processing`을 켜야 합니다.

**캐릭터를 클릭해도 반응이 없어요**
- UniWindowController의 `Hit Test Type`이 **Opacity**인지 확인하세요.
- 모델에 콜라이더가 없으면 자동으로 만들어집니다. 직접 만든 콜라이더가 있다면 캐릭터를 잘 덮는지 확인하세요.

**캐릭터가 뒤돌아 있어요**
- `Mascot Controller > Model Yaw Offset`을 `180`으로 바꾸세요.

**캐릭터가 너무 크거나 작아요**
- 오른쪽 클릭 메뉴의 크게/작게를 쓰거나, 카메라의 `Orthographic Size`를 바꾸세요. 값이 작을수록 크게 보입니다.

**캐릭터가 작업 표시줄에 가려지거나 떠 있어요**
- `Ground Offset Pixels`를 조절하세요.

**종료는 어떻게 하나요?**
- 캐릭터 오른쪽 클릭 → **종료**. 창에 포커스가 있을 때는 `Alt + F4`도 됩니다.
- Android는 캐릭터를 길게 눌러 **종료**하거나 뒤로 가기 버튼. iOS는 다른 앱처럼 홈으로 나가면 됩니다.

**모바일에서 한글이 네모(□)로 나와요**
- 기기에 맞는 한글 글꼴을 못 찾은 것입니다. 한글 TTF 글꼴(예: 무료인 [Noto Sans KR](https://fonts.google.com/noto/specimen/Noto+Sans+KR))을 Assets에 넣고 `Mascot Speech > Font`에 넣으세요.

**모바일에서 캐릭터가 화면 아래에 묻히거나 너무 떠 있어요**
- `Mascot Controller > Mobile Ground Offset Pixels`를 조절하세요.

**Android 홈 화면에 띄웠더니 호시노 주변이 검게 나와요**
- `Tools > Hoshino Mascot > 4. Apply Android Settings`를 누르고 다시 빌드하세요 (Preserve Framebuffer Alpha).

**Android 홈 화면에 띄우기를 눌렀는데 호시노가 안 나타나요**
- 메뉴에 **홈 화면에 띄우기**가 없으면 `Plugins/Android/MascotOverlayService.java`가 빌드에 안 들어간 것입니다. 그 파일을 선택하고 Inspector에서 Android가 체크되어 있는지 보세요.
- Unity 2023 이상은 4번 메뉴로 Application Entry Point를 **Activity**로 바꿨는지 확인하세요.
- 컴퓨터에 연결해서 `adb logcat -s HoshinoOverlay Unity`로 로그를 보면 이유가 나옵니다.

---

## 파일 구성

```
HoshinoMascot/
├─ Runtime/
│  ├─ MascotController.cs   행동 (대기, 걷기, 앉기, 들기, 떨어지기, 낮잠), 클릭과 드래그
│  ├─ MascotMotion.cs       애니메이션 파일 없이 뼈를 직접 움직이는 동작과 표정
│  ├─ MascotSpeech.cs       말풍선과 대사
│  ├─ MascotMenu.cs         오른쪽 클릭(모바일은 길게 누르기) 메뉴
│  ├─ MascotMobile.cs       모바일 화면 설정 (배경, 화면 꺼짐)
│  ├─ MascotOverlay.cs      Android 홈 화면에 띄우기 (창 위치, 카메라 맞추기)
│  ├─ MascotScreen.cs       행동에 쓰는 기기 화면 전체 좌표 (홈 화면 창과 기기 화면 사이 변환)
│  ├─ MascotLookAt.cs       커서 따라보기 (자동으로 붙음)
│  ├─ MascotInput.cs        마우스·터치 입력 (클릭 통과 상태에서도 커서 위치를 읽음)
│  └─ MascotUiSkin.cs       말풍선과 메뉴의 글꼴, 텍스처 생성, 화면 배율과 안전 영역
├─ Plugins/Android/
│  └─ MascotOverlayService.java  홈 화면 창을 띄우는 Android 서비스 (Unity를 별도 프로세스에서 하나 더 띄움)
└─ Editor/
   ├─ MascotSetup.cs                  Tools > Hoshino Mascot 메뉴
   ├─ MascotAndroidBuild.cs           Android 빌드 때 권한과 서비스를 매니페스트에 넣음
   └─ MascotMaterialPostprocessor.cs  MMD 모델 텍스처 연결 (메뉴 3에서 씀)
```

## 주의

- 개인적으로 쓰는 용도로 만들었습니다. 만든 걸 배포하거나 공유하려면 **모델의 이용 규약**과 **블루 아카이브 2차 창작 가이드라인**을 먼저 확인하세요. 대부분 상업적 이용은 금지되어 있습니다.
- 게임에서 추출한 모델은 쓰지 마세요.
- 기본 대사는 캐릭터 말투를 참고해서 새로 쓴 것이고, 게임 속 대사가 아닙니다.
