using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>닫힌 문 앞에서 한 번 일러 준다.</b>
    ///
    /// <b>왜 필요했나.</b> 문서고는 일부러 어둡다(<see cref="RoomDarkness"/>). 그런데
    /// 나가는 길인 판문 여섯도 <b>닫힌 채</b>라, 등불을 등경에 얹어 두고 돌아 나서려 하면
    /// 어두운 벽에 막힌다 — 문이 거기 있는 줄을 모르니 <b>나가지지가 않는다</b>로 읽힌다.
    /// 살펴보기 안내는 정확히 <b>겨눴을 때만</b> 뜨는데, 캄캄한 데서는 무엇을 겨눠야
    /// 할지부터가 모르는 일이다.
    ///
    /// 그래서 문 앞에 다가서면 한 마디 한다. <b>한 번만</b> 한다 — 두 번째부터는
    /// 안내가 아니라 잔소리다. 그리고 <b>문이 아직 닫혀 있을 때만</b> 한다.
    ///
    /// <b>등불을 두고 가는 것도 같이 이른다.</b> 등경에 얹어 둔 채 나서면 다음 방이
    /// 캄캄하다. 손에 없는 것을 손에 없다고 알아채기는 어려우므로, 문을 여는 말 옆에
    /// 한 줄 붙여 준다.
    ///
    /// 붙이는 곳: 문 무리 한가운데 빈 것 하나. 콜라이더는 필요 없다 —
    /// 눈이 어디 있는지만 보면 되는 일이다.
    /// </summary>
    public class ShutDoorNudge : MonoBehaviour
    {
        [Tooltip("지켜볼 문들. 비우면 아래 거리 안의 SwingDoor 를 스스로 찾는다")]
        [SerializeField] private SwingDoor[] _doors;

        [Tooltip("이 거리(m) 안으로 들어오면 이른다")]
        [SerializeField] private float _within = 3.2f;

        [Tooltip("이만큼(초) 머물러야 이른다. 스쳐 지나가는 데 말이 붙으면 안 된다")]
        [SerializeField] private float _after = 0.6f;

        [TextArea(2, 3)]
        [Tooltip("문이 닫혀 있을 때의 한 마디")]
        [SerializeField] private string _line = "판문이 닫혀 있다. *눌러* 열고 나선다.";

        [TextArea(2, 3)]
        [Tooltip("등불을 등경에 얹어 둔 채일 때 덧붙이는 한 마디. 비우면 안 붙인다")]
        [SerializeField] private string _lanternLine = "등불은 등경에 얹어 둔 채다 — 가지고 갈 것이면 도로 집는다.";

        private float _near;
        private bool _said;

        /// <summary>
        /// <b>한 번 멀어진 적이 있나.</b>
        ///
        /// 문서고에 들어서는 자리가 판문에서 한 걸음 거리라, 여태 <b>발을 딛자마자</b>
        /// 「판문이 닫혀 있다, 눌러 열고 나선다」가 떴다 — 방금 들어온 사람에게
        /// 나가는 법부터 일러 준 꼴이다. 이 말은 <b>돌아 나설 때</b> 쓸모가 있지
        /// 들어설 때 쓸모가 있는 것이 아니다.
        ///
        /// 그래서 한 번 방 안으로 들어갔다 오기 전에는 입을 열지 않는다.
        /// </summary>
        private bool _wentIn;

        private void Awake()
        {
            if (_doors != null && _doors.Length > 0) return;
            var all = FindObjectsByType<SwingDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var list = new System.Collections.Generic.List<SwingDoor>();
            foreach (var d in all)
                if (Vector3.Distance(d.transform.position, transform.position) <= _within * 2.5f) list.Add(d);
            _doors = list.ToArray();
        }

        /// <summary>지켜보는 문이 <b>하나라도</b> 열려 있나.</summary>
        private bool AnyOpen
        {
            get
            {
                if (_doors == null) return false;
                foreach (var d in _doors) if (d != null && d.IsOpen) return true;
                return false;
            }
        }

        /// <summary>등불이 어느 등경엔가 얹혀 있나.</summary>
        private static bool LanternLeft
        {
            get
            {
                var all = FindObjectsByType<LanternStand>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var s in all) if (s != null && s.Loaded) return true;
                return false;
            }
        }

        private void Update()
        {
            if (_said || _within <= 0f) return;

            // 문을 한 번 열고 나면 더 일러 줄 것이 없다 — 길을 이미 안 것이다.
            if (AnyOpen) { _said = true; return; }

            var cam = Camera.main;
            if (cam == null) return;
            if (Vector3.Distance(cam.transform.position, transform.position) > _within)
            {
                _near = 0f;
                _wentIn = true;      // 방 안으로 들어갔다 — 이제부터는 돌아 나서는 길이다
                return;
            }
            if (!_wentIn) return;

            _near += Time.deltaTime;
            if (_near < _after) return;

            _said = true;
            string say = _line;
            if (!string.IsNullOrEmpty(_lanternLine) && LanternLeft) say += "\n" + _lanternLine;
            SubtitleView.Show("", say, "", true);
        }
    }
}
