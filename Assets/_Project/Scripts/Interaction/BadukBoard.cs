using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>조사청 바둑판에서 혼자 둔다</b> — 흑도 백도 내 손으로.
    ///
    /// 사건 사이에 머리를 식히는 자리다. 상대가 없으니 이기고 지는 것이 아니라 <b>수를 놓아 보는</b>
    /// 일이다 — 기보를 따라 두거나 사활을 풀어 보거나. 그래서 규칙은 바둑의 뼈대만 지킨다:
    ///
    ///   · 흑부터, 한 수씩 번갈아 둔다(쉬기로 차례를 넘길 수 있다).
    ///   · 활로가 없어진 상대 돌은 들어낸다(따낸 돌을 센다).
    ///   · 스스로 활로를 막는 수(자충)는 못 둔다 — 상대를 따내면서 사는 수는 된다.
    ///   · <b>패</b>: 바로 앞 판과 똑같은 모양으로 되돌리는 수는 못 둔다.
    ///   · 무르기(오른쪽 단추)는 몇 번이고 된다 — 혼자 두는 판에서 무르기가 없으면 연구가 안 된다.
    ///
    /// <b>줄은 판의 그림에서 쟀다.</b> SM_Baduk01a 윗면을 위에서 찍어 줄 열아홉씩을 짚었다 —
    /// 가로 ±0.2127m, 세로 ±0.1951m(판 기준). 판을 바꾸면 <see cref="_halfX"/>·<see cref="_halfZ"/> 를
    /// 다시 잴 것.
    ///
    /// 붙이는 곳: 바둑판(메시가 있는 오브젝트). 누르면 판 앞에 앉아 내려다본다.
    /// </summary>
    public class BadukBoard : MonoBehaviour, ISelectable, IInspectable
    {
        public const int N = 19;

        /// <summary>판 앞에 앉아 두는 중인가. 도구띠·짚기가 이것을 보고 물러난다.</summary>
        public static bool Playing { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Playing = false; }

        [Header("판")]
        [Tooltip("첫 줄에서 끝 줄까지의 반(판 기준 x, m)")]
        [SerializeField] private float _halfX = 0.2127f;
        [Tooltip("첫 줄에서 끝 줄까지의 반(판 기준 z, m)")]
        [SerializeField] private float _halfZ = 0.1951f;
        [Tooltip("줄 한가운데가 판 원점에서 벗어난 만큼(판 기준 x, z)")]
        [SerializeField] private Vector2 _centre = Vector2.zero;
        [Tooltip("판 윗면 높이(판 기준 y). 비워 두면(0) 메시에서 잰다")]
        [SerializeField] private float _topY = 0f;

        [Header("돌")]
        [SerializeField] private float _stoneDiameter = 0.0212f;
        [SerializeField] private float _stoneHeight = 0.0085f;
        [SerializeField] private Color _black = new Color(0.13f, 0.13f, 0.14f);
        [SerializeField] private Color _white = new Color(0.93f, 0.92f, 0.88f);
        [Tooltip("돌 놓는 소리(선택). 여럿이면 돌아가며")]
        [SerializeField] private AudioClip[] _click;

        [Header("앉기")]
        [SerializeField] private float _maxTouchDistance = 2.6f;
        [Tooltip("판 한가운데 위로 눈 높이(m)")]
        [SerializeField] private float _eyeUp = 0.55f;
        [Tooltip("판 한가운데에서 앉은 쪽으로 물러난 거리(m)")]
        [SerializeField] private float _eyeBack = 0.30f;
        [SerializeField] private float _moveSeconds = 0.8f;

        // 0 빈 곳 · 1 흑 · 2 백
        private int[,] _cells = new int[N, N];
        private readonly GameObject[,] _stones = new GameObject[N, N];
        private readonly List<int[,]> _history = new List<int[,]>();
        private readonly List<int> _turnHistory = new List<int>();
        private readonly List<int[]> _capHistory = new List<int[]>();
        private int _turn = 1;
        private readonly int[] _captured = new int[3];   // [1] 흑이 따낸 수 · [2] 백이 따낸 수
        private int[,] _before;                          // 바로 앞 판(패)

        private Camera _cam;
        private Vector3 _fromPos, _toPos;
        private Quaternion _fromRot, _toRot;
        private float _t;
        private int _phase;          // 0 없음 · 1 앉는 중 · 2 두는 중 · 3 일어나는 중
        private GameObject _ghost;
        private Material _blackMat, _whiteMat, _ghostBlack, _ghostWhite;
        private GameObject _hud;
        private TextMeshProUGUI _status, _help;
        private AudioSource _audio;
        private int _clickTurn;
        private string _note = "";
        private float _noteUntil;

        private void Awake()
        {
            if (Mathf.Approximately(_topY, 0f))
            {
                var mf = GetComponentInChildren<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) _topY = mf.sharedMesh.bounds.max.y;
            }
            if (GetComponentInChildren<Collider>() == null)
            {
                var box = gameObject.AddComponent<BoxCollider>();
                var mf = GetComponentInChildren<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) { box.center = mf.sharedMesh.bounds.center; box.size = mf.sharedMesh.bounds.size; }
            }
        }

        // ── 짚기 ─────────────────────────────────

        public string GetInspectTitle() => "바둑판";
        public string GetInspectBody() => Playing ? "" : "누가 두다 만 판이다. 흑도 백도 손수 놓아 볼 수 있다.\n(눌러 판 앞에 앉기)";
        public void OnInspected() { }
        public void OnHoverEnter() { }
        public void OnHoverExit() { }

        public void OnSelect()
        {
            if (_phase != 0 || HubEnding.Playing) return;
            var cam = Camera.main;
            if (cam == null) return;
            if (_maxTouchDistance > 0f && ModelBounds.DistanceTo(transform, cam.transform.position) > _maxTouchDistance) return;
            Sit(cam);
        }

        // ── 앉고 일어서기 ─────────────────────────

        private void Sit(Camera cam)
        {
            _cam = cam;
            _fromPos = cam.transform.position;
            _fromRot = cam.transform.rotation;

            Vector3 centre = GridToWorld(9, 9);
            Vector3 away = _fromPos - centre; away.y = 0f;
            if (away.sqrMagnitude < 0.0001f) away = -transform.forward;
            // 판의 네 변 가운데 사람이 선 쪽에 가장 가까운 변 앞에 앉는다 — 판을 비스듬히 보지 않게
            Vector3 side = Snap(away.normalized);
            _toPos = centre + side * _eyeBack + Vector3.up * _eyeUp;
            _toRot = Quaternion.LookRotation(centre - _toPos, Vector3.up);

            Hands(false);
            Playing = true;
            _phase = 1;
            _t = 0f;
            BuildHud();
            _hud.SetActive(true);
            Refresh();
        }

        private Vector3 Snap(Vector3 dir)
        {
            Vector3 f = transform.forward; f.y = 0f; f.Normalize();
            Vector3 r = transform.right; r.y = 0f; r.Normalize();
            float df = Vector3.Dot(dir, f), dr = Vector3.Dot(dir, r);
            if (Mathf.Abs(df) >= Mathf.Abs(dr)) return df >= 0f ? f : -f;
            return dr >= 0f ? r : -r;
        }

        private void Stand()
        {
            if (_phase != 2) return;
            var p = _toPos; var q = _toRot;
            _toPos = _fromPos; _toRot = _fromRot;
            _fromPos = p; _fromRot = q;
            _phase = 3;
            _t = 0f;
            if (_ghost != null) _ghost.SetActive(false);
            if (_hud != null) _hud.SetActive(false);
        }

        /// <summary>걷기·짚기를 거두거나 돌려준다. 앉아 있는 동안 마우스는 판 위의 손이다.</summary>
        private void Hands(bool on)
        {
            if (_cam == null) return;
            var fly = _cam.GetComponentInParent<DebugFlyCamera>();
            if (fly == null) fly = _cam.GetComponentInChildren<DebugFlyCamera>();
            if (fly != null) { fly.enabled = on; if (on) fly.SyncAngles(); }
            var ray = _cam.GetComponentInParent<MouseRaySelector>();
            if (ray == null) ray = _cam.GetComponentInChildren<MouseRaySelector>();
            if (ray != null) ray.enabled = on;
        }

        private void Update()
        {
            if (_phase == 0) return;

            if (_phase == 1 || _phase == 3)
            {
                _t += Time.deltaTime / Mathf.Max(0.05f, _moveSeconds);
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_t));
                _cam.transform.SetPositionAndRotation(Vector3.Lerp(_fromPos, _toPos, k), Quaternion.Slerp(_fromRot, _toRot, k));
                if (_t < 1f) return;
                if (_phase == 1) { _phase = 2; return; }
                _phase = 0;
                Playing = false;
                Hands(true);
                return;
            }

            if (HubEnding.Playing) { Stand(); return; }
            Play();
        }

        // ── 두기 ─────────────────────────────────

        private void Play()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var ms = Mouse.current;
            if (kb == null || ms == null) return;
            if (Typing.Now) return;
            if (JournalView.AnyOpen) { if (_ghost != null) _ghost.SetActive(false); return; }

            if (kb.escapeKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) { Stand(); return; }
            if (kb.rKey.wasPressedThisFrame) { Clear(); return; }
            if (kb.pKey.wasPressedThisFrame) { Pass(); return; }
            if (ms.rightButton.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) { Undo(); return; }

            int gx, gz;
            bool on = Pick(ms.position.ReadValue(), out gx, out gz);
            bool legal = on && _cells[gx, gz] == 0;
            Ghost(legal, gx, gz);

            if (ms.leftButton.wasPressedThisFrame && legal) Place(gx, gz);
