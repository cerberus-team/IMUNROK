using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IMUNROK.Common
{
    /// <summary>
    /// 표제 — 게임을 켜면 가장 먼저 오는 자리. 어전 씬의 앞 열두 마디다.
    ///
    /// 아무것도 누르지 않아도 저절로 흐른다:
    ///   ① 어둠. 소리만 먼저 든다.
    ///   ② 「이문록」이 위에서 천천히 내려앉는다.
    ///   ③ 어전이 서서히 밝아 오르고, 그와 함께 <b>고개가 숙여진다</b>.
    ///   ④ 제목이 스러지고 왕이 입을 연다.
    ///
    /// 왜 집는 물건을 두지 않는가: 처음엔 소반에 봉서를 놓고 집게 했다. 손으로
    /// 물건을 다루는 게임임을 첫 동작으로 가르치려던 것인데, 실제로 보면 어둠 속에
    /// 상자 하나가 놓여 있을 뿐이라 시작 화면으로 보이지 않았다. 제목이 뜨는 동안
    /// 뒤에 어전이 훤히 보이는 것도 "메뉴가 씬 위에 떠 있는" 꼴이었다.
    /// 물건을 다루는 법은 조사청에서 도구를 받으며 배우면 된다. 여는 자리는
    /// 여는 자리답게, 가만히 두고 보게 한다.
    ///
    /// 어둠은 검은 판을 덮어 만들지 않는다. 어전의 불을 다 끄고 환경광을 내리면
    /// 세계가 실제로 어두워지고, 밝아 오를 때 그 빛이 바닥을 훑고 지나간다.
    /// 판을 덮었다 걷으면 그 훑고 지나감이 안 생긴다.
    ///
    /// 아무 데나 누르면 건너뛴다. 저장이 있으면 <b>꾹 누르기</b>로 하던 데부터.
    /// </summary>
    public class TitleGate : MonoBehaviour
    {
        [Header("제목")]
        [Tooltip("<b>표제 그림</b>. 넣으면 글씨 대신 이것이 내려앉는다. 붓으로 쓴 제자(題字)는 " +
                 "글꼴로 흉내 낼 수 없으므로, 이 자리는 그림을 받는 것이 맞다. 비우면 아래 글씨를 쓴다")]
        [SerializeField] private Texture2D _titleImage;
        [Tooltip("표제 그림의 가로 크기(캔버스 단위). 세로는 그림 비례대로 따라온다")]
        [SerializeField] private float _titleImageWidth = 760f;
        [Tooltip("표제 그림을 캔버스 한가운데에서 얼마나 옮길지. 아래 건너뛰기 안내와 안 붙게")]
        [SerializeField] private Vector2 _titleImageOffset = new Vector2(0f, 55f);
        [Tooltip("표제 그림을 쓸 때도 아래 한자와 가는 줄을 남길지. 그림이 이미 한자면 겹치므로 끈다")]
        [SerializeField] private bool _keepSubtitleWithImage = false;

        [SerializeField] private string _title = "이문록";
        [SerializeField] private string _subtitle = "異聞錄";
        [Tooltip("제목이 앉을 자리. 비우면 카메라 앞에 띄운다")]
        [SerializeField] private Transform _titleAnchor;
        [Tooltip("눈에서 이만큼 앞(m). 고개를 숙인 자세에서는 멀리 둘수록 땅속으로 들어간다")]
        [SerializeField] private float _titleDistance = 0.85f;
        [SerializeField] private float _titleHeight = 0.15f;
        [Tooltip("제목이 이 높이 아래로는 내려가지 않는다(m). 숙인 시선 그대로 두면 " +
                 "바닥 밑에 놓여 통째로 가려진다 — 소리 없이 사라지는 종류의 탈이다")]
        [SerializeField] private float _titleMinHeight = 0.22f;
        [SerializeField] private float _titleScale = 0.00072f;
        [SerializeField] private Font _font;
        private Color _titleColor { get { return UiLook.Text; } }
        private Color _subtitleColor { get { return UiLook.Lit(UiLook.Seal, 0.15f); } }

        [Header("차례(초)")]
        [Tooltip("소리만 있고 아무것도 없는 시간")]
        [SerializeField] private float _blackHold = 1.6f;
        [Tooltip("제목이 내려앉는 시간")]
        [SerializeField] private float _titleIn = 2.4f;
        [Tooltip("제목이 위에서 내려오는 거리(m)")]
        [SerializeField] private float _titleDrop = 0.16f;
        [Tooltip("제목이 다 앉은 뒤 배경이 밝아 오르기까지")]
        [SerializeField] private float _beforeWorld = 0.9f;
        [Tooltip("어전이 밝아 오르는 시간. 길수록 장엄하다")]
        [SerializeField] private float _worldIn = 4.2f;
        [Tooltip("밝아 오른 뒤 제목이 머무는 시간")]
        [SerializeField] private float _titleHold = 1.0f;
        [Tooltip("제목이 스러지는 시간")]
        [SerializeField] private float _titleOut = 1.6f;

        [Header("고개")]
        [Tooltip("처음에 이만큼 <b>더</b> 숙인 채로 시작해, 배경이 밝아 오르는 동안 제자리까지 든다(도). " +
                 "부복은 용안을 우러러보지 않는 자세다 — 얼굴을 바닥에 두고 시작해야 맞다")]
        [SerializeField] private float _bowExtra = 22f;

        [Tooltip("제목이 시선을 따라다닌다. 고개가 도는 동안에도 늘 눈앞에 있게 된다")]
        [SerializeField] private bool _titleFollowsView = true;

        [Header("어전")]
        [Tooltip("표제 동안 꺼둘 것 — 어전의 불·왕·문서. 밝아 오를 때 켠다")]
        [SerializeField] private GameObject[] _hideUntilStart;
        [Tooltip("어명 진행 담당. 제목이 스러지면 켠다")]
        [SerializeField] private IntroController _intro;
        [Tooltip("표제 동안의 환경광. 거의 검정이어야 한다")]
        [SerializeField] private Color _darkAmbient = new Color(0.012f, 0.012f, 0.018f);

        [Header("넘어가기·이어하기")]
        [Tooltip("연출이 흐르는 동안 아래에 뜨는 말")]
        [SerializeField] private string _skipHint = "(아무 키나 · 아무 데나 누르면 건너뛰기)";
        [Tooltip("연출이 다 끝나고 <b>기다릴 때</b> 뜨는 말. 이걸 눌러야 어명이 시작된다")]
        [SerializeField] private string _startPrompt = "누르면 어전에 든다   ·   Space 로도 됩니다";
        [Tooltip("저장이 있을 때의 기다림 문구. 짧게 누르면 처음부터라는 것을 밝혀 둔다 — " +
                 "이어할 것이 있는 사람에게 '누르면 어전에 든다'는 어느 쪽인지 알 수 없는 말이다")]
        [SerializeField] private string _startPromptFresh = "누르면 처음부터   ·   Space 로도 됩니다";
        [SerializeField] private string _hubSceneName = "HubScene";

        [Tooltip("VR 에서 컨트롤러 레이로 누를 수 있도록 눈앞에 보이지 않는 판을 둔다. " +
                 "키보드가 없는 헤드셋에서는 이것이 유일한 넘어가는 길이다")]
        [SerializeField] private bool _makePressTarget = true;
        [Tooltip("그 판의 한 변(m). 눈앞을 넉넉히 덮어야 아무 데나 겨눠도 집힌다")]
        [SerializeField] private float _pressTargetSize = 2f;
        [Tooltip("눈에서 이만큼 앞(m). 바닥·제목보다 앞이라야 레이가 이것을 먼저 맞는다 — " +
                 "뒤에 두면 바닥이 먼저 맞아 눌러도 아무 일이 없다")]
        [SerializeField] private float _pressTargetDistance = 0.45f;

        private GameObject _titleGo;
        private CanvasGroup _titleGroup;
        private Text _hintText;
        private Transform _cam;
        private Quaternion _camHome;
        private Color _ambientHome;
        private bool _ambientTaken;   // Awake 를 지났는가 — 안 지났으면 되돌릴 것도 없다
        private readonly List<Light> _lights = new List<Light>();
        private readonly List<float> _lightHome = new List<float>();
        private bool _done;
        private float _hold;
        private bool _waiting;      // 연출이 끝나 사람의 손을 기다리는 중
        private bool _closing;      // 제목을 걷는 중 — 이때 또 부르면 그 걷기가 죽는다
        private bool _heldByKey;    // 지금 누르고 있는 것이 키보드인가(마우스는 레이가 따로 본다)
        private float _titleLift;   // 제목이 제자리보다 얼마나 위에 떠 있는가(m)
        private GameObject _pressTarget;

        private void Awake()
        {
            // 재우는 일은 Awake 에서 해야 한다. Start 에서 끄면 유니티가 Start 를 부르는
            // 순서에 따라 어명이 먼저 깨어나 첫 줄을 표제 뒤에서 혼자 읽어 버린다.
            if (_hideUntilStart != null)
                foreach (var go in _hideUntilStart)
                    if (go != null) go.SetActive(false);
            if (_intro != null) _intro.enabled = false;

            _ambientHome = RenderSettings.ambientLight;
            _ambientTaken = true;

            // 씬에 굳어 버린 어둠을 '제자리'로 잡으면, 밝아 올라도 어둠에서 어둠으로
            // 보간할 뿐이라 <b>환경광이 영영 안 밝아진다</b>. 어떻게 굳는지는 OnDestroy 에.
            if (SameLight(_ambientHome, _darkAmbient))
                Debug.LogWarning("[TitleGate] 씬의 환경광이 표제의 어둠과 같습니다 — 밝아 올라도 " +
                                 "돌아갈 자리가 없습니다. 씬 Lighting 의 Ambient Color 를 제 밝기로 " +
                                 "되돌려 주십시오.", this);

            RenderSettings.ambientLight = _darkAmbient;
        }

        /// <summary>
        /// <b>내려놓은 어둠을 나가는 길에 도로 걷는다.</b>
        ///
        /// 이 판은 환경광을 <see cref="_darkAmbient"/> 로 내려놓고, 어전이 밝아 오를 때
        /// 제자리로 되돌린다. 그 사이에 판이 죽으면(씬을 옮기거나 플레이를 끊으면)
        /// 내려놓은 어둠이 그대로 남는다. 에디터에서는 그 상태로 씬을 저장하면
        /// <b>어둠이 씬에 굳는다</b>.
        ///
        /// 짐작이 아니다. IntroScene 의 환경광 이력을 보면 (0.1, 0.1, 0.12) 과
        /// (0.012, 0.012, 0.018) 이 여러 커밋에 걸쳐 번갈아 들어와 있고, 뒤엣것은
        /// 이 판의 <c>_darkAmbient</c> 기본값과 한 자리도 다르지 않다. 한 번 굳으면
        /// 다음 판의 <see cref="Awake"/> 가 그 어둠을 제자리로 잡아 되돌릴 데를 잃는다 —
        /// 오류도 경고도 없이 번지는 종류의 탈이라, 나가는 길에 반드시 걷는다.
        /// </summary>
        private void OnDestroy()
        {
            if (_ambientTaken) RenderSettings.ambientLight = _ambientHome;
        }

        /// <summary>두 빛이 눈으로 같은가. 색은 자잘한 자리에서 어긋나므로 딱 맞기를 묻지 않는다.</summary>
        private static bool SameLight(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.002f
                && Mathf.Abs(a.g - b.g) < 0.002f
                && Mathf.Abs(a.b - b.b) < 0.002f;
        }

        private void Start()
        {
            var cam = Camera.main;
            if (cam != null)
            {
                _cam = cam.transform;
                _camHome = _cam.rotation;
                // 이마가 바닥에 닿을 만큼 숙인 채로 시작한다. 배경이 밝아 오르는 동안
                // 제자리까지 고개를 든다 — 왕 쪽을 보다가 숙이는 것이 아니라,
                // 엎드려 있다가 겨우 눈을 드는 순서라야 부복이 된다.
                var e = _camHome.eulerAngles;
                _cam.rotation = Quaternion.Euler(e.x + _bowExtra, e.y, e.z);
            }
            BuildTitle();
            // 누름판은 처음부터 둔다. 헤드셋에는 키보드가 없어 이것이 없으면
            // 연출이 다 끝날 때까지 손쓸 방법이 아예 없다.
            MakePressTarget();
            StartCoroutine(Sequence());
        }

        /// <summary>
        /// 제목을 눈앞에 붙들어 둔다. 고개가 이십도 넘게 도는 동안 제목이 한자리에
        /// 못 박혀 있으면 화면 밖으로 밀려나 아무것도 안 보인다.
        /// </summary>
        private void LateUpdate()
        {
            if (_done || _titleGo == null || _cam == null) return;
            if (!_titleFollowsView && _titleAnchor != null) return;

            Vector3 pos = _cam.position
                          + _cam.forward * _titleDistance
                          + _cam.up * (_titleHeight + _titleLift);

            // 바닥 밑으로는 내려보내지 않는다. 월드 캔버스도 깊이 검사를 받으므로
            // 땅 밑에 놓이면 통째로 가려져, 아무 오류 없이 제목만 사라진다.
            if (pos.y < _titleMinHeight) pos.y = _titleMinHeight;

            _titleGo.transform.position = pos;
            _titleGo.transform.rotation = Quaternion.LookRotation(pos - _cam.position, _cam.up);

        }

        private void Update()
        {
            if (_done) return;

            // <b>키보드는 자리에 따라 다르게 듣는다.</b>
            //
            // 연출이 흐르는 동안에는 <b>아무 키나</b> 건너뛴다 — 급한 사람에게 어느 키를
            // 눌러야 하는지 묻지 않는다.
            //
            // 기다리는 자리에서는 <b>스페이스와 엔터만</b> 듣는다. 여기서도 아무 키나
            // 받으면, 방금 건너뛰려고 누른 그 키가 손에서 떨어지기 전에 다음 걸음까지
            // 밀어 버린다 — 표제가 <b>한 번 깜빡이고 사라지는</b> 것으로 보인다.
            // 실제로 그렇게 됐다. 게다가 눈에 보이는 단추를 세워 두고 아무 키나 받으면
            // 그 단추는 눌러 볼 새도 없이 지나간다.
            //
            // 자리를 옮긴 직후 잠깐(_menuGuard)은 아무것도 안 듣는다. 같은 까닭이다.
            if (_guard > 0f) { _guard -= Time.deltaTime; _heldByKey = KeyHeld(); return; }

            bool key = _waiting ? ConfirmKey() : KeyHeld();
            if (key && !_heldByKey) { _heldByKey = true; Advance(); return; }
            if (!key) _heldByKey = false;

            // 기다리는 동안 안내가 천천히 밝았다 어두웠다 한다 — 눌러야 할 것이 있다는 표
            if (_waiting && _hintText != null)
            {
                float k = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 1.5f));
                var c = _hintText.color;
                _hintText.color = new Color(c.r, c.g, c.b, k);
            }
        }

        /// <summary>
        /// 사람이 눌렀다. 연출 도중이면 끝 모습으로 건너뛰고, 이미 기다리는 중이면
        /// 제목을 걷고 어명을 연다.
        ///
        /// 저절로 넘어가지 않는다. 제목이 뜬 화면은 사람이 "이제 시작한다"고
        /// 마음먹는 자리이지, 흘려보내는 자리가 아니다.
        /// </summary>
        public void Advance()
        {
            if (_done || _closing) return;      // 걷는 중에 또 부르면 그 걷기를 죽인다
            if (_waiting) { _closing = true; StartCoroutine(CloseTitle()); return; }
            StopAllCoroutines();
            ShowAll();
            EnterWaiting();
        }

        // ── 차례 ───────────────────────────────────────

        private IEnumerator Sequence()
        {

            // ① 어둠. 소리만.
            yield return new WaitForSeconds(_blackHold);
            if (_done) yield break;

            // ② 제목이 위에서 내려앉는다. 자리는 LateUpdate 가 시선에 맞춰 잡으므로
            //    여기서는 얼마나 위에 떠 있는지(_titleLift)만 줄여 준다.
            yield return Ramp(_titleIn, delegate (float k)
            {
                float e = 1f - Mathf.Pow(1f - k, 3f);        // 끝에서 부드럽게 멎는다
                _titleGroup.alpha = e;
                _titleLift = Mathf.Lerp(_titleDrop, 0f, e);
            });
            if (_done) yield break;

            yield return new WaitForSeconds(_beforeWorld);
            if (_done) yield break;

            // ③ 어전이 밝아 오르고, 그와 함께 고개가 숙여진다.
            WakeWorld();
            Quaternion bowFrom = _cam != null ? _cam.rotation : Quaternion.identity;
            yield return Ramp(_worldIn, delegate (float k)
            {
                float e = Mathf.SmoothStep(0f, 1f, k);
                RenderSettings.ambientLight = Color.Lerp(_darkAmbient, _ambientHome, e);
                for (int i = 0; i < _lights.Count; i++)
                    if (_lights[i] != null) _lights[i].intensity = _lightHome[i] * e;
                if (_cam != null) _cam.rotation = Quaternion.Slerp(bowFrom, _camHome, e);
            });
            if (_done) yield break;

            yield return new WaitForSeconds(_titleHold);
            if (_done) yield break;

            // ④ 여기서 멎는다. 사람이 누를 때까지 기다린다.
            EnterWaiting();
        }

        /// <summary>연출을 끝 모습으로 즉시 맞춘다(건너뛸 때).</summary>
        private void ShowAll()
        {
            if (_titleGroup != null) _titleGroup.alpha = 1f;
            _titleLift = 0f;
            WakeWorld();
            for (int i = 0; i < _lights.Count; i++)
                if (_lights[i] != null) _lights[i].intensity = _lightHome[i];
            RenderSettings.ambientLight = _ambientHome;
            if (_cam != null) _cam.rotation = _camHome;
        }

        /// <summary>제목을 띄운 채 사람의 손을 기다린다.</summary>
        private void EnterWaiting()
        {
            if (_waiting) return;
            _waiting = true;

            if (_hintText != null)
                _hintText.text = SaveSystem.HasSave && !string.IsNullOrEmpty(_startPromptFresh)
                               ? _startPromptFresh : _startPrompt;

            _guard = MenuGuard;
            MakePressTarget();
        }

        /// <summary>제목을 걷고 어명을 연다.</summary>
        private IEnumerator CloseTitle()
        {
            _waiting = false;
            if (_pressTarget != null) Destroy(_pressTarget);
            yield return Ramp(_titleOut, delegate (float k) { _titleGroup.alpha = 1f - k; });
            Finish();
        }

        /// <summary>
        /// 눈앞에 보이지 않는 판을 하나 둔다. 헤드셋에는 키보드가 없으므로
        /// 컨트롤러 레이로 누를 것이 있어야 한다 — 이 게임의 다른 모든 것과 같은 길이다.
        /// </summary>
        private void MakePressTarget()
        {
            if (!_makePressTarget || _pressTarget != null || _cam == null) return;

            _pressTarget = new GameObject("표제_누름판");
            _pressTarget.transform.SetParent(_cam, false);
            _pressTarget.transform.localPosition = new Vector3(0f, 0f, _pressTargetDistance);
            _pressTarget.transform.localRotation = Quaternion.identity;

            var col = _pressTarget.AddComponent<BoxCollider>();
            col.size = new Vector3(_pressTargetSize, _pressTargetSize, 0.02f);

            _pressTarget.AddComponent<TitlePress>().Bind(this);
        }

        private IEnumerator Ramp(float seconds, System.Action<float> step)
        {
            float dur = Mathf.Max(0.01f, seconds);
            float t = 0f;
            while (t < 1f && !_done)
            {
                t += Time.deltaTime / dur;
                step(Mathf.Clamp01(t));
                yield return null;
            }
            if (!_done) step(1f);
        }

        /// <summary>어전의 불을 켜되 세기를 0에서 시작시킨다 — 밝아 오르게 하려면 필요하다.</summary>
        private void WakeWorld()
        {
            _lights.Clear();
            _lightHome.Clear();
            if (_hideUntilStart == null) return;

            foreach (var go in _hideUntilStart)
            {
                if (go == null) continue;
                go.SetActive(true);
                foreach (var l in go.GetComponentsInChildren<Light>(true))
                {
                    _lights.Add(l);
                    _lightHome.Add(l.intensity);
                    l.intensity = 0f;
                }
            }
        }

        /// <summary>
        /// 표제를 끝내고 어명을 연다. 건너뛸 때도 여기로 온다.
        ///
        /// 여기로 오는 것은 <b>처음부터 하는 사람</b>이다 — 이어하기는 꾹 눌러
        /// <see cref="ContinueSaved"/> 로 빠진다. 그래서 지금 판을 비우고 시작한다.
        /// 같은 실행 안에서 복명까지 보고 표제로 돌아왔을 때, 지난 판의 단서가
        /// 그대로 남아 있으면 새 판의 수첩이 처음부터 차 있다.
        /// </summary>
        private void Finish()
        {
            if (_done) return;
            _done = true;
            _waiting = false;
            Autosave.BeginNewGame();
            if (_pressTarget != null) Destroy(_pressTarget);
            StopAllCoroutines();

            WakeWorld();
            for (int i = 0; i < _lights.Count; i++)
                if (_lights[i] != null) _lights[i].intensity = _lightHome[i];
            RenderSettings.ambientLight = _ambientHome;
            if (_cam != null) _cam.rotation = _camHome;

            if (_titleGo != null) Destroy(_titleGo);

            if (_intro != null) _intro.enabled = true;
            else Debug.LogWarning("[TitleGate] IntroController 가 연결되지 않아 어명이 시작되지 않습니다.", this);
        }

        /// <summary>하던 데부터. 어명은 이미 들은 사람이라 건너뛴다.</summary>
        private void ContinueSaved()
        {
            if (_done) return;
            _done = true;
            StopAllCoroutines();

            if (!SaveSystem.Load())
            {
                Debug.LogWarning("[TitleGate] 불러오기에 실패해 처음부터 시작합니다.", this);
                _done = false;
                Finish();
                return;
            }

            RenderSettings.ambientLight = _ambientHome;
            if (_cam != null) _cam.rotation = _camHome;
            if (_titleGo != null) Destroy(_titleGo);

            if (!string.IsNullOrEmpty(_hubSceneName) && Application.CanStreamedLevelBeLoaded(_hubSceneName))
                ScreenFade.Blink(0.5f, 0.6f, delegate { SceneManager.LoadScene(_hubSceneName); });
            else
                Debug.LogWarning($"[TitleGate] 조사청 씬('{_hubSceneName}')을 찾을 수 없습니다.", this);
        }

        private bool KeyHeld()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            return kb != null && kb.anyKey.isPressed;
