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
        [Tooltip("읽는 중임을 알리는 말")]
        [SerializeField] private string _readPrompt = "맡으려면 한 번 더 누르시오.";

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
            Vector3 toPos = cam.transform.position
                            + cam.transform.forward * _readDistance
                            + cam.transform.up * (half - _readDrop);
            Quaternion toRot = Quaternion.LookRotation(cam.transform.position - toPos, Vector3.up);

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
            SubtitleView.Show("", string.IsNullOrEmpty(_label) ? _readPrompt : _label, _readPrompt);
            _moving = null;
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
            if (!string.IsNullOrEmpty(_label)) SubtitleView.Show("", _label, "(집어 보려면 누르기)");
        }

        public void OnHoverExit()
        {
            if (!_hovered) return;
            _hovered = false;
            RefreshColor();
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
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, c);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
