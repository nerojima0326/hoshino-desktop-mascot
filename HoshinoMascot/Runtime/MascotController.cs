using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HoshinoMascot
{
    /// <summary>
    /// 데스크톱 마스코트의 행동을 담당합니다.
    /// 화면 아래(작업 표시줄 위)에 서 있다가 가끔 돌아다니고, 클릭하면 반응하고,
    /// 끌어서 옮기거나 던질 수 있고, 오래 내버려 두면 낮잠을 잡니다.
    ///
    /// 모델의 가장 위 오브젝트에 붙이세요.
    /// 실제 몸동작은 MascotMotion(뼈를 직접 움직임)이나 Animator Controller가 맡습니다.
    /// Animator 파라미터(Speed, Grabbed, Sleeping, Sitting, Poke)는 전부 선택 사항이라 없으면 무시합니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MascotSpeech))]
    public class MascotController : MonoBehaviour
    {
        public enum State { Idle, Walk, Held, Fall, Sleep, Sit }

        // Animator 파라미터 이름. Animator Controller에 없는 파라미터는 무시합니다.
        const string SpeedParam = "Speed";
        const string GrabbedParam = "Grabbed";
        const string SleepingParam = "Sleeping";
        const string SittingParam = "Sitting";
        const string PokeParam = "Poke";

        const string ScaleKey = "HoshinoMascot.Scale";
        const string PositionKey = "HoshinoMascot.PositionX";

        [Header("참조 (비워두면 자동으로 찾습니다)")]
        public Camera targetCamera;
        public Animator animator;

        [Header("모델")]
        [Tooltip("모델이 뒤를 보고 서 있으면 180으로 바꾸세요.")]
        public float modelYawOffset = 0f;

        [Header("바닥과 낙하 (속도는 '초당 캐릭터 키' 단위)")]
        [Tooltip("화면 맨 아래에서 이만큼 위를 바닥으로 씁니다 (100% 배율 기준 픽셀). 작업 표시줄 높이에 맞추면 작업 표시줄 위에 섭니다.")]
        public float groundOffsetPixels = 48f;
        public float gravity = 15f;
        [Tooltip("이보다 빠르게 떨어지면 아파합니다.")]
        public float hardLandingSpeed = 7f;
        public float maxThrowSpeed = 12f;

        [Header("걷기 (MascotMotion이 있거나 Animator에 Speed 파라미터가 있을 때)")]
        public bool walkAround = true;
        public float walkSpeed = 0.4f;
        public Vector2 idleSecondsRange = new Vector2(5f, 12f);
        public Vector2 walkSecondsRange = new Vector2(2f, 5f);
        [Tooltip("화면 가장자리에서 이만큼(픽셀) 안쪽에서 돌아섭니다.")]
        public float edgeMarginPixels = 150f;
        [Range(0f, 1f), Tooltip("걸을 때 옆으로 돌아서는 정도 (0 = 정면, 1 = 완전히 옆)")]
        public float walkTurnAmount = 0.7f;

        [Header("앉기 (MascotMotion이 있거나 Animator에 Sitting 파라미터가 있을 때)")]
        [Range(0f, 1f), Tooltip("가만히 서 있다가 앉을 확률")]
        public float sitChance = 0.35f;
        [Tooltip("앉아 있는 시간(초)")]
        public Vector2 sitSecondsRange = new Vector2(20f, 60f);

        [Header("혼잣말과 낮잠")]
        public Vector2 chatterSecondsRange = new Vector2(90f, 240f);
        [Tooltip("이 시간(초) 동안 건드리지 않으면 낮잠을 잡니다. 0이면 자지 않습니다.")]
        public float sleepAfterSeconds = 180f;
        [Tooltip("자는 동안 잠꼬대 간격(초)")]
        public float sleepTalkSeconds = 8f;

        [Header("클릭 반응")]
        [Tooltip("짧은 시간에 이 횟수만큼 찌르면 투덜댑니다.")]
        public int annoyedPokeCount = 5;
        public float annoyedWindowSeconds = 3f;

        [Header("크기 (메뉴의 크게/작게)")]
        public float minScale = 0.4f;
        public float maxScale = 3f;

        [Header("기타")]
        public float turnSpeed = 6f;
        [Tooltip("이만큼(픽셀) 움직여야 클릭이 아니라 끌기로 봅니다.")]
        public float dragThresholdPixels = 6f;
        public float maxSwingDegrees = 25f;
        [Tooltip("계속 켜 두는 프로그램이라 프레임을 제한해서 CPU/GPU 사용량을 줄입니다.")]
        public int targetFrameRate = 60;

        public Camera TargetCamera => targetCamera;
        public State CurrentState => state;
        public bool IsSleeping => state == State.Sleep;
        public bool IsSitting => state == State.Sit;
        /// <summary>캐릭터와 같은 깊이에 놓인 마우스 커서의 월드 좌표</summary>
        public Vector3 CursorWorld { get; private set; }
        /// <summary>현재 캐릭터 키 (월드 단위). 속도 설정은 모두 이 값을 기준으로 합니다.</summary>
        public float Height => heightLocal * Mathf.Abs(baseScale.y) * userScale;

        MascotSpeech speech;
        MascotMenu menu;
        MascotMotion motion;
        State state = State.Idle;
        float stateTimer;
        float walkDirection = 1f;
        Vector3 baseScale = Vector3.one;
        float userScale = 1f;
        float heightLocal = 1.6f;
        Vector3 topLocal;
        Transform head;
        Vector3 headTopLocal;
        bool hasController;
        readonly HashSet<string> animatorParams = new HashSet<string>();

        bool pressing;
        Vector2 pressPosition;
        Vector3 grabOffsetLocal;
        Vector3 cursorVelocity;
        Vector3 velocity;
        readonly Queue<float> pokeTimes = new Queue<float>();

        Quaternion facing = Quaternion.identity;
        float swing, swingVelocity;
        float squash, squashVelocity;

        float lastInteractionTime;
        float nextChatterTime;
        float nextSleepTalkTime;
        bool quitting;

        bool HasMotion => motion != null && motion.enabled && motion.IsActive;
        bool CanWalk => walkAround && (HasMotion || animatorParams.Contains(SpeedParam));
        bool CanSit => HasMotion || animatorParams.Contains(SittingParam);
        float GroundY => groundOffsetPixels * MascotUiSkin.Scale;

        void Awake()
        {
            speech = GetComponent<MascotSpeech>();
            menu = GetComponent<MascotMenu>();
            motion = GetComponent<MascotMotion>();
            if (targetCamera == null) targetCamera = Camera.main;
            if (animator == null) animator = GetComponentInChildren<Animator>();
            baseScale = transform.localScale;
            userScale = Mathf.Clamp(PlayerPrefs.GetFloat(ScaleKey, 1f), minScale, maxScale);

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = targetFrameRate;
            Application.runInBackground = true;
        }

        IEnumerator Start()
        {
            if (animator != null)
            {
                animator.applyRootMotion = false;
                hasController = animator.runtimeAnimatorController != null;
                foreach (AnimatorControllerParameter parameter in animator.parameters) animatorParams.Add(parameter.name);
                if (animator.isHuman)
                {
                    head = animator.GetBoneTransform(HumanBodyBones.Head);
                    if (head != null && animator.GetComponent<MascotLookAt>() == null) animator.gameObject.AddComponent<MascotLookAt>();
                }
            }
            SetupCollider();
            // 말풍선은 머리 꼭대기에 붙어 다닙니다 (앉거나 졸 때도)
            if (head == null && motion != null) head = motion.HeadBone;
            if (head != null) headTopLocal = head.InverseTransformPoint(transform.TransformPoint(topLocal));

            float savedX = PlayerPrefs.GetFloat(PositionKey, 0.8f);
            if (targetCamera != null)
            {
                facing = FacingRotation(0f);
                transform.rotation = facing;
                PlaceOnGround(Screen.width * savedX);
                CursorWorld = ScreenToWorld(MascotInput.CursorPosition);
            }
            EnterState(State.Idle);
            lastInteractionTime = Time.time;
            ScheduleChatter();

            // UniWindowController가 창을 모니터 크기로 키울 때까지 잠깐 기다린 뒤 자리를 다시 잡고 인사합니다
            yield return new WaitForSeconds(1f);
            if (targetCamera != null && state == State.Idle) PlaceOnGround(Screen.width * savedX);
            speech.SayGreeting();
            React("happy", 3f);
        }

        void Update()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
                if (targetCamera == null) return;
            }

            float dt = Time.deltaTime;
            Vector2 cursor = MascotInput.CursorPosition;
            Vector3 cursorWorld = ScreenToWorld(cursor);
            if (dt > 0f) cursorVelocity = Vector3.Lerp(cursorVelocity, (cursorWorld - CursorWorld) / dt, 0.5f);
            CursorWorld = cursorWorld;

            HandlePointer(cursor);

            stateTimer -= dt;
            switch (state)
            {
                case State.Idle: UpdateIdle(); break;
                case State.Walk: UpdateWalk(dt); break;
                case State.Fall: UpdateFall(dt); break;
                case State.Sleep: UpdateSleep(); break;
                case State.Sit: UpdateSit(); break;
            }

            UpdateSprings(Mathf.Min(dt, 1f / 30f));
            UpdateRotation(dt);
            // 들고 있을 때는 잡은 지점을 중심으로 흔들리도록 회전을 정한 뒤에 위치를 맞춥니다
            if (state == State.Held) transform.position = CursorWorld + transform.rotation * grabOffsetLocal;
            UpdateScale();
            UpdateAnimator(dt);
        }

        void OnApplicationQuit()
        {
            SavePosition();
            PlayerPrefs.Save();
        }

        // ───────── 메뉴에서 부르는 기능 ─────────

        public void Talk()
        {
            Touch();
            if (state == State.Sleep)
            {
                WakeUp();
                return;
            }
            speech.SayChatter();
            React(RandomHappyFace(), 2.5f);
            ScheduleChatter();
        }

        public void ToggleSleep()
        {
            Touch();
            if (state == State.Sleep) WakeUp();
            else if (state == State.Idle || state == State.Walk || state == State.Sit) FallAsleep();
        }

        public void ToggleSit()
        {
            Touch();
            if (state == State.Sleep) WakeUp();
            else if (state == State.Sit) StandUp();
            else if (state == State.Idle || state == State.Walk) SitDown();
        }

        public void ChangeScale(float factor)
        {
            Touch();
            userScale = Mathf.Clamp(userScale * factor, minScale, maxScale);
            PlayerPrefs.SetFloat(ScaleKey, userScale);
            squashVelocity -= 2f;
        }

        public void Quit()
        {
            if (quitting) return;
            quitting = true;
            SavePosition();
            PlayerPrefs.Save();
            speech.SayRandom(speech.goodbyeLines);
            StartCoroutine(QuitAfter(1.6f));
        }

        // ───────── 다른 컴포넌트가 쓰는 정보 ─────────

        public bool IsPointerOnCharacter(Vector2 screenPosition)
        {
            if (targetCamera == null) return false;
            Ray ray = targetCamera.ScreenPointToRay(screenPosition);
            foreach (RaycastHit hit in Physics.RaycastAll(ray, 1000f, ~0, QueryTriggerInteraction.Collide))
            {
                if (hit.collider.transform.IsChildOf(transform)) return true;
            }
            return false;
        }

        /// <summary>말풍선을 띄울 위치 (머리 위, Unity 화면 좌표)</summary>
        public Vector2 BubbleAnchorScreen
        {
            get
            {
                Vector3 top = head != null ? head.TransformPoint(headTopLocal) : transform.TransformPoint(topLocal);
                return targetCamera.WorldToScreenPoint(top);
            }
        }

        // ───────── 입력 ─────────

        void HandlePointer(Vector2 cursor)
        {
            if (quitting) return;

            if (MascotInput.LeftDown && !IsOverUi(cursor) && IsPointerOnCharacter(cursor))
            {
                pressing = true;
                pressPosition = cursor;
            }
            if (!pressing) return;

            bool released = MascotInput.LeftUp || !MascotInput.LeftHeld;
            if (released)
            {
                pressing = false;
                if (state == State.Held) Drop();
                else Poke();
            }
            else if (state != State.Held && (cursor - pressPosition).magnitude > dragThresholdPixels * MascotUiSkin.Scale)
            {
                PickUp();
            }
        }

        bool IsOverUi(Vector2 cursor)
        {
            return speech.ContainsScreenPoint(cursor) || (menu != null && menu.ContainsScreenPoint(cursor));
        }

        void Poke()
        {
            Touch();
            if (state == State.Sleep)
            {
                WakeUp();
                return;
            }

            pokeTimes.Enqueue(Time.time);
            while (pokeTimes.Count > 0 && Time.time - pokeTimes.Peek() > annoyedWindowSeconds) pokeTimes.Dequeue();
            bool annoyed = pokeTimes.Count >= annoyedPokeCount;
            if (annoyed) pokeTimes.Clear();

            speech.SayRandom(annoyed ? speech.annoyedLines : speech.pokeLines);
            React(annoyed ? "annoyed" : RandomHappyFace(), annoyed ? 3f : 2f);
            if (hasController && animatorParams.Contains(PokeParam)) animator.SetTrigger(PokeParam);
            if (motion != null) motion.Poke();
            squashVelocity += 4f;
            if (state == State.Walk) EnterState(State.Idle);
        }

        void PickUp()
        {
            bool wasSleeping = state == State.Sleep;
            grabOffsetLocal = Quaternion.Inverse(transform.rotation) * (transform.position - CursorWorld);
            velocity = Vector3.zero;
            EnterState(State.Held);
            speech.SayRandom(wasSleeping ? speech.wakeUpLines : speech.pickUpLines);
            React("surprised", 0.7f);
            Touch();
        }

        void Drop()
        {
            // 놓는 순간의 마우스 속도를 이어받아서 던질 수 있습니다
            velocity = Vector3.ClampMagnitude(cursorVelocity, maxThrowSpeed * Height);
            EnterState(State.Fall);
            Touch();
        }

        // ───────── 상태 ─────────

        void EnterState(State next)
        {
            state = next;
            switch (next)
            {
                case State.Idle:
                    stateTimer = Random.Range(idleSecondsRange.x, idleSecondsRange.y);
                    break;
                case State.Walk:
                    stateTimer = Random.Range(walkSecondsRange.x, walkSecondsRange.y);
                    break;
                case State.Sleep:
                    nextSleepTalkTime = Time.time + sleepTalkSeconds;
                    break;
                case State.Sit:
                    stateTimer = Random.Range(sitSecondsRange.x, sitSecondsRange.y);
                    break;
            }
        }

        void UpdateIdle()
        {
            StickToGround();
            if (DozeOrChatter()) return;

            if (stateTimer > 0f) return;
            if (CanWalk && Random.value < 0.5f)
            {
                // 가장자리 근처에 있으면 화면 안쪽으로 걸어갑니다
                float x = targetCamera.WorldToScreenPoint(transform.position).x / Mathf.Max(1, Screen.width);
                walkDirection = x < 0.3f ? 1f : x > 0.7f ? -1f : (Random.value < 0.5f ? -1f : 1f);
                EnterState(State.Walk);
            }
            else if (CanSit && Random.value < sitChance)
            {
                SitDown();
            }
            else
            {
                EnterState(State.Idle);
            }
        }

        void UpdateSit()
        {
            StickToGround();
            if (DozeOrChatter()) return;
            if (stateTimer <= 0f) StandUp();
        }

        /// <summary>오래 안 건드렸으면 낮잠을 자고(true), 아니면 가끔 혼잣말을 합니다.</summary>
        bool DozeOrChatter()
        {
            if (sleepAfterSeconds > 0f && Time.time - lastInteractionTime > sleepAfterSeconds)
            {
                FallAsleep();
                return true;
            }
            if (Time.time >= nextChatterTime)
            {
                if (!speech.IsShowing)
                {
                    speech.SayChatter();
                    if (Random.value < 0.4f) React(RandomHappyFace(), 2f);
                }
                ScheduleChatter();
            }
            return false;
        }

        void SitDown()
        {
            EnterState(State.Sit);
            if (!speech.IsShowing && Random.value < 0.5f) speech.SayRandom(speech.sitDownLines);
        }

        void StandUp()
        {
            EnterState(State.Idle);
            if (!speech.IsShowing && Random.value < 0.3f) speech.SayRandom(speech.standUpLines);
        }

        void UpdateWalk(float dt)
        {
            transform.position += targetCamera.transform.right * (walkDirection * walkSpeed * Height * dt);

            float x = targetCamera.WorldToScreenPoint(transform.position).x;
            float margin = edgeMarginPixels * MascotUiSkin.Scale;
            if ((x < margin && walkDirection < 0f) || (x > Screen.width - margin && walkDirection > 0f))
                walkDirection = -walkDirection;

            StickToGround();
            if (stateTimer <= 0f) EnterState(State.Idle);
        }

        void UpdateFall(float dt)
        {
            Vector3 up = targetCamera.transform.up;
            Vector3 right = targetCamera.transform.right;

            velocity -= up * (gravity * Height * dt);
            velocity -= right * (Vector3.Dot(velocity, right) * 0.5f * dt); // 공기 저항
            transform.position += velocity * dt;

            // 화면 가장자리와 천장에서 튕깁니다
            Vector3 screen = targetCamera.WorldToScreenPoint(transform.position);
            float sideSpeed = Vector3.Dot(velocity, right);
            if ((screen.x < 0f && sideSpeed < 0f) || (screen.x > Screen.width && sideSpeed > 0f))
                velocity -= right * (sideSpeed * 1.5f);
            float upSpeed = Vector3.Dot(velocity, up);
            if (screen.y > Screen.height && upSpeed > 0f)
                velocity -= up * (upSpeed * 1.5f);

            if (screen.y > GroundY || upSpeed > 0f) return;

            // 착지
            float impact = -upSpeed / Mathf.Max(Height, 0.001f);
            velocity = Vector3.zero;
            StickToGround();
            EnterState(State.Idle);
            squashVelocity += Mathf.Clamp(impact * 0.6f, 1f, 6f);
            if (impact > hardLandingSpeed)
            {
                speech.SayRandom(speech.hardLandingLines);
                React("dizzy", 2.5f);
            }
            else if (impact > 2f && Random.value < 0.5f)
            {
                speech.SayRandom(speech.landLines);
                React("happy", 1.5f);
            }
            SavePosition();
        }

        void UpdateSleep()
        {
            StickToGround();
            if (Time.time < nextSleepTalkTime) return;
            if (!speech.IsShowing) speech.SayRandom(speech.sleepTalkLines);
            nextSleepTalkTime = Time.time + sleepTalkSeconds;
        }

        void FallAsleep()
        {
            EnterState(State.Sleep);
            speech.SayRandom(speech.fallAsleepLines);
        }

        void WakeUp()
        {
            EnterState(State.Idle);
            speech.SayRandom(speech.wakeUpLines);
            React("surprised", 1.2f);
            squashVelocity -= 3f; // 화들짝 늘어나는 느낌
        }

        void Touch() => lastInteractionTime = Time.time;

        /// <summary>MascotMotion이 있으면 잠깐 표정을 바꿉니다.</summary>
        void React(string face, float seconds)
        {
            if (motion != null) motion.React(face, seconds);
        }

        static string RandomHappyFace()
        {
            float roll = Random.value;
            return roll < 0.6f ? "happy" : roll < 0.8f ? "blush" : "wink";
        }

        void ScheduleChatter()
        {
            nextChatterTime = Time.time + Random.Range(chatterSecondsRange.x, chatterSecondsRange.y);
        }

        // ───────── 움직임과 연출 ─────────

        void UpdateSprings(float dt)
        {
            float swingTarget = 0f;
            if (state == State.Held)
            {
                // 옆으로 끌면 진자처럼 반대쪽으로 흔들립니다
                float sideSpeed = Vector3.Dot(cursorVelocity, targetCamera.transform.right) / Mathf.Max(Height, 0.001f);
                swingTarget = Mathf.Clamp(-sideSpeed * 8f, -maxSwingDegrees, maxSwingDegrees);
            }
            else if (state == State.Sleep && !HasMotion && !animatorParams.Contains(SleepingParam))
            {
                swingTarget = 6f; // 잠자는 애니메이션이 없으면 살짝 기울여서 조는 느낌을 냅니다
            }

            swingVelocity += ((swingTarget - swing) * 120f - swingVelocity * 10f) * dt;
            swing += swingVelocity * dt;

            squashVelocity += (-squash * 300f - squashVelocity * 14f) * dt;
            squash = Mathf.Clamp(squash + squashVelocity * dt, -0.3f, 0.3f);
        }

        void UpdateRotation(float dt)
        {
            float side = state == State.Walk ? walkDirection * walkTurnAmount : 0f;
            facing = Quaternion.Slerp(facing, FacingRotation(side), 1f - Mathf.Exp(-turnSpeed * dt));
            transform.rotation = Quaternion.AngleAxis(swing, targetCamera.transform.forward) * facing;
        }

        /// <summary>side: 0이면 화면 쪽, ±1이면 완전히 옆을 봅니다.</summary>
        Quaternion FacingRotation(float side)
        {
            Vector3 toCamera = -targetCamera.transform.forward;
            Vector3 sideways = targetCamera.transform.right * (side < 0f ? -1f : 1f);
            Vector3 direction = Vector3.Slerp(toCamera, sideways, Mathf.Abs(side));
            return Quaternion.LookRotation(direction, targetCamera.transform.up) * Quaternion.Euler(0f, modelYawOffset, 0f);
        }

        void UpdateScale()
        {
            float breathe = hasController || HasMotion ? 0f : Mathf.Sin(Time.time * 2.2f) * 0.01f; // 애니메이션이 없을 때 숨 쉬는 느낌
            float wide = 1f + squash * 0.5f;
            float tall = 1f - squash + breathe;
            transform.localScale = Vector3.Scale(baseScale * userScale, new Vector3(wide, tall, wide));
        }

        void UpdateAnimator(float dt)
        {
            if (!hasController) return;
            if (animatorParams.Contains(SpeedParam)) animator.SetFloat(SpeedParam, state == State.Walk ? 1f : 0f, 0.15f, dt);
            if (animatorParams.Contains(GrabbedParam)) animator.SetBool(GrabbedParam, state == State.Held || state == State.Fall);
            if (animatorParams.Contains(SleepingParam)) animator.SetBool(SleepingParam, state == State.Sleep);
            if (animatorParams.Contains(SittingParam)) animator.SetBool(SittingParam, state == State.Sit);
        }

        // ───────── 좌표 ─────────

        Vector3 ScreenToWorld(Vector2 screenPosition)
        {
            float depth = targetCamera.WorldToScreenPoint(transform.position).z;
            return targetCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, depth));
        }

        void PlaceOnGround(float screenX)
        {
            float padding = 40f * MascotUiSkin.Scale;
            float x = Mathf.Clamp(screenX, padding, Mathf.Max(padding, Screen.width - padding));
            transform.position = ScreenToWorld(new Vector2(x, GroundY));
        }

        void StickToGround()
        {
            PlaceOnGround(targetCamera.WorldToScreenPoint(transform.position).x);
        }

        void SavePosition()
        {
            if (targetCamera == null || Screen.width <= 0) return;
            float x = targetCamera.WorldToScreenPoint(transform.position).x / Screen.width;
            PlayerPrefs.SetFloat(PositionKey, Mathf.Clamp01(x));
        }

        // ───────── 준비 ─────────

        void SetupCollider()
        {
            Collider existing = GetComponentInChildren<Collider>();
            Bounds bounds;
            if (existing != null) bounds = existing.bounds;
            else if (!TryGetRendererBounds(out bounds)) bounds = new Bounds(transform.position + Vector3.up * 0.8f, new Vector3(0.5f, 1.6f, 0.5f));

            Vector3 lossy = transform.lossyScale;
            heightLocal = Mathf.Max(0.01f, bounds.size.y / Mathf.Abs(lossy.y));
            topLocal = transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z));

            if (existing != null) return;

            // 클릭 판정용 콜라이더를 모델 크기에 맞춰 자동으로 만듭니다
            var capsule = gameObject.AddComponent<CapsuleCollider>();
            float width = Mathf.Max(bounds.size.x / Mathf.Abs(lossy.x), bounds.size.z / Mathf.Abs(lossy.z));
            capsule.center = transform.InverseTransformPoint(bounds.center);
            capsule.height = heightLocal;
            capsule.radius = Mathf.Min(width * 0.5f, heightLocal * 0.25f);
        }

        bool TryGetRendererBounds(out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (Renderer meshRenderer in GetComponentsInChildren<Renderer>())
            {
                if (!found)
                {
                    bounds = meshRenderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(meshRenderer.bounds);
                }
            }
            return found;
        }

        IEnumerator QuitAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
