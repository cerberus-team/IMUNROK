using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common
{
    /// <summary>
    /// 사랑방을 따로 씬으로 떼어 놓으면 끊기는 자리들을 <b>이름으로 다시 잇는다</b>.
    ///
    /// 왜 필요한가: 유니티는 씬을 건너뛰는 참조를 저장하지 못한다. 인스펙터에서 끌어다
    /// 놓아도 저장하는 순간 비워진다. 그래서 실내 씬이 올라온 뒤에 이름으로 찾아 물려 준다.
    ///
    /// 끊기는 자리는 다섯이다 — 甲이 나갈 문, 보료 경첩, 문갑 서랍 뼈와 그 속 문서,
    /// 그리고 내가 앉을 방석. 전부 마당 쪽 부품이 실내 쪽 물건을 가리키는 방향이다.
    ///
    /// <b>이름으로 잇는 것이 위험하지 않은가</b>: 위험하다. 이름을 바꾸면 조용히 끊긴다.
    /// 그래서 못 찾은 것은 남김없이 경고로 찍는다 — 조용히 실패하지 않는 것이 여기서는
    /// 잇는 것보다 중요하다.
    /// </summary>
    public class SarangbangBinder : MonoBehaviour
    {
        [Tooltip("실내가 들어 있는 씬 이름. 이 씬이 올라오면 잇는다")]
        [SerializeField] private string _sceneName = "Onggojip_사랑방";

        [Header("마당 쪽 부품 — 실내 물건을 가리켜야 하는 것들")]
        [SerializeField] private BokdongController _gap;
        [SerializeField] private AshRake _bojaLift;
        [SerializeField] private AshRake _mungapDrawer;
        [SerializeField] private PlayerSeat _seat;
        [SerializeField] private InteriorSceneSwap _swap;

        [Header("찾을 이름")]
        [SerializeField] private string _exitDoorName = "쪽문_서";
        [SerializeField] private string _bojaHingeName = "보료_경첩";
        [SerializeField] private string _drawerBoneName = "Dummy053_00";
        [SerializeField] private string _drawerBoxName = "빠진_서랍";
        [SerializeField] private string _cushionName = "방석";
        [SerializeField] private string _sitSpotName = "甲_보료자리";

        private bool _bound;

        private void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; TryBindLoaded(); }
        private void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

        private void OnSceneLoaded(Scene s, LoadSceneMode mode)
        {
            if (s.name == _sceneName) Bind(s);
        }

        /// <summary>이미 올라와 있으면(에디터에서 두 씬을 함께 열어 둔 경우) 곧바로 잇는다.</summary>
        private void TryBindLoaded()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.isLoaded && s.name == _sceneName) { Bind(s); return; }
            }
        }

        /// <summary>실내 씬 안에서 이름으로 찾아 마당 쪽 부품에 물려 준다.</summary>
        public void Bind(Scene interior)
        {
            if (_bound) return;

            var found = new System.Collections.Generic.Dictionary<string, Transform>();
            foreach (var root in interior.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (!found.ContainsKey(t.name)) found[t.name] = t;

            var missing = new System.Text.StringBuilder();
            System.Func<string, Transform> get = delegate(string n)
            {
                if (string.IsNullOrEmpty(n)) return null;
                Transform t;
                if (found.TryGetValue(n, out t)) return t;
                missing.Append(' ').Append(n);
                return null;
            };

            var exitDoor = get(_exitDoorName);
            var bojaHinge = get(_bojaHingeName);
            var drawerBone = get(_drawerBoneName);
            var drawerBox = get(_drawerBoxName);
            var cushion = get(_cushionName);

            if (_gap != null && exitDoor != null)
                _gap.BindLeaveDoor(exitDoor.GetComponent<DoorController>(), null, null);

            if (_bojaLift != null && bojaHinge != null) _bojaLift.BindHinge(bojaHinge);

            if (_mungapDrawer != null)
            {
                if (drawerBone != null) _mungapDrawer.BindHinge(drawerBone);
                if (drawerBox != null) _mungapDrawer.BindAfter(drawerBox.gameObject);
            }

            // 앉을 자리는 방석이고, 마주 볼 것은 甲의 보료 자리다. 보료 자리는 마당 쪽에
            // 있으므로 여기서 찾지 않는다 — 실내에서 찾을 것은 방석뿐이다.
            if (_seat != null && cushion != null)
            {
                Transform look = null;
                var spot = GameObject.Find(_sitSpotName);
                if (spot != null) look = spot.transform;
                _seat.BindSeat(cushion, look);
            }

            if (_swap != null) _swap.BindInterior(interior);

            _bound = true;

            if (missing.Length > 0)
                Debug.LogWarning("[사랑방 잇기] 실내 씬에서 못 찾은 이름:" + missing +
                                 "  — 이름이 바뀌었는지 확인하시오.", this);
            else
                Debug.Log("[사랑방 잇기] 실내 씬을 다 이었습니다.", this);
        }
    }
}