#endif
            if (_noteUntil > 0f && Time.unscaledTime > _noteUntil) { _noteUntil = 0f; _note = ""; Refresh(); }
        }

        private bool Pick(Vector2 screen, out int gx, out int gz)
        {
            gx = gz = -1;
            var ray = _cam.ScreenPointToRay(screen);
            Vector3 top = transform.TransformPoint(new Vector3(_centre.x, _topY, _centre.y));
            var plane = new Plane(transform.up, top);
            float d;
            if (!plane.Raycast(ray, out d)) return false;
            Vector3 local = transform.InverseTransformPoint(ray.GetPoint(d));
            float cx = _halfX * 2f / (N - 1), cz = _halfZ * 2f / (N - 1);
            float fx = (local.x - _centre.x + _halfX) / cx;
            float fz = (local.z - _centre.y + _halfZ) / cz;
            gx = Mathf.RoundToInt(fx);
            gz = Mathf.RoundToInt(fz);
            if (gx < 0 || gx >= N || gz < 0 || gz >= N) return false;
            // 칸 사이 한가운데를 누른 것은 어느 점도 아니다 — 곁에 놓이는 헛손을 막는다
            return Mathf.Abs(fx - gx) < 0.42f && Mathf.Abs(fz - gz) < 0.42f;
        }

        private Vector3 GridToWorld(int gx, int gz)
        {
            float cx = _halfX * 2f / (N - 1), cz = _halfZ * 2f / (N - 1);
            var local = new Vector3(_centre.x - _halfX + gx * cx, _topY, _centre.y - _halfZ + gz * cz);
            return transform.TransformPoint(local);
        }

        private void Place(int x, int z)
        {
            var next = (int[,])_cells.Clone();
            int me = _turn, them = 3 - _turn;
            next[x, z] = me;

            int took = 0;
            foreach (var nb in Neighbours(x, z))
                if (next[nb.x, nb.y] == them && !HasLiberty(next, nb.x, nb.y))
                    took += RemoveGroup(next, nb.x, nb.y);

            if (!HasLiberty(next, x, z)) { Say("스스로 숨을 막는 자리다 — 둘 수 없다."); return; }
            if (_before != null && Same(next, _before)) { Say("패다 — 바로 되따낼 수는 없다. 다른 곳을 한 번 두고 오시오."); return; }

            Push();
            _before = _cells;
            _cells = next;
            _captured[me] += took;
            _turn = them;
            Sync();
            Click();
            if (took > 0) Say((me == 1 ? "흑" : "백") + "이 " + took + "점을 따냈다.");
            else Refresh();
        }

        private void Pass()
        {
            Push();
            _before = _cells;
            _turn = 3 - _turn;
            Say((_turn == 1 ? "백" : "흑") + "이 한 수 쉬었다.");
        }

        private void Undo()
        {
            int n = _history.Count;
            if (n == 0) { Say("무를 수가 없다 — 빈 판이다."); return; }
            _cells = _history[n - 1];
            _turn = _turnHistory[n - 1];
            var cap = _capHistory[n - 1];
            _captured[1] = cap[1]; _captured[2] = cap[2];
            _history.RemoveAt(n - 1); _turnHistory.RemoveAt(n - 1); _capHistory.RemoveAt(n - 1);
            _before = _history.Count > 0 ? _history[_history.Count - 1] : null;
            Sync();
            Say("한 수 물렀다.");
        }

        private void Clear()
        {
            Push();
            _cells = new int[N, N];
            _before = null;
            _turn = 1;
            _captured[1] = _captured[2] = 0;
            Sync();
            Say("판을 비웠다. (오른쪽 단추로 되돌릴 수 있다)");
        }

        private void Push()
        {
            _history.Add((int[,])_cells.Clone());
            _turnHistory.Add(_turn);
            _capHistory.Add(new[] { 0, _captured[1], _captured[2] });
            if (_history.Count > 600) { _history.RemoveAt(0); _turnHistory.RemoveAt(0); _capHistory.RemoveAt(0); }
        }

        // ── 규칙 ─────────────────────────────────

        private static IEnumerable<Vector2Int> Neighbours(int x, int z)
        {
            if (x > 0) yield return new Vector2Int(x - 1, z);
            if (x < N - 1) yield return new Vector2Int(x + 1, z);
            if (z > 0) yield return new Vector2Int(x, z - 1);
            if (z < N - 1) yield return new Vector2Int(x, z + 1);
        }

        private static bool HasLiberty(int[,] b, int x, int z)
        {
            int colour = b[x, z];
            var seen = new bool[N, N];
            var stack = new Stack<Vector2Int>();
            stack.Push(new Vector2Int(x, z));
            seen[x, z] = true;
            while (stack.Count > 0)
            {
                var p = stack.Pop();
                foreach (var nb in Neighbours(p.x, p.y))
                {
                    int v = b[nb.x, nb.y];
                    if (v == 0) return true;
                    if (v == colour && !seen[nb.x, nb.y]) { seen[nb.x, nb.y] = true; stack.Push(nb); }
                }
            }
            return false;
        }

        private static int RemoveGroup(int[,] b, int x, int z)
        {
            int colour = b[x, z];
            if (colour == 0) return 0;
            int n = 0;
            var stack = new Stack<Vector2Int>();
            stack.Push(new Vector2Int(x, z));
            b[x, z] = 0;
            while (stack.Count > 0)
            {
                var p = stack.Pop();
                n++;
                foreach (var nb in Neighbours(p.x, p.y))
                    if (b[nb.x, nb.y] == colour) { b[nb.x, nb.y] = 0; stack.Push(nb); }
            }
            return n;
        }

        private static bool Same(int[,] a, int[,] b)
        {
            for (int x = 0; x < N; x++) for (int z = 0; z < N; z++) if (a[x, z] != b[x, z]) return false;
            return true;
        }

        // ── 그리기 ───────────────────────────────

        private void Sync()
        {
            for (int x = 0; x < N; x++)
                for (int z = 0; z < N; z++)
                {
                    int v = _cells[x, z];
                    var s = _stones[x, z];
                    if (v == 0) { if (s != null) s.SetActive(false); continue; }
                    if (s == null) { s = NewStone("돌_" + x + "_" + z, false); _stones[x, z] = s; }
                    s.GetComponent<Renderer>().sharedMaterial = v == 1 ? BlackMat : WhiteMat;
                    s.transform.position = GridToWorld(x, z) + transform.up * (_stoneHeight * 0.5f);
                    s.SetActive(true);
                }
        }

        private void Ghost(bool show, int x, int z)
        {
            if (!show) { if (_ghost != null) _ghost.SetActive(false); return; }
            if (_ghost == null) _ghost = NewStone("놓을자리", true);
            _ghost.GetComponent<Renderer>().sharedMaterial = _turn == 1 ? GhostBlack : GhostWhite;
            _ghost.transform.position = GridToWorld(x, z) + transform.up * (_stoneHeight * 0.5f);
            _ghost.SetActive(true);
        }

        private GameObject NewStone(string name, bool ghost)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(transform, true);
            go.transform.rotation = transform.rotation;
            // 판이 기울어 있어도 돌은 판 결을 따른다. 크기는 판의 배율을 되돌려 실제 치수로.
            Vector3 ls = transform.lossyScale;
            go.transform.localScale = new Vector3(_stoneDiameter / ls.x, _stoneHeight / ls.y, _stoneDiameter / ls.z);
            var r = go.GetComponent<Renderer>();
            r.shadowCastingMode = ghost ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            return go;
        }

        private Material BlackMat { get { return _blackMat != null ? _blackMat : (_blackMat = Mat(_black, 0.88f, false)); } }
        private Material WhiteMat { get { return _whiteMat != null ? _whiteMat : (_whiteMat = Mat(_white, 0.55f, false)); } }
        private Material GhostBlack { get { return _ghostBlack != null ? _ghostBlack : (_ghostBlack = Mat(_black, 0.3f, true)); } }
        private Material GhostWhite { get { return _ghostWhite != null ? _ghostWhite : (_ghostWhite = Mat(_white, 0.3f, true)); } }

        private static Material Mat(Color c, float smooth, bool ghost)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            var m = new Material(sh) { name = ghost ? "바둑돌_그림자" : "바둑돌" };
            if (ghost)
            {
                c.a = 0.45f;
                // URP Lit 을 투명으로
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            return m;
        }

        private void Click()
        {
            if (_click == null || _click.Length == 0) return;
            if (_audio == null)
            {
                _audio = gameObject.AddComponent<AudioSource>();
                _audio.playOnAwake = false;
                _audio.spatialBlend = 0.6f;
            }
            var clip = _click[_clickTurn++ % _click.Length];
            if (clip != null) _audio.PlayOneShot(clip, 0.8f);
        }

        // ── 판 둘레의 글 ──────────────────────────

        private void BuildHud()
        {
            if (_hud != null) return;
            _hud = new GameObject("바둑_글판", typeof(Canvas), typeof(CanvasScaler));
            var canvas = _hud.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 120;
            var scaler = _hud.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            var ink = new Color(0.96f, 0.93f, 0.85f, 1f);
            _status = UiText.New("차례", "", new Vector2(0f, 470f), new Vector2(1400f, 70f), _hud.transform, 38f, TextAnchor.MiddleCenter, ink);
            _help = UiText.New("안내",
                "왼쪽 딸깍  돌 놓기     오른쪽 딸깍  무르기     P  한 수 쉬기     R  판 비우기     Esc · Space  일어서기",
                new Vector2(0f, -490f), new Vector2(1800f, 50f), _hud.transform, 26f, TextAnchor.MiddleCenter, new Color(ink.r, ink.g, ink.b, 0.8f));
            _status.raycastTarget = false;
            _help.raycastTarget = false;
            _hud.SetActive(false);
        }

        private void Say(string line)
        {
            _note = line;
            _noteUntil = Time.unscaledTime + 2.6f;
            Refresh();
        }

        private void Refresh()
        {
            if (_status == null) return;
            string who = _turn == 1 ? "<b>흑</b> 차례" : "<b>백</b> 차례";
            string caps = "따낸 돌  흑 " + _captured[1] + " · 백 " + _captured[2];
            _status.text = string.IsNullOrEmpty(_note) ? who + "      " + caps : _note;
        }

        private void OnDisable()
        {
            if (_phase != 0) { Playing = false; Hands(true); _phase = 0; }
            if (_hud != null) _hud.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_hud != null) Destroy(_hud);
        }
    }
}
