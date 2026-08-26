// Phase별 활성 오브젝트와 낮/밤 조명을 인스펙터에 연결해 진행 단계를 전환합니다.
using System;
using UnityEngine;

namespace IMUNROK.Seocheon
{
    public enum Phase
    {
        Arrival,
        VillageSearch,
        GwanaGate,
        NightDeal,
        OutsideOpen,
        Corruption,
        Chuldu,
        Dream,
        Final
    }

    public sealed class SeocheonPhase : MonoBehaviour
    {
        [Serializable]
        public sealed class PhaseObjects
        {
            [Tooltip("이 설정이 적용될 진행 단계")]
            public Phase phase;
            [Tooltip("이 단계에서 켤 오브젝트")]
            public GameObject[] enableObjects = Array.Empty<GameObject>();
            [Tooltip("이 단계에서 끌 오브젝트")]
            public GameObject[] disableObjects = Array.Empty<GameObject>();
        }

        [Tooltip("게임 시작 시 적용할 단계")]
        [SerializeField] private Phase initialPhase = Phase.Arrival;
        [Tooltip("단계별 활성/비활성 오브젝트 설정")]
        [SerializeField] private PhaseObjects[] phaseObjects = Array.Empty<PhaseObjects>();
        [Tooltip("단계 전환 시 이전 단계의 '켤 오브젝트'를 자동으로 끕니다")]
        [SerializeField] private bool cleanupPreviousEnabledObjects = true;

        [Header("낮/밤 조명")]
        [Tooltip("낮에 켤 조명 루트")]
        [SerializeField] private GameObject lightDay;
        [Tooltip("밤에 켤 조명 루트")]
        [SerializeField] private GameObject lightNight;
        [Tooltip("밤 조명을 사용할 진행 단계")]
        [SerializeField] private Phase[] nightPhases = { Phase.NightDeal, Phase.Dream };

        [Header("성 밖 개방")]
        [Tooltip("OutsideOpen 진입 시 끌 다리 경계 오브젝트(_Boundary_Bridge를 지정)")]
        [SerializeField] private GameObject outsideBoundaryBlocker;

        [Header("관아 외삼문")]
        [Tooltip("문지기가 막고 선 자리의 차단 오브젝트(_GateBlocker_Center를 지정)")]
        [SerializeField] private GameObject gateBlocker;
        [Tooltip("이 단계부터 외삼문이 열린다. 그 전까지는 문지기가 막는다")]
        [SerializeField] private Phase gateOpensAtPhase = Phase.Chuldu;

        public Phase CurrentPhase { get; private set; }
        public event Action<Phase> PhaseChanged;

        private bool initialized;
#if UNITY_EDITOR
        private static readonly GUIContent[] PhaseDebugContents =
        {
            new GUIContent("Seocheon Phase: Arrival"),
            new GUIContent("Seocheon Phase: VillageSearch"),
            new GUIContent("Seocheon Phase: GwanaGate"),
            new GUIContent("Seocheon Phase: NightDeal"),
            new GUIContent("Seocheon Phase: OutsideOpen"),
            new GUIContent("Seocheon Phase: Corruption"),
            new GUIContent("Seocheon Phase: Chuldu"),
            new GUIContent("Seocheon Phase: Dream"),
            new GUIContent("Seocheon Phase: Final")
        };

        private GUIContent phaseDebugContent;
#endif

        private void Awake()
        {
            AdvanceTo(initialPhase);
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (SeocheonInput.TryGetDebugPhaseIndex(out int index))
                AdvanceTo((Phase)Mathf.Clamp(index, 0, (int)Phase.Final));
#endif
        }

        /// <summary>
        /// ★앞으로만 진행합니다. 결합으로 뒤늦게 앞 단계 규칙이 성립해도 되돌아가지 않습니다.
        /// (디버그 점프는 AdvanceTo 를 직접 써서 아무 데나 갈 수 있게 남겨 둡니다)
        /// </summary>
        public bool TryAdvanceTo(Phase phase)
        {
            if (initialized && phase <= CurrentPhase) return false;
            AdvanceTo(phase);
            return true;
        }

        public void AdvanceTo(Phase phase)
        {
            Phase previousPhase = CurrentPhase;
            if (initialized && cleanupPreviousEnabledObjects && previousPhase != phase)
                CleanupEnabledObjects(previousPhase);

            CurrentPhase = phase;
            ApplyPhaseObjects(phase);
            ApplyLighting(phase);
            ApplyOutsideBoundary(phase);
            ApplyGateBlocker(phase);
            initialized = true;
#if UNITY_EDITOR
            phaseDebugContent = PhaseDebugContents[(int)phase];
#endif
            PhaseChanged?.Invoke(phase);
        }

        private void CleanupEnabledObjects(Phase phase)
        {
            for (int i = 0; i < phaseObjects.Length; i++)
            {
                PhaseObjects setting = phaseObjects[i];
                if (setting != null && setting.phase == phase)
                    SetActive(setting.enableObjects, false);
            }
        }

        private void ApplyPhaseObjects(Phase phase)
        {
            for (int i = 0; i < phaseObjects.Length; i++)
            {
                PhaseObjects setting = phaseObjects[i];
                if (setting == null || setting.phase != phase) continue;
                SetActive(setting.enableObjects, true);
                SetActive(setting.disableObjects, false);
            }
        }

        private static void SetActive(GameObject[] objects, bool active)
        {
            if (objects == null) return;
            for (int i = 0; i < objects.Length; i++)
                if (objects[i] != null) objects[i].SetActive(active);
        }

        private void ApplyLighting(Phase phase)
        {
            bool night = false;
            if (nightPhases != null)
            {
                for (int i = 0; i < nightPhases.Length; i++)
                {
                    if (nightPhases[i] != phase) continue;
                    night = true;
                    break;
                }
            }

            if (lightDay != null) lightDay.SetActive(!night);
            if (lightNight != null) lightNight.SetActive(night);
        }

        private void ApplyOutsideBoundary(Phase phase)
        {
            if (outsideBoundaryBlocker != null)
                outsideBoundaryBlocker.SetActive(phase < Phase.OutsideOpen);
        }

        /// <summary>
        /// 외삼문 차단. ★열리는 시점은 인스펙터의 gateOpensAtPhase 로 정합니다.
        /// disableObjects 를 쓰지 않는 이유 — 그쪽은 한 번 끄면 다시 켜지지 않습니다.
        /// </summary>
        private void ApplyGateBlocker(Phase phase)
        {
            if (gateBlocker != null)
                gateBlocker.SetActive(phase < gateOpensAtPhase);
        }

#if UNITY_EDITOR
        private void OnGUI()
        {
            GUI.Label(new Rect(12f, 12f, 360f, 28f), phaseDebugContent);
        }
#endif
    }
}
