using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 관아 외삼문의 낮·밤 (2026-08-21).
    ///   낮 — 열려 있다. 플레이어는 관여하지 못한다(연출만 여닫는다).
    ///   밤 — 닫혀 있다. 2026-09-10부터는 <b>잠긴 채 조준은 된다</b>: 눌러도 열리지 않고 안내만 뜬다.
    ///
    /// ■ 밤 안내 (2026-09-10)
    ///   개구멍 이야기를 아직 못 들은 플레이어는 밤의 관아 앞에서 왜 못 들어가는지 알 길이 없었다(통과 시험 실측).
    ///   그래서 닫힌 문을 누르면 "정문으로는 안 되겠다 — 마을 사람 가운데 다른 길을 아는 이가 있을지도"를 띄운다.
    ///   ⚠️ 아이들이라고 알려 주지 않는다. 이미 개구멍을 알면 그 줄은 빠지고 문이 닫혔다는 말만 남는다.
    ///   낮의 같은 안내는 집무실 문(<see cref="OfficeDayGate"/>)이 띄운다.
    /// </summary>
    [AddComponentMenu("이문록/외삼문 낮밤 (GateDayNight)")]
    [DisallowMultipleComponent]
    public class GateDayNight : MonoBehaviour
    {
        public DoubleHingeDoor door;

        [Tooltip("낮의 기본 상태 — 열림")]
        public bool openByDay = true;
        [Tooltip("밤의 기본 상태 — 닫힘")]
        public bool openByNight = false;

        // 2026-09-10 — 예전의 lockAtNight 스위치는 뺐다. 씬에 false 로 저장돼 있어 코드 기본값을 바꿔도
        //   그대로 꺼진 채였고, 개구멍이 정규 경로인 이상 밤의 정문은 언제나 잠겨야 한다.

        [TextArea(2, 4)]
        public string nightClosedMessage = "외삼문이 굳게 닫혀 있다. 밤에는 아무도 들이지 않는 모양이다.";

        [Tooltip("잠긴 밤 문의 조준 문구")]
        public string nightPrompt = "살펴보기";

        bool _seeded;

        void Reset() => door = GetComponent<DoubleHingeDoor>();

        void OnEnable()
        {
            // ★에디터에서 AddComponent 하면 여기가 곧바로 불린다. 그때 문짝을 움직이면
            //  씬에 '열린 자세'가 저장돼 버려 닫힘 기준이 깨진다 — 플레이 중에만 손댄다.
            if (!Application.isPlaying) return;
            if (door == null) door = GetComponent<DoubleHingeDoor>();
            GyeonuWorld.Changed += OnWorldChanged;
            Apply(true);          // 씬이 뜰 때는 애니메이션 없이 그 자세로 시작
            _seeded = true;
        }

        void OnDisable()
        {
            if (!Application.isPlaying) return;
            GyeonuWorld.Changed -= OnWorldChanged;
        }

        void OnWorldChanged() => Apply(!_seeded);

        void Apply(bool instant)
        {
            if (door == null) return;
            bool night = GyeonuWorld.Night;
            door.locked = night;
            door.lockedPrompt = nightPrompt;
            door.lockedMessage = door.locked ? NightMessage() : "";
            door.SetOpen(night ? openByNight : openByDay, instant);
        }

        string NightMessage()
        {
            string msg = nightClosedMessage;
            if (!GyeonuWorld.Has(GyeonuWorld.F_개구멍이야기)) msg += "\n" + OfficeDayGate.EntryHint;
            return msg;
        }
    }
}
