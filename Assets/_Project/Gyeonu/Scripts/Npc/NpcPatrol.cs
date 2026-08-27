using System.Collections;
using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 잠깐 나와 둘러보고 돌아가는 걸음 (2026-08-25).
    /// 문서 「25. 수령」의 <b>앞마당 루틴</b>과 <b>은하담 시찰</b>이 같은 한 틀이다.
    ///
    /// <code>
    /// 앞마당   SittingIdle → SitToStand → StandIdle → Walk → 지점 → StandIdle
    ///          → Walk → 다른 지점 → StandIdle → Walk → 동헌 복귀 → StandToSit → SittingIdle
    /// 은하담   Walk → 정지 → StandIdle → Walk → 다른 위치 → StandIdle → 반복   (앉지 않는다)
    /// </code>
    ///
    /// ■ 왜 NavMesh를 쓰지 않는가
    ///   걸어야 할 길이 <b>마당 두어 걸음</b>이다. 그 정도에 내비메시를 구우면 씬마다 베이크
    ///   데이터가 생기고, 배경 담당이 담장 하나 옮길 때마다 다시 구워야 한다. 지점은 사람이
    ///   찍은 몇 개뿐이고 사이에 장애물이 없으므로 직선으로 간다. 바닥 높이만 광선으로 짚는다.
    ///
    /// ■ ⚠️ 움직임을 느리고 절제되게 (문서 「25」)
    ///   부산스러우면 진중하고 계산적인 이미지가 깨진다. 걸음은 느리고, 지점마다 반드시 선다.
    ///
    /// ■ 대화가 걸리면 멈춘다
    ///   플레이어가 말을 걸면 <see cref="NpcActor"/> 가 Talk(2)로 Walk(3)를 밀어낸다. 그런데
    ///   <b>몸이 계속 미끄러지면</b> 마주 선 카메라 앞에서 상대가 옆으로 흘러간다. 그래서 여기서도
    ///   대화 중에는 발을 멈춘다.
    /// </summary>
    public class NpcPatrol : MonoBehaviour
    {
        [Header("지점 — 자기 자리 기준 상대 좌표(m)")]
        public Vector3[] points = new Vector3[0];

        [Header("모션 이름")]
        public string walkState = "Walk";
        public string standIdle = "StandIdle";

        [Tooltip("앉은 자리에서 출발하는 인물만. 비우면 서 있는 채로 시작한다")]
        public string sitIdle = "";
        public string sitToStand = "";
        public string standToSit = "";

        [Header("속도와 텀")]
        [Tooltip("걷는 속도(m/s). 수령은 느리다")]
        public float speed = 0.85f;

        [Tooltip("도는 속도(도/초)")]
        public float turnSpeed = 120f;

        [Tooltip("지점마다 서서 둘러보는 시간(초)")]
        public Vector2 restRange = new Vector2(3f, 5f);

        [Tooltip("한 번 나갔다 오는 데 도는 지점 수. 문서의 '2세트'")]
        public int pointsPerTrip = 2;

        [Tooltip("루틴 사이에 쉬는 시간(초)")]
        public Vector2 idleRange = new Vector2(45f, 80f);

        [Tooltip("켜면 자리로 돌아오지 않고 지점 사이를 계속 오간다 — 은하담 시찰")]
        public bool roamForever = false;

        [Tooltip("걸음마다 발밑을 짚는다. 끄면 출발점과 목적지를 잇는 직선으로 미끄러진다. " +
                 "⚠️ 큰 층계를 오르내리는 길에는 쓰지 마라 — 한 번 아래로 떨어지면 구조물 속으로 " +
                 "들어가 다시 올라오지 못한다. 지금 배치는 전부 평지 또는 완만한 지형이다")]
        public bool stickToGround = true;

        [Tooltip("발밑을 얼마나 위에서 내려다볼지(m). 한 걸음에 오를 수 있는 턱 높이가 된다")]
        public float groundProbeAbove = 1.5f;

        [Header("조건")]
        [Tooltip("이 시간대에만 돈다. 비우면 아무 때나")]
        public TimeOfDay[] onlyAt = new[] { TimeOfDay.Day };

        NpcActor actor;
        NpcDialogue talk;
        Vector3 home;
        Quaternion homeRot;
        int cursor;

        void Awake()
        {
            actor = GetComponent<NpcActor>();
            talk = GetComponent<NpcDialogue>();
            home = transform.position;
            homeRot = transform.rotation;
        }

        void OnEnable() { StartCoroutine(Loop()); }
        void OnDisable() { StopAllCoroutines(); }

        bool TimeOk()
        {
            if (onlyAt == null || onlyAt.Length == 0) return true;
            foreach (var t in onlyAt) if (t == GyeonuCase.Time) return true;
            return false;
        }

        bool Blocked => talk != null && talk.Session != null;

        IEnumerator Loop()
        {
            yield return new WaitForSeconds(Random.Range(idleRange.x, idleRange.y) * 0.4f);

            while (true)
            {
                if (points.Length == 0 || !TimeOk() || Blocked) { yield return new WaitForSeconds(1f); continue; }

                if (!string.IsNullOrEmpty(sitToStand) && actor != null && actor.Has(sitToStand))
                {
                    actor.Play(sitToStand, NpcActor.Pri.Transition);
                    yield return new WaitForSeconds(2.3f);
                    actor.SetBase(standIdle);
                }

                int n = roamForever ? 1 : Mathf.Max(1, pointsPerTrip);
                for (int i = 0; i < n; i++)
                {
                    yield return WalkTo(home + points[cursor % points.Length]);
                    cursor++;
                    if (actor != null) actor.SetBase(standIdle);
                    yield return new WaitForSeconds(Random.Range(restRange.x, restRange.y));
                }

                if (!roamForever)
                {
                    // ⚠️ 돌아올 때는 바닥을 다시 짚지 않는다. 앉은 자리는 사람이 눈으로 맞춘
                    //    높이라(수령은 대청마루보다 0.32m 낮게 앉아 있다) 광선으로 짚으면
                    //    엉덩이가 마루 위로 떠오른다. 처음 서 있던 그 자리로 정확히 되돌린다.
                    yield return WalkTo(home, snapGround: false);
                    // ⚠️ 걸음마다 바닥을 짚느라 마지막 한 발이 마루 위(4.00)에 얹힌다. 앉은 자리는
                    //    사람이 눈으로 맞춘 3.68이라 그대로 앉으면 엉덩이가 0.32m 떠오른다 —
                    //    도착했으면 처음 그 자리로 정확히 되돌린다 (2026-08-25 실측).
                    transform.position = home;
                    yield return TurnTo(homeRot);
                    if (!string.IsNullOrEmpty(standToSit) && actor != null && actor.Has(standToSit))
                    {
                        actor.Play(standToSit, NpcActor.Pri.Transition);
                        yield return new WaitForSeconds(2.3f);
                        actor.SetBase(sitIdle);
                    }
                }

                yield return new WaitForSeconds(Random.Range(idleRange.x, idleRange.y));
            }
        }

        IEnumerator WalkTo(Vector3 goal, bool snapGround = true)
        {
            if (snapGround) goal = Ground(goal, groundProbeAbove, 4f, transform);
            while (true)
            {
                if (Blocked) { yield return null; continue; }        // 말을 걸면 발을 멈춘다
                Vector3 to = goal - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude < 0.04f) break;

                if (actor != null) actor.Play(walkState, NpcActor.Pri.Move, hold: true);
                var want = Quaternion.LookRotation(to.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * Time.deltaTime);

                // ⚠️ 걸음마다 바닥을 다시 짚는다. 목적지만 짚고 직선으로 미끄러지면 <b>바닥을 뚫고
                //    지나간다</b> — 수령이 대청(y 4.0)에서 월대(1.80)로 내려갈 때 기단 돌담 속을
                //    가로질렀다(2026-08-25 실측). 계단이 있는 길이라 발밑만 보면 저절로 층계를 탄다.
                Vector3 step = Vector3.MoveTowards(transform.position, goal, speed * Time.deltaTime);
                transform.position = stickToGround ? Ground(step, groundProbeAbove, 4f, transform) : step;
                yield return null;
            }
            if (actor != null) actor.Release();
        }

        IEnumerator TurnTo(Quaternion want)
        {
            while (Quaternion.Angle(transform.rotation, want) > 1.5f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * Time.deltaTime);
                yield return null;
            }
        }

        /// <summary>
        /// 바닥에 발을 붙인다. 지형과 구조물(월대·마루·평상) 둘 다 짚는다.
        ///
        /// ⚠️ 위에서 <b>조금만</b> 내려다본다(기본 1.5m). 높은 데서 쏘면 실내에서 지붕·천장을
        ///    먼저 맞아 사람이 처마 위로 올라선다. 걸음마다 부르는 자리라 발밑만 보면 충분하다.
        ///    씬에 처음 세울 때처럼 높이를 모르는 자리는 <c>above</c> 를 크게 준다.
        /// </summary>
        public static Vector3 Ground(Vector3 p, float above = 1.5f, float dist = 4f, Transform ignore = null)
        {
            // ⚠️ <b>자기 몸을 밟고 올라선다.</b> 탐침이 시작되는 자리는 그 사람의 가슴께라
            //    Raycast 하나로는 자기 캡슐이 먼저 걸린다. 그러면 발밑이 곧 자기 머리가 되어
            //    한 걸음마다 키만큼 솟는다 — 수령이 세 걸음 만에 y 1628까지 올라갔다(2026-08-25 실측).
            //    그래서 RaycastAll 로 전부 받아 자기 것을 걸러 내고 가장 높은 바닥을 고른다.
            var hits = Physics.RaycastAll(new Vector3(p.x, p.y + above, p.z), Vector3.down, dist,
                                          ~0, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            foreach (var h in hits)
            {
                if (ignore != null && h.collider.transform.IsChildOf(ignore)) continue;
                if (h.point.y > best) best = h.point.y;
            }
            if (best > float.NegativeInfinity) { p.y = best; return p; }

            var t = Terrain.activeTerrain;
            if (t != null) p.y = t.SampleHeight(p) + t.transform.position.y;
            return p;
        }

        void OnDrawGizmosSelected()
        {
            Vector3 h = Application.isPlaying ? home : transform.position;
            Gizmos.color = Color.cyan;
            foreach (var p in points) Gizmos.DrawWireSphere(h + p, 0.35f);
        }
    }
}
