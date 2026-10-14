# SD(치비) 비율 변환 스크립트

`sd_chibi.py`는 일반 비율 캐릭터 모델을 SD 비율(머리 크고 몸 짧은 2~3등신)로 바꾸고 Unity용 FBX로 내보냅니다.
뼈대와 메시를 같이 바꾸기 때문에 Unity에서 애니메이션을 붙여도 비율이 유지되고, 표정(셰이프 키)도 그대로 남습니다.

| 변환 전 → 후 (테스트 인형) | |
|---|---|
| 키 | 1.58 → 1.31 (x0.83) |
| 등신 | 약 6.0 → 2.8 |

## 준비

1. **Blender 4.2 이상** (5.2에서 확인함)
2. PMX(MMD) 모델이면 **MMD Tools** 확장: Blender → `Edit > Preferences > Get Extensions` → "MMD Tools" 검색 → Install
3. 개조가 허용된 팬메이드 모델. 결과물도 원래 모델의 이용 규약을 따릅니다 (재배포 금지인 모델이면 변환한 FBX도 공유하면 안 됩니다).

## 실행

명령 프롬프트(cmd)에서 한 줄로 실행합니다. Steam으로 설치한 Blender 경로 예시:

```
"C:\Program Files (x86)\Steam\steamapps\common\Blender\blender.exe" -b --python-exit-code 1 --python sd_chibi.py -- --input hoshino.pmx --dry-run
```

1. **`--dry-run`으로 뼈 분류부터 확인**합니다. 머리(head), 몸통(torso), 다리(leg) 등에 뼈가 제대로 들어갔는지 봅니다.
2. 미리보기를 만들어 비율을 확인합니다.
   ```
   ... --python sd_chibi.py -- --input hoshino.pmx --preview 비교.png
   ```
3. 마음에 들면 FBX로 내보냅니다. `.blend`로도 저장해 두면 Blender에서 손보기 좋습니다.
   ```
   ... --python sd_chibi.py -- --input hoshino.pmx --hair 1.2 --preview 비교.png --fbx hoshino_sd.fbx --blend hoshino_sd.blend
   ```

이미 Blender에서 불러와 저장한 `.blend`가 있으면 `--input` 대신 파일을 앞에 둡니다:
`blender.exe -b hoshino.blend --python sd_chibi.py -- --fbx hoshino_sd.fbx`

## 옵션

| 옵션 | 기본값 | 뜻 |
|---|---|---|
| `--head` | 1.45 | 머리 크기 배율. 1.3이면 자연스럽고, 1.8이면 아주 큰 머리 |
| `--hair` | 머리와 같음 | 머리카락 뼈 배율. 긴 머리가 바닥까지 늘어지면 1.0~1.3 |
| `--neck` | 0.5 | 목 길이 배율 |
| `--torso` | 0.85 | 몸통 배율 |
| `--arm` | 0.75 | 팔 배율 |
| `--leg-length` | 0.55 | 다리 길이 배율 |
| `--leg-width` | 0.85 | 다리 굵기와 발 크기 배율 |
| `--arms-down` | (그대로) | 팔을 수평에서 이 각도만큼 내림. T포즈 모델을 애니메이션 없이 세워 둘 때 60~70. Humanoid 애니메이션을 쓸 거면 넣지 마세요 |

호시노(zero3d 무료판)에 쓴 값: `--head 1.45 --hair 1.1 --arms-down 65` → 키 1.28m → 1.08m, 약 2.2등신, 팔을 내린 SD 모습 (1.8은 머리가 너무 커서 줄임)

## 동작 방식

- 뼈 이름으로 부위를 찾습니다. MMD 표준 이름(頭, 首, 上半身, 足, ひざ, 足首 …), 영어(Head, Spine, UpperLeg …), VRM(J_Bip_C_Head …)을 알아봅니다.
- 부위마다 관절을 중심으로 크기를 바꾸고, 정점은 뼈 가중치대로 섞어서 옮깁니다. 셰이프 키도 같은 방식으로 옮깁니다.
- 다리를 줄인 만큼 몸 전체를 내려서 **발바닥이 원래 바닥 높이**에 오게 합니다.
- IK 뼈(足ＩＫ 등)는 위치가 같은 발목 뼈를 따라갑니다.
- FBX는 Unity 기준(Y 위, 단위 m, Apply Transform)으로 내보냅니다. Unity에서 루트 회전 0, 크기 1, 정면 +Z로 들어오는 것을 확인했습니다.
- MMD 재질은 FBX가 못 읽는 셰이더라서, 내보내기 직전에 기본 텍스처·색·투명도를 일반 셰이더에 연결해서 텍스처가 FBX에 같이 들어가게 합니다. (`--blend`로 저장한 파일은 MMD 재질 그대로입니다)
- Unity에서는 `Tools > Hoshino Mascot > 3. Set Up Selected Model Materials`로 머티리얼을 정리하세요.

## 알아둘 것

- MMD 물리(강체·조인트)는 FBX에 넣지 않습니다. Unity에서는 따로 흔들림(SpringBone, Magica Cloth 등)을 붙여야 합니다.
- Unity에서 Rig를 **Generic**으로 두면 바로 마스코트로 쓸 수 있습니다 (애니메이션 없이 숨쉬기·흔들림 효과).
- **Humanoid**(Mixamo 애니메이션, 커서 따라보기)로 쓰려면 일본어 뼈 이름을 직접 연결해야 할 수 있습니다.
