using UnityEngine;

namespace HoshinoMascot
{
    /// <summary>
    /// 머리와 눈이 마우스 커서(모바일은 손가락)를 따라가게 합니다.
    /// 휴머노이드 모델이면 MascotController가 Animator 오브젝트에 자동으로 붙입니다.
    /// Animator Controller의 레이어 설정(톱니바퀴)에서 IK Pass를 켜야 동작합니다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class MascotLookAt : MonoBehaviour
    {
        [Range(0f, 1f)] public float weight = 0.8f;
        [Range(0f, 1f)] public float bodyWeight = 0.1f;
        [Range(0f, 1f)] public float headWeight = 0.7f;
        [Range(0f, 1f)] public float eyesWeight = 0.6f;
        [Tooltip("시선이 커서를 따라가는 속도")]
        public float followSpeed = 6f;

        Animator animator;
        MascotController controller;
        float currentWeight;
        Vector3 lookPosition;
        bool initialized;

        void Awake()
        {
            animator = GetComponent<Animator>();
            controller = GetComponentInParent<MascotController>();
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (controller == null || controller.TargetCamera == null) return;

            MascotController.State state = controller.CurrentState;
            bool looking = state == MascotController.State.Idle
                           || state == MascotController.State.Walk
                           || state == MascotController.State.Held
                           || state == MascotController.State.Sit;
            currentWeight = Mathf.MoveTowards(currentWeight, looking ? weight : 0f, Time.deltaTime * 2f);

            // 커서보다 조금 화면 앞쪽을 보게 해서, 모니터 앞의 선생님을 보는 느낌을 냅니다
            Vector3 target = controller.LookTargetWorld - controller.TargetCamera.transform.forward * (controller.Height * 1.5f);
            if (!initialized)
            {
                lookPosition = target;
                initialized = true;
            }
            lookPosition = Vector3.Lerp(lookPosition, target, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));

            animator.SetLookAtWeight(currentWeight, bodyWeight, headWeight, eyesWeight, 0.5f);
            animator.SetLookAtPosition(lookPosition);
        }
    }
}
