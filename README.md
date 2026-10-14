# Hoshino Desktop Mascot

블루 아카이브 **타카나시 호시노**를 바탕화면에 띄워 두는 Unity 데스크톱 마스코트 개인 프로젝트입니다.
배경이 투명하고 항상 맨 위에 떠 있으며, 클릭하면 말을 하고, 끌어서 옮기거나 던질 수 있고, 오래 두면 낮잠을 잡니다.
Android/iOS로 빌드하면 휴대폰에서도 앱으로 돌아가고 (배경 위에 캐릭터가 섭니다), Android에서는 홈 화면 위에 띄울 수도 있습니다 (실험).

> 3D 모델은 들어 있지 않습니다. 개조를 허용하는 팬메이드 모델을 직접 받아서 쓰세요.

## 구성

| 폴더 | 내용 |
|---|---|
| [HoshinoMascot/](HoshinoMascot/) | Unity 스크립트 (행동, 말풍선, 오른쪽 클릭·길게 누르기 메뉴, 모바일 화면, 설정 메뉴). 설치 순서는 [README](HoshinoMascot/README.md) |
| [blender/](blender/) | 일반 비율 모델을 SD(치비) 비율로 바꾸고 Unity용 FBX로 내보내는 Blender 스크립트. 사용법은 [README](blender/README.md) |

## 흐름

1. 팬메이드 모델 준비 (PMX, FBX 등). 이용 규약에서 개조와 형식 변환이 되는지 확인
2. Blender + MMD Tools로 `blender/sd_chibi.py` 실행 → SD 비율 FBX
3. Unity(Built-In Render Pipeline)에 UniWindowController, lilToon(선택), `HoshinoMascot` 폴더를 넣고 `Tools > Hoshino Mascot` 메뉴로 설정
4. Windows로 빌드해서 실행. 휴대폰은 Android/iOS로 빌드 ([모바일](HoshinoMascot/README.md#모바일-androidios)). Android APK는 `Tools > Hoshino Mascot > 5. Build Android APK` 한 번으로 만들어집니다

확인한 환경: Windows 11, Unity 6000.3.18f1, Blender 5.2.2, UniWindowController 0.9.8, lilToon 2.3.4
(모바일과 Android 홈 화면 띄우기는 컴파일까지만 확인했고 실제 기기에서는 아직 확인하지 않았습니다)

## 주의

- 블루 아카이브의 팬 프로젝트이며 넥슨, Yostar와 관계가 없습니다. [「ブルーアーカイブ」二次創作ガイドライン](https://bluearchive.jp/fankit/Guidelines)을 따릅니다.
- 게임에서 추출한 모델은 쓰지 않습니다.
- 모델의 이용 규약이 재배포를 금지하면, 그 모델을 넣어 빌드한 실행 파일도 다른 사람에게 주면 안 됩니다.
- 기본 대사는 캐릭터 말투를 참고해 새로 쓴 것이며 게임 속 대사가 아닙니다.

## 라이선스

이 저장소의 코드와 문서는 [MIT 라이선스](LICENSE)입니다.
블루 아카이브와 캐릭터에 관한 권리는 각 권리자에게 있으며, MIT 라이선스는 여기에 적용되지 않습니다. 모델과 함께 쓸 때는 그 모델의 이용 규약을 따르세요.
