using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>귀</b> — 소리를 듣고 돌아본다.
    ///
    /// <see cref="NoiseMeter"/> 는 재기만 하고 누가 어떻게 반응할지는 모른다. 그것을
    /// 아는 쪽이 여기다. 사람마다 귀가 다르기 때문이다 — 늙은 하인은 어둡고, 마름은
    /// 밝고, 문밖의 개는 아주 밝다.
    ///
    /// <b>세 켜로 듣는다</b>
    ///   ① <see cref="_notice"/> 넘으면 — <b>돌아본다</b>. 아직 아무 일도 아니다.
    ///   ② <see cref="_alarm"/> 넘으면 — <b>다가온다</b>. 들킬 참이다.
    ///   ③ 벽 너머는 <see cref="_throughWalls"/> 만큼 줄여 듣는다. 벽은 소리를 막는다.
    ///
    /// 붙이는 곳: 듣는 사람(늙은하인·마름·마을사람). 반응은 이벤트로 뺀다 —
    /// 이 부품이 직접 걷게 하면 사람마다 다른 반응을 줄 수가 없다.
    /// </summary>
    public class NoiseListener : MonoBehaviour
    {
        /// <summary>지금 씬에 있는 귀들. <see cref="NoiseMeter"/> 가 훑는다.</summary>
        public static readonly List<NoiseListener> All = new List<NoiseListener>();

        [Header("귀")]
        [Tooltip("이 거리(m) 밖에서 난 소리는 아무리 커도 안 들린다")]
        [SerializeField] private float _hearRadius = 10f;
        [Tooltip("이만큼(0~1) 들리면 돌아본다")]
        [Range(0f, 1f)] [SerializeField] private float _notice = 0.25f;
        [Tooltip("이만큼(0~1) 들리면 다가온다")]
        [Range(0f, 1f)] [SerializeField] private float _alarm = 0.6f;

        [Tooltip("벽 너머로 들리는 몫(0~1). 0.35 면 벽 하나 건너면 세 곱 남짓 줄어든다. " +
                 "1이면 벽을 안 친다")]
        [Range(0f, 1f)] [SerializeField] private float _throughWalls = 0.35f;
        [Tooltip("벽으로 칠 층. 사람·소품은 빼야 한다 — 앞에 선 사람 때문에 귀가 먹으면 안 된다")]
        [SerializeField] private LayerMask _wallMask = ~0;

        [Header("다시 놀라기까지")]
        [Tooltip("한 번 반응하고 이만큼(초)은 다시 안 놀란다. 안 그러면 문 한 번에 열 번 돌아본다")]
        [SerializeField] private float _cooldown = 2.5f;

        [Header("들었을 때 — 손 안 대도 되는 기본 반응")]
        [Tooltip("소리 난 쪽으로 <b>몸을 돌린다</b>. 이벤트를 하나도 안 이어도 이것만은 한다 — " +
                 "아무 반응이 없으면 귀를 달아 놓고도 달아 놓은 줄을 모른다")]
        [SerializeField] private bool _turnToward = true;
        [Tooltip("돌아서는 빠르기(초당 도). 홱 도는 것과 천천히 도는 것은 딴 사람이다")]
        [SerializeField] private float _turnSpeed = 160f;
        [Tooltip("<b>실제로 돌릴 것.</b> 비우면 이 부품이 붙은 오브젝트를 돌린다.\n\n" +
                 "그 오브젝트 밑에 <b>길잡이 표식</b>(경유점·설 자리·앉을 자리)을 매달아 둔 " +
                 "인물에게는 반드시 <b>몸</b>을 지목해야 한다. 아니면 소리 한 번에 " +
                 "표식이 통째로 딸려 돌아, 그 사람이 걸어갈 자리가 매번 딴 곳이 된다")]
        [SerializeField] private Transform _turn;

        /// <summary>
        /// 돌릴 몸. <see cref="_turn"/> 을 비워 두면 제 오브젝트다.
        ///
        /// <b>이 갈래가 왜 필요한가.</b> 마름은 제 오브젝트 밑에 경유점·설 자리·앉을
        /// 자리를 자식으로 달고 있다. 여기서 제 오브젝트를 돌리면 그 표식들이 함께
        /// 돌아 <b>세계 좌표가 통째로 바뀐다</b> — 문을 두드리는 소리 한 번에 축이
        /// 274도 돌아가, 앉을 자리가 마루에서 1.4m 떨어진 마당 한복판으로 옮겨 갔다.
        /// 게다가 몸 자신도 자식이라 그 축을 중심으로 <b>휩쓸려 날아간다</b>.
        /// (<see cref="FaceThePlayer"/> 가 같은 까닭으로 이미 이 갈래를 두고 있다.)
        /// </summary>
        private Transform Body { get { return _turn != null ? _turn : transform; } }
        [Tooltip("돌아본 채로 이만큼(초) 있다가 하던 대로 돌아간다")]
        [SerializeField] private float _turnHold = 2.0f;

        [Header("들었을 때")]
        [Tooltip("돌아볼 만큼 들렸다. 소리 난 자리를 넘긴다")]
        [SerializeField] private UnityEvent<Vector3> _onNoticed;
        [Tooltip("다가올 만큼 들렸다")]
        [SerializeField] private UnityEvent<Vector3> _onAlarmed;

        [Header("보기")]
        [Tooltip("들은 것을 콘솔에 적는다. 맞춰 볼 때만")]
        [SerializeField] private bool _log = false;

        /// <summary>마지막으로 들은 크기(0~1). 눈금이나 표정에 쓴다.</summary>
        public float LastHeard { get; private set; }

        private float _quietUntil;

        private void OnEnable() { All.Add(this); }
        private void OnDisable() { All.Remove(this); }

        /// <summary>
        /// 소리 하나를 들려 준다. <see cref="NoiseMeter"/> 가 부른다.
        /// </summary>
        /// <param name="at">소리가 난 자리</param>
        /// <param name="loudness">낸 크기(0~1)</param>
        /// <param name="reach">그 크기로 소리가 닿는 거리(m)</param>
        /// <param name="what">무슨 소리였나(기록용)</param>
        public void Hear(Vector3 at, float loudness, float reach, string what)
        {
            float d = Vector3.Distance(at, transform.position);
            if (d > _hearRadius || d > reach) { LastHeard = 0f; return; }

            // 거리만큼 잦아든다 — 코앞이면 그대로, 닿는 끝이면 0.
            float heard = loudness * (1f - Mathf.Clamp01(d / Mathf.Max(0.01f, reach)));

            // 벽 너머는 줄여 듣는다.
            if (_throughWalls < 1f && Blocked(at)) heard *= _throughWalls;

            LastHeard = heard;
            if (Time.time < _quietUntil || heard < _notice) return;

            _quietUntil = Time.time + _cooldown;
            if (_log) DevLog.Note($"[귀] {Josa.IGa(name)} 들었다 — {what ?? "무언가"} {heard * 100f:F0}% ({d:F1}m)", this);

            if (_turnToward)
            {
                Vector3 flat = at - Body.position; flat.y = 0f;
                if (flat.sqrMagnitude > 0.0004f)
                {
                    _lookAt = Quaternion.LookRotation(flat.normalized, Vector3.up);
                    _lookUntil = Time.time + _turnHold;
                }
            }

            if (heard >= _alarm) _onAlarmed?.Invoke(at);
            else _onNoticed?.Invoke(at);
        }

        /// <summary>
        /// <b>이 귀는 몸을 안 돌린다.</b>
        ///
        /// <see cref="NoiseWarning"/> 처럼 <b>몸이 없는 귀</b>가 부른다. 그런 귀는
        /// 문밖의 기척을 대신하는 빈 것인데, 하필 그 하나가 <b>플레이어 카메라</b>에
        /// 붙어 있었다 — <c>[RequireComponent]</c> 이 자동으로 달아 준 것이라
        /// 아무도 값을 손보지 않았다. 그래서 서랍을 열 때마다 <b>내 시선이 서랍 쪽으로
        /// 홱 돌아갔다</b>(실측: 소리 한 번에 카메라 yaw 0 → 90).
        /// </summary>
        public void DontTurn() { _turnToward = false; _lookUntil = 0f; }

        private Quaternion _lookAt;
        private float _lookUntil;

        /// <summary>돌아본 채로 잠깐 있는다. 다른 것이 몸을 돌리는 중이면 비켜 준다.</summary>
        private void Update()
        {
            if (!_turnToward || Time.time >= _lookUntil) return;
            var body = Body;
            body.rotation = Quaternion.RotateTowards(body.rotation, _lookAt, _turnSpeed * Time.deltaTime);
        }

        /// <summary>
        /// 소리와 나 사이에 벽이 있나.
        ///
        /// <b>사람은 벽이 아니다.</b> 이 말은 처음부터 여기 적혀 있었는데 코드는
        /// <b>아무 콜라이더나</b> 벽으로 세고 있었다. 사람에게 몸이 없던 동안에는
        /// 그래도 맞았지만(<see cref="PersonBody"/> 를 달면서 몸이 생겼다), 이제는
        /// 마름이 앞을 지나간다고 서랍 소리가 안 들리는 일이 난다. 그래서 걸린 것을
        /// 하나하나 보고 <b>사람의 몸은 건너뛴다</b>.
        /// </summary>
        private bool Blocked(Vector3 at)
        {
            Vector3 ear = transform.position + Vector3.up * 1.4f;
            Vector3 to = at - ear;
            float dist = to.magnitude;
            if (dist < 0.05f) return false;

            var hits = Physics.RaycastAll(ear, to / dist, dist, _wallMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
                if (!PersonBody.Is(hits[i].collider)) return true;
            return false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, _hearRadius);
        }
#endif
    }
}
