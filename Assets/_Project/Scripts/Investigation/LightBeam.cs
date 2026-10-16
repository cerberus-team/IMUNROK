using System.Collections.Generic;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>볕 한 줄기를 거울로 꺾어 글자에 앉힌다</b> — 문서고의 놀이.
    ///
    /// <b>왜 이 놀이가 성립하나.</b> 세 가지가 맞물린다.
    ///   ① <b>문짝이 넷</b>이라 한 짝만 열 수 있다(<see cref="SunShaft"/>). 통째로 열리던
    ///      때에는 볕이 쏟아져 들어와 줄기랄 것이 없었다.
    ///   ② <b>둘 이상 열면 흩어진다.</b> 사방에서 드는 볕은 그늘을 지우고, 눌린 자국은
    ///      그늘로 읽는 것이다. 그러니 「많이 열수록 잘 보인다」가 아니라 그 반대다 —
    ///      이 규칙이 있어야 <b>어느 짝을 여느냐</b>가 고르는 일이 된다.
    ///   ③ <b>거울은 돌아간다</b>(<see cref="BronzeMirror"/>). 누를 때마다 한 뼘씩 돌고,
    ///      꺾인 줄기가 벽을 훑다가 어느 각에서 이 면에 앉는다.
    ///
    /// <b>줄기를 그린다.</b> 안 보이는 빛으로 맞히라 하면 그것은 수수께끼가 아니라
    /// 제비뽑기다. 문에서 거울로, 거울에서 떨어지는 데까지 선을 긋고 닿은 데에 불을
    /// 하나 놓는다 — 어디로 가는지 눈이 먼저 알아야 돌려 볼 마음이 난다.
    ///
    /// <b>해는 나란히 든다.</b> 볕의 방향은 여기 하나로 적는다. 문짝마다 따로 두면
    /// 해가 문마다 하나씩 있는 꼴이 된다.
    ///
    /// 붙이는 곳: 볕이 앉아야 할 면(궤 뚜껑·서안 상판). 콜라이더가 있어야 한다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class LightBeam : MonoBehaviour, IInspectable
    {
        public enum Source { 볕, 등불 }

        [Header("빛의 출처")]
        [Tooltip("<b>볕</b>: 문 한 짝으로 드는 햇볕(예전 방식). <b>등불</b>: 등경에 얹은 등불이 거울에 받혀 줄기가 된다 — " +
                 "문을 다 닫아야 하고, <b>어느 등경에 얹었나</b>와 거울의 각이 함께 답을 가른다(2026-09-14)")]
        [SerializeField] private Source _source = Source.볕;
        [Tooltip("등불 모드 — 이만큼(초) 줄기가 머물러야 드러난다. 스치자마자 떨어지면 무엇이 일어났는지 못 본다")]
        [SerializeField] private float _dwellSeconds = 0.8f;
        [Tooltip("등불 모드 — 등불을 얹었는데 문이 열려 있으면 이 말과 함께 문을 닫는다")]
        [TextArea(2, 3)] [SerializeField] private string _closeDoorsLine = "불빛이 새지 않게 문을 닫았다. 이제 거울로 불빛을 돌려 볼 수 있다.";
        [Tooltip("등불 모드 — 처음 등불을 얹었을 때 어디를 비출지 일러 주는 말. 이 면에 길표도 선다")]
        [TextArea(2, 3)] [SerializeField] private string _aimLine = "";
        [SerializeField] private string _aimMark = "먼지가 흐트러진 칸";
        [Tooltip("드러나는 순간 한 번 — 서가에 끼워 둔 문서를 떨어뜨리는 것 따위(DropFromShelf.Drop)")]
        [SerializeField] private UnityEngine.Events.UnityEvent _onFound;

        [Header("볕")]
        [Tooltip("볕이 <b>나아가는</b> 쪽(세계 기준). 문 밖에서 방 안으로 향해야 한다. " +
                 "y 를 조금만 내려 두면 낮게 스치는 사광이 된다")]
        [SerializeField] private Vector3 _sunward = new Vector3(0.30f, -0.18f, -1f);

        [Tooltip("한 줄기가 갈 수 있는 거리(m)")]
        [SerializeField] private float _reach = 24f;

        [Tooltip("거울에 몇 번까지 꺾이나")]
        [SerializeField] private int _bounces = 3;

        [Tooltip("이 켜만 맞는다. 볕이 통과해야 할 것(빛줄기 자신·안개)은 빼 둘 것")]
        [SerializeField] private LayerMask _mask = ~0;

        [Header("그리기")]
        [SerializeField] private float _width = 0.045f;
        [SerializeField] private Color _color = new Color(1f, 0.93f, 0.70f, 0.55f);

        [Tooltip("줄기가 떨어진 데를 밝히는 불의 세기. 0 이면 불 없이 선만 긋는다")]
        [SerializeField] private float _spotIntensity = 2.6f;

        [Tooltip("그 불이 닿는 거리(m)")]
        [SerializeField] private float _spotRange = 2.5f;

        [Header("드러나는 것")]
        [Tooltip("맞으면 켜진다. 비워 두면 글만 바뀐다")]
        [SerializeField] private GameObject _reveal;

        [Tooltip("한 번 뜨면 그대로 둔다 — 알아본 것을 도로 못 알아볼 수는 없다")]
        [SerializeField] private bool _keepOnceSeen = true;

        [Header("글")]
        [SerializeField] private string _title = "";
        [TextArea(2, 4)] [SerializeField] private string _bodyDark = "어둡다. 문을 한 짝 열면 볕이 들겠다.";
        [TextArea(2, 4)] [SerializeField] private string _bodyFlood = "문을 여럿 열어 놓아 볕이 사방에서 든다 — 그늘이 지지 않아 아무 무늬도 안 뜬다.";
        [TextArea(2, 4)] [SerializeField] private string _bodyMiss = "볕 한 줄기가 방을 가로지른다. 다만 이 면에는 닿지 않는다.";
        [TextArea(2, 4)] [SerializeField] private string _bodyHit = "꺾여 든 볕이 면을 낮게 스치자, 눌린 자국이 떠오른다.";

        [Header("수첩")]
        [SerializeField] private string _clueKey = "";
        [TextArea(2, 4)] [SerializeField] private string _clueText = "";
        [SerializeField] private CaseId _case = CaseId.Case1_Onggojip;

        [Header("맞혔을 때 — 무엇이 달라졌는지 보이게")]
        [Tooltip("볕이 앉는 순간 한 번 뜨는 한 줄(자막). 비우면 안 뜬다. " +
                 "여태 맞혀도 줄기만 그대로 서 있고 아무 말이 없어, 푼 것과 안 푼 것이 구별되지 않았다")]
        [TextArea(2, 3)] [SerializeField] private string _foundLine = "";
        [Tooltip("이 볕이 읽어 낸 것이 <b>어느 문서의 이야기</b>인지 — 그 단서 줄 뒤에도 한 줄을 덧붙인다(비우면 안 붙임)")]
        [SerializeField] private string _alsoAppendKey = "";
        [TextArea(2, 4)] [SerializeField] private string _alsoAppendText = "";

        /// <summary>0 어둠 · 1 흩어짐 · 2 줄기는 있으나 빗나감 · 3 맞음</summary>
        private int _state;
        private bool _seen;

        private SunShaft[] _shafts;
        private Collider _me;
        private LineRenderer _line;
        private Light _spot;
        private readonly List<Vector3> _pts = new List<Vector3>();

        private void Awake()
        {
            _me = GetComponent<Collider>();
            _shafts = Object.FindObjectsByType<SunShaft>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Build();
            if (_reveal != null) _reveal.SetActive(false);
        }

        /// <summary>줄기를 그릴 선과 떨어진 데를 밝힐 불을 하나씩 짓는다.</summary>
        private void Build()
        {
            var go = new GameObject("빛줄기");
            go.transform.SetParent(transform, false);
            go.layer = gameObject.layer;

            _line = go.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.widthMultiplier = _width;
            _line.numCapVertices = 2;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.material = BeamMaterial(_color);
            _line.startColor = _line.endColor = _color;
            _line.enabled = false;

            if (_spotIntensity > 0f)
            {
                var lg = new GameObject("떨어진자리_불");
                lg.transform.SetParent(transform, false);
                _spot = lg.AddComponent<Light>();
                _spot.type = LightType.Spot;
                _spot.spotAngle = 34f;
                _spot.range = _spotRange;
                _spot.intensity = _spotIntensity;
                _spot.color = new Color(1f, 0.95f, 0.82f);
                _spot.shadows = LightShadows.None;
                _spot.enabled = false;
            }
        }

        /// <summary>
        /// 줄기를 칠할 것. 꾸러미마다 있는 셰이더가 달라, <b>있는 것을 차례로</b> 찾는다 —
        /// 없는 이름을 하나만 적어 두면 그 프로젝트에서는 분홍 줄이 된다.
        /// </summary>
        private static Material BeamMaterial(Color tint)
        {
            // <c>Sprites/Default</c> 를 먼저 찾는다 — 불빛 없이 그려지고, <b>알파와 꼭짓점
            // 색을 그대로 받는</b> 몇 안 되는 셰이더다. URP 의 Particles/Unlit 은 갓 만들면
            // 불투명이라, 묽게 칠한 줄기가 <b>흰 널빤지</b>로 나온다.
            string[] names = { "Sprites/Default", "Universal Render Pipeline/Particles/Unlit", "Unlit/Color" };
            foreach (var n in names)
            {
                var sh = Shader.Find(n);
                if (sh == null) continue;
                var m = new Material(sh);
                m.name = "빛줄기";
                if (m.HasProperty("_Color")) m.SetColor("_Color", tint);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
                return m;
            }
            return null;
        }

        private LanternStand[] _stands;
        private BronzeMirror[] _mirrors;
        private SwingDoor[] _doorsAll;
        private bool _aimed;
        private float _dwell;
        private bool _hadLamp;

        private void Update()
        {
            if (_source == Source.등불) { UpdateLantern(); return; }
            int open = 0;
            SunShaft only = null;
            for (int i = 0; i < _shafts.Length; i++)
            {
                if (_shafts[i] == null || !_shafts[i].Open) continue;
                open++;
                only = _shafts[i];
            }

            if (open == 0) { _state = 0; Draw(false); return; }
            if (open > 1) { _state = 1; Draw(false); return; }

            bool hitMe = Trace(only.Origin, _sunward.normalized);
            _state = hitMe ? 3 : 2;
            Draw(true);

            // <b>한 번 본 뒤에도 빛은 그대로 든다.</b> 여태 여기서 아예 물러나 줄기를
            // 지웠다 — 답을 맞히는 순간 방을 가로지르던 빛이 사라져, 맞혔다는 표시가
            // 아니라 <b>고장</b>으로 보였다. 다시 안 적을 뿐이지 볕이 걷히는 것은 아니다.
            if (_seen && _keepOnceSeen) return;
            if (!hitMe) return;
            Found();
        }

        /// <summary>
        /// <b>등경의 등불을 거울로 받아 한 곳에 앉힌다.</b>
        ///
        /// 볕으로 짠 판은 「문을 어느 짝 여나」가 손잡이였는데, 그러면 등불을 들고 다니는 이 게임의
        /// 손과 따로 놀았다. 이제 등불을 <b>어느 등경에 얹나</b>가 첫 손잡이이고, 거울의 각이 둘째다.
        /// 등불은 사방을 비추지만 줄기로 서는 것은 <b>거울에 받힌 빛</b>뿐이다 — 그래서 얹은 자리에서
        /// 각 거울로 곧게 선을 긋고, 갈아 놓은 면에 들면 꺾어 따라간다.
        /// 문이 하나라도 열려 있으면 햇빛이 들어 불빛 줄기가 안 선다 — 얹는 순간 문을 닫는다.
        /// </summary>
        private void UpdateLantern()
        {
            if (_stands == null) _stands = Object.FindObjectsByType<LanternStand>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (_mirrors == null) _mirrors = Object.FindObjectsByType<BronzeMirror>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (_doorsAll == null)
            {
                // <b>이 방의 문만</b> 본다(2026-09-15) — 씬 전체의 문 열다섯을 보아, 등불을 얹으면 동헌 장지문까지
                // 닫히고 동헌 문 하나만 열려 있어도 줄기가 안 섰다. 볕이 드는 짝(SunShaft)이 곧 이 방의 문이다.
                var list = new List<SwingDoor>();
                foreach (var sh in Object.FindObjectsByType<SunShaft>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    var d = sh != null ? sh.GetComponent<SwingDoor>() : null;
                    if (d != null && !list.Contains(d)) list.Add(d);
                }
                _doorsAll = list.ToArray();
            }

            LanternStand lit = null;
            foreach (var st in _stands) if (st != null && st.Loaded) { lit = st; break; }
            if (lit == null) { _hadLamp = false; _state = 0; _dwell = 0f; Draw(false); return; }

            bool doorOpen = false;
            foreach (var d in _doorsAll) if (d != null && d.IsOpen) { doorOpen = true; break; }

            // 막 얹었다 — 열린 문을 닫고, 어디를 비출지 한 번 일러 준다.
            if (!_hadLamp)
            {
                _hadLamp = true;
                if (doorOpen)
                {
                    foreach (var d in _doorsAll) if (d != null && d.IsOpen) d.SetOpen(false, false);
                    doorOpen = false;
                    if (!string.IsNullOrEmpty(_closeDoorsLine) && (_seen || _aimed)) SubtitleView.Show("", _closeDoorsLine, "", false);
                }
                if (!_aimed && !_seen)
                {
                    _aimed = true;
                    string line = string.IsNullOrEmpty(_aimLine) ? _closeDoorsLine : _aimLine;
                    if (!string.IsNullOrEmpty(line)) SubtitleView.Show("", line, "", true);
                    if (!string.IsNullOrEmpty(_aimMark)) WayMark.Show(transform, _aimMark);
                }
            }
            // 얹어 둔 채 문을 열었다 — 햇빛에 흩어져 줄기가 안 선다.
            if (doorOpen) { _state = 1; _dwell = 0f; Draw(false); return; }

            Vector3 lamp = lit.LampPosition;
            bool landed = false;
            List<Vector3> best = null;
            foreach (var m in _mirrors)
            {
                if (m == null) continue;
                Vector3 dir = (m.Here - lamp).normalized;
                // 등불에서 거울까지 곧게 닿고, 갈아 놓은 면에 들어야 줄기가 선다
                RaycastHit first;
                if (!Physics.Raycast(lamp + dir * 0.15f, dir, out first, _reach, _mask, QueryTriggerInteraction.Collide)) continue;
                if (first.collider.GetComponentInParent<BronzeMirror>() != m || !m.Catches(dir)) continue;
                bool hit = Trace(lamp + dir * 0.13f, dir);
                if (hit || best == null) best = new List<Vector3>(_pts);
                if (hit) { landed = true; break; }
            }
            if (best == null) { _state = 2; _dwell = 0f; Draw(false); return; }
            _pts.Clear(); _pts.AddRange(best);
            _state = landed ? 3 : 2;
            Draw(true);

            if (_seen && _keepOnceSeen) return;
            if (!landed) { _dwell = 0f; return; }
            _dwell += Time.deltaTime;
            if (_dwell < _dwellSeconds) return;
            WayMark.Hide();
            Found();
        }

        private void Found()
        {
            _seen = true;
            _onFound?.Invoke();
            if (_reveal != null) _reveal.SetActive(true);
            var j = Journal.Instance;
            if (!string.IsNullOrEmpty(_clueKey) && j != null)
                j.AddClue(_case, _clueKey, string.IsNullOrEmpty(_clueText) ? _bodyHit : _clueText,
                          null, ClueKind.물증, true, string.IsNullOrEmpty(_title) ? null : _title);
            if (!string.IsNullOrEmpty(_alsoAppendKey) && !string.IsNullOrWhiteSpace(_alsoAppendText) && j != null)
            {
                foreach (var c in j.GetClues(_case))
                {
                    if (c == null || c.key != _alsoAppendKey || string.IsNullOrEmpty(c.text)) continue;
                    if (!c.text.Contains(_alsoAppendText.Trim()))
                        j.UpgradeClue(_case, _alsoAppendKey, c.text + System.Environment.NewLine + System.Environment.NewLine + _alsoAppendText.Trim());
                    break;
                }
            }
            if (!string.IsNullOrEmpty(_foundLine)) SubtitleView.Show("", _foundLine, "", false);
        }

        /// <summary>줄기를 따라가며 꺾는다. 끝이 이 면에 닿았으면 참.</summary>
        private bool Trace(Vector3 p, Vector3 d)
        {
            _pts.Clear();
            _pts.Add(p);

            bool landed = false;
            for (int b = 0; b <= _bounces; b++)
            {
                RaycastHit hit;
                if (!Physics.Raycast(p + d * 0.02f, d, out hit, _reach, _mask, QueryTriggerInteraction.Collide))
                {
                    _pts.Add(p + d * _reach);
                    break;
                }

                _pts.Add(hit.point);

                var mirror = hit.collider.GetComponentInParent<BronzeMirror>();
                // 맞았다는 것부터 알려 준다 — 꺾이든 멎든. 거울이 제 살핌글에 적는다.
                if (mirror != null) mirror.NoteHit(d);
                // <b>갈아 놓은 면에 들었을 때만 꺾인다</b>(<see cref="BronzeMirror.Catches"/>).
                // 여태 앞뒤를 안 가려, 볕이 <b>나무 등판에 맞고도</b> 튕겨 나갔다 —
                // 화면으로 보면 나무가 거울 노릇을 하는 꼴이고, 맞는 각도 한 바퀴에
                // 둘이 되어 「돌려서 맞춘다」가 헐거워졌다. 등판에 들면 거기서 멎는다.
                if (mirror != null && b < _bounces && mirror.Catches(d))
                {
                    Vector3 n = mirror.Normal;
                    d = Vector3.Reflect(d, n);
                    p = hit.point;
                    continue;
                }

                landed = hit.collider == _me;
                break;
            }

            if (_spot != null && _pts.Count >= 2)
            {
                Vector3 end = _pts[_pts.Count - 1];
                Vector3 from = _pts[_pts.Count - 2];
                Vector3 dir = (end - from).normalized;
                _spot.transform.position = end - dir * Mathf.Min(1.2f, _spotRange * 0.5f);
                _spot.transform.rotation = Quaternion.LookRotation(dir);
            }
            return landed;
        }

        private void Draw(bool on)
        {
            if (_line != null)
            {
                _line.enabled = on;
                if (on)
                {
                    _line.positionCount = _pts.Count;
                    for (int i = 0; i < _pts.Count; i++) _line.SetPosition(i, _pts[i]);
                }
            }
            if (_spot != null) _spot.enabled = on;
        }

        // ── 살펴보기 ──
        public string GetInspectTitle() { return _title; }

        public string GetInspectBody()
        {
            if (_seen && _keepOnceSeen) return _bodyHit;
            switch (_state)
            {
                case 3:  return _bodyHit;
                case 2:  return _bodyMiss;
                case 1:  return _bodyFlood;
                default: return _bodyDark;
            }
        }

        public void OnInspected() { }
    }
}
