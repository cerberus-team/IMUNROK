using System;
using System.Collections;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 시야를 검게 덮었다가 걷는 연출(페이드). 순간이동·장면 전환에 쓴다.
    ///
    /// 왜 필요한가: 플레이어를 갑자기 다른 자리로 옮기면 눈은 움직였다고 하는데
    /// 세상이 툭 갈린다. 옮기는 순간을 어둠으로 덮으면 그 끊김이 사라진다.
    /// (눈을 감았다 뜨는 식 — 'blink teleport')
    ///
    /// 화면 전체를 덮는 UI가 아니라 카메라 코앞에 검은 판을 두는 방식이다.
    /// 스크린 오버레이는 세상 속 판과 켜가 어긋나기 때문.
    ///
    /// 쓰는 법 — 씬에 미리 둘 필요 없다:
    ///   ScreenFade.Blink(0.25f, 0.35f, () => { 옮기는_처리(); });
    ///   ScreenFade.To(1f, 0.3f);   // 그냥 어둡게
    /// </summary>
    public class ScreenFade : MonoBehaviour
    {
        private static ScreenFade _instance;

        private Material _mat;
        private Transform _quad;
        private Camera _cam;
        private float _alpha;
        private Coroutine _running;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>덮는 빛깔. 기본은 검정이고, 안개 경계만 잿빛으로 바꿔 쓴다.</summary>
        private Color _tint = Color.black;

        public static ScreenFade Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<ScreenFade>();
                    if (_instance == null)
                        _instance = new GameObject("_화면페이드").AddComponent<ScreenFade>();
                }
                return _instance;
            }
        }

        /// <summary>지금 어두워지는 중이거나 어두운 상태인가.</summary>
        public static bool IsFading => _instance != null && _instance._alpha > 0.001f;

        /// <summary>
        /// 어둡게 → (중간에 onBlack 실행) → 밝게. 순간이동은 onBlack 안에서 하면 된다.
        /// 완전히 어두운 순간에 옮기므로 플레이어는 이동 자체를 보지 못한다.
        /// </summary>
        public static void Blink(float outDuration, float inDuration, Action onBlack)
        {
            Instance._tint = Color.black;
            Instance.StartBlink(outDuration, inDuration, onBlack);
        }

        /// <summary>
        /// 검정 말고 <b>다른 빛깔로</b> 덮었다 걷는다.
        ///
        /// 안개에 삼켜져 돌아 나오는 자리에 쓴다. 거기서 검게 꺼지면 기절한 것이 되고,
        /// 기절은 이 게임이 하는 말이 아니다 — 흐린 잿빛으로 덮이면 <b>안개에 묻혔다</b>가
        /// 된다. 같은 순간이동인데 뜻이 달라진다.
        /// 다음 번 <see cref="Blink(float,float,Action)"/> 는 도로 검정으로 돌아간다.
        /// </summary>
        public static void Blink(float outDuration, float inDuration, Action onBlack, Color tint)
        {
            Instance._tint = tint;
            Instance.StartBlink(outDuration, inDuration, onBlack);
        }

        /// <summary>목표 어둡기(0=밝음, 1=완전 검정)로 서서히 바꾼다.</summary>
        public static void To(float target, float duration) => Instance.StartTo(target, duration);

        /// <summary>
        /// <b>검은 화면에서 씬이 다 읽히기를 기다렸다가 들여보내고, 눈을 뜬다.</b>
        ///
        /// <c>SceneManager.LoadScene</c> 은 동기라 부르는 순간 화면이 그 자리에서 굳는다.
        /// 재 보니 에디터에서 서천 1.72초 · 옹고집 1.15초였다 — 덮으러 다가오던 것이
        /// <b>얼굴 앞에서 얼어붙는다</b>. 연출이 끝나는 바로 그 순간에 멎기 때문이다.
        ///
        /// 그래서 미리(<c>LoadSceneAsync</c> + <c>allowSceneActivation = false</c>) 읽어
        /// 두고 이것을 부른다. 읽는 일은 덮는 연출 뒤에서 돌고, 다 읽혔을 때 들여보낸다.
        /// 아직 덜 읽혔으면 <b>검은 화면에서</b> 기다린다 — 기다림은 어둠 속이라야 한다.
        ///
        /// 이 판은 씬을 넘어 살아남으므로(DontDestroyOnLoad) 새 씬에서 눈뜨는 일까지
        /// 여기서 마칠 수 있다. 부르는 쪽은 씬과 함께 사라지니 거기 맡길 수 없다.
        /// </summary>
        public static void EnterWhenReady(AsyncOperation op, float openSeconds = 0.9f, float blackHold = 0.4f)
        {
            Instance.StartCoroutine(Instance.EnterRoutine(op, openSeconds, blackHold));
        }

        private IEnumerator EnterRoutine(AsyncOperation op, float openSeconds, float blackHold)
        {
            // <b>먼저 다 감겨야 한다.</b> 아직 뜨고 있는 눈으로 씬을 갈아 끼우면
            // 옮겨 간 것이 아니라 화면이 튄 것이 된다. 하염없이 기다리지는 않는다 —
            // 부르는 쪽이 어둡게 걸지 않았을 수도 있다.
            float waitBlack = 0f;
            while (_alpha < 0.999f && waitBlack < 3f) { waitBlack += Time.unscaledDeltaTime; yield return null; }

            // 0.9 에서 멎는다 — 들여보내라고 하기 전까지 유니티가 더 올리지 않는다.
            while (op != null && !op.isDone && op.progress < 0.9f) yield return null;
            if (op != null) op.allowSceneActivation = true;

            // 새 씬이 첫 칸을 돌 때까지 어둠을 붙들고 있는다. 한 칸으로 모자랄 때가
            // 있어 둘을 센다 — 눈뜬 첫 그림에 아직 안 선 것이 비치면 그게 더 눈에 띈다.
            yield return null;
            yield return null;

            // <b>검은 채로 한 박자 둔다.</b> 다 읽히자마자 곧바로 눈을 뜨면 어두워진 것이
            // <b>깜빡임</b>으로 지나가 버려서, 옮겨 간 것이 아니라 화면이 튄 것이 된다.
            // 눈을 감았다는 것이 한 번은 느껴져야 뜨는 것도 느껴진다.
            float held = 0f;
            while (held < blackHold) { held += Time.unscaledDeltaTime; yield return null; }

            StartTo(0f, openSeconds);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            Build();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            if (_mat != null) Destroy(_mat);
        }

        private void Build()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            _mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            // 반투명으로 세팅 — 알파가 0일 때는 아무것도 안 가려야 한다
            _mat.SetFloat("_Surface", 1f);
            _mat.SetFloat("_Blend", 0f);
            _mat.SetFloat("_ZWrite", 0f);
            _mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _mat.renderQueue = 4000;   // 무엇보다 마지막에 그려 확실히 덮는다
            SetAlpha(0f);

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "페이드판";
            Destroy(go.GetComponent<Collider>());   // 레이·클릭을 가로막으면 안 된다
            go.GetComponent<MeshRenderer>().sharedMaterial = _mat;
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _quad = go.transform;
            _quad.SetParent(transform, false);
            go.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_quad == null || !_quad.gameObject.activeSelf) return;
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            // 카메라 코앞에 붙여 시야를 통째로 덮는다. 넉넉히 키워 가장자리까지 남김없이 가린다.
            float d = Mathf.Max(_cam.nearClipPlane * 2f, 0.05f);
            _quad.SetPositionAndRotation(_cam.transform.position + _cam.transform.forward * d,
                                         _cam.transform.rotation);
            float h = 2f * d * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 4f;
            _quad.localScale = new Vector3(h * Mathf.Max(1f, _cam.aspect), h, 1f);
        }

        private void SetAlpha(float a)
        {
            _alpha = Mathf.Clamp01(a);
            if (_mat != null) _mat.SetColor(BaseColorId, new Color(_tint.r, _tint.g, _tint.b, _alpha));
            if (_quad != null) _quad.gameObject.SetActive(_alpha > 0.001f);
        }

        private void StartTo(float target, float duration)
        {
            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(FadeRoutine(target, duration));
        }

        private void StartBlink(float outDuration, float inDuration, Action onBlack)
        {
            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(BlinkRoutine(outDuration, inDuration, onBlack));
        }

        private IEnumerator FadeRoutine(float target, float duration)
        {
            float from = _alpha, t = 0f;
            if (duration <= 0f) { SetAlpha(target); yield break; }
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;   // 시간이 멈춰도 페이드는 돌아야 한다
                SetAlpha(Mathf.Lerp(from, target, t / duration));
                yield return null;
            }
            SetAlpha(target);
        }

        private IEnumerator BlinkRoutine(float outDuration, float inDuration, Action onBlack)
        {
            yield return FadeRoutine(1f, outDuration);
            try { onBlack?.Invoke(); }
            catch (Exception e) { Debug.LogError($"[ScreenFade] 어두운 동안 실행한 처리에서 오류: {e}"); }
            yield return null;                 // 한 프레임 두어 옮긴 자리가 확실히 반영되게
            yield return FadeRoutine(0f, inDuration);
            _running = null;
        }
    }
}
