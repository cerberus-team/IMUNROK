using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 암문 발견 힌트의 조건부 표시.
    ///
    /// ■ 길에는 아무 표시도 없다 (2026-08-20 방침 확정)
    ///   징검돌·다져진 흙길·발자국 같은 **경로 안내는 전부 없앴다.** 비밀문인데 가는 길이
    ///   표시돼 있으면 비밀이 아니다. 플레이어는 단서 문서로 위치를 알고 찾아가야 한다.
    ///   남은 힌트는 **문 자체의 문틈 빛** 하나뿐이다 — 그것도 문 앞까지 왔을 때만 보인다.
    ///
    /// ■ 보이는 조건 — 단서 + 밤
    ///   단서(<see cref="GyeonuWorld.F_암문단서"/>)가 없으면 석축은 그냥 석축이다.
    ///   낮에는 문이 열리지도 않으므로 힌트를 보여 봐야 헛걸음만 시킨다.
    ///
    /// ■ 단서 연결
    ///   지금은 단서 시스템이 없어 디버그 메뉴(Tools ▸ 이문록 ▸ 디버그 ▸ 암문 단서 획득)가
    ///   플래그를 켠다. 실제 단서 아이템이 생기면 그쪽에서
    ///   GyeonuWorld.Set(GyeonuWorld.F_암문단서) 한 줄만 부르면 된다.
    /// </summary>
    [AddComponentMenu("이문록/암문 힌트 (AmmunHintController)")]
    [DisallowMultipleComponent]
    public class AmmunHintController : MonoBehaviour
    {
        [Header("단서 + 밤일 때만 보이는 것 (문틈 빛)")]
        public GameObject[] revealOnClue;

        [Header("인스펙터 디버그 (Play 중 체크하면 단서 획득과 동일)")]
        [Tooltip("체크하면 GyeonuWorld에 암문 단서 플래그를 세운다")]
        public bool debugGrantClue;

        bool _wasSetByInspector;

        void OnEnable()
        {
            GyeonuWorld.Changed += Apply;
            Apply();
        }

        void OnDisable() => GyeonuWorld.Changed -= Apply;

        void Update()
        {
            // 인스펙터 체크박스 → 플래그 (에디터 검증용)
            if (debugGrantClue && !GyeonuWorld.Has(GyeonuWorld.F_암문단서))
                GyeonuWorld.Set(GyeonuWorld.F_암문단서);
            else if (!debugGrantClue && GyeonuWorld.Has(GyeonuWorld.F_암문단서) && _wasSetByInspector)
                GyeonuWorld.Set(GyeonuWorld.F_암문단서, false);
            _wasSetByInspector = debugGrantClue;
        }

        /// <summary>현재 플래그 상태를 씬에 반영한다.</summary>
        public void Apply()
        {
            // 문이 열려 있으면 끈다 — 문틈이 없는데 문틈 빛이 보일 수는 없다.
            // (2026-08-20: 열고 나서도 개구부 둘레에 노란 테두리가 그대로 남아 있었다)
            var door = GetComponentInChildren<SecretStoneDoor>(true);
            bool opened = door != null && door.IsOpen;
            bool clue = GyeonuWorld.Has(GyeonuWorld.F_암문단서) && GyeonuWorld.Night && !opened;

            if (revealOnClue != null)
                foreach (var go in revealOnClue)
                    if (go != null && go.activeSelf != clue) go.SetActive(clue);
        }
    }
}
