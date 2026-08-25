using System.Collections;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>안개 경계</b> — 조사청을 둘러싼 세상의 끝.
    ///
    /// 조사청 바깥은 들판이고 들판 끝은 안개다. 안개 너머에는 아무것도 없다 —
    /// 판때기가 끝나고 하늘과 맞닿는 곧은 금이 나온다. 그것을 못 보게 막는 방법은
    /// 둘뿐이다. <b>담을 세우거나, 돌려보내거나.</b>
    ///
    /// 담은 안 된다. 문을 열고 나가 하늘을 보라고 만든 마당인데 스무 걸음 만에
    /// 보이지 않는 벽에 코를 박으면, 넓은 들판이 아니라 좁은 우리가 된다.
    /// 그래서 <b>돌려보낸다</b>. 안개 속으로 걸어 들어가면 잿빛에 잠겼다가,
    /// 눈을 뜨면 조사청이 <b>앞에</b> 있다. 막힌 것이 아니라 <b>홀린 것</b>이다 —
    /// 안개에 홀려 제자리로 돌아 나오는 것은 조선 이야기가 늘 하던 말이다.
    ///
    /// 세 켜로 일한다.
    ///   ① <see cref="_soft"/> 밖 — 안개가 <b>조여 온다</b>. 유니티 안개의 시작·끝
    ///      거리를 좁혀, 멀어질수록 눈앞이 흐려진다. 말 없이도 "여기가 끝이다"가 된다.
    ///   ② <see cref="_hard"/> 밖 — 잿빛으로 덮었다가 <see cref="_returnTo"/> m 자리에
    ///      다시 세우고 <b>조사청 쪽으로 돌려 세운다</b>.
    ///   ③ 돌아온 뒤 <see cref="_grace"/> 초 동안은 다시 잡지 않는다. 안 그러면
    ///      경계에 걸친 채로 페이드가 겹쳐 화면이 껌뻑인다.
    ///
    /// 안개 값은 이 부품이 <b>매 프레임 쓰므로</b>, 꺼질 때 원래 값으로 돌려놓는다.
    /// 플레이를 멈춘 뒤 씬의 안개가 2m 로 남아 있으면 안 되기 때문이다.
    ///
    /// 붙이는 곳: 플레이어(= 카메라 뿌리). [이문록 ▸ 조사청 ▸ 하늘과 안개] 가 달아 준다.
    /// </summary>
    public class FogBoundary : MonoBehaviour
    {
        [Header("가운데")]
        [Tooltip("이 자리를 가운데로 잡는다(월드). 조사청 자리를 넣는다")]
        [SerializeField] private Vector3 _center = new Vector3(70.13f, 137.73f, 283.33f);

        [Header("경계(가운데에서 잰 수평 거리, m)")]
        [Tooltip("여기서부터 안개가 조여 오기 시작한다")]
        [SerializeField] private float _soft = 34f;
        [Tooltip("여기를 넘으면 돌려보낸다")]
        [SerializeField] private float _hard = 44f;
        [Tooltip("돌려보낼 자리. 안개 안쪽으로 넉넉히 들어온 곳이어야 한다")]
        [SerializeField] private float _returnTo = 30f;

        [Header("안개")]
        [Tooltip("평소 안개 — 시작·끝 거리(m)")]
        [SerializeField] private Vector2 _calm = new Vector2(19f, 48f);
        [Tooltip("경계에 다다랐을 때 안개 — 시작·끝 거리(m)")]
        [SerializeField] private Vector2 _thick = new Vector2(1.5f, 13f);

        [Header("돌려보내기")]
        [SerializeField] private float _fadeOut = 0.55f;
        [SerializeField] private float _fadeIn = 1.1f;
        [Tooltip("덮는 빛깔. 검정이 아니라 안개빛이라야 홀린 것이 된다")]
        [SerializeField] private Color _veil = new Color(0.62f, 0.66f, 0.73f);
        [Tooltip("돌아온 뒤 이만큼은 다시 안 잡는다(초)")]
        [SerializeField] private float _grace = 1.2f;

        private bool _busy;
        private float _until;
        private Vector2 _saved;
        private bool _savedOk;

        private void OnEnable()
        {
            _saved = new Vector2(RenderSettings.fogStartDistance, RenderSettings.fogEndDistance);
            _savedOk = true;
        }

        private void OnDisable()
        {
            // 씬에 남기지 않는다. 이 부품이 만진 값은 이 부품이 치운다.
            if (!_savedOk) return;
            RenderSettings.fogStartDistance = _saved.x;
            RenderSettings.fogEndDistance = _saved.y;
        }

        private void Update()
        {
            float d = Flat(transform.position - _center).magnitude;

            // ① 안개가 조여 온다 — 나갈수록 눈앞이 좁아진다
            float t = Mathf.InverseLerp(_soft, _hard, d);
            t = t * t;                                   // 초반엔 천천히, 끝에서 급하게
            RenderSettings.fogStartDistance = Mathf.Lerp(_calm.x, _thick.x, t);
            RenderSettings.fogEndDistance = Mathf.Lerp(_calm.y, _thick.y, t);

            // ② 넘으면 돌려보낸다
            if (_busy || Time.time < _until || d < _hard) return;
            StartCoroutine(TurnBack());
        }

        private IEnumerator TurnBack()
        {
            _busy = true;

            ScreenFade.Blink(_fadeOut, _fadeIn, Place, _veil);

            // 페이드가 다 끝날 때까지 기다렸다가 다시 지켜본다. 어두운 동안 또
            // 잡히면 페이드가 겹쳐 화면이 껌뻑인다.
            yield return new WaitForSecondsRealtime(_fadeOut + _fadeIn);
            _until = Time.time + _grace;
            _busy = false;
        }

        /// <summary>안개에 잠긴 순간에 부른다 — 자리를 옮기고 조사청 쪽으로 돌려 세운다.</summary>
        private void Place()
        {
            Vector3 away = Flat(transform.position - _center);
            if (away.sqrMagnitude < 0.01f) away = Vector3.forward;
            away.Normalize();

            // 나갔던 쪽 그대로, 거리만 당겨 세운다. 엉뚱한 데로 옮기면 길을 잃는다 —
            // 돌아왔다는 느낌이지 순간이동한 느낌이 아니어야 한다.
            Vector3 p = _center + away * _returnTo;
            p.y = transform.position.y;
            transform.position = p;

            // 조사청을 마주 본다. 이것이 이 장치의 요지다 — 눈을 뜨면 집이 앞에 있다.
            Vector3 look = Flat(_center - p);
            if (look.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);

            // 시점을 제가 들고 있는 부품에게 "바깥에서 돌려놨다"고 알린다.
            // 이 말을 안 하면 마우스를 누르는 순간 옛 각도로 홱 돌아간다.
            var fly = GetComponentInChildren<DebugFlyCamera>(true);
            if (fly != null) fly.SyncAngles();
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        /// <summary>
        /// 도구가 세워 줄 때 한 번에 맞춘다.
        ///
        /// 경계 거리와 안개 값은 <b>고리를 세우는 쪽</b>이 정해야 한다. 여기에 따로
        /// 적어 두면 고리 반지름을 고칠 때마다 두 군데를 고쳐야 하고, 한 번 잊으면
        /// 안개 벽 밖에서 붙잡히거나 벽에 닿기도 전에 되돌려진다.
        /// </summary>
        public void Configure(Vector3 center, float soft, float hard, float returnTo, Vector2 calm)
        {
            _center = center;
            _soft = soft;
            _hard = hard;
            _returnTo = returnTo;
            _calm = calm;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            var c = _center;
            Gizmos.color = new Color(0.5f, 0.8f, 1f, 0.6f); Ring(c, _returnTo);
            Gizmos.color = new Color(1f, 0.9f, 0.4f, 0.6f); Ring(c, _soft);
            Gizmos.color = new Color(1f, 0.35f, 0.3f, 0.8f); Ring(c, _hard);
        }

        private static void Ring(Vector3 c, float r)
        {
            Vector3 prev = c + new Vector3(r, 0f, 0f);
            for (int i = 1; i <= 48; i++)
            {
                float a = i / 48f * Mathf.PI * 2f;
                Vector3 p = c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }
#endif
    }
}
