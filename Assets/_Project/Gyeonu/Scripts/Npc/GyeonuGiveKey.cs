using System.Collections;
using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 견우가 무언가를 건네는 순간 (2026-08-25). 문서 「24. GiveKey — 총 2회」 그대로다.
    ///
    /// <code>
    /// ① 선아 집 열쇠   신뢰도 40 → 루프 정지 → GiveKey 1회 → 지급 → Idle 복귀
    /// ② 비밀지도       신뢰도 70 → 도피 계획을 말한 뒤 → GiveKey 재사용 → 지급 → Idle 복귀
    /// </code>
    ///
    /// ■ 왜 "임계가 켜지는 순간"이 아니라 "말을 걸고 있는 동안"에 보는가
    ///   신뢰도 40은 단서를 내미는 순간 넘을 수도 있고, 앞선 대화의 누적으로 넘을 수도 있다.
    ///   임계가 켜지는 프레임에 곧바로 건네면 <b>마을 반대편에서 열쇠가 생기는</b> 일이 난다.
    ///   그래서 임계는 <see cref="GyeonuCase"/> 가 기억하고, <b>건네는 일</b>은 눈앞에 서 있을 때만
    ///   여기서 한다. 이미 넘긴 채로 처음 만나도 그 자리에서 건네므로 놓칠 수 없다.
    ///
    /// ■ ①은 자리를 가린다
    ///   문서: "밤에 선아 집 마당에서 견우 등장 → GiveKey". 그래서 밤·선아집 마당에 세운
    ///   인스턴스에만 <see cref="keyNeedsNight"/> 를 켠다. 낮의 견우는 열쇠를 주지 않는다.
    ///
    /// ■ ⚠️ GiveKey 재생 중 LookAround·Walk가 끼어들지 않게 한다 (문서)
    ///   <see cref="NpcActor.Pri.Story"/> 로 넣으면 랜덤·이동이 통째로 밀린다.
    /// </summary>
    [RequireComponent(typeof(NpcDialogue))]
    public class GyeonuGiveKey : MonoBehaviour
    {
        [Header("① 선아 집 열쇠 — 신뢰도 40")]
        public bool givesKey = true;

        [Tooltip("밤에만 건넨다. 낮의 견우(집 마당)에서는 꺼 둔다")]
        public bool keyNeedsNight = true;

        [Tooltip("소지품에서 찾을 열쇠 물건 id. 선아 집 열쇠는 SEONA_HOUSE_KEY다 (2026-09-10)")]
        public string keyItemId = "SEONA_HOUSE_KEY";

        [Header("② 타공 비밀지도 — 신뢰도 70")]
        public bool givesMap = true;

        [Tooltip("소지품에서 찾을 물건 id. 비밀지도는 A1이다")]
        public string mapItemId = "A1";

        [Header("연출")]
        public string giveState = "GiveKey";

        [Tooltip("말이 끝나고 이만큼 뒤에 건넨다(초). 곧바로 내밀면 대사와 겹쳐 어수선하다")]
        public float delay = 1.2f;

        NpcDialogue talk;
        bool _running;

        void Awake() { talk = GetComponent<NpcDialogue>(); }

        void Update()
        {
            if (_running || talk == null || talk.Session == null) return;   // 마주 서 있을 때만
            if (talk.Session.Busy) return;                                  // 대답을 기다리는 중엔 끼어들지 않는다

            if (givesKey && !GyeonuCase.HasSeonaHouseKey && GyeonuCase.Fired(Threshold.Trust40))
            {
                if (!keyNeedsNight || GyeonuCase.Night) { StartCoroutine(Give(Kind.Key)); return; }
            }

            if (givesMap && GyeonuCase.Fired(Threshold.Trust70) && !GyeonuCase.HasClue(ClueId.A1))
                StartCoroutine(Give(Kind.Map));
        }

        enum Kind { Key, Map }

        IEnumerator Give(Kind kind)
        {
            _running = true;
            yield return new WaitForSeconds(delay);

            var actor = talk.Actor;
            if (actor != null)
            {
                actor.PauseRandom(true);
                actor.Play(giveState, NpcActor.Pri.Story);
            }

            // 손이 올라온 뒤에 물건이 생긴다 — 클립 3.77초의 중간쯤.
            yield return new WaitForSeconds(1.6f);

            if (kind == Kind.Key)
            {
                // 실물 열쇠가 소지품에 들어가고(worldFlag = F_선아집열쇠), 상태 쪽 표시도 함께 선다.
                var key = Inventory.Find(keyItemId);
                if (key != null) Inventory.Add(key);
                else Debug.LogWarning("[견우] 열쇠 물건(" + keyItemId + ")을 못 찾아 상태 표시만 세웠다.");
                GyeonuCase.HasSeonaHouseKey = true;
                DebugToast.Show("견우에게서 선아 집 열쇠를 받았다.", 4f);
                Debug.Log("[견우] 선아 집 열쇠 지급 (신뢰도 " + GyeonuCase.Trust + ")");
            }
            else
            {
                var map = Inventory.Find(mapItemId);
                if (map != null && Inventory.Add(map))
                    Debug.Log("[견우] 타공 비밀지도 지급 (신뢰도 " + GyeonuCase.Trust + ")");
                else
                {
                    // 소지품 정의가 아직 없을 때도 진행이 막히지 않게 단서만이라도 세운다.
                    GyeonuWorld.Set(GyeonuWorld.F_비밀지도획득);
                    Debug.LogWarning("[견우] 지도 물건(" + mapItemId + ")을 못 찾아 단서만 세웠다.");
                }
                // B1 — 견우의 진짜 증언. 지도를 건네는 이 자리가 곧 도피 계획을 말하는 자리다.
                GyeonuCase.AddClue(ClueId.B1);
            }

            yield return new WaitForSeconds(1.4f);
            if (actor != null) actor.PauseRandom(false);
            _running = false;
        }
    }
}
