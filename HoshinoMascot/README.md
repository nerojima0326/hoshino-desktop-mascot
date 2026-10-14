# 호시노 데스크톱 마스코트 (Unity)

바탕화면 위에 캐릭터를 띄워 두는 Unity 프로젝트용 스크립트 모음입니다.
배경은 투명하고 항상 맨 위에 떠 있으며, 캐릭터가 없는 곳은 클릭이 아래 창으로 통과합니다.

## 할 수 있는 것

| 조작 | 반응 |
|---|---|
| 왼쪽 클릭 | 반응하고 말을 합니다. 자고 있으면 깨어납니다. 연타하면 투덜댑니다. |
| 왼쪽 드래그 | 들어서 옮깁니다. 끌면 진자처럼 흔들리고, 놓으면 떨어집니다. 휙 던질 수도 있습니다. |
| 오른쪽 클릭 | 메뉴: 말 걸기 / 낮잠 재우기 / 크게 / 작게 / 종료 |
| 말풍선 클릭 | 말풍선을 닫습니다. |
| 가만히 두기 | 가끔 혼잣말을 하고, 돌아다니고(걷기 애니메이션이 있을 때), 3분이 지나면 낮잠을 잡니다. |

- 켤 때 시간대(아침/낮/저녁/밤)에 맞는 인사를 합니다.
- 머리가 마우스 커서를 따라봅니다 (휴머노이드 모델 + IK Pass 켰을 때).
- 크기와 위치는 다음에 켤 때도 기억합니다.
- 애니메이션이 하나도 없어도 숨쉬기, 찌그러짐, 흔들림 같은 효과로 움직입니다.

---

## 준비물

- **Unity 2022.3 LTS 이상** (Unity 6도 됩니다)
- **Windows 10/11**
- 호시노 모델 (FBX, VRM, 또는 PMX를 FBX로 바꾼 것). 팬메이드 모델은 이용 규약을 꼭 확인하세요.
- [UniWindowController](https://github.com/kirurobo/UniWindowController): 투명 창을 만들어 주는 무료 오픈소스 라이브러리 (MIT 라이선스)

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
   - 마스코트 스크립트 3개가 붙고, 카메라가 투명 배경용으로 설정됩니다.
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

## 애니메이션 붙이기 (선택)

애니메이션이 없어도 동작하지만, 있으면 훨씬 살아 있는 느낌이 납니다.

1. [Mixamo](https://www.mixamo.com)에서 대기(Idle), 걷기(Walking), 매달림(Hanging 등), 잠(Sleeping 등) 애니메이션을 받습니다.
   걷기는 **In Place**에 체크하고 받으세요.
2. 받은 FBX도 `Rig: Humanoid`로 바꿉니다.
3. Animator Controller를 만들어서 모델의 Animator에 넣고, 아래 파라미터를 추가합니다. **이름과 타입이 정확해야** 합니다.

| 파라미터 | 타입 | 스크립트가 넣는 값 | 연결 예시 |
|---|---|---|---|
| `Speed` | Float | 걸을 때 1, 아니면 0 | Idle ↔ Walk (Speed > 0.5) |
| `Grabbed` | Bool | 들려 있거나 떨어지는 중이면 true | Any State → Hanging |
| `Sleeping` | Bool | 낮잠 중이면 true | Idle ↔ Sleep |
| `Poke` | Trigger | 클릭했을 때 | Idle → 놀라는 동작 → Idle |

- `Speed` 파라미터가 없으면 돌아다니지 않습니다. 미끄러지듯 걷는 걸 막기 위해서입니다.
- 머리가 커서를 따라보게 하려면 Animator 창의 레이어 톱니바퀴에서 **IK Pass**를 켜세요.

---

## 바꿔 볼 만한 설정

모델에 붙은 컴포넌트의 Inspector에서 바꿀 수 있습니다.

**Mascot Controller**
- `Ground Offset Pixels`: 바닥 높이. 작업 표시줄 위에 서게 하려면 작업 표시줄 높이(보통 48)로.
- `Model Yaw Offset`: 모델이 뒤돌아 있으면 `180`
- `Sleep After Seconds`: 몇 초 동안 안 건드리면 잘지 (0이면 안 잠)
- `Chatter Seconds Range`: 혼잣말 간격
- `Walk Speed`, `Gravity`: 속도는 "초당 캐릭터 키" 단위라서 모델 크기와 상관없이 비슷하게 움직입니다.

**Mascot Speech**
- 상황별 대사 목록을 마음대로 고치거나 늘릴 수 있습니다.
- 말풍선 색, 글자 크기, 표시 시간

**Mascot Menu**
- 메뉴 색, 크기, 크게/작게 배율

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

---

## 파일 구성

```
HoshinoMascot/
├─ Runtime/
│  ├─ MascotController.cs   행동 (대기, 걷기, 들기, 떨어지기, 낮잠), 클릭과 드래그
│  ├─ MascotSpeech.cs       말풍선과 대사
│  ├─ MascotMenu.cs         오른쪽 클릭 메뉴
│  ├─ MascotLookAt.cs       커서 따라보기 (자동으로 붙음)
│  ├─ MascotInput.cs        마우스 입력 (클릭 통과 상태에서도 커서 위치를 읽음)
│  └─ MascotUiSkin.cs       말풍선과 메뉴의 글꼴, 텍스처 생성
└─ Editor/
   └─ MascotSetup.cs        Tools > Hoshino Mascot 메뉴
```

## 주의

- 개인적으로 쓰는 용도로 만들었습니다. 만든 걸 배포하거나 공유하려면 **모델의 이용 규약**과 **블루 아카이브 2차 창작 가이드라인**을 먼저 확인하세요. 대부분 상업적 이용은 금지되어 있습니다.
- 게임에서 추출한 모델은 쓰지 마세요.
- 기본 대사는 캐릭터 말투를 참고해서 새로 쓴 것이고, 게임 속 대사가 아닙니다.
