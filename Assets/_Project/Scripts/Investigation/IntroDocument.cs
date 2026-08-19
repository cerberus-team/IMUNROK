using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common
{
    /// <summary>
    /// 어전에서 왕이 내리는 봉서 하나.
    ///
    /// 두 마디로 다룬다:
    ///   ① <b>굴러온다</b> — 왕 쪽에서 떼구르르 굴러와 발치에 멎는다.
    ///   ② <b>집어 본다</b> — 누르면 눈앞으로 떠올라 펼쳐지고, 안에 적힌 것이 보인다.
    ///      거기서 한 번 더 누르면 그 사건을 맡는다. 다른 것을 누르면 이건 도로 내려간다.
    ///
    /// 왜 한 번에 안 고르게 하는가: 봉투만 보고 고르는 것은 고르는 것이 아니라 찍는 것이다.
    /// 무엇이 적혔는지 읽고 나서 정해야 고른 것이 된다. 그래서 첫 누름은 '읽기',
    /// 두 번째 누름이 '맡기'다.
    ///
    /// 붙이는 법: 빈 껍데기에 콜라이더와 이 부품을 두고, 두루마리 프리팹을 자식으로 넣는다.
    /// 두루마리 쪽 콜라이더는 없앤다 — 한 오브젝트에 ISelectable 이 둘이면
    /// 레이가 엉뚱한 쪽을 부른다.
    /// </summary>
    public class IntroDocument : MonoBehaviour, ISelectable
    {
        private enum Phase { 숨음, 굴러오는중, 놓임, 떠오르는중, 읽는중, 내려가는중 }

        [SerializeField] private CaseId _caseId = CaseId.Case1_Onggojip;
        [Tooltip("문서를 집었을 때 진입할 조사청 씬 이름")]
        [SerializeField] private string _hubSceneName = "HubScene";

        [Tooltip("가리켰을 때 뜰 이름. 어느 사건인지 알고 고를 수 있어야 한다")]
        [SerializeField] private string _label = "";

        [Header("펼쳤을 때 보일 글")]
        [Tooltip("이 사건의 문서 그림. [이문록 ▸ 사건 문서 굽기] 로 만든 것")]
        [SerializeField] private Texture2D _document;

        [Header("집어 보기")]
        [Tooltip("눈에서 이만큼 앞에 들어 올린다(m)")]
        [SerializeField] private float _readDistance = 0.62f;
        [Tooltip("눈높이에서 이만큼 내려 잡는다(m)")]
        [SerializeField] private float _readDrop = 0.06f;
        [Tooltip("떠오르는 데 걸리는 시간(초)")]
        [SerializeField] private float _liftSeconds = 0.7f;
        [Tooltip("읽는 동안 시선을 따라오는 빠르기. 낮으면 천천히 따라와 손에 든 느낌이 난다")]
        [SerializeField] private float _readFollow = 8f;
        [Tooltip("읽는 중임을 알리는 말")]
        [SerializeField] private string _readPrompt = "맡으려면 글을 · 물리려면 옆을 누르시오";

        [Header("이름표")]
        [Tooltip("물건에서 이만큼 위에 뜬다(m). 자막판처럼 화면을 가리지 않게 물건 곁에 붙인다")]
        [SerializeField] private float _labelHeight = 0.20f;
        [Tooltip("펼쳐 든 동안 종이 아래에 붙는 틈(m). 크게 잡으면 화면 밖으로 밀려난다")]
        [SerializeField] private float _readLabelGap = 0.045f;
        [Tooltip("펼쳐 든 동안 이름표를 눈에서 이만큼 떨어진 곳에 못 박는다(m). " +
                 "두루마리는 아랫축이 앞으로 튀어나와 있어, 조금 빼는 정도로는 " +
                 "글자가 그 축에 걸쳐 파묻힌다. 아예 그보다 앞에 세운다")]
        [SerializeField] private float _readLabelDistance = 0.34f;
        [SerializeField] private int _labelFontSize = 40;
        [SerializeField] private Color _labelColor = new Color(1f, 0.92f, 0.72f);

        [SerializeField] private Color _paperColor = new Color(0.85f, 0.80f, 0.68f); // 종이/한지 색
        [Range(0f, 1f)]
        [SerializeField] private float _hoverBrighten = 0.30f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        // 모델이 자식으로 들어온다(두루마리 프리팹). 자기 자신에게만 렌더러를 찾으면
        // 아무것도 안 보이고 색도 안 바뀐다.
        private Renderer[] _renderers;
        private MaterialPropertyBlock _mpb;
        private Collider _collider;
        private ScrollUnroll _scroll;
        private Transform _roll;          // 두루마리(자식) — 굴릴 때 이것을 돌린다
        private Renderer _sealBand;       // 봉인 띠 — 뜯으면 없어진다

        private Vector3 _homeLocal;       // 굴러와 멎는 자리
        private Quaternion _homeRot;
        private Phase _phase = Phase.숨음;
        private bool _hovered;
        private Coroutine _moving;
        private GameObject _putBack;      // 종이 바깥을 누르면 물리는 판
        private Vector3 _restCenter, _restSize;
        private bool _restSaved;

        public CaseId CaseId => _caseId;
        /// <summary>지금 눈앞에 펼쳐 읽는 중인가.</summary>
        public bool IsReading => _phase == Phase.읽는중 || _phase == Phase.떠오르는중;

        /// <summary>에디터 생성기에서 세팅.</summary>
        public void Initialize(CaseId id, string hubSceneName)
        {
            _caseId = id;
            _hubSceneName = hubSceneName;
        }

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _collider = GetComponent<Collider>();
            _mpb = new MaterialPropertyBlock();
            _scroll = GetComponentInChildren<ScrollUnroll>(true);
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == "봉인") _sealBand = t.GetComponent<Renderer>();
            if (_scroll != null)
            {
                _roll = _scroll.transform;
                _scroll.SetDocument(_document);
                _scroll.SetInstant(0f);
            }
            _homeLocal = transform.localPosition;
            _homeRot = transform.localRotation;
            SetColor(_paperColor);
            Hide();
        }

        private void Hide()
        {
            _phase = Phase.숨음;
            SetRenderers(false);
            if (_collider != null) _collider.enabled = false;
        }

        // ── ① 굴러온다 ────────────────────────────────

        /// <summary>왕 쪽에서 떼구르르 굴러와 제자리에 멎는다.</summary>
        public void PlaySlideIn(float delay, float duration, float fromDistance)
        {
            StartCoroutine(RollIn(delay, duration, fromDistance));
        }

        private IEnumerator RollIn(float delay, float duration, float fromDistance)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);

            SetRenderers(true);
            _phase = Phase.굴러오는중;

            Vector3 from = _homeLocal + new Vector3(0f, 0f, fromDistance);   // 왕 쪽에서
            transform.localPosition = from;

            // 구르는 각은 굴러간 거리를 둘레로 나눈 값이다. 그래야 미끄러지지 않고 굴러 보인다.
            float radius = Mathf.Max(0.01f, RollRadius());
            float spin = fromDistance / (2f * Mathf.PI * radius) * 360f;
            Quaternion rollHome = _roll != null ? _roll.localRotation : Quaternion.identity;

            float t = 0f;
            float dur = Mathf.Max(0.01f, duration);
            while (t < 1f)
            {
                t += Time.deltaTime / dur;
                float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 2.2f);   // 굴러와 스르르 멎는다
                transform.localPosition = Vector3.Lerp(from, _homeLocal, e);
                if (_roll != null)
                    _roll.localRotation = rollHome * Quaternion.AngleAxis(spin * e, Vector3.right);
                yield return null;
            }
            transform.localPosition = _homeLocal;
            if (_roll != null) _roll.localRotation = rollHome;

            if (_collider != null) _collider.enabled = true;
            _phase = Phase.놓임;
        }

        /// <summary>구르는 반지름 — 두루마리 축의 굵기 절반.</summary>
        private float RollRadius()
        {
            var b = new Bounds();
            bool f = true;
            foreach (var r in _renderers)
            {
                if (r == null) continue;
                if (f) { b = r.bounds; f = false; } else b.Encapsulate(r.bounds);
            }
            return f ? 0.07f : b.size.y * 0.5f;
        }

        // ── ② 집어 본다 ───────────────────────────────

        /// <summary>눈앞으로 떠올라 펼쳐진다.</summary>
        public void Lift()
        {
            if (_phase != Phase.놓임) return;
            if (_moving != null) StopCoroutine(_moving);
            _moving = StartCoroutine(LiftRoutine());
        }

        private IEnumerator LiftRoutine()
        {
            _phase = Phase.떠오르는중;
            var cam = Camera.main;
            if (cam == null) { _phase = Phase.놓임; yield break; }

            Vector3 fromPos = transform.position;
            Quaternion fromRot = transform.rotation;

            // 종이는 축에 매달려 아래로 자란다. 그대로 눈높이에 두면 글이 죄 아래에 걸리므로
            // 다 폈을 때의 절반만큼 올려 달아 한가운데가 눈에 오게 한다.
            float half = _scroll != null ? _scroll.FullHeight * 0.5f : 0.2f;
            Vector3 toPos = ReadPosition(cam, half);
            Quaternion toRot = ReadRotation(cam);

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, _liftSeconds);
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                transform.position = Vector3.Lerp(fromPos, toPos, e);
                transform.rotation = Quaternion.Slerp(fromRot, toRot, e);
                yield return null;
            }

            // 봉인은 뜯은 셈이다. 남겨 두면 펼친 글 위를 붉은 띠가 가로지른다.
            if (_sealBand != null) _sealBand.enabled = false;

            if (_scroll != null) _scroll.Unroll();
            _phase = Phase.읽는중;
            SubtitleView.Hide();          // 펼친 글을 자막판이 덮으면 읽을 수가 없다
            ShowLabel(_readPrompt);
            FitColliderToPaper();
            MakePutBackTarget();
            _moving = null;
        }

        /// <summary>
        /// 읽는 동안 집는 자리를 펼친 종이에 맞춘다. 말렸을 때의 넓적한 자리를 그대로
        /// 두면 글 밖 허공을 눌러도 '맡기'가 된다.
        /// </summary>
        private void FitColliderToPaper()
        {
            if (_collider == null || _scroll == null) return;
            var box = _collider as BoxCollider;
            if (box == null) return;

            Transform paper = null;
            foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == "종이") paper = t;
            var r = paper == null ? null : paper.GetComponent<Renderer>();
            if (r == null) return;

            if (!_restSaved) { _restCenter = box.center; _restSize = box.size; _restSaved = true; }
            Vector3 ls = transform.lossyScale;
            box.center = transform.InverseTransformPoint(r.bounds.center);
            box.size = new Vector3(r.bounds.size.x / Mathf.Max(0.001f, ls.x),
                                   r.bounds.size.y / Mathf.Max(0.001f, ls.y),
                                   0.06f / Mathf.Max(0.001f, ls.z));
        }

        private void RestoreCollider()
        {
            var box = _collider as BoxCollider;
            if (box == null || !_restSaved) return;
            box.center = _restCenter;
            box.size = _restSize;
        }

        /// <summary>
        /// 종이 바깥을 누르면 도로 내려놓는다.
        ///
        /// 얼굴 앞에 펼친 종이는 그 뒤를 가린다 — 옆의 봉서를 곧장 누를 수가 없다.
        /// 기하로 풀 일이 아니라 조작으로 풀 일이다. 실제로도, VR 에서도, 손에 든 것을
        /// 먼저 내려놓고 다른 것을 집는다. 종이 밖 아무 데나 누르면 물러진다.
        /// </summary>
        private void MakePutBackTarget()
        {
            if (_putBack != null) return;
            var cam = Camera.main;
            if (cam == null) return;

            _putBack = new GameObject("물리기판");
            _putBack.transform.SetParent(cam.transform, false);
            _putBack.transform.localPosition = new Vector3(0f, 0f, _readDistance + 0.35f);
            var col = _putBack.AddComponent<BoxCollider>();
            col.size = new Vector3(6f, 6f, 0.02f);
            _putBack.AddComponent<DocumentPutBack>().Bind(this);
        }

        private void KillPutBackTarget()
        {
            if (_putBack != null) { Destroy(_putBack); _putBack = null; }
        }

        /// <summary>
        /// 읽을 때 종이가 놓일 자리. 종이는 축에 매달려 아래로 자라므로, 축을 종이
        /// 절반만큼 위에 달아야 <b>종이 한가운데가</b> 시선 위에 온다.
        /// </summary>
        private Vector3 ReadPosition(Camera cam, float half)
        {
            Vector3 center = cam.transform.position
                             + cam.transform.forward * _readDistance
                             - cam.transform.up * _readDrop;
            return center + cam.transform.up * half;
        }

        /// <summary>
        /// 읽을 때 종이가 향할 쪽. 시선의 정반대를 보게 하면 종이 면과 화면이
        /// 정확히 나란해진다 — 한 치도 기울지 않는다.
        ///
        /// 앞서는 '카메라 자리를 바라보게' 했는데, 종이가 축보다 아래에 매달려 있어
        /// 그 방향과 시선이 열세 도 어긋났다. 바라볼 곳은 카메라의 <b>자리</b>가 아니라
        /// 카메라가 보는 <b>방향</b>이다.
        /// </summary>
        private Quaternion ReadRotation(Camera cam)
        {
            return Quaternion.LookRotation(-cam.transform.forward, cam.transform.up);
        }

        /// <summary>도로 발치에 내려놓는다(다른 봉서를 집었을 때).</summary>
        public void Lower()
        {
            if (_phase != Phase.읽는중 && _phase != Phase.떠오르는중) return;
            if (_moving != null) StopCoroutine(_moving);
            _moving = StartCoroutine(LowerRoutine());
        }

        private IEnumerator LowerRoutine()
        {
            _phase = Phase.내려가는중;
            HideLabel();
            KillPutBackTarget();
            RestoreCollider();
            if (_scroll != null) _scroll.Roll();
            if (_sealBand != null) _sealBand.enabled = true;   // 도로 말았으니 띠도 돌아온다

            Vector3 fromPos = transform.position;
            Quaternion fromRot = transform.rotation;
            Vector3 toPos = transform.parent != null ? transform.parent.TransformPoint(_homeLocal) : _homeLocal;
            Quaternion toRot = transform.parent != null ? transform.parent.rotation * _homeRot : _homeRot;

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, _liftSeconds);
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                transform.position = Vector3.Lerp(fromPos, toPos, e);
                transform.rotation = Quaternion.Slerp(fromRot, toRot, e);
                yield return null;
            }
            transform.localPosition = _homeLocal;
            transform.localRotation = _homeRot;
            _phase = Phase.놓임;
            _moving = null;
        }

        /// <summary>
        /// 고르지 않은 봉서를 굳힌다. 하나를 맡는 순간 나머지도 눌러지면
        /// 사건 둘이 한꺼번에 시작돼 버린다.
        /// </summary>
        /// <summary>
        /// 지금 당장 안 보이게 한다. 고른 뒤 화면을 검게 덮을 때 쓴다 —
        /// 눈앞에 들어 올린 두루마리는 그 검은 막보다 앞에 있어서, 그냥 두면
        /// 캄캄한 화면 위에 두루마리만 덩그러니 남는다.
        /// </summary>
        public void HideNow()
        {
            HideLabel();
            KillPutBackTarget();
            SetRenderers(false);
            if (_collider != null) _collider.enabled = false;
        }

        public void Freeze()
        {
            _hovered = false;
            if (_collider != null) _collider.enabled = false;
            if (_phase == Phase.읽는중 || _phase == Phase.떠오르는중) Lower();
            _phase = Phase.숨음;
            RefreshColor();
        }

        // ── ISelectable ───────────────────────────────

        public void OnHoverEnter()
        {
            if (_phase != Phase.놓임) return;
            _hovered = true;
            RefreshColor();
            ShowLabel(_label);

            // 자막판은 폭이 화면만 해서 옆의 봉서를 덮는다. 가리키는 동안에는
            // 곁에 붙는 작은 이름표가 그 일을 대신하므로 자막은 물러난다.
            SubtitleView.Hide();
        }

        public void OnHoverExit()
        {
            if (!_hovered) return;
            _hovered = false;
            RefreshColor();
            if (_phase == Phase.읽는중) return;

            HideLabel();
            // 아무것도 안 가리키게 되면 원래 안내로 돌아간다.
            var intro = FindFirstObjectByType<IntroController>();
            if (intro != null) intro.RestorePickPrompt();
        }

        // ── 이름표 ────────────────────────────────────
        //
        // 자막판(SubtitleView)은 폭이 화면만 해서, 물건 이름을 그것으로 띄우면
        // 옆에 놓인 다른 봉서를 통째로 덮어 고를 수가 없다. 이름표는 그 물건 곁에
        // 작게 붙어야 한다 — VR 에서 물건을 가리켰을 때의 보통 방식이기도 하다.

        private GameObject _labelGo;
        private UnityEngine.UI.Text _labelText;

        private void ShowLabel(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            if (_labelGo == null) BuildLabel();
            _labelText.text = s;
            _labelGo.SetActive(true);
        }

        private void HideLabel()
        {
            if (_labelGo != null) _labelGo.SetActive(false);
        }

        private void BuildLabel()
        {
            _labelGo = new GameObject("이름표", typeof(Canvas));
            var canvas = _labelGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = canvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(900f, 90f);
            rt.localScale = Vector3.one * 0.0006f;

            var t = new GameObject("글", typeof(UnityEngine.UI.Text));
            _labelText = t.GetComponent<UnityEngine.UI.Text>();
            _labelText.font = UiFont.Resolve(null);
            _labelText.fontSize = _labelFontSize;
            _labelText.color = _labelColor;
            _labelText.alignment = TextAnchor.MiddleCenter;
            _labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _labelText.verticalOverflow = VerticalWrapMode.Overflow;
            var sh = t.AddComponent<UnityEngine.UI.Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.85f);
            sh.effectDistance = new Vector2(2.5f, -2.5f);
            var trt = t.GetComponent<RectTransform>();
            trt.SetParent(rt, false);
            trt.sizeDelta = new Vector2(880f, 80f);
        }

        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;

            // 읽는 동안에는 고개를 돌려도 종이가 늘 정면을 보게 따라온다.
            // 헤드셋에서는 머리가 가만히 있지 않으므로, 한 번 맞춰 놓는 것만으로는
            // 곧 비스듬해진다. 손에 든 것을 눈앞에 고쳐 드는 것과 같다.
            if (_phase == Phase.읽는중 && _scroll != null)
            {
                float half = _scroll.FullHeight * 0.5f;
                float t = 1f - Mathf.Exp(-_readFollow * Time.deltaTime);   // 프레임률에 안 흔들리는 감쇠
                transform.position = Vector3.Lerp(transform.position, ReadPosition(cam, half), t);
                transform.rotation = Quaternion.Slerp(transform.rotation, ReadRotation(cam), t);
            }

            if (_labelGo == null || !_labelGo.activeSelf) return;

            var b = new Bounds(transform.position, Vector3.zero);
            bool f = true;
            foreach (var r in _renderers)
            {
                if (r == null || !r.enabled) continue;
                if (f) { b = r.bounds; f = false; } else b.Encapsulate(r.bounds);
            }
            // 발치에 놓인 동안엔 물건 위에, 얼굴 앞에 펼친 동안엔 종이 아래에 붙인다.
            // 펼친 종이는 화면을 거의 채우므로 그 위에 두면 이름표가 화면 밖으로 밀려난다.
            const float LabelScale = 0.0006f;

            if (_phase == Phase.읽는중)
            {
                // 종이 아래에 두되, 눈에서 정해진 거리에 못 박는다. 두루마리보다 앞이라야
                // 아랫축에 걸려 글자가 파묻히지 않는다. 가까워진 만큼 작게 그려
                // 보기에는 늘 같은 크기가 되게 한다.
                Vector3 want = b.center - cam.transform.up * (b.size.y * 0.5f + _readLabelGap);
                Vector3 dir = (want - cam.transform.position).normalized;
                Vector3 at = cam.transform.position + dir * _readLabelDistance;

                _labelGo.transform.position = at;
                _labelGo.transform.rotation = Quaternion.LookRotation(-cam.transform.forward, cam.transform.up);
                // 눈앞으로 당겨 세운 만큼 작게 그린다. 0.62 는 예전에 두던 거리이고,
                // 거기에 한 번 더 줄여 화면 폭의 절반쯤에 들어오게 한다 —
                // 이름표가 종이만큼 커지면 읽을 것이 둘이 된다.
                _labelGo.transform.localScale = Vector3.one * (LabelScale * (_readLabelDistance / 0.62f) * 0.62f);
                return;
            }

            Vector3 pos = new Vector3(b.center.x, b.max.y + _labelHeight, b.center.z);
            _labelGo.transform.position = pos;
            _labelGo.transform.rotation = Quaternion.LookRotation(pos - cam.transform.position, Vector3.up);
            _labelGo.transform.localScale = Vector3.one * LabelScale;
        }

        private void OnDestroy()
        {
            if (_labelGo != null) Destroy(_labelGo);
            KillPutBackTarget();
        }

        public void OnSelect()
        {
            // 첫 누름 = 집어서 읽기. 두 번째 누름 = 이 사건을 맡기.
            if (_phase == Phase.놓임)
            {
                var intro = FindFirstObjectByType<IntroController>();
                if (intro != null) intro.NowReading(this);   // 다른 봉서는 내려간다
                Lift();
                return;
            }
            if (_phase != Phase.읽는중) return;

            GameState.Instance.StartCase(_caseId);
            Debug.Log($"[IntroDocument] {_caseId} 를 맡았습니다 — 조사청으로 갑니다.");

            var ic = FindFirstObjectByType<IntroController>();
            if (ic != null) { ic.TakeChosen(_caseId); return; }

            if (!string.IsNullOrEmpty(_hubSceneName) && Application.CanStreamedLevelBeLoaded(_hubSceneName))
                SceneManager.LoadScene(_hubSceneName);
            else
                Debug.LogWarning($"[IntroDocument] 조사청 씬('{_hubSceneName}')을 찾을 수 없습니다.", this);
        }

        private void RefreshColor()
        {
            Color c = _paperColor;
            if (_hovered) c = Color.Lerp(c, Color.white, _hoverBrighten);
            SetColor(c);
        }

        private void SetRenderers(bool on)
        {
            if (_renderers == null) return;
            foreach (var r in _renderers) if (r != null) r.enabled = on;
        }

        private void SetColor(Color c)
        {
            if (_renderers == null) return;
            foreach (var r in _renderers)
            {
                if (r == null) continue;
                // 펼쳐진 종이에는 글 그림이 얹혀 있다. 거기까지 물들이면 글씨가 사라진다.
                if (_scroll != null && r.transform.name == "종이") continue;
                // 봉인 띠는 인주 빛이라야 한다. 종이색을 덮어씌우면 붉은 띠가 하얘진다.
                if (r.transform.name == "봉인") continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, c);
                r.SetPropertyBlock(_mpb);
            }
        }
    }

    /// <summary>
    /// 펼쳐 든 봉서의 종이 바깥. 누르면 도로 내려놓는다.
    /// <see cref="IntroDocument"/> 가 코드로 세우므로 인스펙터에서 다룰 일은 없다.
    /// </summary>
    public class DocumentPutBack : MonoBehaviour, ISelectable
    {
        private IntroDocument _doc;
        public void Bind(IntroDocument doc) => _doc = doc;

        public void OnHoverEnter() { }
        public void OnHoverExit() { }
        public void OnSelect() { if (_doc != null) _doc.Lower(); }
    }
}
