using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>마지막은 조사청 문을 나서며 끝난다</b> — 손을 놓게 하는 엔딩.
    ///
    /// 여태 엔딩은 따로 된 씬(어전)이었고, 거기에 가는 길이 놓여 있지 않았다. 장계를 봉하면
    /// 관아 교의에 앉은 채로 멎었다. 이제 장계를 봉하면 조사청으로 돌아오고, 거기서
    /// <b>조작을 거둔 채</b> 걸어 나간다:
    ///
    ///   ① 방에서 눈을 뜬다. 조사청 노래가 잦아들고 엔딩 노래가 배어 나온다.
    ///   ② 어명이 들린다 — 문이 열리고, 어사는 제 발로(그러나 손은 놓고) 문을 나서 댓돌을 내려간다.
    ///   ③ 마당에 서서 <b>천천히 고개를 든다</b>. 마지막 물음이 하늘 아래에 남는다.
    ///   ④ 하늘을 보는 채로 크레딧이 <b>영화처럼 아래에서 위로</b> 올라간다.
    ///   ⑤ 어두워지고 표제로 돌아간다.
    ///
    /// <b>왜 손을 거두나.</b> 여기서 둘러보게 두면 사람은 바둑판이며 서가로 간다 — 끝을 맺는 자리가
    /// 아니라 또 하나의 방이 된다. 길은 정해 두되 걸음은 사람 걸음으로 느리게, 고개는 걷는 쪽을
    /// 따라 돌게 해서 끌려가는 것이 아니라 <b>나서는</b> 것으로 보이게 한다.
    ///
    /// 크레딧의 글(맺음말·만든 사람·쓰인 것들)은 어전 엔딩(<see cref="EndingController"/>)에 적어 둔
    /// 것을 그대로 옮겨 왔다.
    ///
    /// 붙이는 곳: 조사청 씬에 빈 오브젝트 하나. 길(<see cref="_path"/>)은 그 자식 표식들.
    /// </summary>
    public class HubEnding : MonoBehaviour
    {
        /// <summary>
        /// 다음에 조사청에 들어서면 엔딩을 튼다. 장계를 봉한 쪽(<see cref="ReportDesk"/>)이 세운다.
        /// 저장에 싣지 않는다 — 봉한 그 길로 곧장 오는 것이라 한 판 안에서만 산다.
        /// </summary>
        public static bool Pending { get; private set; }

        public static void Request() { Pending = true; }

        /// <summary>엔딩이 도는 중인가. 도구띠·수첩 같은 손짓이 이것을 보고 물러날 수 있다.</summary>
        public static bool Playing { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Pending = false; Playing = false; }

        [Header("길")]
        [Tooltip("눈을 뜨는 자리(조사청 방). 여기로 옮겨 놓고 시작한다")]
        [SerializeField] private Transform _start;
        [Tooltip("차례로 밟고 갈 표식. 마지막이 고개를 드는 자리다. 높이는 발밑을 재서 맞추므로 x·z 만 본다")]
        [SerializeField] private Transform[] _path;
        [Tooltip("걷기 시작하면서 여는 문짝")]
        [SerializeField] private DoorController[] _doors;
        [Tooltip("걷는 빠르기(m/초). 사람이 생각에 잠겨 걷는 빠르기")]
        [SerializeField] private float _walkSpeed = 0.8f;
        [SerializeField] private float _eyeHeight = 1.6f;
        [Tooltip("눈을 뜨고 문이 열리기까지 기다리는 시간(초)")]
        [SerializeField] private float _beforeWalk = 3.5f;

        [Header("하늘")]
        [Tooltip("고개를 드는 각(도). 음수가 위다")]
        [SerializeField] private float _lookUpPitch = -58f;
        [SerializeField] private float _lookUpSeconds = 7f;

        [Header("노래")]
        [Tooltip("<b>엔딩 노래</b>. 조사청 노래가 잦아들고 이것이 배어 나온다. 비우면 조용히 간다")]
        [SerializeField] private AudioClip _song;
        [Range(0f, 1f)] [SerializeField] private float _songVolume = 0.85f;
        [SerializeField] private float _songFadeIn = 4f;

        [Header("어명")]
        [SerializeField] private string _speaker = "어명";
        [Tooltip("한 줄이 떠 있는 시간(초)")]
        [SerializeField] private float _secondsPerLine = 5.5f;
        [TextArea(2, 3)] [SerializeField] private string _openLine = "복명(復命)을 받았다. 어사, 옹진현의 일을 보았노라.";
        [TextArea(2, 3)] [SerializeField] private string _lineTruth = "그대는 이야기를 걷어내고 사실만을 좇았구나. 냉정하나, 그것이 국법이다.";
        [TextArea(2, 3)] [SerializeField] private string _lineHalf = "그대는 사람을 바로 짚었으나, 장계의 몇 줄은 끝내 서지 못하였다. 반쯤 걷힌 이야기로다.";
        [TextArea(2, 3)] [SerializeField] private string _lineMercy = "그대는 법 앞에서도 사람의 사정을 먼저 보았다. 무르나, 어질다 하겠다.";
        [TextArea(2, 3)] [SerializeField] private string _lineFake = "그대는 설화가 덮은 것을 그대로 두었다. 편하나, 그 자리에 진실은 없다.";
        [TextArea(2, 3)] [SerializeField] private string _lineNone = "그대의 장계는 아직 닿지 않았다. 그래도 문을 나서라.";
        [TextArea(2, 3)] [SerializeField] private string _leaveLine = "이제 문을 나서라. 그대가 걷어낸 이야기들이 저 하늘 아래 아직 떠돈다.";
        [Tooltip("고개를 들 때 남는 마지막 물음")]
        [TextArea(2, 3)] [SerializeField] private string _finalLine = "그래서, 그 이야기들은 거짓이었느냐.";

        [Header("크레딧 — 아래에서 위로")]
        [SerializeField] private Texture2D _logo;
        [TextArea(2, 3)] [SerializeField] private string _thanksLine = "플레이해 주셔서 감사합니다.";
        [TextArea(3, 8)] [SerializeField] private string _creditsLine = "이문록 (異聞錄)";
        [TextArea(2, 4)] [SerializeField] private string[] _sourceLines = new string[0];
        [Tooltip("다 올라가는 데 걸리는 시간(초). Space 를 누르고 있으면 세 곱으로 빨라진다")]
        [SerializeField] private float _creditsSeconds = 42f;

        [Header("끝나고")]
        [Tooltip("다 올라가고 어두워진 뒤 갈 씬. 비우면 어둠에 머문다")]
        [SerializeField] private string _afterScene = "IntroScene";

        [Header("시험")]
        [Tooltip("켜면 장계를 안 봉해도 씬을 켜자마자 돈다 — 편집기에서 엔딩만 볼 때")]
        [SerializeField] private bool _playOnStartForTest = false;

        private Camera _cam;
        private float _yaw, _pitch;
        private AudioSource _songSource;

        private void Start()
        {
            var gs = GameState.Instance;
            if (!Pending && gs != null && gs.EndingPending) Pending = true;    // 이어하기
            if (!Pending && !_playOnStartForTest) return;
            StartCoroutine(Run());
        }

        [ContextMenu("지금 엔딩 틀기(플레이 중)")]
        private void PlayNow()
        {
            if (!Application.isPlaying || Playing) return;
            StartCoroutine(Run());
        }

        // ── 한 바퀴 ────────────────────────────────

        private IEnumerator Run()
        {
            Playing = true;
            yield return null;                       // 시작 자리에 세우는 쪽(PlayerStart)이 먼저 돈다
            Pending = false;                         // 맞이하는 쪽(EntryRise)이 Start 에서 이것을 본 뒤에 내린다
            if (GameState.Instance != null) GameState.Instance.EndingPending = false;
            _cam = Camera.main;
            if (_cam == null) { Playing = false; yield break; }

            HandsOff(true);
            ScreenFade.To(1f, 0f);

            if (_start != null)
            {
                _cam.transform.position = Grounded(_start.position);
                _cam.transform.rotation = Quaternion.Euler(0f, _start.eulerAngles.y, 0f);
            }
            _yaw = _cam.transform.eulerAngles.y;
            _pitch = 0f;
            Apply();

            SceneMusic.DuckAll(0f, 2.5f);
            StartSong();
            yield return new WaitForSeconds(0.6f);
            ScreenFade.To(0f, 2.4f);

            var lines = DecreeLines();
            StartCoroutine(Speak(lines));

            yield return new WaitForSeconds(_beforeWalk);
            if (_doors != null) foreach (var d in _doors) if (d != null) { d.Unlock(); d.Open(); }
            yield return new WaitForSeconds(1.0f);

            yield return Walk();

            // 어명이 다 들린 뒤에 고개를 든다 — 마지막 물음은 하늘을 보며 듣는다.
            while (_spoken < lines.Count) yield return null;
            yield return new WaitForSeconds(0.8f);
            SubtitleView.Show(_speaker, _finalLine, "", false);
            yield return LookUp();
            yield return new WaitForSeconds(3.5f);
            SubtitleView.Hide();

            yield return new WaitForSeconds(1.2f);
            yield return Roll();

            yield return new WaitForSeconds(1.5f);
            ScreenFade.To(1f, 3f);
            FadeSong(3.2f);
            yield return new WaitForSeconds(3.6f);

            HandsOff(false);
            Playing = false;
            if (!string.IsNullOrEmpty(_afterScene) && Application.CanStreamedLevelBeLoaded(_afterScene))
            {
                ScreenFade.To(0f, 1.2f);
                SceneManager.LoadScene(_afterScene);
            }
        }

        /// <summary>조작을 거둔다(끝나면 돌려준다). 걷기·짚기·자막 닫기가 여기서 멎는다.</summary>
        private void HandsOff(bool off)
        {
            if (_cam == null) return;
            var fly = _cam.GetComponentInParent<DebugFlyCamera>();
            if (fly == null) fly = _cam.GetComponentInChildren<DebugFlyCamera>();
            if (fly != null) { fly.enabled = !off; if (!off) fly.SyncAngles(); }
            var ray = _cam.GetComponentInParent<MouseRaySelector>();
            if (ray == null) ray = _cam.GetComponentInChildren<MouseRaySelector>();
            if (ray != null) ray.enabled = !off;
            WorldHudAnchor.StowAll = off;
            SubtitleView.Closable = !off;
        }

        private List<string> DecreeLines()
        {
            var lines = new List<string>();
            if (!string.IsNullOrEmpty(_openLine)) lines.Add(_openLine);
            var gs = GameState.Instance;
            var v = gs != null ? gs.GetVerdict(CaseId.Case1_Onggojip) : Verdict.None;
            string tag = gs != null ? gs.GetEndingTag(CaseId.Case1_Onggojip) : "";
            // 장계가 매긴 결(참·반·자비·헛)이 있으면 그것을 따른다
            string judged = tag == "참" ? _lineTruth : tag == "반" ? _lineHalf : tag == "자비" ? _lineMercy : tag == "헛" ? _lineFake
                          : v == Verdict.Truth ? _lineTruth
                          : v == Verdict.Mercy ? _lineMercy
                          : v == Verdict.AcceptFake ? _lineFake
                          : _lineNone;
            if (!string.IsNullOrEmpty(judged)) lines.Add(judged);
            if (!string.IsNullOrEmpty(_leaveLine)) lines.Add(_leaveLine);
            return lines;
        }

        private int _spoken;

        private IEnumerator Speak(List<string> lines)
        {
            _spoken = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                SubtitleView.Show(_speaker, lines[i], "", false);
                yield return new WaitForSeconds(_secondsPerLine);
                _spoken = i + 1;
            }
            SubtitleView.Hide();
        }

        // ── 걷기 ───────────────────────────────────

        private IEnumerator Walk()
        {
            if (_path == null) yield break;
            float bob = 0f;
            foreach (var mark in _path)
            {
                if (mark == null) continue;
                while (true)
                {
                    Vector3 at = _cam.transform.position;
                    Vector3 to = mark.position; to.y = at.y;
                    Vector3 flat = to - at;
                    float left = flat.magnitude;
                    if (left < 0.08f) break;

                    float step = Mathf.Min(left, _walkSpeed * Time.deltaTime);
                    Vector3 dir = flat / left;
                    Vector3 next = at + dir * step;

                    // 고개는 가는 쪽을 느리게 따라 돈다 — 홱 돌면 끌려가는 것으로 보인다.
                    float want = Quaternion.LookRotation(dir).eulerAngles.y;
                    _yaw = Mathf.MoveTowardsAngle(_yaw, want, 55f * Time.deltaTime);
                    _pitch = Mathf.MoveTowards(_pitch, Descending(next) ? 9f : 2f, 6f * Time.deltaTime);

                    bob += step;
                    Vector3 g = Grounded(next);
                    float bobY = Mathf.Sin(bob * Mathf.PI * 2f / 1.3f) * 0.018f;
                    // 턱을 내려설 때 뚝 떨어지지 않게 높이는 따라간다
                    float y = Mathf.MoveTowards(at.y, g.y, 1.4f * Time.deltaTime);
                    _cam.transform.position = new Vector3(next.x, y + bobY * 0.2f, next.z);
                    Apply();
                    yield return null;
                }
            }
            // 발을 멈추고 한 숨 — 고개가 앞을 본다
            float t = 0f;
            while (t < 1.2f)
            {
                t += Time.deltaTime;
                _pitch = Mathf.MoveTowards(_pitch, 0f, 8f * Time.deltaTime);
                Vector3 p = _cam.transform.position;
                p.y = Mathf.MoveTowards(p.y, Grounded(p).y, 1.4f * Time.deltaTime);
                _cam.transform.position = p;
                Apply();
                yield return null;
            }
        }

        private bool Descending(Vector3 p)
        {
            return Grounded(p + _cam.transform.forward * 0.6f).y < p.y - _eyeHeight * 0.1f - 0.05f;
        }

        /// <summary>발밑을 재어 눈높이를 얹는다. 지붕에 먼저 맞지 않게 눈 조금 위에서 쏜다.</summary>
        private Vector3 Grounded(Vector3 p)
        {
            Vector3 from = new Vector3(p.x, (_cam != null ? _cam.transform.position.y : p.y) + 0.5f, p.z);
            RaycastHit hit;
            if (Physics.Raycast(from, Vector3.down, out hit, 8f, ~0, QueryTriggerInteraction.Ignore))
                return new Vector3(p.x, hit.point.y + _eyeHeight, p.z);
            return p;
        }

        private IEnumerator LookUp()
        {
            float from = _pitch, t = 0f;
            while (t < _lookUpSeconds)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / _lookUpSeconds));
                _pitch = Mathf.Lerp(from, _lookUpPitch, k);
                Apply();
                yield return null;
            }
        }

        private void Apply()
        {
            _cam.transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        // ── 노래 ───────────────────────────────────

        private void StartSong()
        {
            if (_song == null) return;
            _songSource = gameObject.AddComponent<AudioSource>();
            _songSource.clip = _song;
            _songSource.loop = false;
            _songSource.spatialBlend = 0f;
            _songSource.volume = 0f;
            _songSource.Play();
            StartCoroutine(FadeTo(_songSource, _songVolume, _songFadeIn));
        }

        private void FadeSong(float seconds)
        {
            if (_songSource != null) StartCoroutine(FadeTo(_songSource, 0f, seconds));
        }

        private static IEnumerator FadeTo(AudioSource s, float target, float seconds)
        {
            float from = s.volume, t = 0f;
            while (t < seconds && s != null)
            {
                t += Time.deltaTime;
                s.volume = Mathf.Lerp(from, target, t / Mathf.Max(0.01f, seconds));
                yield return null;
            }
            if (s != null) s.volume = target;
        }

        // ── 크레딧 ─────────────────────────────────

        /// <summary>
        /// 영화의 끝처럼 <b>아래에서 위로</b> 올라간다. 하늘을 보는 채이므로 글 뒤에는 하늘이 있다 —
        /// 판을 깔지 않고 아래·위 가장자리만 옅게 어둡혀 글이 떠오르고 스러지게 한다.
        /// </summary>
        private IEnumerator Roll()
        {
            var go = new GameObject("엔딩_크레딧", typeof(Canvas), typeof(CanvasScaler));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            var group = go.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            // 가장자리를 옅게 — 글이 화면 끝에서 뚝 잘리지 않고 스며든다
            Edge(go.transform, true);
            Edge(go.transform, false);

            var column = new GameObject("글줄", typeof(RectTransform)).GetComponent<RectTransform>();
            column.SetParent(go.transform, false);
            column.anchorMin = column.anchorMax = column.pivot = new Vector2(0.5f, 0.5f);

            float y = 0f;
            const float width = 1200f;
            var ink = new Color(0.97f, 0.94f, 0.86f, 1f);

            if (_logo != null)
            {
                var ri = new GameObject("표제", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                ri.transform.SetParent(column, false);
                ri.texture = _logo;
                ri.raycastTarget = false;
                float w = 620f, h = w * _logo.height / Mathf.Max(1f, _logo.width);
                ri.rectTransform.sizeDelta = new Vector2(w, h);
                ri.rectTransform.anchoredPosition = new Vector2(0f, y - h * 0.5f);
                y -= h + 140f;
            }

            var blocks = new List<string>();
            if (!string.IsNullOrEmpty(_creditsLine)) blocks.Add(_creditsLine);
            if (_sourceLines != null) foreach (var s in _sourceLines) if (!string.IsNullOrEmpty(s)) blocks.Add(s);
            if (!string.IsNullOrEmpty(_thanksLine)) blocks.Add(_thanksLine);

            for (int i = 0; i < blocks.Count; i++)
            {
                var tmp = UiText.New("글" + i, blocks[i].Replace("\\n", "\n"), Vector2.zero, new Vector2(width, 100f),
                                     column, i == blocks.Count - 1 ? 52f : 42f, TextAnchor.UpperCenter, ink);
                tmp.textWrappingMode = TextWrappingModes.Normal;
                tmp.lineSpacing = 18f;
                tmp.raycastTarget = false;
                tmp.ForceMeshUpdate();
                float h = Mathf.Max(60f, tmp.preferredHeight);
                tmp.rectTransform.sizeDelta = new Vector2(width, h);
                tmp.rectTransform.anchoredPosition = new Vector2(0f, y - h * 0.5f);
                // 맺음말 앞은 더 띄운다 — 마지막 한 줄이 따로 남아야 한다
                y -= h + (i == blocks.Count - 2 ? 360f : 150f);
            }
            float total = -y;

            // 화면 아래 밖에서 시작해 위 밖으로 나간다. 맺음말은 한가운데에 머문다.
            float screenH = 1080f;
            float startY = -screenH * 0.5f - 40f;                 // 첫 줄 윗변이 화면 아래 끝에
            float endY = total - screenH * 0.5f + 60f;             // 끝줄이 한가운데 조금 위에
            column.anchoredPosition = new Vector2(0f, startY);

            float t = 0f;
            float fadeIn = 0f;
            while (t < _creditsSeconds)
            {
                float speed = 1f;
#if ENABLE_INPUT_SYSTEM
                var kb = Keyboard.current;
                if (kb != null && kb.spaceKey.isPressed) speed = 3f;
#endif
                t += Time.deltaTime * speed;
                fadeIn = Mathf.Min(1f, fadeIn + Time.deltaTime / 1.5f);
                group.alpha = fadeIn;
                column.anchoredPosition = new Vector2(0f, Mathf.Lerp(startY, endY, t / _creditsSeconds));
                yield return null;
            }
            yield return new WaitForSeconds(4f);

            float k = 1f;
            while (k > 0f) { k -= Time.deltaTime / 2.5f; group.alpha = Mathf.Max(0f, k); yield return null; }
            Destroy(go);
        }

        private static void Edge(Transform parent, bool top)
        {
            var img = new GameObject(top ? "위_그늘" : "아래_그늘", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            img.transform.SetParent(parent, false);
            img.raycastTarget = false;
            var tex = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int i = 0; i < 64; i++)
            {
                float a = Mathf.Pow(1f - i / 63f, 2f) * 0.55f;     // 가장자리에서 짙고 안으로 옅다
                tex.SetPixel(0, top ? 63 - i : i, new Color(0.02f, 0.03f, 0.06f, a));
            }
            tex.Apply();
            img.texture = tex;
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0f, top ? 1f : 0f);
            rt.anchorMax = new Vector2(1f, top ? 1f : 0f);
            rt.pivot = new Vector2(0.5f, top ? 1f : 0f);
            rt.sizeDelta = new Vector2(0f, 300f);
            rt.anchoredPosition = Vector2.zero;
        }
    }
}
