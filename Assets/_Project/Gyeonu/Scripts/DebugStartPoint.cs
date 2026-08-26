using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// ⚠️ **검증 전용 시작 지점** (2026-08-23). 씬을 단독으로 Play할 때 어디서 시작할지 고른다.
    ///
    /// ■ 왜 필요한가
    ///   씬 하나에 진입점이 둘 이상이면(관측실 = 암문 통로 / 서고 관아 통로) 씬에 저장된
    ///   워커 자리 하나만으로는 늘 그 한 곳에서만 시작하게 된다. 안쪽을 손볼 때마다
    ///   42m를 걸어 들어가는 것은 검증이 아니라 형벌이다.
    ///
    /// ■ 정식 스폰 마커는 건드리지 않는다
    ///   `SpawnPoint_*` 는 씬 전환이 쓰는 것이고, 여기는 **따로 관리하는 시험용 자리**다.
    ///   진입점에 해당하는 자리는 좌표를 베끼지 않고 <see cref="Spot.markerName"/> 으로
    ///   그 마커를 실행 시각에 찾아 쓴다 — 마커가 옮겨지면 여기도 저절로 따라간다.
    ///
    /// ■ 씬 전환으로 들어왔을 때는 물러난다
    ///   <see cref="SceneTransition.IsTransitioning"/> 이면 아무것도 하지 않는다.
    ///   설령 겹치더라도 SceneTransition은 새 씬의 Start가 한 번 돈 **뒤에** 배치하므로
    ///   정식 마커가 이긴다. 두 겹으로 막아 둔다.
    ///
    /// ■ 빌드에 남더라도
    ///   <see cref="useOnPlay"/> 를 끄면 완전히 잠잠하다. 나중에 통째로 지우려면 이 파일과
    ///   씬의 `디버그_시작지점` 오브젝트만 지우면 흔적이 없다.
    /// </summary>
    [AddComponentMenu("이문록/검증용 시작 지점 (DebugStartPoint)")]
    [DisallowMultipleComponent]
    public class DebugStartPoint : MonoBehaviour
    {
        [System.Serializable]
        public class Spot
        {
            [Tooltip("메뉴·로그에 뜨는 이름")]
            public string label = "이름 없는 자리";

            [Tooltip("채우면 이 이름의 정식 스폰 마커를 찾아 그 자리에 선다 " +
                     "(좌표를 베끼지 않는다). 비우면 아래 position/yaw를 쓴다")]
            public string markerName = "";

            public Vector3 position;
            [Tooltip("바라볼 방향 (도). 0 = +Z")]
            public float yaw;
            [Tooltip("위아래 시선 (도). 양수가 아래")]
            public float pitch;
        }

        [Tooltip("끄면 씬에 저장된 워커 자리를 그대로 쓴다 (원래 동작)")]
        public bool useOnPlay = true;

        [Tooltip("몇 번째 자리에서 시작할지")]
        public int index;

        public Spot[] spots;

        public int Count => spots != null ? spots.Length : 0;
        public string LabelOf(int i) =>
            spots != null && i >= 0 && i < spots.Length ? spots[i].label : "―";

        void Start()
        {
            if (!useOnPlay) return;
            // 씬 전환으로 들어온 것이면 정식 마커가 임자다
            if (SceneTransition.IsTransitioning) return;
            Apply();
        }

        /// <summary>고른 자리로 워커를 옮긴다. 에디터 메뉴도 이것을 부른다.</summary>
        public void Apply()
        {
            if (spots == null || spots.Length == 0) return;
            int i = Mathf.Clamp(index, 0, spots.Length - 1);
            var s = spots[i];

            Vector3 pos = s.position;
            float yaw = s.yaw;
            if (!string.IsNullOrEmpty(s.markerName))
            {
                var marker = GameObject.Find(s.markerName);
                if (marker != null) { pos = marker.transform.position; yaw = marker.transform.eulerAngles.y; }
                else Debug.LogWarning($"[검증 시작] 마커 '{s.markerName}' 를 못 찾았다 — 적어 둔 좌표를 쓴다");
            }
            pos += Vector3.up * 0.1f;   // 바닥에 살짝 띄워 놓는다 (씬 전환과 같은 여유)

            var walk = FindFirstObjectByType<DebugWalkController>(FindObjectsInactive.Exclude);
            if (walk == null) { Debug.LogWarning("[검증 시작] 이 씬에 디버그 워커가 없다"); return; }

            // ⚠️ CharacterController가 켜져 있으면 위치 대입이 먹지 않는다 (내부 위치를 따로 들고 있다).
            var cc = walk.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            walk.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            if (cc != null) cc.enabled = true;
            walk.Pitch = s.pitch;

            Debug.Log($"[검증 시작] {s.label} — {pos.ToString("F2")} yaw {yaw:F0} pitch {s.pitch:F0}");
        }
    }
}
