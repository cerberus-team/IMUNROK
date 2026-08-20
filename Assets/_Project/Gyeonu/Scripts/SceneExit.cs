using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 다른 씬으로 나가는 출구 — 씬 전환의 재사용 컴포넌트.
    ///
    /// 발동 방식을 둘 다 지원한다:
    ///   • 트리거   — 콜라이더(isTrigger) 안으로 걸어 들어가면 전환. 고갯길·통로 끝처럼
    ///                "지나가면 넘어가는" 곳.
    ///   • 상호작용 — 조준해서 클릭하면 전환. 문·암문처럼 "들어가겠다고 정하는" 곳.
    ///                <see cref="Interactable"/> 를 상속하므로 기존 DebugInteractor가
    ///                그대로 조준·클릭한다 — VR 컨트롤러로 바뀌어도 이 코드는 무수정.
    ///
    /// ■ 도착 직후 되튕김 방지
    ///   출구와 도착 스폰은 보통 몇 m 안에 붙어 있다. 도착하자마자 트리거에 겹쳐 있으면
    ///   무한 왕복이 된다. 그래서 ① 도착 후 <see cref="armDelay"/> 동안은 트리거를 무시하고,
    ///   ② <see cref="requireExitFirst"/> 가 켜져 있으면 한 번 볼륨 밖으로 나갔다 들어와야
    ///   발동한다. 상호작용 방식은 플레이어가 직접 누르는 것이므로 이 제한을 받지 않는다.
    /// </summary>
    [AddComponentMenu("이문록/씬 출구 (SceneExit)")]
    [DisallowMultipleComponent]
    public class SceneExit : Interactable
    {
        public enum Trigger
        {
            /// <summary>볼륨 안으로 걸어 들어가면 전환.</summary>
            걸어서_트리거,
            /// <summary>조준 후 클릭하면 전환.</summary>
            상호작용,
            /// <summary>둘 다 허용.</summary>
            둘_다,
        }

        [Header("목적지")]
        [Tooltip("로드할 씬 이름 (Build Settings에 등록돼 있어야 한다)")]
        public string targetScene = "";
        [Tooltip("도착 씬에서 플레이어를 세울 마커 이름. 비우면 씬 기본 위치.")]
        public string targetSpawn = "";

        [Header("발동")]
        public Trigger mode = Trigger.걸어서_트리거;
        [Tooltip("조준했을 때 표시할 행동 문구")]
        public string promptText = "들어가기";

        [Header("잠금")]
        public bool locked = false;
        [TextArea]
        public string lockedMessage = "지금은 갈 수 없다.";

        [Header("진행 조건 (전부 만족해야 통과)")]
        [Tooltip("GyeonuWorld의 플래그 키들. 비워 두면 조건 없음.")]
        public string[] requiredFlags;
        [TextArea]
        [Tooltip("조건이 모자랄 때 뜨는 안내. {0} 자리에 모자란 조건 설명이 들어간다.")]
        public string blockedMessage = "{0}";
        [Tooltip("이 출구만 조건을 무시한다 — 배경 검증용. " +
                 "전역으로 풀려면 Tools ▸ 이문록 ▸ 디버그 ▸ 진행 조건 무시.")]
        public bool debugIgnoreConditions = false;

        [Header("도착 씬 연출")]
        [Tooltip("비우지 않으면 전환 직전에 FogReveal.Arm(키)를 부른다 — 그 키를 가진 진입 안개가 " +
                 "이 경로로 들어올 때만 발동한다. (도착 씬의 FogReveal은 arm=External 이어야 한다)")]
        public string armFogRevealKey = "";

        [Header("암전")]
        [Tooltip("나갈 때 어두워지는 시간(초)")]
        public float fadeOut = 0.45f;
        [Tooltip("도착해서 밝아지는 시간(초)")]
        public float fadeIn = 0.55f;

        [Header("되튕김 방지 (트리거 방식에만 적용)")]
        [Tooltip("씬에 도착한 뒤 이 시간(초) 동안은 트리거가 발동하지 않는다")]
        public float armDelay = 1.2f;
        [Tooltip("한 번 볼륨 밖으로 나갔다 들어와야 발동한다")]
        public bool requireExitFirst = true;

        bool _leftVolumeOnce;
        bool _blockedNotified;

        public override string Prompt => locked ? "살펴보기" : promptText;

        public override bool CanInteract(GameObject actor)
            => mode == Trigger.상호작용 || mode == Trigger.둘_다;

        public override void Interact(GameObject actor)
        {
            if (!CanInteract(actor)) return;
            if (locked) { DebugToast.ShowPinned(lockedMessage); return; }
            if (!ConditionsMet()) { DebugToast.ShowPinned(BlockedText()); return; }
            Depart();
        }

        void OnTriggerEnter(Collider other)
        {
            if (mode == Trigger.상호작용) return;
            if (locked) return;
            if (!IsPlayer(other)) return;

            if (requireExitFirst && !_leftVolumeOnce) return;
            if (Time.time - SceneTransition.LastArrivalTime < armDelay) return;

            // 조건 미달이면 넘어가지 않고 안내만 띄운다.
            // 볼륨 안에 계속 서 있어도 문구가 도배되지 않게 한 번만 알린다 —
            // 밖으로 나갔다 들어오면 다시 알린다(조건을 갖춰 돌아온 경우를 위해).
            if (!ConditionsMet())
            {
                if (!_blockedNotified) { _blockedNotified = true; DebugToast.ShowPinned(BlockedText()); }
                return;
            }

            Depart();
        }

        bool ConditionsMet()
            => debugIgnoreConditions || GyeonuWorld.HasAll(requiredFlags);

        /// <summary>모자란 조건에 맞춰 안내 문구를 만든다.</summary>
        string BlockedText()
        {
            var missing = GyeonuWorld.Missing(requiredFlags);
            if (missing.Count == 0) return "";

            var parts = new System.Text.StringBuilder();
            for (int i = 0; i < missing.Count; i++)
            {
                if (i > 0) parts.Append(", ");
                parts.Append(FlagHint(missing[i]));
            }

            return string.IsNullOrEmpty(blockedMessage)
                ? parts.ToString()
                : string.Format(blockedMessage, parts.ToString());
        }

        /// <summary>플래그별 자연스러운 결핍 설명. 여기 없는 키는 키 이름 그대로 보여 준다(작업 중 눈에 띄게).</summary>
        static string FlagHint(string flag)
        {
            switch (flag)
            {
                case GyeonuWorld.F_비밀지도획득:  return "길을 적은 것이 없다";
                case GyeonuWorld.F_타공지도_길밝힘: return "별빛이 아직 길을 밝히지 않았다";
                default: return flag;
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (!IsPlayer(other)) return;
            _leftVolumeOnce = true;
            _blockedNotified = false;   // 다시 들어오면 안내를 한 번 더 (조건을 갖춰 돌아왔을 수 있다)
        }

        void Start() => Rearm();

        void OnEnable()  => SceneTransition.PlayerPlaced += Rearm;
        void OnDisable() => SceneTransition.PlayerPlaced -= Rearm;

        /// <summary>
        /// "지금 플레이어가 내 볼륨 밖에 있는가"로 발동 준비 상태를 다시 잡는다.
        ///
        /// 밖에 있으면 곧바로 발동 가능(정상적으로 걸어 들어온 경우).
        /// 안에 있으면 한 번 나갔다 들어와야 발동한다 — 도착 스폰이 복귀 트리거에 겹쳐도
        /// 무한 왕복이 되지 않게 하는 안전장치다. Start()뿐 아니라 씬 전환으로 플레이어가
        /// 새로 놓일 때마다(PlayerPlaced) 다시 판정해야 맞다.
        /// </summary>
        void Rearm()
        {
            if (!requireExitFirst) { _leftVolumeOnce = true; return; }

            var col = GetComponent<Collider>();
            if (col == null) { _leftVolumeOnce = true; return; }

            var walker = FindFirstObjectByType<DebugWalkController>(FindObjectsInactive.Exclude);
            if (walker == null) { _leftVolumeOnce = true; return; }

            _leftVolumeOnce = !col.bounds.Contains(walker.transform.position + Vector3.up * 0.9f);
        }

        void Depart()
        {
            if (SceneTransition.IsTransitioning) return;

            // 도착 씬의 진입 연출을 이 경로로 들어올 때만 켠다.
            // (씬 로드마다 켜지는 SceneEntry 모드로 두면, 실내에서 되돌아 나올 때도
            //  언덕 밑 연출이 다시 돌아 엉뚱한 자리에서 안개가 낀다.)
            if (!string.IsNullOrEmpty(armFogRevealKey))
                FogReveal.Arm(armFogRevealKey);

            SceneTransition.Go(targetScene, targetSpawn, fadeOut, fadeIn);
        }

        static bool IsPlayer(Collider other)
        {
            if (other == null) return false;
            if (other is CharacterController) return true;
            return other.GetComponentInParent<DebugWalkController>() != null
                || other.GetComponentInParent<CharacterController>() != null;
        }
    }
}
