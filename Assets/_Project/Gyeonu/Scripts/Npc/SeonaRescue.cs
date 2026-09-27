using System.Collections;
using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 선아를 찾아낸 순간 (2026-08-25). 문서 「31. 선아 — 상태 변화」 그대로다.
    ///
    /// <code>
    /// 구출 전     서고 안쪽에서 FallIdle. 랜덤 모션 없음
    /// 구출 이벤트  FallIdle → StandingUp → StandingIdle
    ///             ⚠️ StandingUp 재생 중 다른 애니메이션 금지
    /// 구출 후      Walk로 플레이어 추종. 멈추면 StandingIdle
    /// </code>
    ///
    /// ■ 언제가 '구출'인가
    ///   서고 안쪽까지 들어와 <b>말을 건 그 순간</b>이다. 따로 버튼을 두지 않는다 —
    ///   쓰러져 있는 사람 앞에서 상호작용 문구를 한 번 더 고르게 하면 극적인 순간이 절차가 된다.
    ///
    /// ■ ⚠️ 다른 애니메이션 금지를 어떻게 지키는가
    ///   <see cref="NpcActor.Pri.Story"/> 로 넣으면 대화 모션(2)·이동(3)·랜덤(5)이 전부 밀린다.
    ///   선아는 대화 전용 모션이 없으므로 실제로 끼어들 수 있는 것은 추종의 Walk뿐인데,
    ///   <see cref="NpcFollow"/> 도 Story가 도는 동안에는 발을 떼지 않는다.
    ///
    /// ■ 구출 표시를 클립이 끝난 뒤에 세우는 까닭
    ///   <see cref="GyeonuCase.SeonaRescued"/> 가 서는 순간 신뢰도 체계가 끝나고 C3 게이트가 열린다.
    ///   아직 바닥에 누워 있는데 "구출 완료"가 되면 저널과 눈앞이 어긋난다. 몸을 일으킨 뒤에 세운다.
    /// </summary>
    [RequireComponent(typeof(NpcDialogue))]
    public class SeonaRescue : MonoBehaviour
    {
        [Header("모션")]
        public string fallIdle = "FallIdle";
        public string standingUp = "StandingUp";
        public string standingIdle = "StandingIdle";

        [Tooltip("StandingUp 클립 길이(초). 이 시간 동안 아무것도 끼어들지 못한다")]
        public float standUpSeconds = 7.6f;

        [Header("얼굴 높이 — 자세가 바뀌면 함께 바뀐다")]
        [Tooltip("쓰러져 있을 때. 발밑에서 잰다")]
        public float eyeFallen = 0.55f;

        [Tooltip("몸을 일으킨 뒤")]
        public float eyeStanding = 1.50f;

        [Header("일어서는 동안 발을 바닥에 붙이기 (2026-09-10)")]
        [Tooltip("StandingUp 클립을 0.2초마다 재서 구운 표 — 그 순간 몸의 최저점이 루트보다 얼마나 위인가(m). " +
                 "루트 y = 바닥 − 이 값. 첫 키는 FallIdle 의 값(0.52)이라 크로스페이드와 이어진다. " +
                 "⚠️ 이 팩의 쓰러짐·일어섬 클립은 루트를 두고 몸만 움직여서, 누운 루트(바닥 −0.52)에서 " +
                 "그대로 일어서면 무릎까지 바닥에 박힌 채 선다 (실측 0.70m)")]
        public AnimationCurve riseCurve = new AnimationCurve(
            new Keyframe(0.0f, 0.517f), new Keyframe(0.2f, 0.372f), new Keyframe(0.4f, 0.360f),
            new Keyframe(0.6f, 0.359f), new Keyframe(0.8f, 0.380f), new Keyframe(1.0f, 0.387f),
            new Keyframe(1.2f, 0.409f), new Keyframe(1.4f, 0.438f), new Keyframe(1.6f, 0.430f),
            new Keyframe(1.8f, 0.390f), new Keyframe(2.0f, 0.291f), new Keyframe(2.2f, 0.221f),
            new Keyframe(2.4f, 0.163f), new Keyframe(2.6f, 0.165f), new Keyframe(2.8f, 0.144f),
            new Keyframe(3.0f, 0.280f), new Keyframe(3.2f, 0.293f), new Keyframe(3.4f, 0.504f),
            new Keyframe(3.6f, 0.559f), new Keyframe(3.8f, 0.509f), new Keyframe(4.0f, 0.399f),
            new Keyframe(4.2f, 0.210f), new Keyframe(4.4f, 0.170f), new Keyframe(4.6f, 0.154f),
            new Keyframe(4.8f, 0.112f), new Keyframe(5.0f, 0.060f), new Keyframe(5.2f, 0.040f),
            new Keyframe(5.4f, 0.053f), new Keyframe(5.6f, 0.071f), new Keyframe(5.8f, 0.099f),
            new Keyframe(6.0f, 0.074f), new Keyframe(6.2f, 0.049f), new Keyframe(6.4f, 0.038f),
            new Keyframe(6.6f, 0.025f), new Keyframe(6.8f, 0.017f), new Keyframe(7.0f, 0.006f),
            new Keyframe(7.2f, 0.004f), new Keyframe(7.4f, 0.001f), new Keyframe(7.6f, 0.000f));

        NpcDialogue talk;
        NpcActor actor;
        bool _started;

        void Awake()
        {
            talk = GetComponent<NpcDialogue>();
            actor = GetComponent<NpcActor>();
            bool up = GyeonuCase.SeonaRescued;
            if (actor != null && !up) actor.SetBase(fallIdle, snap: true);
            // ⚠️ 쓰러져 있는 사람의 얼굴은 무릎 높이에 있다. 선 사람 눈높이를 겨누면
            //    카메라가 허공을 보고 대화창만 뜬다 — 자세에 맞춰 겨눌 곳을 바꾼다.
            if (talk != null) talk.eyeHeightOverride = up ? eyeStanding : eyeFallen;
        }

        void Update()
        {
            if (_started || GyeonuCase.SeonaRescued) return;
            if (talk == null || talk.Session == null) return;      // 말을 걸어야 시작한다
            _started = true;
            StartCoroutine(StandUp());
        }

        IEnumerator StandUp()
        {
            yield return new WaitForSeconds(1.4f);                // 첫 마디가 끝나갈 즈음

            if (actor != null)
            {
                actor.PauseRandom(true);
                actor.Play(standingUp, NpcActor.Pri.Story);
            }

            // 일어서는 동안 몸의 최저점을 바닥에 붙인 채 루트를 끌어올린다. 서고 바닥은 루트보다
            // 0.52 위(누운 자세 기준)이므로 여기서 재면 -6.60 이 나온다. 클립이 끝나면 발이 바닥에 닿아 있다.
            float floorY = NpcPatrol.Ground(transform.position, 1.5f, 4f, transform).y;
            float t = 0f;
            while (t < standUpSeconds)
            {
                t += Time.deltaTime;
                Vector3 p = transform.position;
                p.y = floorY - riseCurve.Evaluate(Mathf.Min(t, standUpSeconds));
                transform.position = p;
                yield return null;
            }
            {
                Vector3 p = transform.position;
                p.y = floorY;
                transform.position = p;
            }

            if (actor != null)
            {
                actor.SetBase(standingIdle);
                actor.ResetToBase();
            }
            if (talk != null) talk.eyeHeightOverride = eyeStanding;

            GyeonuCase.SeonaRescued = true;
            DebugToast.Show("선아를 찾았다.", 4f);
            Debug.Log("[선아] 구출 — 이제부터 플레이어를 따라 걷는다.");

            var follow = GetComponent<NpcFollow>();
            if (follow != null) follow.enabled = true;
        }
    }
}
