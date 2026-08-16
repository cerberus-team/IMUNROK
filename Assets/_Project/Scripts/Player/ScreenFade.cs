using System;
using System.Collections;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 시야를 검게 덮었다가 걷는 연출(페이드). 순간이동·장면 전환에 쓴다.
    ///
    /// VR에서 왜 필요한가: 플레이어를 갑자기 다른 자리로 옮기면 눈은 움직였다고 하는데
    /// 몸은 가만히 있어서 멀미가 난다. 옮기는 순간을 어둠으로 덮으면 그 충돌이 사라진다.
    /// (이것이 VR 순간이동의 표준 방식 — 'blink teleport')
    ///
    /// 화면 전체를 덮는 UI가 아니라 카메라 코앞에 검은 판을 두는 방식이다.
    /// 스크린 오버레이는 헤드셋에 렌더링되지 않기 때문.
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
            => Instance.StartBlink(outDuration, inDuration, onBlack);

        /// <summary>목표 어둡기(0=밝음, 1=완전 검정)로 서서히 바꾼다.</summary>
        public static void To(float target, float duration) => Instance.StartTo(target, duration);

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

            // 카메라 코앞에 붙여 시야를 통째로 덮는다. 넉넉히 키워 VR의 넓은 시야각도 남김없이 가린다.
            float d = Mathf.Max(_cam.nearClipPlane * 2f, 0.05f);
            _quad.SetPositionAndRotation(_cam.transform.position + _cam.transform.forward * d,
                                         _cam.transform.rotation);
            float h = 2f * d * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 4f;
            _quad.localScale = new Vector3(h * Mathf.Max(1f, _cam.aspect), h, 1f);
        }

        private void SetAlpha(float a)
        {
            _alpha = Mathf.Clamp01(a);
            if (_mat != null) _mat.SetColor(BaseColorId, new Color(0f, 0f, 0f, _alpha));
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
