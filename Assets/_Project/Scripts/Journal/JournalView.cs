using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IMUNROK.Common
{
    /// <summary>
    /// 수첩(手帖) — J로 펼쳐 사건 개요와 단서를 카드로 보여준다.
    ///  · 한지 테마(_paperTex/_font)면 두루마리 느낌으로, 없으면 어두운 기본.
    ///  · 단서마다 카드(문구 + 상황 그림 썸네일). 그림 카드는 클릭하면 크게 펼쳐짐.
    ///  · 심문 중이면 카드에 "들이밀기"(증거 제시) 버튼이 뜬다.
    /// 한지/폰트는 [이문록 ▸ 연출: 심문 한지 테마 적용]이 함께 넣어준다.
    /// </summary>
    public class JournalView : MonoBehaviour
    {
        [SerializeField] private bool _startOpen = false;

        [TextArea(3, 10)]
        [Tooltip("사건 개요(봉서 요지). 처음부터 볼 수 있음")]
        [SerializeField] private string _caseBrief = "";

        [Header("한지 테마(선택)")]
        [SerializeField] private Texture2D _paperTex;
        [SerializeField] private Font _font;
        [Range(0.3f, 1f)] [SerializeField] private float _paperAlpha = 1f;   // 수첩은 불투명(책처럼)

        private bool _open;
        private Vector2 _scrollL, _scrollR;   // 좌(정황)·우(물증) 페이지 스크롤
        private Texture2D _zoomImg;   // 크게 펼친 증거 그림(null=닫힘)

        /// <summary>수첩이 펼쳐져 있나(다른 UI가 참고해 자기를 숨김).</summary>
        public static bool AnyOpen { get; private set; }

        private GUIStyle _titleStyle, _caseStyle, _clueStyle, _hintStyle, _btnStyle;
        private Texture2D _card, _thumbBg, _dim, _divider;

        private void Awake() => _open = _startOpen;

        public void Toggle() => _open = !_open;
        public void Open() => _open = true;
        public void Close() => _open = false;

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && kb.jKey.wasPressedThisFrame) _open = !_open;
#endif
            AnyOpen = _open;   // 다른 UI가 참고
        }

        private void OnGUI()
        {
            EnsureStyles();

            // 닫힘: 좌하단(왼손잡이면 우하단) 코너 버튼
            if (!_open)
            {
                const float bw = 150f, bh = 32f, m = 16f;
                float bxc = HudSide.LeftHanded ? (Screen.width - m - bw) : m;
                if (GUI.Button(new Rect(bxc, Screen.height - bh - m, bw, bh), "수첩 (J)", _btnStyle))
                    _open = true;
                return;
            }

            // (검정 배경 없음 — 뒤 배경은 보이고, 수첩만 불투명하게 화면에 고정)
            float w = Mathf.Min(860f, Screen.width - 80f);
            float h = Mathf.Min(760f, Screen.height - 80f);
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;

            PanelBg(new Rect(x, y, w, h));
            GUI.Label(new Rect(x + 24, y + 16, w - 140, 30), "수첩 (手帖)", _titleStyle);
            if (GUI.Button(new Rect(x + w - 96, y + 14, 82, 28), "✕ 닫기 (J)", _btnStyle)) _open = false;

            var gs = GameState.Instance;
            if (!gs.InCase)
            {
                GUI.Label(new Rect(x + 24, y + 60, w - 48, 44),
                    "조사청 (사건 밖) — 사건에 들어가면 여기 단서가 보인다.", _clueStyle);
                DrawZoom();
                return;
            }
            CaseId current = gs.CurrentCase.Value;
            var clues = Journal.Instance.GetClues(current);
            bool talking = InterrogationController.AnyOpen && InterrogationController.Active != null;

            // ── 상단: 사건 제목 + 개요(두 페이지 위에 걸침) ──
            float hx = x + 24f, hy = y + 52f;
            GUI.Label(new Rect(hx, hy, w - 48f, 26f), CaseTitle(current), _caseStyle); hy += 30f;
            if (!string.IsNullOrEmpty(_caseBrief))
            {
                float bh2 = Mathf.Min(_clueStyle.CalcHeight(new GUIContent(_caseBrief), w - 56f), 84f);
                GUI.Label(new Rect(hx, hy, w - 48f, bh2), _caseBrief, _clueStyle); hy += bh2 + 8f;
            }

            // ── 펼친 책: 왼쪽=정황, 오른쪽=물증, 가운데 책등 ──
            var jung = new List<ClueEntry>();
            var mul = new List<ClueEntry>();
            foreach (var c in clues) { if (c.kind == ClueKind.정황) jung.Add(c); else mul.Add(c); }

            float colTop = hy + 6f;
            float colBottom = y + h - 20f;
            float colH = colBottom - colTop;
            float centerX = x + w * 0.5f;
            GUI.DrawTexture(new Rect(centerX - 1f, colTop, 2f, colH), _divider);   // 책등

            const float colGap = 26f;
            float colW = w * 0.5f - 24f - colGap * 0.5f;
            DrawColumn(new Rect(x + 24f, colTop, colW, colH), "정황(情況)", jung, ref _scrollL, current, talking, "· (아직 들은 정황이 없다)");
            DrawColumn(new Rect(centerX + colGap * 0.5f, colTop, colW, colH), "물증(物證)", mul, ref _scrollR, current, talking, "· (아직 찾은 물증이 없다)");

            DrawZoom();
        }

        // 한 페이지(정황 또는 물증) — 제목 + 카드 스크롤
        private void DrawColumn(Rect area, string header, List<ClueEntry> list, ref Vector2 scroll,
                                CaseId caseId, bool talking, string emptyMsg)
        {
            GUI.Label(new Rect(area.x, area.y, area.width, 22f), "◈ " + header, _caseStyle);
            Rect view = new Rect(area.x, area.y + 26f, area.width, area.height - 26f);
            float iw = view.width - 18f;

            float content = 0f;
            foreach (var c in list) content += CardHeight(c, caseId, iw, talking) + 8f;
            if (list.Count == 0) content += 26f;

            scroll = GUI.BeginScrollView(view, scroll, new Rect(0, 0, iw, content));
            float cy = 0f;
            if (list.Count == 0)
                GUI.Label(new Rect(4f, cy, iw - 8f, 22f), emptyMsg, _clueStyle);
            else
                foreach (var c in list)
                {
                    float ch = CardHeight(c, caseId, iw, talking);
                    DrawCard(new Rect(0, cy, iw, ch), c, caseId, talking);
                    cy += ch + 8f;
                }
            GUI.EndScrollView();
        }

        // 단서 카드 하나
        private void DrawCard(Rect r, ClueEntry c, CaseId caseId, bool talking)
        {
            GUI.DrawTexture(r, _card);
            Texture2D img = Journal.Instance.GetClueImage(caseId, c.key);

            float pad = 10f;
            float thumb = img != null ? r.height - pad * 2f : 0f;
            float tx = r.x + pad;
            if (img != null)
            {
                GUI.DrawTexture(new Rect(tx - 2, r.y + pad - 2, thumb + 4, thumb + 4), _thumbBg);
                GUI.DrawTexture(new Rect(tx, r.y + pad, thumb, thumb), img, ScaleMode.ScaleToFit);
                if (GUI.Button(new Rect(tx, r.y + pad, thumb, thumb), GUIContent.none, GUIStyle.none))
                    _zoomImg = img;   // 클릭하면 크게
                tx += thumb + 12f;
            }

            bool canPresent = talking && !c.key.EndsWith("_revealed");
            float btnW = canPresent ? 90f : 0f;
            float textW = r.xMax - tx - pad - btnW;
            GUI.Label(new Rect(tx, r.y + pad, textW, r.height - pad * 2f), "· " + c.text, _clueStyle);

            if (canPresent && GUI.Button(new Rect(r.xMax - pad - 84f, r.y + (r.height - 26f) * 0.5f, 84f, 26f), "제시하기", _btnStyle))
                InterrogationController.Active.PresentFromJournal(c);
        }

        private float CardHeight(ClueEntry c, CaseId caseId, float iw, bool talking)
        {
            bool canPresent = talking && !c.key.EndsWith("_revealed");
            bool hasImg = Journal.Instance.GetClueImage(caseId, c.key) != null;
            float pad = 10f;
            float thumb = hasImg ? 72f : 0f;
            float btnW = canPresent ? 90f : 0f;
            float textW = iw - pad - (hasImg ? thumb + 12f : 0f) - pad - btnW;
            float textH = _clueStyle.CalcHeight(new GUIContent("· " + c.text), Mathf.Max(40f, textW));
            return Mathf.Max(thumb, textH, 26f) + pad * 2f;
        }

        // 크게 펼친 증거 그림
        private void DrawZoom()
        {
            if (_zoomImg == null) return;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _dim);
            float mw = Screen.width * 0.7f, mh = Screen.height * 0.7f;
            GUI.DrawTexture(new Rect((Screen.width - mw) * 0.5f, (Screen.height - mh) * 0.5f, mw, mh),
                _zoomImg, ScaleMode.ScaleToFit);
            GUI.Label(new Rect(0, Screen.height - 40, Screen.width, 24), "(아무 곳이나 눌러 닫기)", _hintStyle);
            if (GUI.Button(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none, GUIStyle.none))
                _zoomImg = null;
        }

        private void PanelBg(Rect r)
        {
            if (_paperTex != null)
            {
                var prev = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, _paperAlpha);
                GUI.DrawTexture(r, _paperTex, ScaleMode.ScaleAndCrop);
                GUI.color = prev;
            }
            else GUI.DrawTexture(r, _card);
        }

        private static string CaseTitle(CaseId id) => id switch
        {
            CaseId.Case1_Onggojip => "제일사건 · 옹고집전",
            CaseId.Case2_Seocheon => "제이사건 · 서천꽃밭",
            CaseId.Case3_Gyeonu   => "제삼사건 · 견우직녀",
            _ => id.ToString(),
        };

        private void EnsureStyles()
        {
            if (_clueStyle != null) return;
            bool paper = _paperTex != null;
            Color ink = new Color(0.16f, 0.11f, 0.07f);

            _card    = Solid(paper ? new Color(0f, 0f, 0f, 0.05f) : new Color(0.05f, 0.05f, 0.07f, 0.9f));
            _thumbBg = Solid(new Color(0f, 0f, 0f, 0.3f));
            _dim     = Solid(new Color(0f, 0f, 0f, 0.85f));
            _divider = Solid(paper ? new Color(0.4f, 0.26f, 0.15f, 0.45f) : new Color(1f, 1f, 1f, 0.15f));   // 책등

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20, fontStyle = FontStyle.Bold,
                normal = { textColor = paper ? new Color(0.5f, 0.15f, 0.1f) : new Color(1f, 0.85f, 0.4f) }
            };
            _caseStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16, fontStyle = FontStyle.Bold,
                normal = { textColor = paper ? ink : new Color(0.8f, 0.88f, 1f) }
            };
            _clueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15, wordWrap = true,
                normal = { textColor = paper ? ink : Color.white }
            };
            _hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = paper ? new Color(0.4f, 0.2f, 0.12f) : new Color(1f, 1f, 1f, 0.6f) }
            };
            _btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 13 };

            if (_font != null)
            {
                _titleStyle.font = _font; _caseStyle.font = _font; _clueStyle.font = _font;
                _hintStyle.font = _font; _btnStyle.font = _font;
            }
        }

        private static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            t.hideFlags = HideFlags.HideAndDontSave;
            return t;
        }
    }
}
