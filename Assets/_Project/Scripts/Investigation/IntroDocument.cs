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
        [Tooltip("눈에서 이만큼 앞에 들어 올린다(m). 이보다 가까이는 들지 않는다 — " +
                 "종이가 화면에 다 안 들어오면 들어올 때까지 저절로 물러난다")]
        [SerializeField] private float _readDistance = 0.62f;

        [Tooltip("펼친 종이가 화면 세로를 차지할 몫. 1이면 딱 맞아 가장자리가 아슬아슬하다")]
        [Range(0.6f, 1f)]
        [SerializeField] private float _readFill = 0.92f;
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
        [Tooltip("종이 끝을 누르는 아랫축이 차지하는 자리(m). 이만큼 더 내려가야 축을 지난다")]
        [SerializeField] private float _bottomRodRoom = 0.035f;
        [Tooltip("펼쳐 든 동안 이름표를 눈에서 이만큼 떨어진 곳에 못 박는다(m). " +
                 "두루마리는 아랫축이 앞으로 튀어나와 있어, 조금 빼는 정도로는 " +
                 "글자가 그 축에 걸쳐 파묻힌다. 아예 그보다 앞에 세운다")]
        [SerializeField] private float _readLabelDistance = 0.34f;
        [Tooltip("이름표 글씨 크기. 보이는 크기는 거리에 상관없이 늘 같다")]
        [SerializeField] private int _labelFontSize = 56;
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
            float half = PaperHalf();
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
        ///
        /// <b>한 번만 맞추면 안 된다.</b> 두루마리는 <see cref="ScrollUnroll.Unroll"/> 로
        /// <b>시간을 두고</b> 풀린다. 푸는 일을 시킨 그 프레임에 자리를 재면 종이는 아직
        /// 말린 채라 세로 5cm 짜리 띠가 나오고, 그 뒤로 영영 안 고쳐진다.
        /// 그러면 화면의 90%가 「물리기」인 <see cref="DocumentPutBack"/> 판이 되어,
        /// 안내대로 <b>글을 눌러도 봉서가 도로 내려갈 뿐</b> 사건을 맡을 수가 없다.
        /// 그래서 읽는 동안 매 프레임 다시 잰다 — 종이가 자라는 만큼 자리도 자란다.
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
            _putBack.transform.localPosition = new Vector3(0f, 0f, ReadDistance(cam) + 0.35f);
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
        /// <summary>
        /// 축에서 종이 한가운데까지의 거리. 종이는 축 바로 아래가 아니라 축 굵기만큼
        /// 내려 달려 있으므로(그래야 제목 한자가 축에 안 가린다) 그 틈까지 세어야
        /// 종이 한가운데가 시선 위에 온다.
        /// </summary>
        private float PaperHalf()
        {
            if (_scroll == null) return 0.2f;
            return (_scroll.TopGap + _scroll.FullHeight * 0.5f) * ScrollScale();
        }

        /// <summary>축에서 종이 끝까지의 거리(m). 이름표를 그 밑에 붙일 때 쓴다.</summary>
        private float PaperRun()
        {
            if (_scroll == null) return 0.4f;
            return (_scroll.TopGap + _scroll.FullHeight) * ScrollScale();
        }

        /// <summary>
        /// 두루마리가 제 배율을 갖고 있다(씬에서 1.05배로 놓았다). 스크립트가 돌려주는
        /// 길이는 두루마리 안쪽 자로 잰 것이므로, 세계에 대고 쓰려면 그 배율을 곱해야 한다.
        /// 이것을 빠뜨리면 잰 길이가 실제보다 짧아 종이가 화면 아래로 밀려 잘린다.
        /// </summary>
        private float ScrollScale()
        {
            return _scroll == null ? 1f : Mathf.Abs(_scroll.transform.lossyScale.y);
        }

        private Vector3 ReadPosition(Camera cam, float half)
        {
            Vector3 center = cam.transform.position
                             + cam.transform.forward * ReadDistance(cam)
                             - cam.transform.up * _readDrop;
            return center + cam.transform.up * half;
        }

        /// <summary>
        /// 펼친 종이를 들어 올릴 거리. 못 박으면 안 된다 — 얼마나 멀리 들어야 다 보이는지는
        /// <b>보는 이의 시야각</b>이 정한다. 모니터는 세로 60도라 종이 한 장도 빠듯해
        /// 멀찍이 들어야 하고, 헤드셋은 그 갑절이라 같은 종이를 코앞에 들어도 다 들어온다.
        /// 시야각에서 뽑아 쓰면 리그를 갈아 끼워도 다시 맞출 일이 없다.
        /// </summary>
        private float ReadDistance(Camera cam)
        {
            // 종이 + 그 아래 이름표 자리까지가 화면 세로에 들어와야 한다.
            float span = PaperRun() + _bottomRodRoom + _readLabelGap + 0.06f;
            float halfTan = Mathf.Tan(Mathf.Max(1f, cam.fieldOfView) * 0.5f * Mathf.Deg2Rad);
            float need = span / (2f * halfTan * Mathf.Clamp(_readFill, 0.3f, 1f));
            return Mathf.Max(_readDistance, need);
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

        /// <summary>
        /// <b>봉서로 화면을 덮는다.</b> 이것을 든 채 다음 자리로 넘어간다는 말이다.
        ///
        /// 여태는 봉서를 맡으면 <b>화면이 그냥 캄캄해졌다</b>. 그 검은 막은 아무것도
        /// 아니라서, 화면이 <b>바뀐 것</b>이지 내가 <b>들고 간 것</b>이 아니었다.
        /// 손에 쥔 것으로 화면을 덮으면 그 종이가 다음 자리까지 따라온 것이 된다 —
        /// 조사청에 닿았을 때 손에 봉서가 있는 것이 자연스러워진다.
        ///
        /// 어떻게: 읽던 자리(눈앞 0.6m)에서 <b>눈 바로 앞</b>까지 끌어당긴다.
        /// 종이 한 장은 그 거리에서 시야를 통째로 덮는다 — 키울 것도 없다.
        /// 끝에 가서는 <b>가속</b>한다. 고르게 당기면 종이가 다가오는 것이 아니라
        /// 화면이 확대되는 것으로 보인다.
        ///
        /// 자막·이름표는 먼저 걷는다. 종이 뒤에 글이 비쳐 보이면 종이가 아니라
        /// 유리로 보인다.
        /// </summary>
        /// <param name="seconds">덮는 데 걸리는 시간</param>
        /// <param name="then">다 덮은 뒤에 할 일 — 대개 씬 갈아 끼우기</param>
        public void CoverScreen(float seconds, System.Action then)
        {
            if (_moving != null) StopCoroutine(_moving);
            _moving = StartCoroutine(CoverRoutine(seconds, then));
        }

        private IEnumerator CoverRoutine(float seconds, System.Action then)
        {
            var cam = Camera.main;
            if (cam == null) { if (then != null) then(); yield break; }

            HideLabel();
            KillPutBackTarget();
            SubtitleView.Hide();
            if (_collider != null) _collider.enabled = false;

            // <b>읽는중에서 빠져나와야 한다.</b> LateUpdate 가 「읽는중」인 동안
            // 종이를 <b>매 프레임 읽는 자리로 끌어다 놓기</b> 때문이다(고개를 돌려도
            // 종이가 정면을 보게 하려고 둔 것). 코루틴이 당겨 놓으면 그 프레임 끝에
            // 도로 밀려나서, 6초를 걸어 놓고도 종이가 <b>한 뼘도 안 움직였다</b>.
            //
            // 떠오르는중으로 옮긴다 — LateUpdate 는 손을 떼고, IsReading 은 참으로
            // 남아 다른 데서 「지금 읽는 중」으로 세는 셈은 그대로 간다.
            _phase = Phase.떠오르는중;

            Vector3 fromPos = transform.position;
            Quaternion fromRot = transform.rotation;

            float dur = Mathf.Max(0.05f, seconds);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / dur;
                float k = Mathf.Clamp01(t);

                // 끝에서 빨라진다 — 다가오는 것은 가까울수록 빨리 커진다
                float e = k * k * k;

                // 눈 바로 앞. 카메라의 앞 자름면보다 조금 앞이라야 잘리지 않는다.
                float near = Mathf.Max(cam.nearClipPlane + 0.02f, 0.055f);
                float half = PaperHalf();
                Vector3 to = cam.transform.position
                             + cam.transform.forward * near
                             + cam.transform.up * half;      // 종이는 축에 매달려 아래로 자란다

                transform.position = Vector3.Lerp(fromPos, to, e);
                transform.rotation = Quaternion.Slerp(fromRot, ReadRotation(cam), Mathf.Min(1f, e * 2f));
                yield return null;
            }

            _moving = null;
            if (then != null) then();
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
                float half = PaperHalf();
                float t = 1f - Mathf.Exp(-_readFollow * Time.deltaTime);   // 프레임률에 안 흔들리는 감쇠
                transform.position = Vector3.Lerp(transform.position, ReadPosition(cam, half), t);
                transform.rotation = Quaternion.Slerp(transform.rotation, ReadRotation(cam), t);

                // 종이가 풀리는 동안 집는 자리도 같이 자라야 한다. 위 주석 참고 —
                // 펼치라고 시킨 그 프레임에 한 번 재고 마는 것이 오래 묵은 탈이었다.
                FitColliderToPaper();
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
            // 이름표는 <b>눈에 보이는 크기</b>가 늘 같아야 한다. 세계 크기를 못 박아 두면
            // 발치에 놓였을 땐 좁쌀만 하고 눈앞에 들면 커진다. 거리에 비례해 키운다.
            const float LabelPerMeter = 0.0006f;
            // 화면 아래로 빠지지 않게 붙들어 두는 자리(뷰포트 0~1).
            const float MinLabelViewportY = 0.06f;

            if (_phase == Phase.읽는중)
            {
                // 종이 아래에 두되, 눈에서 정해진 거리에 못 박는다. 두루마리보다 앞이라야
                // 아랫축에 걸려 글자가 파묻히지 않는다. 가까워진 만큼 작게 그려
                // 보기에는 늘 같은 크기가 되게 한다.
                // 두루마리의 아래는 세계의 아래가 아니라 <b>보는 사람의 아래</b>다.
                // 세계 기준 경계상자의 세로로 재면 종이가 시선 쪽으로 세워져 있는 만큼
                // 어긋나, 이름표가 종이 위에 얹힌다. 축에서 종이 끝까지를 직접 센다.
                float toBottom = PaperRun() + _bottomRodRoom + _readLabelGap;
                Vector3 want = transform.position - cam.transform.up * toBottom;

                // 펼친 종이가 화면을 거의 채우면 <b>그 아래는 이미 화면 밖</b>이다. 각도로만
                // 재어 눈앞으로 당기면 밖에 있는 것을 가까이 옮긴 것일 뿐, 여전히 안 보인다.
                // 화면 안 좌표로 옮겨 붙인다 — 자리가 남으면 종이 밑에, 모자라면 화면
                // 아래 가장자리에. 안내가 소리 없이 사라지던 것이 이것이었다.
                Vector3 vp = cam.WorldToViewportPoint(want);
                if (vp.z <= 0f) { HideLabel(); return; }
                vp.y = Mathf.Max(vp.y, MinLabelViewportY);
                vp.z = _readLabelDistance;
                Vector3 at = cam.ViewportToWorldPoint(vp);

                _labelGo.transform.position = at;
                // 캔버스는 <b>앞면이 뒤를 보게</b> 세워야 글자가 바로 읽힌다.
                // 카메라 쪽(-forward)을 보게 하면 뒷면을 보는 셈이라 글씨가 뒤집힌다.
                _labelGo.transform.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);
                // 눈앞으로 당겨 세운 만큼 작게 그린다 — 보이는 크기는 그대로다.
                _labelGo.transform.localScale = Vector3.one * (LabelPerMeter * _readLabelDistance);
                return;
            }

            Vector3 pos = new Vector3(b.center.x, b.max.y + _labelHeight, b.center.z);
            _labelGo.transform.position = pos;
            _labelGo.transform.rotation = Quaternion.LookRotation(pos - cam.transform.position, Vector3.up);
            _labelGo.transform.localScale =
                Vector3.one * (LabelPerMeter * Vector3.Distance(pos, cam.transform.position));
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
