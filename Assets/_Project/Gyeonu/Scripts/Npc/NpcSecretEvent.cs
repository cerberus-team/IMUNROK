using System.Collections;
using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// "털어놓는" 순간의 연출 (2026-08-25). 문서 「28. 아이02 비밀길 이벤트」 그대로다.
    ///
    /// <code>
    /// 플레이어가 부드럽게 캐묻거나 조건 충족
    ///   → 아이02 Secret 시작 (Jump 금지, 랜덤 루프 정지)
    ///   → 아이01 Talk로 끼어들어 말린다
    ///   → 아이02가 결국 비밀길 설명
    ///   → 종료 후 둘 다 StandIdle
    /// </code>
    ///
    /// ■ 왜 끼어드는 쪽을 따로 두는가
    ///   말리는 것은 <b>다른 사람의 몸</b>이다. 아이02의 애니메이터가 아이01을 움직일 수는 없고,
    ///   대화 세션은 말을 건 상대 한 명만 안다. 그래서 씬에서 짝을 지어 준다 —
    ///   <see cref="companion"/> 가 비어 있으면 혼자 털어놓는다(최초의 견우가 그렇다).
    ///
    /// ■ 이벤트당 1회 (문서)
    ///   플래그가 이미 서 있으면 연출을 다시 돌리지 않는다. 말은 다시 해도 몸짓은 한 번뿐이다.
    /// </summary>
    public class NpcSecretEvent : MonoBehaviour
    {
        [Header("털어놓는 쪽")]
        [Tooltip("비우면 이 오브젝트의 NpcActor")]
        public NpcActor actor;

        [Tooltip("재생할 모션. 없으면 아무것도 하지 않는다")]
        public string secretState = "Secret";

        [Header("말리는 쪽 — 아이01")]
        [Tooltip("비워도 된다. 최초의 견우는 혼자 말한다")]
        public NpcActor companion;

        public string companionState = "Talk";

        [Tooltip("털어놓기 시작하고 이만큼 뒤에 끼어든다(초)")]
        public float companionDelay = 1.6f;

        bool _fired;

        void Awake() { if (actor == null) actor = GetComponent<NpcActor>(); }

        /// <summary>대화 쪽이 [비밀] 표식을 읽었을 때 부른다.</summary>
        public void Fire()
        {
            if (_fired) return;
            _fired = true;
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            if (actor != null && !string.IsNullOrEmpty(secretState) && actor.Has(secretState))
            {
                // ⚠️ 문서 — Secret 재생 중 Jump 금지, 랜덤 루프 정지.
                //    Pri.Story 로 넣으면 NpcActor가 랜덤을 알아서 멈춘다. 대화 중이라
                //    이미 멈춰 있지만, 대화가 먼저 끝나도 연출이 살아 있게 여기서도 잠근다.
                actor.PauseRandom(true);
                actor.Play(secretState, NpcActor.Pri.Story);
            }

            if (companion != null && !string.IsNullOrEmpty(companionState) && companion.Has(companionState))
            {
                yield return new WaitForSeconds(companionDelay);
                companion.Play(companionState, NpcActor.Pri.Story);
            }

            // 종료 후 둘 다 각자의 기본 자세로 — NpcActor가 클립 길이를 보고 스스로 돌아간다.
            yield return new WaitForSeconds(2.5f);
            if (actor != null) actor.PauseRandom(false);
        }
    }
}
