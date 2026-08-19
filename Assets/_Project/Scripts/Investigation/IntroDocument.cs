using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common
{
    /// <summary>
    /// 어전(도입)에서 왕이 내려놓는 세 사건 문서 중 하나(두루마리/종이 프리미티브).
    /// 왕의 대사가 끝나면 하나씩 "주르륵" 밀려나오고, 하나를 집으면
    /// 그 사건을 InProgress로 표시한 뒤 조사청(HubScene)으로 진입한다.
    ///
    /// 선택은 ISelectable로 추상화되어, 지금은 마우스 클릭 / 나중엔 VR 손뻗기(레이·Grab)로
    /// 같은 OnSelect()가 호출된다.
    /// </summary>
    public class IntroDocument : MonoBehaviour, ISelectable
    {
        [SerializeField] private CaseId _caseId = CaseId.Case1_Onggojip;
        [Tooltip("문서를 집었을 때 진입할 조사청 씬 이름")]
        [SerializeField] private string _hubSceneName = "HubScene";

        [Tooltip("가리켰을 때 뜰 이름. 어느 사건인지 알고 고를 수 있어야 한다")]
        [SerializeField] private string _label = "";

        [SerializeField] private Color _paperColor = new Color(0.85f, 0.80f, 0.68f); // 종이/한지 색
        [Range(0f, 1f)]
        [SerializeField] private float _hoverBrighten = 0.30f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        // 모델이 자식으로 들어올 수 있다(두루마리 프리팹처럼). 자기 자신에게만
        // 렌더러를 찾으면 그런 구조에서 아무것도 안 보이고 색도 안 바뀐다.
        private Renderer[] _renderers;
        private MaterialPropertyBlock _mpb;
        private Collider _collider;

        private Vector3 _shownLocalPos; // 최종 위치(빌더가 놓은 자리)
        private bool _ready;            // 슬라이드가 끝나 선택 가능한 상태인가
        private bool _hovered;

        public CaseId CaseId => _caseId;

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
            _shownLocalPos = transform.localPosition;
            SetColor(_paperColor);
            Hide();
        }

        /// <summary>등장 전 숨김(렌더러·콜라이더 off).</summary>
        private void Hide()
        {
            _ready = false;
            SetRenderers(false);
            if (_collider != null) _collider.enabled = false;
        }

        /// <summary>왕 대사 후, 시차를 두고 스르륵 밀려나오는 등장 연출.</summary>
        public void PlaySlideIn(float delay, float duration, float fromDistance)
        {
            StartCoroutine(SlideRoutine(delay, duration, fromDistance));
        }

        private IEnumerator SlideRoutine(float delay, float duration, float fromDistance)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);

            SetRenderers(true);
            Vector3 from = _shownLocalPos + new Vector3(0f, 0f, fromDistance); // 왕 쪽에서
            float t = 0f;
            float dur = Mathf.Max(0.01f, duration);
            while (t < 1f)
            {
                t += Time.deltaTime / dur;
                transform.localPosition = Vector3.Lerp(from, _shownLocalPos, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
                yield return null;
            }
            transform.localPosition = _shownLocalPos;

            if (_collider != null) _collider.enabled = true;
            _ready = true;
        }

        // ── ISelectable ─────────────────────────────

        public void OnHoverEnter()
        {
            if (!_ready) return;
            _hovered = true;
            RefreshColor();

            // 어느 사건인지 모른 채 고르게 하면 고르는 것이 아니라 찍는 것이 된다.
            if (!string.IsNullOrEmpty(_label)) SubtitleView.Show("", _label, "(집으려면 누르기)");
        }

        public void OnHoverExit()
        {
            _hovered = false;
            RefreshColor();
        }

        /// <summary>
        /// 고르지 않은 문서를 굳힌다. 하나를 집는 순간 나머지도 눌러지면
        /// 사건 둘이 한꺼번에 시작돼 버린다.
        /// </summary>
        public void Freeze()
        {
            _ready = false;
            _hovered = false;
            if (_collider != null) _collider.enabled = false;
            RefreshColor();
        }

        public void OnSelect()
        {
            if (!_ready) return;

            // 집은 문서의 사건이 그 자리에서 시작된다. 조사청 사건판의 그 큐브가
            // 주황으로 켜져 있고, 거기서 현장으로 들어가면 된다.
            //
            // 한때 "어느 걸 집어도 셋 다 받는다"로 바꿔 본 적이 있다. 어전에서 고르면
            // 첫 조사청 방문이 할 일 없는 통로가 된다는 이유였는데, 그 이유가 틀렸다 —
            // 조사청은 도구를 지급받는 곳이고 기록대가 있는 곳이라 들를 까닭이 충분하다.
            GameState.Instance.StartCase(_caseId);
            Debug.Log($"[IntroDocument] {_caseId} 문서를 집었습니다 — 조사청으로 갑니다.");

            var intro = FindFirstObjectByType<IntroController>();
            if (intro != null) { intro.TakeChosen(_caseId); return; }

            // 어전에 진행 담당이 없으면(따로 시험할 때) 혼자 넘어간다.
            if (!string.IsNullOrEmpty(_hubSceneName) && Application.CanStreamedLevelBeLoaded(_hubSceneName))
                SceneManager.LoadScene(_hubSceneName);
            else
                Debug.LogWarning($"[IntroDocument] 조사청 씬('{_hubSceneName}')을 찾을 수 없습니다. " +
                                 $"File ▸ Build Profiles 의 씬 목록에 추가했는지, 이름이 맞는지 확인하세요.");
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
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, c);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
