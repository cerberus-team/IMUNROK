using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace IMUNROK.Common
{
    /// <summary>
    /// 두루마리 — 말린 뭉치에서 종이가 아래로 주르륵 풀린다.
    ///
    /// 왜 이 방식인가: 처음엔 2D 영상도 후보였으나 VR에서는 못 쓴다. 평면 영상은
    /// 고개를 돌려도 따라오지 않아 종이가 아니라 눈앞의 스크린으로 보인다.
    /// 그렇다고 펼쳐진 채 두면 "방금 받았다"는 느낌이 없다 — 이 연출의 목적이 바로
    /// 그 느낌이므로, 실제로 풀리게 만든다.
    ///
    /// 무겁게 만들 필요는 없다. 판 하나의 세로를 키우면서 아래 축을 같이 내리면 끝이다.
    /// 다만 판만 늘리면 글자가 같이 늘어나 고무처럼 보인다. 그래서 재질의 타일링·오프셋을
    /// 길이에 맞춰 함께 움직여, <b>늘 위에서부터 제 비율로</b> 드러나게 한다.
    ///
    /// 세 자리에서 같은 물건을 쓴다:
    ///   · 표제      — 소반 위 봉서(집으면 시작)
    ///   · 어명      — 왕이 내리는 봉서 세 통
    ///   · 조사종이  — 사건에 들어설 때 펼쳐지는 사건 개요
    /// 사건마다 <see cref="SetDocument"/> 로 그림만 갈아 끼우면 된다.
    ///
    /// 붙이는 법: [이문록 ▸ 두루마리 만들기] 로 프리팹을 뽑아 쓰거나,
    /// 직접 만들 때는 윗축·종이(Quad)·아랫축을 자식으로 두고 여기에 연결한다.
    /// 세로 길이는 그림의 가로세로비에서 스스로 구하므로 손으로 적지 않는다.
    /// </summary>
    public class ScrollUnroll : MonoBehaviour, ISelectable
    {
        [Header("부재")]
        [Tooltip("위에 걸린 말린 뭉치. 풀릴수록 가늘어진다")]
        [SerializeField] private Transform _topRod;

        [Tooltip("글이 적힌 종이 판(Quad). 세로로 자란다")]
        [SerializeField] private Transform _paper;

        [Tooltip("종이 아래를 누르는 축. 종이 끝을 따라 내려간다. 없어도 된다")]
        [SerializeField] private Transform _bottomRod;

        [Tooltip("종이의 렌더러. 비우면 _paper 에서 찾는다")]
        [SerializeField] private Renderer _paperRenderer;

        [Tooltip("종이의 콜라이더. 있으면 길이에 맞춰 같이 자란다")]
        [SerializeField] private BoxCollider _paperCollider;

        [Header("종이")]
        [Tooltip("종이 가로폭(m). 세로는 그림 비율에서 저절로 정해진다")]
        [SerializeField] private float _width = 0.42f;

        [Tooltip("이 두루마리에 적힌 글(사건 문서 그림). 사건마다 갈아 끼운다")]
        [SerializeField] private Texture2D _document;

        [Tooltip("그림이 없을 때 쓸 세로÷가로 비율")]
        [SerializeField] private float _fallbackAspect = 1.6f;

        [Tooltip("종이를 축보다 이만큼 내려 단다(m). 축 굵기의 절반쯤이면 된다. " +
                 "0이면 축이 종이 윗머리를 덮어 문서 제목이 잘려 보인다")]
        [SerializeField] private float _topGap = 0.075f;

        [Tooltip("다 풀렸을 때 뭉치가 남는 굵기(1이면 그대로, 0.5면 절반)")]
        [Range(0.2f, 1f)]
        [SerializeField] private float _rodShrink = 0.55f;

        [Header("풀림")]
        [SerializeField] private float _unrollSeconds = 1.4f;
        [SerializeField] private float _rollSeconds = 0.8f;

        [Tooltip("켜지는 순간 스스로 풀린다. 사건 진입 연출처럼 저절로 나와야 할 때")]
        [SerializeField] private bool _unrollOnEnable = false;

        [Tooltip("다 풀린 뒤 손대면 도로 말린다(다 읽고 놓는 동작)")]
        [SerializeField] private bool _clickToRoll = true;

        [Header("알림")]
        [Tooltip("다 풀렸을 때")]
        public UnityEvent OnUnrolled;
        [Tooltip("도로 말려 닫혔을 때 — 수첩으로 넣는 처리를 여기 건다")]
        public UnityEvent OnRolled;

        // URP Lit 의 색·그림 프로퍼티. 문자열 조회는 한 번만 한다.
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");

        private MaterialPropertyBlock _mpb;
        private Coroutine _running;
        private float _shown;          // 0 = 말림, 1 = 다 풀림
        private bool _rodBaseCached;
        private Vector3 _rodBase;

        /// <summary>지금 다 풀려 있는가.</summary>
        public bool IsOpen => _shown >= 0.999f;

        /// <summary>종이가 축보다 얼마나 내려 달렸는가(m). 종이 한가운데를 계산할 때 쓴다.</summary>
        public float TopGap => _topGap;

        /// <summary>이 두루마리의 종이가 다 풀렸을 때의 세로 길이(m).</summary>
        public float FullHeight
        {
            get
            {
                float aspect = _document != null && _document.width > 0
                    ? (float)_document.height / _document.width
                    : _fallbackAspect;
                return Mathf.Max(0.01f, _width * aspect);
            }
        }

        private void Reset()
        {
            if (_paper == null) _paper = transform.Find("종이");
            if (_topRod == null) _topRod = transform.Find("윗축");
            if (_bottomRod == null) _bottomRod = transform.Find("아랫축");
        }

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            if (_paperRenderer == null && _paper != null) _paperRenderer = _paper.GetComponent<Renderer>();
            if (_paperCollider == null && _paper != null) _paperCollider = _paper.GetComponent<BoxCollider>();
            CacheRod();
            Apply(0f);
        }

        private void OnEnable()
        {
            if (!_unrollOnEnable) return;
            Apply(0f);
            Unroll();
        }

        private void CacheRod()
        {
            if (_rodBaseCached || _topRod == null) return;
            _rodBase = _topRod.localScale;
            _rodBaseCached = true;
        }

        // ── 바깥에서 부르는 것 ──────────────────────────

        /// <summary>이 두루마리에 적힐 글을 갈아 끼운다. 세로 길이도 그림 비율에 맞춰 다시 잡힌다.</summary>
        public void SetDocument(Texture2D document)
        {
            _document = document;
            Apply(_shown);
        }

        /// <summary>주르륵 풀린다.</summary>
        public void Unroll() { Play(1f, _unrollSeconds); }

        /// <summary>도로 말린다.</summary>
        public void Roll() { Play(0f, _rollSeconds); }

        /// <summary>연출 없이 즉시 그 상태로 둔다(0 = 말림, 1 = 풀림).</summary>
        public void SetInstant(float amount)
        {
            if (_running != null) { StopCoroutine(_running); _running = null; }
            Apply(Mathf.Clamp01(amount));
        }

        private void Play(float target, float seconds)
        {
            if (!isActiveAndEnabled) { Apply(target); return; }
            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(Move(target, seconds));
        }

        private IEnumerator Move(float target, float seconds)
        {
            float from = _shown;
            float dur = Mathf.Max(0.01f, seconds);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / dur;
                Apply(Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t))));
                yield return null;
            }
            Apply(target);
            _running = null;

            if (target >= 0.999f) OnUnrolled?.Invoke();
            else if (target <= 0.001f) OnRolled?.Invoke();
        }

        // ── 실제로 모양을 만드는 곳 ────────────────────

        /// <summary>풀린 정도(0~1)를 부재들에 반영.</summary>
        private void Apply(float amount)
        {
            // 편집 모드에서는 Awake 가 불리지 않는다. 씬을 짜는 도구가 SetInstant 로
            // 말린 모습을 만들어 두는 일이 있으므로, 없으면 여기서 만든다.
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            if (_paperRenderer == null && _paper != null) _paperRenderer = _paper.GetComponent<Renderer>();
            if (_paperCollider == null && _paper != null) _paperCollider = _paper.GetComponent<BoxCollider>();

            _shown = Mathf.Clamp01(amount);
            float full = FullHeight;
            float h = full * _shown;

            if (_paper != null)
            {
                // 판은 가운데가 중심이므로, 위쪽 끝을 원점에 붙이려면 절반만큼 내려 놓는다.
                // 원점에 딱 붙이면 그 자리에 걸린 축이 종이 윗머리를 덮는다 — 문서의
                // 제목(訴狀·牒報 같은 한자)이 바로 거기 있어서 잘려 보였다.
                // 축 굵기만큼 내려 달아 종이가 축 밑에서 시작하게 한다.
                _paper.localScale = new Vector3(_width, Mathf.Max(0.0001f, h), 1f);
                _paper.localPosition = new Vector3(0f, -_topGap - h * 0.5f, 0f);

                var r = _paperRenderer;
                if (r != null)
                {
                    r.enabled = h > 0.001f;

                    // 판만 늘리면 글자가 고무처럼 늘어난다. 드러난 만큼만 그림의 위쪽을
                    // 잘라 쓰면(타일링 = 비율, 오프셋 = 나머지) 언제나 제 비율로 보인다.
                    // v 는 아래가 0, 위가 1이므로 위에서부터 드러나려면 오프셋이 1-비율이다.
                    r.GetPropertyBlock(_mpb);
                    var st = new Vector4(1f, _shown, 0f, 1f - _shown);
                    _mpb.SetVector(BaseMapStId, st);
                    if (_document != null)
                    {
                        _mpb.SetTexture(BaseMapId, _document);
                        _mpb.SetTexture(MainTexId, _document);
                    }
                    r.SetPropertyBlock(_mpb);
                }
            }

            if (_paperCollider != null)
            {
                // 콜라이더는 판의 자식이 아니라 판 자신에 붙어 있다 — 판의 배율이 이미
                // 곱해지므로 여기서는 1×1 을 그대로 두면 된다. 다만 다 말렸을 때는 꺼둔다.
                _paperCollider.enabled = _clickToRoll && _shown > 0.05f;
            }

            if (_bottomRod != null)
            {
                _bottomRod.localPosition = new Vector3(0f, -_topGap - h, 0f);
                var br = _bottomRod.GetComponent<Renderer>();
                if (br != null) br.enabled = h > 0.001f;
            }

            if (_topRod != null)
            {
                CacheRod();
                // 종이가 빠져나온 만큼 뭉치가 가늘어진다. 굵기(x·z)만 줄이고 길이(y)는 둔다.
                float k = Mathf.Lerp(1f, _rodShrink, _shown);
                _topRod.localScale = new Vector3(_rodBase.x * k, _rodBase.y, _rodBase.z * k);
            }
        }

        // ── 손대기(마우스·VR 레이) ─────────────────────

        public void OnHoverEnter() { }
        public void OnHoverExit() { }

        public void OnSelect()
        {
            if (!_clickToRoll) return;
            if (!IsOpen) return;      // 풀리는 중에 건드려 닫히면 읽을 새가 없다
            Roll();
        }
    }
}
