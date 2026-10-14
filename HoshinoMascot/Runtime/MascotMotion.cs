using System.Collections.Generic;
using UnityEngine;

namespace HoshinoMascot
{
    /// <summary>
    /// 애니메이션 파일 없이 뼈와 표정(셰이프 키)을 직접 움직입니다. 졸리고 느긋한 호시노에 맞춘 동작입니다.
    /// - 서 있기: 구부정하게 고개를 갸웃하고 체중을 좌우로 옮기며 흔들흔들
    /// - 가끔 하는 행동: 기지개 하품, 서서 꾸벅꾸벅 졸다가 화들짝, 흔들흔들, 두리번
    /// - 걷기(느릿느릿 뒤뚱뒤뚱), 앉기(손 짚고 기대앉기), 낮잠(앉아서 고개 기대고 새근새근)
    /// - 들려 있을 때 다리 대롱대롱, 떨어질 때 팔 허우적, 찔렀을 때 움찔
    /// - 표정: 상황별 표정, 눈 깜빡임, 말할 때 입 모양(あいうえお)
    ///
    /// MMD 뼈 이름(センター, 上半身, 足.L ...), 휴머노이드, VRM 뼈 이름을 알아봅니다.
    /// Animator Controller로 진짜 애니메이션을 붙이면 저절로 꺼집니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class MascotMotion : MonoBehaviour
    {
        [System.Serializable]
        public class FacePreset
        {
            public string name;
            [Tooltip("셰이프키이름:세기(0~1)를 쉼표로 구분합니다. 예) 笑い:1, ω:0.6")]
            public string shapes;
            [Tooltip("이 표정일 때 눈을 깜빡일지")]
            public bool blink = true;
        }

        [Header("걷기 (느릿느릿 뒤뚱뒤뚱)")]
        [Tooltip("1초에 걷는 걸음 수")]
        public float stepsPerSecond = 1.9f;
        [Tooltip("다리를 앞뒤로 흔드는 각도")]
        public float legSwing = 22f;
        [Tooltip("다리를 앞으로 내밀 때 무릎을 굽히는 각도")]
        public float kneeBend = 30f;
        [Tooltip("팔을 앞뒤로 흔드는 각도 (작을수록 팔을 축 늘어뜨림)")]
        public float armSwing = 12f;
        [Tooltip("걸을 때 몸을 좌우로 흔드는 각도")]
        public float waddle = 5f;

        [Header("서 있을 때")]
        [Tooltip("고개를 갸웃하는 기본 각도")]
        public float headTilt = 6f;
        [Tooltip("구부정한 정도")]
        public float slouch = 6f;
        [Tooltip("가끔 하는 행동(하품, 졸기, 흔들흔들, 두리번) 사이 간격(초)")]
        public Vector2 idleActionInterval = new Vector2(7f, 15f);

        [Header("앉기와 낮잠")]
        [Range(0f, 1f), Tooltip("앉을 때 엉덩이를 내리는 정도 (엉덩이 관절 높이에 대한 비율)")]
        public float sitDepth = 0.8f;
        [Tooltip("앉을 때 다리를 벌리는 각도")]
        public float sitLegSpread = 14f;

        [Header("표정 (모델에 없는 셰이프 키는 무시합니다)")]
        [Tooltip("눈 감기 셰이프 키. 쉼표로 여러 개 적으면 먼저 찾은 것을 씁니다")]
        public string blinkShapes = "まばたき,blink,Blink,Fcl_EYE_Close,eye_close,EyeClose";
        [Tooltip("말할 때 번갈아 쓰는 입 모양")]
        public string talkShapes = "あ,い,う,え,お,Fcl_MTH_A,Fcl_MTH_I,Fcl_MTH_U,Fcl_MTH_E,Fcl_MTH_O";
        [Tooltip("눈 깜빡임 간격(초)")]
        public Vector2 blinkIntervalRange = new Vector2(2.5f, 6f);
        [Tooltip("상황별 표정")]
        public List<FacePreset> faces = DefaultFaces();

        [Header("기타")]
        public bool lookAtCursor = true;
        [Range(0f, 1f)] public float lookWeight = 0.6f;
        [Tooltip("동작이 바뀔 때 섞이는 속도 (작을수록 느긋하게)")]
        public float transitionSpeed = 3.5f;
        [Tooltip("Animator Controller가 있어도 이 동작을 쓸지")]
        public bool overrideAnimator = false;

        static List<FacePreset> DefaultFaces() => new List<FacePreset>
        {
            new FacePreset { name = "normal", shapes = "まばたき:0.3, 困る:0.25, 口角上げ:0.3" },
            new FacePreset { name = "relaxed", shapes = "なごみ:0.9, 口角上げ:0.4, 困る:0.2", blink = false },
            new FacePreset { name = "happy", shapes = "笑い:1, にこり:0.6, ω:0.7", blink = false },
            new FacePreset { name = "wink", shapes = "ウィンク:1, にやり:0.6, にこり:0.4", blink = false },
            new FacePreset { name = "blush", shapes = "笑い:0.7, 照れ:0.8, ω:0.5, 困る:0.3", blink = false },
            new FacePreset { name = "surprised", shapes = "びっくり:1, お:0.7, 上:0.7", blink = false },
            new FacePreset { name = "annoyed", shapes = "じと目:0.8, む:0.9, 怒り:0.35" },
            new FacePreset { name = "dizzy", shapes = "はちゅ目:1, ワ:0.4, 困る:0.8, 汗:1", blink = false },
            new FacePreset { name = "held", shapes = "びっくり:0.3, 困る:0.8, はぅ口:0.7, 汗:0.6" },
            new FacePreset { name = "yawn", shapes = "まばたき:0.85, ワ:1, 涙:0.6, 困る:0.5", blink = false },
            new FacePreset { name = "doze", shapes = "まばたき:0.75, 困る:0.3, 口角上げ:0.2", blink = false },
            new FacePreset { name = "sleep", shapes = "まばたき:1, 困る:0.15, あ:0.15", blink = false },
        };

        enum Action { None, Yawn, Doze, Sway, LookAround }

        class Bone
        {
            public Transform transform;
            public Quaternion restRotation;
            public Vector3 restPosition;
        }

        class Shape
        {
            public SkinnedMeshRenderer renderer;
            public int index;
            public float current;
            public float target;
        }

        class ParsedFace
        {
            public readonly List<(List<Shape> shapes, float weight)> entries = new List<(List<Shape>, float)>();
            public bool blink;
        }

        MascotController controller;
        MascotSpeech speech;
        Animator animator;
        bool initialized;

        Bone hips, spine, chest, neck, head;
        Bone upperArmL, upperArmR, lowerArmL, lowerArmR;
        Bone upperLegL, upperLegR, lowerLegL, lowerLegR;
        readonly List<Bone> bones = new List<Bone>();
        float hipHeight; // 루트 기준 엉덩이 관절 높이 (루트 로컬 단위)

        readonly List<Shape> slots = new List<Shape>();
        readonly Dictionary<string, List<Shape>> shapesByName = new Dictionary<string, List<Shape>>();
        readonly Dictionary<string, ParsedFace> parsedFaces = new Dictionary<string, ParsedFace>();
        List<Shape> blink = new List<Shape>();
        readonly List<List<Shape>> vowels = new List<List<Shape>>();

        MascotController.State state = MascotController.State.Idle;
        float walk, sit, sleep, held, fall; // 동작별 섞는 비율 (0~1)
        float walkPhase;
        float pokeTime = -100f;
        Vector2 look; // x: 좌우, y: 위아래 (도)

        Action action;
        float actionStart, actionLength, nextActionTime;
        bool dozeWokeUp;
        string reaction;
        float reactionUntil;

        float nextBlinkTime, blinkStart = -100f;
        float nextVowelTime;
        int vowel = -1;
        float vowelWeight;

        /// <summary>뼈를 찾았고, 진짜 애니메이션이 없어서 이 동작을 쓰는 중인지</summary>
        public bool IsActive
        {
            get
            {
                Initialize();
                return hips != null && (overrideAnimator || animator == null || animator.runtimeAnimatorController == null);
            }
        }

        public Transform HeadBone
        {
            get
            {
                Initialize();
                return head?.transform;
            }
        }

        void Awake() => Initialize();

        void LateUpdate()
        {
            if (!IsActive) return;
            float time = Time.time;
            float dt = Time.deltaTime;
            if (controller != null) state = controller.CurrentState;

            UpdateWeights(dt);
            if (walk > 0.01f) walkPhase = (walkPhase + dt * stepsPerSecond * Mathf.PI) % (Mathf.PI * 2f);
            UpdateAction(time);
            UpdateLook(dt);
            ApplyPose(time);
            ApplyFace(time, dt, false);
        }

        // ───────── 다른 컴포넌트가 부르는 기능 ─────────

        /// <summary>클릭했을 때 움찔하는 동작</summary>
        public void Poke() => pokeTime = Time.time;

        /// <summary>잠깐 동안 표정을 바꿉니다 (faces의 name).</summary>
        public void React(string face, float seconds)
        {
            reaction = face;
            reactionUntil = Time.time + seconds;
            action = Action.None;
            ScheduleAction(Time.time);
        }

        /// <summary>에디터에서 자세와 표정을 바로 확인할 때 씁니다 (섞지 않고 바로 그 모습으로).</summary>
        public void PreviewPose(MascotController.State previewState, float phase, float time, string previewAction = null, float actionProgress = 0.5f, string face = null)
        {
            Initialize();
            state = previewState;
            walk = sit = sleep = held = fall = 0f;
            UpdateWeights(float.PositiveInfinity);
            walkPhase = phase;
            look = Vector2.zero;
            action = Action.None;
            if (!string.IsNullOrEmpty(previewAction) && System.Enum.TryParse(previewAction, true, out Action parsed))
            {
                action = parsed;
                actionLength = ActionLength(parsed);
                actionStart = time - actionProgress * actionLength;
            }
            reaction = face;
            reactionUntil = face != null ? time + 1f : 0f;
            nextBlinkTime = float.MaxValue;
            ApplyPose(time);
            ApplyFace(time, 0f, true);
        }

        /// <summary>모든 뼈와 표정을 원래대로 돌려 놓습니다.</summary>
        public void ResetPose()
        {
            ResetBones();
            foreach (Shape shape in slots)
            {
                shape.current = shape.target = 0f;
                shape.renderer.SetBlendShapeWeight(shape.index, 0f);
            }
        }

        // ───────── 준비 ─────────

        void Initialize()
        {
            if (initialized) return;
            initialized = true;
            controller = GetComponent<MascotController>();
            speech = GetComponent<MascotSpeech>();
            animator = GetComponentInChildren<Animator>();
            Transform[] all = GetComponentsInChildren<Transform>(true);

            hips = FindBone(all, HumanBodyBones.Hips, "センター", "Hips", "J_Bip_C_Hips", "Center");
            spine = FindBone(all, HumanBodyBones.Spine, "上半身", "Spine", "J_Bip_C_Spine");
            chest = FindBone(all, HumanBodyBones.Chest, "上半身2", "Chest", "J_Bip_C_Chest");
            neck = FindBone(all, HumanBodyBones.Neck, "首", "Neck", "J_Bip_C_Neck");
            head = FindBone(all, HumanBodyBones.Head, "頭", "Head", "J_Bip_C_Head");
            upperArmL = FindBone(all, HumanBodyBones.LeftUpperArm, "腕.L", "左腕", "LeftUpperArm", "J_Bip_L_UpperArm", "UpperArm.L", "UpperArm_L");
            upperArmR = FindBone(all, HumanBodyBones.RightUpperArm, "腕.R", "右腕", "RightUpperArm", "J_Bip_R_UpperArm", "UpperArm.R", "UpperArm_R");
            lowerArmL = FindBone(all, HumanBodyBones.LeftLowerArm, "ひじ.L", "左ひじ", "LeftLowerArm", "J_Bip_L_LowerArm", "LowerArm.L", "LowerArm_L");
            lowerArmR = FindBone(all, HumanBodyBones.RightLowerArm, "ひじ.R", "右ひじ", "RightLowerArm", "J_Bip_R_LowerArm", "LowerArm.R", "LowerArm_R");
            upperLegL = FindBone(all, HumanBodyBones.LeftUpperLeg, "足.L", "左足", "LeftUpperLeg", "J_Bip_L_UpperLeg", "UpperLeg.L", "UpperLeg_L");
            upperLegR = FindBone(all, HumanBodyBones.RightUpperLeg, "足.R", "右足", "RightUpperLeg", "J_Bip_R_UpperLeg", "UpperLeg.R", "UpperLeg_R");
            lowerLegL = FindBone(all, HumanBodyBones.LeftLowerLeg, "ひざ.L", "左ひざ", "LeftLowerLeg", "J_Bip_L_LowerLeg", "LowerLeg.L", "LowerLeg_L");
            lowerLegR = FindBone(all, HumanBodyBones.RightLowerLeg, "ひざ.R", "右ひざ", "RightLowerLeg", "J_Bip_R_LowerLeg", "LowerLeg.R", "LowerLeg_R");

            if (upperLegL != null && upperLegR != null)
            {
                float left = transform.InverseTransformPoint(upperLegL.transform.position).y;
                float right = transform.InverseTransformPoint(upperLegR.transform.position).y;
                hipHeight = Mathf.Max(0f, (left + right) * 0.5f);
            }

            foreach (string name in blinkShapes.Split(','))
            {
                blink = Shapes(name.Trim());
                if (blink.Count > 0) break;
            }
            foreach (string name in talkShapes.Split(','))
            {
                List<Shape> found = Shapes(name.Trim());
                if (found.Count > 0) vowels.Add(found);
            }
            foreach (FacePreset face in faces) parsedFaces[face.name] = ParseFace(face);

            nextBlinkTime = Time.time + Random.Range(blinkIntervalRange.x, blinkIntervalRange.y);
            ScheduleAction(Time.time);
        }

        Bone FindBone(Transform[] all, HumanBodyBones humanBone, params string[] names)
        {
            Transform found = null;
            if (animator != null && animator.isHuman) found = animator.GetBoneTransform(humanBone);
            for (int i = 0; found == null && i < names.Length; i++)
            {
                foreach (Transform candidate in all)
                {
                    if (string.Equals(candidate.name, names[i], System.StringComparison.OrdinalIgnoreCase))
                    {
                        found = candidate;
                        break;
                    }
                }
            }
            if (found == null) return null;
            var bone = new Bone { transform = found, restRotation = found.localRotation, restPosition = found.localPosition };
            bones.Add(bone);
            return bone;
        }

        /// <summary>이 이름의 셰이프 키를 모든 메시에서 찾습니다. 같은 셰이프 키는 한 번만 만듭니다.</summary>
        List<Shape> Shapes(string name)
        {
            if (shapesByName.TryGetValue(name, out List<Shape> cached)) return cached;
            var found = new List<Shape>();
            foreach (SkinnedMeshRenderer meshRenderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (meshRenderer.sharedMesh == null) continue;
                int index = meshRenderer.sharedMesh.GetBlendShapeIndex(name);
                if (index < 0) continue;
                Shape shape = slots.Find(s => s.renderer == meshRenderer && s.index == index);
                if (shape == null)
                {
                    shape = new Shape { renderer = meshRenderer, index = index };
                    slots.Add(shape);
                }
                found.Add(shape);
            }
            shapesByName[name] = found;
            return found;
        }

        ParsedFace ParseFace(FacePreset face)
        {
            var parsed = new ParsedFace { blink = face.blink };
            if (string.IsNullOrEmpty(face.shapes)) return parsed;
            foreach (string part in face.shapes.Split(','))
            {
                string[] pair = part.Split(':');
                if (pair.Length != 2 || !float.TryParse(pair[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float weight)) continue;
                List<Shape> found = Shapes(pair[0].Trim());
                if (found.Count > 0) parsed.entries.Add((found, weight));
            }
            return parsed;
        }

        // ───────── 상태와 가끔 하는 행동 ─────────

        void UpdateWeights(float dt)
        {
            float k = 1f - Mathf.Exp(-transitionSpeed * dt);
            walk = Mathf.Lerp(walk, state == MascotController.State.Walk ? 1f : 0f, k);
            sit = Mathf.Lerp(sit, state == MascotController.State.Sit || state == MascotController.State.Sleep ? 1f : 0f, k);
            sleep = Mathf.Lerp(sleep, state == MascotController.State.Sleep ? 1f : 0f, k);
            // 들기와 떨어지기는 빨리 바뀌어야 자연스럽습니다
            float fast = 1f - Mathf.Exp(-transitionSpeed * 3f * dt);
            held = Mathf.Lerp(held, state == MascotController.State.Held ? 1f : 0f, fast);
            fall = Mathf.Lerp(fall, state == MascotController.State.Fall ? 1f : 0f, fast);
        }

        void UpdateAction(float time)
        {
            bool calm = state == MascotController.State.Idle || state == MascotController.State.Sit;
            if (!calm)
            {
                action = Action.None;
                if (time > nextActionTime - 2f) nextActionTime = time + 2f;
                return;
            }

            if (action != Action.None)
            {
                float progress = (time - actionStart) / actionLength;
                if (action == Action.Doze && !dozeWokeUp && progress >= 0.72f)
                {
                    dozeWokeUp = true;
                    if (speech != null && !speech.IsShowing && Random.value < 0.5f) speech.SayRandom(speech.dozeWakeLines);
                }
                if (progress >= 1f)
                {
                    action = Action.None;
                    ScheduleAction(time);
                }
                return;
            }

            bool busy = speech != null && speech.IsShowing;
            bool reacting = reaction != null && time < reactionUntil;
            if (time < nextActionTime || busy || reacting) return;

            float roll = Random.value;
            Action next = state == MascotController.State.Sit
                ? (roll < 0.35f ? Action.Yawn : roll < 0.75f ? Action.Doze : Action.Sway)
                : (roll < 0.3f ? Action.Yawn : roll < 0.55f ? Action.Doze : roll < 0.8f ? Action.Sway : Action.LookAround);
            action = next;
            actionStart = time;
            actionLength = ActionLength(next);
            dozeWokeUp = false;
            if (next == Action.Yawn && speech != null && Random.value < 0.6f) speech.SayRandom(speech.yawnLines);
        }

        static float ActionLength(Action value)
        {
            switch (value)
            {
                case Action.Yawn: return 3.2f;
                case Action.Doze: return 4.5f;
                case Action.Sway: return 4f;
                case Action.LookAround: return 3f;
                default: return 1f;
            }
        }

        void ScheduleAction(float time) => nextActionTime = time + Random.Range(idleActionInterval.x, idleActionInterval.y);

        void UpdateLook(float dt)
        {
            Vector2 target = Vector2.zero;
            if (lookAtCursor && head != null && controller != null && controller.TargetCamera != null)
            {
                // 커서보다 조금 화면 앞쪽을 보게 해서, 모니터 앞의 선생님을 보는 느낌을 냅니다
                Camera targetCamera = controller.TargetCamera;
                Vector3 point = controller.CursorWorld - targetCamera.transform.forward * (controller.Height * 1.5f);
                Vector3 local = transform.InverseTransformDirection(point - head.transform.position);
                float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                float pitch = -Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
                target = new Vector2(Mathf.Clamp(yaw, -40f, 40f), Mathf.Clamp(pitch, -25f, 25f)) * lookWeight;
            }
            // 졸린 호시노답게 천천히 쳐다봅니다
            look = Vector2.Lerp(look, target, 1f - Mathf.Exp(-3f * dt));
        }

        // ───────── 자세 ─────────

        // 각도는 캐릭터 기준 축으로 돌립니다 (오른쪽 = X, 위 = Y, 앞 = Z).
        //  X축 +: 위로 뻗은 뼈는 앞으로 숙이고, 아래로 뻗은 뼈(팔, 다리)는 뒤로 갑니다.
        //  Z축 +: 아래로 뻗은 뼈가 캐릭터의 오른쪽으로, 위로 뻗은 뼈(머리)는 왼쪽으로 기웁니다.
        //  Y축 +: 앞을 보던 뼈가 캐릭터의 오른쪽을 봅니다.
        void ApplyPose(float time)
        {
            ResetBones();

            float w = Ease(walk), si = Ease(sit), sl = Ease(sleep), h = Ease(held), f = Ease(fall);
            float hang = Mathf.Clamp01(h + f);
            float stand = Mathf.Clamp01(1f - w - si - hang);
            float awake = 1f - sl;
            float s = Mathf.Sin(walkPhase);
            float c = Mathf.Cos(walkPhase);

            float breathe = Mathf.Sin(time * Mathf.Lerp(2f, 1.2f, sl));
            // 여러 속도의 물결을 겹쳐서 기계적으로 반복되는 느낌을 줄입니다
            float sway = Mathf.Sin(time * 0.9f) * 2.5f + Mathf.Sin(time * 0.37f + 1.3f) * 1.5f;
            float poke = time >= pokeTime ? Mathf.Exp(-(time - pokeTime) * 5f) : 0f;
            float flap = Mathf.Sin(time * 7f);
            float dangle = Mathf.Sin(time * 2.6f) * 10f;
            bool dizzyNow = reaction == "dizzy" && time < reactionUntil;
            float wobble = dizzyNow ? Mathf.Sin(time * 6f) : 0f;

            // 가끔 하는 행동
            float progress = action != Action.None ? Mathf.Clamp01((time - actionStart) / actionLength) : 0f;
            float envelope = action != Action.None ? Smooth01(progress / 0.15f) * Smooth01((1f - progress) / 0.15f) : 0f;
            float yawn = action == Action.Yawn ? Smooth01(progress / 0.35f) * Smooth01((1f - progress) / 0.3f) : 0f;
            float doze = action == Action.Doze ? DozeCurve(progress) : 0f;
            float swayAction = action == Action.Sway ? envelope : 0f;
            float swayWave = Mathf.Sin((time - actionStart) * Mathf.PI * 2f * 0.6f);
            float lookAround = action == Action.LookAround ? envelope : 0f;
            float lookWave = Mathf.Sin((time - actionStart) * Mathf.PI * 2f / 3f);

            // 엉덩이: 앉으면 내려가고, 걸으면 통통 들썩입니다
            if (hips != null)
            {
                float bob = w * 0.06f * (Mathf.Abs(c) - 0.5f);
                hips.transform.position += transform.TransformVector(Vector3.up * (hipHeight * (bob - si * sitDepth)));
            }

            // 몸통: 구부정하게, 좌우로 흔들흔들
            float spinePitch = slouch * awake * (1f - hang) + breathe * 1.2f * awake + w * 3f - si * 8f * awake + sl * 12f
                               - h * 6f - poke * 6f - yawn * 10f + doze * 6f;
            float spineRoll = sway * stand + w * waddle * s + swayAction * 5f * swayWave + sl * 9f + wobble * 3f;
            Rotate(spine, Vector3.right, spinePitch);
            Rotate(spine, Vector3.forward, spineRoll);
            Rotate(spine, Vector3.up, w * 5f * s);
            Rotate(chest, Vector3.right, breathe * 1.5f + sl * 6f - yawn * 6f);

            // 머리: 갸웃, 커서 보기, 졸면 꾸벅
            float nod = Mathf.Sin(time * 1.4f) * 3f * sl;
            Rotate(neck, Vector3.right, sl * 8f + doze * 10f + look.y * 0.4f * awake - slouch * 0.5f * awake);
            Rotate(neck, Vector3.up, look.x * 0.4f * awake);
            Rotate(head, Vector3.right, sl * 12f + nod + doze * 20f + look.y * 0.6f * awake - poke * 6f - yawn * 12f);
            Rotate(head, Vector3.up, look.x * 0.6f * awake + lookAround * 30f * lookWave);
            float headRoll = headTilt * awake * (1f - hang) - sway * 0.8f * stand + sl * 20f + poke * 12f
                             - swayAction * 6f * swayWave - w * waddle * 0.6f * s + wobble * 10f;
            Rotate(head, Vector3.forward, headRoll);

            // 다리: 걸을 때 번갈아 앞뒤로, 앉으면 앞으로 뻗고, 들리면 대롱대롱
            Rotate(upperLegL, Vector3.right, -legSwing * s * w - si * 80f + hang * (dangle - 12f));
            Rotate(upperLegR, Vector3.right, legSwing * s * w - si * 80f + hang * (-dangle - 12f));
            Rotate(upperLegL, Vector3.forward, -si * sitLegSpread);
            Rotate(upperLegR, Vector3.forward, si * sitLegSpread);
            Rotate(lowerLegL, Vector3.right, kneeBend * Mathf.Max(0f, c) * w + si * 18f + hang * 30f);
            Rotate(lowerLegR, Vector3.right, kneeBend * Mathf.Max(0f, -c) * w + si * 18f + hang * 30f);

            // 팔: 축 늘어뜨리고, 앉으면 뒤로 짚고, 자면 무릎 위에, 하품하면 기지개
            // 하품 기지개: SD 체형은 머리가 커서 팔을 곧게 위로 뻗으면 머리에 가려지므로 V자로 벌립니다
            float raise = h * (35f + flap * 8f) + f * (70f + flap * 12f) + poke * 20f + si * 12f * awake + yawn * 110f;
            float armPitch = -6f * stand + si * 38f * awake - sl * 25f - yawn * 10f;
            float elbow = 15f * stand * (1f - yawn) + w * 12f + h * 20f + sl * 45f + yawn * 10f;
            Rotate(upperArmL, Vector3.right, armSwing * s * w + armPitch);
            Rotate(upperArmR, Vector3.right, -armSwing * s * w + armPitch);
            Rotate(upperArmL, Vector3.forward, -raise + breathe * 1.5f * awake);
            Rotate(upperArmR, Vector3.forward, raise - breathe * 1.5f * awake);
            Rotate(lowerArmL, Vector3.right, -elbow);
            Rotate(lowerArmR, Vector3.right, -elbow);
        }

        /// <summary>천천히 고개가 떨어지다가(0→1) 화들짝 들고(→-0.2) 원래대로(→0)</summary>
        static float DozeCurve(float progress)
        {
            if (progress < 0.72f) return Smooth01(progress / 0.72f);
            if (progress < 0.78f) return Mathf.Lerp(1f, -0.2f, (progress - 0.72f) / 0.06f);
            return Mathf.Lerp(-0.2f, 0f, Smooth01((progress - 0.78f) / 0.22f));
        }

        static float Smooth01(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        static float Ease(float x) => Smooth01(x);

        void ResetBones()
        {
            foreach (Bone bone in bones)
            {
                bone.transform.localRotation = bone.restRotation;
                bone.transform.localPosition = bone.restPosition;
            }
        }

        void Rotate(Bone bone, Vector3 axis, float degrees)
        {
            if (bone == null || Mathf.Abs(degrees) < 0.001f) return;
            bone.transform.rotation = Quaternion.AngleAxis(degrees, transform.TransformDirection(axis)) * bone.transform.rotation;
        }

        // ───────── 표정 ─────────

        string CurrentFace(float time)
        {
            if (reaction != null && time < reactionUntil) return reaction;
            if (action != Action.None)
            {
                float progress = (time - actionStart) / actionLength;
                switch (action)
                {
                    case Action.Yawn: return progress > 0.15f && progress < 0.85f ? "yawn" : "normal";
                    case Action.Doze: return progress < 0.72f ? "doze" : progress < 0.88f ? "surprised" : "normal";
                    case Action.Sway: return "relaxed";
                }
            }
            switch (state)
            {
                case MascotController.State.Sleep: return "sleep";
                case MascotController.State.Held: return "held";
                case MascotController.State.Fall: return "surprised";
                case MascotController.State.Sit: return "relaxed";
                default: return "normal";
            }
        }

        void ApplyFace(float time, float dt, bool immediate)
        {
            if (slots.Count == 0) return;
            foreach (Shape shape in slots) shape.target = 0f;

            parsedFaces.TryGetValue(CurrentFace(time), out ParsedFace face);
            if (face != null)
            {
                foreach (var (shapes, weight) in face.entries)
                    foreach (Shape shape in shapes) shape.target = Mathf.Max(shape.target, weight);
            }

            // 눈 깜빡임 (눈을 이미 감은 표정이면 하지 않습니다)
            if (face == null || face.blink)
            {
                if (time >= nextBlinkTime)
                {
                    blinkStart = time;
                    nextBlinkTime = time + Random.Range(blinkIntervalRange.x, blinkIntervalRange.y);
                }
                float blinkProgress = (time - blinkStart) / 0.16f;
                float closed = blinkProgress >= 0f && blinkProgress <= 1f ? Mathf.Sin(blinkProgress * Mathf.PI) : 0f;
                foreach (Shape shape in blink) shape.target = Mathf.Max(shape.target, closed);
            }

            // 말하는 동안 입 모양을 이것저것 바꿉니다
            bool talking = speech != null && speech.IsTalking && state != MascotController.State.Sleep;
            if (talking && vowels.Count > 0)
            {
                if (time >= nextVowelTime)
                {
                    vowel = Random.Range(0, vowels.Count);
                    vowelWeight = Random.Range(0.35f, 0.85f);
                    nextVowelTime = time + Random.Range(0.07f, 0.13f);
                }
                foreach (Shape shape in vowels[vowel]) shape.target = Mathf.Max(shape.target, vowelWeight);
            }

            float k = immediate ? 1f : 1f - Mathf.Exp(-18f * dt);
            foreach (Shape shape in slots)
            {
                shape.current = Mathf.Lerp(shape.current, shape.target, k);
                shape.renderer.SetBlendShapeWeight(shape.index, shape.current * 100f);
            }
        }
    }
}
