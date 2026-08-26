using UnityEngine;

namespace IMUNROK.Seocheon
{
    /// <summary>
    /// 촬영용 카메라 무빙 — <b>위치와 시선을 따로 움직인다</b> (2026-08-27).
    ///
    /// ■ 왜 따로 움직이나  ★이 파일의 핵심
    ///   「전진 → 멈춤 → 고개 들기」를 한 줄로 이으면 이음매에서 <b>화면이 멈춘 것처럼 보인다</b>.
    ///   위치가 다 멎은 뒤에 회전이 시작되기 때문이다.
    ///   그래서 위치와 회전에 <b>각자의 시작 시각과 길이</b>를 준다.
    ///   회전을 위치가 멎기 <b>전에</b> 시작하면 (<see cref="lookStart"/> &lt; <see cref="moveTime"/>)
    ///   둘이 겹치면서 이음매가 사라진다 — 다가가는 도중에 이미 고개가 들리기 시작한다.
    ///
    /// ■ 기본 구성
    ///   위치 : 꽃밭 → 나무 앞  (부드럽게 출발해 부드럽게 멎는다)
    ///   시선 : 나무 → 달        (위치가 멎기 조금 전에 시작해 더 오래 이어진다)
    ///   ⇒ 끝부분은 <b>제자리에서 시선만 올라간다</b>.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public sealed class SeocheonRecCamMove : MonoBehaviour
    {
        [Header("위치 — 어디서 어디로")]
        [SerializeField] private Vector3 fromPos;
        [SerializeField] private Vector3 toPos;
        [Tooltip("움직이기 시작하는 시각(초)")]
        [SerializeField] private float moveStart = 0.6f;
        [Tooltip("움직이는 데 걸리는 시간(초)")]
        [SerializeField] private float moveTime = 6f;

        [Header("시선 — 무엇을 보다가 무엇으로")]
        [SerializeField] private Vector3 fromRot;
        [SerializeField] private Vector3 toRot;
        [Tooltip("★위치가 멎기 전에 시작해야 이음매가 안 보인다 (moveStart+moveTime 보다 작게)")]
        [SerializeField] private float lookStart = 4.5f;
        [Tooltip("시선이 옮겨가는 시간(초). 길수록 여유롭다.")]
        [SerializeField] private float lookTime = 6f;

        [Header("시야각")]
        [SerializeField] private float fromFov = 62f;
        [SerializeField] private float toFov = 60f;

        [Header("끝난 뒤")]
        [Tooltip("마지막 그림을 보여 주는 여운(초)")]
        [SerializeField] private float hold = 2f;

        [Header("에디터 도구")]
        [SerializeField] private bool captureFrom;
        [SerializeField] private bool captureTo;
        [SerializeField] private bool previewFrom;
        [SerializeField] private bool previewTo;

        /// <summary>연출 전체 길이 — Recorder 의 End 에 넣을 값.</summary>
        public float TotalTime
        {
            get { return Mathf.Max(moveStart + moveTime, lookStart + lookTime) + hold; }
        }

        private Camera cam;
        private float t0;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            ApplyAt(0f);
            t0 = Time.time;
        }

        // ★LateUpdate — 다른 것이 카메라를 만져도 이쪽이 마지막에 덮어쓴다.
        private void LateUpdate() { ApplyAt(Time.time - t0); }

        private void ApplyAt(float t)
        {
            float kPos = Seg(t, moveStart, moveTime);
            float kRot = Seg(t, lookStart, lookTime);

            transform.position = Vector3.Lerp(fromPos, toPos, Smooth(kPos));
            transform.rotation = Quaternion.Slerp(Quaternion.Euler(fromRot), Quaternion.Euler(toRot), Smooth(kRot));
            if (cam != null) cam.fieldOfView = Mathf.Lerp(fromFov, toFov, Smooth(kRot));
        }

        /// <summary>구간 안에서의 진행도 0~1.</summary>
        private static float Seg(float t, float start, float len)
        {
            if (len <= 0f) return t >= start ? 1f : 0f;
            return Mathf.Clamp01((t - start) / len);
        }

        /// <summary>양끝을 완만하게 — 출발과 도착이 모두 부드럽다.</summary>
        private static float Smooth(float k) { return k * k * (3f - 2f * k); }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            var c = GetComponent<Camera>();
            if (captureFrom) { captureFrom = false; fromPos = transform.position; fromRot = transform.eulerAngles; if (c) fromFov = c.fieldOfView; }
            if (captureTo)   { captureTo   = false; toPos   = transform.position; toRot   = transform.eulerAngles; if (c) toFov   = c.fieldOfView; }
            if (previewFrom) { previewFrom = false; Put(fromPos, fromRot, fromFov, c); }
            if (previewTo)   { previewTo   = false; Put(toPos,   toRot,   toFov,   c); }
        }

        private void Put(Vector3 p, Vector3 r, float f, Camera c)
        {
            transform.position = p; transform.rotation = Quaternion.Euler(r);
            if (c != null) c.fieldOfView = f;
        }
#endif
    }
}