#else
            return false;
#endif
        }

        /// <summary>기다리는 자리에서 "그래" 하는 키 — 스페이스와 엔터.</summary>
        private bool ConfirmKey()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return false;
            return kb.spaceKey.isPressed || kb.enterKey.isPressed || kb.numpadEnterKey.isPressed;
#else
            return false;
#endif
        }

        /// <summary>이 시간(초) 동안은 키를 안 듣는다. 방금 누른 키가 다음 걸음까지 밀지 않게.</summary>
        private const float MenuGuard = 0.45f;
        private float _guard;

        private bool MouseHeld()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            return UiGuard.AnywhereHeld;   // 단추 위에서는 안 센다(연출이 같이 건너뛰어진다)
#else
            return false;
#endif
        }

        // ── 제목 글씨 ──────────────────────────────────

        private void BuildTitle()
        {
            var cam = Camera.main;
            _titleGo = new GameObject("표제_글씨", typeof(Canvas), typeof(CanvasGroup));
            var canvas = _titleGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            _titleGroup = _titleGo.GetComponent<CanvasGroup>();
            _titleGroup.alpha = 0f;

            var rt = canvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(900f, 520f);
            rt.localScale = Vector3.one * _titleScale;

            _titleLift = _titleDrop;

            if (!_titleFollowsView && _titleAnchor != null)
            {
                rt.position = _titleAnchor.position;
                rt.rotation = _titleAnchor.rotation;
            }
            else if (cam != null)
            {
                // 자리는 LateUpdate 가 매 프레임 시선에 맞춰 잡는다. 여기서는 첫 프레임에
                // 엉뚱한 데서 튀어나오지 않게 한 번 미리 놓아 둘 뿐이다.
                var t = cam.transform;
                rt.position = t.position + t.forward * _titleDistance + t.up * (_titleHeight + _titleLift);
                rt.rotation = Quaternion.LookRotation(rt.position - t.position, t.up);
            }

            var font = UiFont.Resolve(_font);
            UiFont.Publish(_font);

            bool byImage = _titleImage != null;
            if (byImage) MakeTitleImage(rt);
            else MakeText(rt, Spaced(_title), font, 210, _titleColor, new Vector2(0f, 62f));

            if (!byImage || _keepSubtitleWithImage)
            {
                var rule = new GameObject("가는줄", typeof(Image));
                var rrt = rule.GetComponent<RectTransform>();
                rrt.SetParent(rt, false);
                rrt.anchoredPosition = new Vector2(0f, -68f);
                rrt.sizeDelta = new Vector2(300f, 3f);
                rule.GetComponent<Image>().color = new Color(_subtitleColor.r, _subtitleColor.g, _subtitleColor.b, 0.55f);

                MakeText(rt, Spaced(_subtitle), font, 78, _subtitleColor, new Vector2(0f, -132f));
            }

            // 연출이 흐르는 동안에는 <b>건너뛰기</b> 한 줄만. 이어하기는 이 자리에
            // 적지 않는다 — 그것은 연출이 끝난 뒤 오른쪽 위에 단추로 나타난다.
            // 안내는 <b>로고 밑을 따라간다</b>. 못 박아 두면 로고를 키우는 순간
            // 글씨가 그림 안으로 들어가 파묻힌다 — 실제로 680 을 1180 으로 키우니
            // 그렇게 됐다. 자리를 값에서 뽑으면 다시 맞출 일이 없다.
            float hintY = -225f;
            if (byImage)
            {
                float imgH = _titleImageWidth * (_titleImage.height / Mathf.Max(1f, (float)_titleImage.width));
                hintY = _titleImageOffset.y - imgH * 0.5f - 90f;
            }
            _hintText = MakeText(rt, _skipHint, font, 34,
                                 new Color(_titleColor.r, _titleColor.g, _titleColor.b, 0.45f), new Vector2(0f, hintY));
        }

        /// <summary>
        /// 표제 그림을 얹는다.
        ///
        /// 글씨가 아니라 <b>그림</b>인 까닭: 붓으로 쓴 제자는 획의 갈라짐과 번짐이 곧
        /// 그 글씨다. 글꼴로는 그 자리에 닿지 못한다.
        ///
        /// 세로 크기는 재지 않고 <b>그림의 비례에서 따온다</b>. 로고를 다른 것으로 갈아도
        /// 숫자를 다시 맞출 일이 없다. 글씨와 달리 그림자는 얹지 않는다 — 이미 획 둘레에
        /// 제 번짐을 갖고 있는 그림에 판때기 그림자를 더하면 오려 붙인 것처럼 보인다.
        /// </summary>
        private void MakeTitleImage(RectTransform parent)
        {
            var go = new GameObject("표제_제자", typeof(RawImage));
            var img = go.GetComponent<RawImage>();
            img.texture = _titleImage;
            img.color = _titleColor;
            img.raycastTarget = false;

            float ar = _titleImage.height / Mathf.Max(1f, (float)_titleImage.width);
            var irt = go.GetComponent<RectTransform>();
            irt.SetParent(parent, false);
            irt.anchoredPosition = _titleImageOffset;
            irt.sizeDelta = new Vector2(_titleImageWidth, _titleImageWidth * ar);
        }

        /// <summary>글자 사이를 한 칸씩 벌린다. 낡은 UI 글자에는 자간이 없다.</summary>
        private static string Spaced(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length < 2) return s;
            var sb = new System.Text.StringBuilder(s.Length * 2);
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(s[i]);
            }
            return sb.ToString();
        }

        private Text MakeText(RectTransform parent, string s, Font font, int size, Color color, Vector2 offset)
        {
            var go = new GameObject(s, typeof(Text));
            var t = go.GetComponent<Text>();
            t.text = s;
            t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;

            // 어둠 위에 얹는 글씨라 획이 흐려 보인다. 뒤에 그림자를 한 겹 깔면 또렷해진다.
            var sh = go.AddComponent<Shadow>();
            sh.effectColor = UiLook.With(UiLook.Shadow, 0.75f);
            sh.effectDistance = new Vector2(3f, -3f);

            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchoredPosition = offset;
            rt.sizeDelta = new Vector2(880f, size + 40f);
            return t;
        }
    }

    /// <summary>
    /// 표제에서 눈앞에 두는 보이지 않는 누름판. 헤드셋에는 키보드가 없으니
    /// 컨트롤러 레이가 집을 것이 하나는 있어야 한다.
    /// <see cref="TitleGate"/> 가 코드로 붙이므로 인스펙터에서 다룰 일은 없다.
    /// </summary>
    public class TitlePress : MonoBehaviour, ISelectable
    {
        private TitleGate _gate;
        public void Bind(TitleGate gate) => _gate = gate;

        public void OnHoverEnter() { }
        public void OnHoverExit() { }
        public void OnSelect() { if (_gate != null) _gate.Advance(); }
    }
}
