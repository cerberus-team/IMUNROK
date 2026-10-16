using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>등불을 내려놓는 자리</b> — 등경(燈檠).
    ///
    /// 등불에는 여태 손이 하나 묶여 있었다. 종이를 한 손에 들고 등불을 다른 손에 들면
    /// <b>돋보기를 들 손이 없다</b>. 그래서 배접 속을 비춰 놓고도 그 글자를 확대해
    /// 읽을 수가 없었다 — 두 도구가 서로를 막고 있었던 셈이다.
    ///
    /// 내려놓으면 풀린다. 놓인 등불은 <see cref="Firelight"/> 를 그대로 지니므로
    /// <b>그 자리에서 계속 비춘다</b>(배접을 읽는 것은 등불의 재주가 아니라 불의
    /// 재주다). 손은 둘 다 빈다. 대신 <b>그 자리를 떠날 수 없다</b> — 들고 다니면
    /// 어디서나 비추되 한 손이 묶이고, 내려놓으면 손이 풀리되 발이 묶인다.
    /// 그것이 이 물건이 만드는 선택이다.
    ///
    /// 붙이는 곳: 등경 뿌리(콜라이더와 함께). <see cref="_prop"/> 에는 <b>놓였을 때
    /// 보일 등불</b>을 걸어 두고 꺼 둔다 — 그 등불에 빛(Light)과 Firelight 가
    /// 붙어 있어야 놓았을 때 제 몫을 한다.
    ///
    /// <b>빈 등경으로 시작한다.</b> 처음부터 불이 놓여 있으면 플레이어가 제 등불을
    /// 내려놓을 까닭이 없다. 방은 어둡고 등경은 비어 있어야, 들고 온 불을 여기 얹는
    /// 일이 제 손으로 하는 일이 된다.
    /// </summary>
    public class LanternStand : MonoBehaviour, IInspectable, ISelectable
    {
        [Tooltip("이 도구를 들고 있을 때만 내려놓을 수 있다")]
        [SerializeField] private string _toolId = "lantern";

        [Tooltip("놓였을 때 보일 등불. 빛(Light)과 Firelight 가 붙어 있어야 한다. 처음엔 꺼 둔다")]
        [SerializeField] private GameObject _prop;

        [Tooltip("이 거리(m) 안에서만 놓고 집을 수 있다")]
        [SerializeField] private float _maxTouchDistance = 2.5f;

        [SerializeField] private string _title = "등경";

        [Tooltip("비었고 등불도 없을 때")]
        [TextArea(2, 3)] [SerializeField] private string _emptyBody = "등잔을 얹는 자리다. 걸이가 비어 있다.";

        [Tooltip("비었고 등불을 손에 들었을 때")]
        [TextArea(2, 3)] [SerializeField] private string _readyBody = "여기 등불을 얹으면 두 손이 빈다.";

        [Tooltip("등불이 놓여 있을 때")]
        [TextArea(2, 3)] [SerializeField] private string _litBody = "등불이 걸려 있다. 이 자리를 비추고 있다.";

        [SerializeField] private string _readyHint = "(눌러 얹기)";
        [SerializeField] private string _litHint = "(눌러 도로 들기)";

        /// <summary>지금 등불이 얹혀 있나.</summary>
        public bool Loaded => _prop != null && _prop.activeSelf;

        /// <summary>얹힌 등불의 불꽃 자리 — 빛(Light)이 있는 데. 없으면 등경 위 한 자.</summary>
        public Vector3 LampPosition
        {
            get
            {
                var lt = _prop != null ? _prop.GetComponentInChildren<Light>(true) : null;
                return lt != null ? lt.transform.position : transform.position + Vector3.up * 0.98f;
            }
        }

        private void Awake()
        {
            if (_prop != null) _prop.SetActive(false);
            if (GetComponent<Collider>() == null)
                Debug.LogWarning($"[{name}] 콜라이더가 없어 눌러도 잡히지 않습니다.", this);
        }

        // ── 가리키기 ──
        public string GetInspectTitle() => _title;

        public string GetInspectBody()
        {
            if (Loaded) return Join(_litBody, _litHint);
            if (Holding) return Join(_readyBody, _readyHint);
            return _emptyBody;
        }

        public void OnInspected() { }

        private static string Join(string body, string hint)
            => string.IsNullOrEmpty(hint) ? body : body + "\n" + hint;

        /// <summary>등불을 손에 들고 있나.</summary>
        private bool Holding => ToolbeltHud.SelectedToolId == _toolId;

        // ── 어두운 데 서 있으면 한 번 일러 준다 ──

        [Header("일러 주기")]
        [Tooltip("이 거리(m) 안으로 들어와 이만큼 머물면 한 마디 한다. 0 이면 아무 말도 안 한다")]
        // 7m 는 <b>방만 했다</b>. 서고가 12×6 이라 어디에 서 있든 등경 안쪽이어서,
        // 「등경에 다가섰다」가 아니라 「서고에 들어섰다」로 떴다. 게다가 등경이 셋이라
        // 셋이 저마다 제 몫을 말해 같은 말을 세 번 넘겨야 했다. 팔 닿을 만큼 와야 한다.
        [SerializeField] private float _nudgeWithin = 3f;
        [SerializeField] private float _nudgeAfter = 0.5f;

        [TextArea(2, 3)] [SerializeField] private string _nudgeEmptyHanded =
            "어둡다. 등불을 들고 와 등경에 얹어야 뒤질 만하겠다.";
        [TextArea(2, 3)] [SerializeField] private string _nudgeHolding =
            "등불을 든 채로는 한 손뿐이다. *등경*에 얹으면 두 손이 빈다.";

        private float _near;

        /// <summary>
        /// <b>등경을 통틀어 한 번만</b> 말한다.
        ///
        /// 등경은 서고에 셋이다. 저마다 제 몫을 일러 주면 같은 말을 세 번 읽어야 하고,
        /// 그러면 일러 주기가 <b>안내가 아니라 방해</b>가 된다. 일러 줄 것은 「등불을
        /// 얹는다」는 한 가지이지 「이 등경에 얹는다」가 아니므로, 셋 가운데 먼저 닿은
        /// 하나가 말하고 나면 나머지는 잠자코 있는다. 씬을 다시 읽으면 도로 풀린다.
        /// </summary>
        private static bool _nudgedAnywhere;

        private void OnEnable()
        {
            // 씬을 갈아 들어올 때마다 처음으로 돌린다 — static 은 씬을 넘어 산다.
            if (All == 0) _nudgedAnywhere = false;
            All++;
        }

        private void OnDisable() { All--; if (All < 0) All = 0; }

        private static int All;

        /// <summary>
        /// <b>어두운데 아무 말도 없으면 그건 고장으로 읽힌다.</b>
        ///
        /// 서고는 일부러 어둡다(<see cref="RoomDarkness"/>). 그래야 등불이 걸치는
        /// 물건이 아니라 <b>쓰는</b> 물건이 된다. 그런데 들어선 사람에게는 그 뜻이
        /// 안 보인다 — 캄캄한 방에 대장이 어디 있는지도 모르고 서 있으면, 어둠이
        /// 설계인지 탈인지 알 길이 없다.
        ///
        /// 그래서 <b>서성이면</b> 한 마디 한다. 문을 열자마자 말하지 않는 까닭은,
        /// 들어서는 그 순간에는 스스로 둘러볼 틈을 주어야 하기 때문이다. 다섯 셈쯤
        /// 헤매고 나서야 일러 준다 — 그리고 <b>한 번만</b> 한다.
        ///
        /// 손에 등불이 있으면 말이 달라진다. 없으면 "가져와야겠다", 있으면
        /// "얹으면 두 손이 빈다" — 사람이 지금 어디쯤 와 있는지에 대고 말한다.
        /// </summary>
        private void Update()
        {
            if (_nudgedAnywhere || _nudgeWithin <= 0f || Loaded) return;

            var cam = Camera.main;
            if (cam == null) return;
            if (Vector3.Distance(cam.transform.position, transform.position) > _nudgeWithin)
            { _near = 0f; return; }

            _near += Time.deltaTime;
            if (_near < _nudgeAfter) return;

            _nudgedAnywhere = true;
            SubtitleView.Show("", Holding ? _nudgeHolding : _nudgeEmptyHanded, "", true);
        }

        // ── 누르기 ──
        public void OnHoverEnter() { }
        public void OnHoverExit() { }

        public void OnSelect()
        {
            var cam = Camera.main;
            if (cam != null && _maxTouchDistance > 0f &&
                ModelBounds.DistanceTo(transform, cam.transform.position) > _maxTouchDistance)
                return;
            if (_prop == null) return;

            var belt = ToolbeltHud.Instance;
            if (Loaded)
            {
                // 도로 든다. 벨트에 없으면 도로 넣어 주고 든다 — 얹을 때 빼 두었으므로
                // 여기서 돌려주지 않으면 영영 못 든다.
                if (belt == null) return;
                if (_taken != null) belt.GrantAt(_taken, _takenAt);   // 뒤가 아니라 <b>제자리</b>로
                if (!belt.SelectTool(_toolId)) return;
                _taken = null;
                _prop.SetActive(false);
            }
            else
            {
                if (!Holding) return;
                _prop.SetActive(true);

                // <b>벨트에서 빼 둔다.</b> 손만 비우면(Select(0)) 등불은 여전히 띠에
                // 걸려 있어, Q 를 한 바퀴 돌리면 <b>등경 위의 불과 손 안의 불이 둘</b>이
                // 된다 — 하나뿐인 물건이 둘이 되는 것은 어떤 셈으로도 맞지 않는다.
                // 얹어 둔 동안에는 아예 없는 물건이 되고, 도로 집을 때 돌려받는다.
                _taken = Find(belt, _toolId);
                if (_taken != null) { _takenAt = belt.IndexOf(_taken); belt.Revoke(_taken); }
                else belt.Select(0);
            }
        }

        /// <summary>얹어 두는 동안 벨트에서 빼 놓은 도구. 도로 집을 때 돌려준다.</summary>
        private ToolDef _taken;

        /// <summary>뺄 때 띠의 몇째였나 — 도로 넣을 때 그 자리로 돌려놓는다.</summary>
        private int _takenAt = -1;

        private static ToolDef Find(ToolbeltHud belt, string id)
        {
            if (belt == null || string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < belt.Tools.Count; i++)
                if (belt.Tools[i] != null && belt.Tools[i].id == id) return belt.Tools[i];
            return null;
        }
    }
}
