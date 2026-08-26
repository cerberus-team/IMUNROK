using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 플레이어를 걸어서 따라간다 (2026-08-25). 문서 「31. 선아」의 추종 구간이다.
    ///
    /// <code>
    /// 구출 후   Walk로 플레이어 추종. 멈추면 StandingIdle
    /// 구간      서고 → 서고~집무실 복도 → 집무실 → 관아 씬
    /// </code>
    ///
    /// ■ ⚠️ 순간이동이 아니라 실제로 걸어서 따라온다 (문서)
    ///   그래서 텔레포트를 쓰지 않는다. 다만 <b>따라잡지 못하는 상황</b>은 반드시 생긴다 —
    ///   플레이어가 뛰거나, 좁은 문틀에 걸리거나, 층계를 오르거나. 그때 사람이 사라지면
    ///   구출한 사람을 잃어버린 것처럼 보이므로 <see cref="catchUpDistance"/> 를 넘어가면
    ///   <b>보이지 않는 동안에만</b> 뒤로 붙여 준다. 눈앞에서 미끄러지지는 않는다.
    ///
    /// ■ 문틀·모서리
    ///   길찾기가 없으므로 정면이 막히면 좌우로 45° 씩 비껴 본다. 서고와 집무실은 방이 넓고
    ///   문이 하나뿐이라 이 정도로 지나간다. 그래도 막히면 위의 붙여 주기가 받아 낸다.
    /// </summary>
    public class NpcFollow : MonoBehaviour
    {
        [Header("모션")]
        public string walkState = "Walk";
        public string idleState = "StandingIdle";

        [Header("거리")]
        [Tooltip("이보다 멀면 걷기 시작한다(m)")]
        public float startDistance = 2.6f;

        [Tooltip("이보다 가까우면 멈춘다(m)")]
        public float stopDistance = 1.9f;

        [Tooltip("이보다 벌어지면 보이지 않을 때 뒤로 붙여 준다(m)")]
        public float catchUpDistance = 14f;

        [Header("걸음")]
        public float speed = 1.35f;
        public float turnSpeed = 220f;

        [Tooltip("붙여 줄 때 플레이어 뒤 이만큼(m)")]
        public float catchUpBehind = 2.2f;

        [Header("조건")]
        [Tooltip("선아를 구출한 뒤에만 따라간다")]
        public bool requireRescued = true;

        NpcActor actor;
        NpcDialogue talk;
        bool _walking;

        void Awake()
        {
            actor = GetComponent<NpcActor>();
            talk = GetComponent<NpcDialogue>();
        }

        void Update()
        {
            if (requireRescued && !GyeonuCase.SeonaRescued) return;
            if (actor != null && actor.CurrentPriority == NpcActor.Pri.Story) return;   // StandingUp 중엔 가만히
            if (talk != null && talk.Session != null) { Halt(); return; }               // 마주 서 있으면 멈춘다

            var cam = Camera.main;
            if (cam == null) return;

            Vector3 me = transform.position;
            Vector3 you = cam.transform.position;
            Vector3 to = you - me;
            to.y = 0f;
            float d = to.magnitude;

            if (d > catchUpDistance && !Seen(cam))
            {
                Vector3 back = you - cam.transform.forward * catchUpBehind;
                back.y = you.y;
                transform.position = NpcPatrol.Ground(back, 1.5f, 4f, transform);
                Halt();
                return;
            }

            if (_walking ? d <= stopDistance : d <= startDistance) { Halt(); return; }

            _walking = true;
            if (actor != null) actor.Play(walkState, NpcActor.Pri.Move, hold: true);

            Vector3 dir = Steer(to.normalized);
            var want = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * Time.deltaTime);
            transform.position = NpcPatrol.Ground(me + dir * speed * Time.deltaTime, 1.5f, 4f, transform);
        }

        void Halt()
        {
            if (!_walking) return;
            _walking = false;
            if (actor == null) return;
            actor.Release();
            actor.SetBase(idleState);
        }

        /// <summary>
        /// 정면이 막혔으면 좌우로 비껴 본다.
        ///
        /// ⚠️ 탐침이 <b>자기 몸통 안에서</b> 시작한다. 그대로 쏘면 자기 캡슐이 거리 0으로 잡혀
        ///    어느 쪽으로도 못 간다고 판정한다 — 자기 것은 걸러 내야 한다 (2026-08-25).
        /// </summary>
        Vector3 Steer(Vector3 dir)
        {
            Vector3 eye = transform.position + Vector3.up * 0.9f;
            if (!Blocked(eye, dir)) return dir;
            foreach (float a in new[] { 45f, -45f, 80f, -80f })
            {
                Vector3 d = Quaternion.AngleAxis(a, Vector3.up) * dir;
                if (!Blocked(eye, d)) return d;
            }
            return dir;
        }

        bool Blocked(Vector3 from, Vector3 dir)
        {
            foreach (var h in Physics.SphereCastAll(from, 0.3f, dir, 1.2f, ~0, QueryTriggerInteraction.Ignore))
                if (!h.collider.transform.IsChildOf(transform)) return true;
            return false;
        }

        bool Seen(Camera cam)
        {
            Vector3 to = transform.position + Vector3.up * 1.4f - cam.transform.position;
            return Vector3.Angle(cam.transform.forward, to) <= 65f;
        }
    }
}
