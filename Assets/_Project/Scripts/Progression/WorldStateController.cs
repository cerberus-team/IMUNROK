using System.Collections;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 조사청의 "세계 상태"를 관리한다.
    ///  - 닫힘(어둠): 창밖이 어둡고 안개 낀 상태. 봉서함 비활성.
    ///  - 열림(밝음): 세 사건을 모두 풀면 창호가 서서히 밝아지고 봉서함이 활성화됨.
    ///
    /// GameState.OnAllCasesCompleted 를 구독해, 세 사건이 모두 완료되는 순간
    /// 조명·창호·창빛을 시간에 걸쳐 밝히고 봉서함을 켠다.
    /// (씬에 다시 들어왔을 때 이미 완료 상태면 즉시 열린 모습으로 시작.)
    /// </summary>
    public class WorldStateController : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private Light _sun;               // 방 전체 조명(Directional)
        [SerializeField] private Renderer _windowRenderer; // 창호(Quad)
        [SerializeField] private Light _windowLight;        // 창으로 들어오는 빛(Point)
        [SerializeField] private BongseoBox _bongseo;       // 봉서함

        [Header("닫힘(어둠) 상태 값")]
        [SerializeField] private float _closedSunIntensity = 0.7f;
        [SerializeField] private Color _closedAmbient = new Color(0.18f, 0.18f, 0.22f);
        [SerializeField] private Color _closedWindowColor = new Color(0.03f, 0.03f, 0.05f);
        [SerializeField] private float _closedWindowLight = 0f;

        [Header("열림(밝음) 상태 값")]
        [SerializeField] private float _openSunIntensity = 1.3f;
        [SerializeField] private Color _openAmbient = new Color(0.70f, 0.72f, 0.78f);
        [SerializeField] private Color _openWindowColor = new Color(1.0f, 0.97f, 0.90f);
        [SerializeField] private float _openWindowLight = 2.2f;

        [Header("전환")]
        [Tooltip("어둠→밝음 전환에 걸리는 시간(초)")]
        [SerializeField] private float _transitionDuration = 3f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private GameState _state;
        private MaterialPropertyBlock _mpb;
        private bool _opened;

        /// <summary>에디터 생성기에서 참조를 주입할 때 사용.</summary>
        public void Initialize(Light sun, Renderer windowRenderer, Light windowLight, BongseoBox bongseo)
        {
            _sun = sun;
            _windowRenderer = windowRenderer;
            _windowLight = windowLight;
            _bongseo = bongseo;
        }

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            _state = GameState.Instance;
            _state.OnAllCasesCompleted += HandleAllCompleted;
            _state.OnCaseChanged += HandleCaseChanged;
        }

        private void OnDisable()
        {
            if (_state != null)
            {
                _state.OnAllCasesCompleted -= HandleAllCompleted;
                _state.OnCaseChanged -= HandleCaseChanged;
            }
        }

        /// <summary>리셋 등으로 완료가 풀리면 다시 닫힘(어둠) 상태로 되돌린다(테스트 편의).</summary>
        private void HandleCaseChanged(CaseId _)
        {
            if (_opened && !_state.AllCasesCompleted)
            {
                _opened = false;
                StopAllCoroutines();
                ApplyState(0f);
                if (_bongseo != null) _bongseo.Deactivate();
            }
        }

        private void Start()
        {
            // 조사청은 "사건 밖" — 수첩에 사건 단서가 보이지 않도록 현재 사건을 비운다.
            _state.ExitToHub();

            // 씬 시작 시점의 상태에 맞춰 즉시 세팅(재진입 대비).
            if (_state.AllCasesCompleted)
            {
                ApplyState(1f); // 열린 모습
                _opened = true;
                if (_bongseo != null) _bongseo.Activate();
            }
            else
            {
                ApplyState(0f); // 닫힌 모습
                if (_bongseo != null) _bongseo.Deactivate();
            }
        }

        private void HandleAllCompleted()
        {
            if (_opened) return;
            _opened = true;
            StartCoroutine(OpenRoutine());
        }

        private IEnumerator OpenRoutine()
        {
            Debug.Log("[WorldStateController] 세계가 열립니다 — 창호가 밝아집니다.");
            float t = 0f;
            float dur = Mathf.Max(0.01f, _transitionDuration);
            while (t < 1f)
            {
                t += Time.deltaTime / dur;
                ApplyState(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
                yield return null;
            }
            ApplyState(1f);

            if (_bongseo != null) _bongseo.Activate();
        }

        /// <summary>k=0이면 닫힘(어둠), k=1이면 열림(밝음). 그 사이는 보간.</summary>
        private void ApplyState(float k)
        {
            if (_sun != null)
                _sun.intensity = Mathf.Lerp(_closedSunIntensity, _openSunIntensity, k);

            RenderSettings.ambientLight = Color.Lerp(_closedAmbient, _openAmbient, k);

            if (_windowLight != null)
                _windowLight.intensity = Mathf.Lerp(_closedWindowLight, _openWindowLight, k);

            if (_windowRenderer != null)
            {
                Color wc = Color.Lerp(_closedWindowColor, _openWindowColor, k);
                _windowRenderer.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, wc);
                _windowRenderer.SetPropertyBlock(_mpb);
            }
        }
    }
}
