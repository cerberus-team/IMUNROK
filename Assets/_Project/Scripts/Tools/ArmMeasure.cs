using System.Collections;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>걷힌 팔을 눈앞에 두고 유척을 댄다.</b>
    ///
    /// 소매를 걷게 하면(<see cref="SleeveOrder"/>) 이 오브젝트가 켜진다. 여태 켜지는 것은
    /// <b>보이지 않는 상자 하나</b>였다 — 팔 모형이 없으니 무엇을 재는지 보이지 않았고, 유척은
    /// 화면 한가운데로 그 상자를 겨누어야 재어졌다. 「두 치 남짓」이 사건을 끝내는 장면인데
    /// 그 장면이 화면에 없었다.
    ///
    /// 이제 켜지는 순간 팔을 <b>물증 보는 판의 무대</b>(<see cref="EvidenceStage"/>)에 올린다.
    /// 끌어서 돌려 보고, 도구띠에서 유척을 고른 뒤 <see cref="ToolRaise"/>(F)를 누르고 있으면
    /// 놋쇠 자가 팔 안쪽 흉터 위에 놓이고 눈금을 읽는다. 다 읽으면 옆의 <see cref="Measurable"/> 이
    /// 재어 수첩에 적는다(G07 · G08). 판을 닫았다가도 팔을 짚으면 다시 올라온다.
    ///
    /// 甲 과 乙 에 똑같이 붙인다 — 같은 자를 양쪽 팔에 대는 것이 어사의 일이다.
    /// </summary>
    [RequireComponent(typeof(Measurable))]
    public class ArmMeasure : MonoBehaviour, ISelectable
    {
        [Header("무대에 올릴 팔")]
        [SerializeField] private GameObject _armPrefab;
        [Tooltip("무대에 처음 세우는 자세. 팔 안쪽이 눈을 보게")]
        [SerializeField] private Vector3 _armEuler = new Vector3(-65f, 0f, 0f);
        [SerializeField] private string _title = "왼팔 안쪽";
        [Tooltip("무대에 올렸을 때의 처음 거리(1 = 팔 전체가 꽉 차게). 휠로 더 당기고 민다")]
        [SerializeField] private float _startZoom = 0.62f;

        [Header("유척")]
        [Tooltip("자 모형(유척.fbx). 실제 크기 그대로 둔다 — 한 자가 한 자로 보여야 치수가 믿긴다")]
        [SerializeField] private GameObject _rulerPrefab;
        [SerializeField] private Material _rulerMaterial;
        [Tooltip("팔 프리팹 뿌리 기준으로 자를 놓을 자리(흉터 위)")]
        [SerializeField] private Vector3 _rulerLocalPos = new Vector3(0.09f, 0.145f, 0.01f);
        [SerializeField] private Vector3 _rulerLocalEuler = new Vector3(0f, 0f, 9f);
        [Tooltip("눈금을 읽는 데 드는 시간(초)")]
        [SerializeField] private float _measureSeconds = 1.8f;

        [Header("수첩")]
        [Tooltip("잰 뒤 수첩 카드에 걸 이름")]
        [SerializeField] private string _clueTitle = "";
        [Tooltip("수첩 카드에 걸 그림(팔을 찍어 둔 것). 모형만 걸면 카드가 빈 칸이 된다")]
        [SerializeField] private Texture2D _clueImage;

        private const string HintKey = "유척";
        private Measurable _m;
        private bool _open;
        private float _dwell;
        private bool _propShown;

        private void Awake() { _m = GetComponent<Measurable>(); }

        private void OnEnable()
        {
            // 소매가 걷힌 그 칸에는 인물의 대답이 먼저 뜬다 — 한 박자 쉬고 올린다.
            if (Application.isPlaying) StartCoroutine(OpenSoon());
        }

        private IEnumerator OpenSoon()
        {
            yield return new WaitForSeconds(0.6f);
            Open();
        }

        /// <summary>팔을 무대에 올린다.</summary>
        public void Open()
        {
            if (_armPrefab == null) return;
            DocumentView.Show(null, _title, "", null, null, true);
            DocumentView.SetStageModel(_armPrefab, _armEuler);
            // 팔 전체(소매까지)를 담으면 흉터가 손톱만 하다 — 아래팔로 한 걸음 다가선다.
            EvidenceStage.SetZoom(_startZoom);
            _open = true;
            _dwell = 0f;
            _propShown = false;
        }

        private void OnDisable()
        {
            _open = false;
            StatusPanel.Clear(HintKey);
        }

        private void Update()
        {
            if (!_open) return;
            if (!DocumentView.IsOpen || !EvidenceStage.HasModel)
            {
                _open = false;
                _propShown = false;
                StatusPanel.Clear(HintKey);
                return;
            }

            bool ruler = ToolbeltHud.SelectedToolId == "yucheok";
            bool laying = ruler && ToolRaise.Held;

            if (_m.Measured)
            {
                ShowRuler(true);
                StatusPanel.Set(HintKey, 1, "▶ " + _m.ResultLine());
                return;
            }

            if (!ruler)
            {
                ShowRuler(false);
                _dwell = 0f;
                StatusPanel.Set(HintKey, 1, "▶ 도구띠에서 *유척* 을 고른다 (Q) — 그리고 " + Controls.Raise + " 흉터에 댄다");
                return;
            }

            if (!laying)
            {
                ShowRuler(false);
                _dwell = Mathf.Max(0f, _dwell - Time.deltaTime);
                StatusPanel.Set(HintKey, 1, "▶ " + Controls.Raise + " 유척을 팔 안쪽에 댄다");
                return;
            }

            ShowRuler(true);
            _dwell += Time.deltaTime;
            int pct = Mathf.RoundToInt(Mathf.Clamp01(_dwell / Mathf.Max(0.1f, _measureSeconds)) * 100f);
            StatusPanel.Set(HintKey, 1, "▶ 눈금을 읽는 중… " + pct + "%");
            if (_dwell < _measureSeconds) return;

            _m.Measure();
            if (!string.IsNullOrEmpty(_clueTitle) && Journal.Instance != null && !string.IsNullOrEmpty(_m.ClueKey))
            {
                Journal.Instance.SetTitle(_m.Case, _m.ClueKey, _clueTitle);
                if (_clueImage != null) Journal.Instance.AddClue(_m.Case, _m.ClueKey, "", _clueImage);   // 이미 있으면 그림만 건다
                // 수첩에서 다시 돌려 볼 수 있게 팔을 건다.
                Journal.Instance.AttachDocument(_m.Case, _m.ClueKey, null, _clueTitle, "", null, null,
                                                _armPrefab, _armEuler);
            }
            SubtitleView.Show("", _m.ResultLine(), "", true);
        }

        private void ShowRuler(bool on)
        {
            if (on == _propShown) return;
            _propShown = on;
            if (!on) { EvidenceStage.ClearProp(); return; }
            if (_rulerPrefab == null) return;
            EvidenceStage.ShowProp(_rulerPrefab, _rulerLocalPos, Quaternion.Euler(_rulerLocalEuler), _rulerMaterial);
        }

        // ── 걷힌 팔을 짚으면 다시 올린다 ──
        public void OnHoverEnter() { }
        public void OnHoverExit() { }
        public void OnSelect() { if (!DocumentView.IsOpen) Open(); }
    }
}
