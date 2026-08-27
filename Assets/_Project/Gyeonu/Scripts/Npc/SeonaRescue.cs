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
            yield return new WaitForSeconds(standUpSeconds);

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
