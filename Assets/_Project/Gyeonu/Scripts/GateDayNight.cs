using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 관아 외삼문의 시간대별 기본 상태 — 낮에는 열려 있고 밤에는 닫혀 있다.
    ///
    /// ■ 왜 별도 컴포넌트인가
    ///   여닫이 자체(DoubleHingeDoor)는 마을 사립문·김명관 일각문이 함께 쓰는 공용 부품이라
    ///   시간대를 알아서는 안 된다. "언제 어떤 자세로 있어야 하는가"는 이 씬의 연출이므로 분리한다.
    ///   <see cref="WorldTimeSync"/> 가 하늘을 바꾸는 것과 같은 결(<see cref="GyeonuWorld.Changed"/> 구독)이다.
    ///
    /// ■ 플레이어 조작과의 관계
    ///   시간대가 바뀌는 순간에만 기본 자세로 되돌린다. 그 뒤 플레이어가 클릭해 연/닫은 상태는
    ///   다음 시간대 변화까지 유지된다 — 문을 열어 두고 들어갔는데 매 프레임 도로 닫히면 곤란하다.
    ///
    /// ■ 밤 통제 (아직 끄고 둔다)
    ///   <see cref="lockAtNight"/> 를 켜면 밤에는 클릭해도 열리지 않는다. 기획상 밤의 관아는
    ///   정문으로 못 들어가고 다른 경로(개구멍)로 들어가게 되어 있으나, 그 경로가 아직 없으므로
    ///   지금은 꺼 둔다. 개구멍이 생기면 이 체크 하나만 켜면 된다.
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

        [Tooltip("★밤에는 클릭해도 열리지 않게 한다. 개구멍 경로가 생기면 켤 것 (지금은 꺼 둠)")]
        public bool lockAtNight = false;

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
            door.locked = night && lockAtNight;
            door.SetOpen(night ? openByNight : openByDay, instant);
        }
    }
}
