using System.Collections;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>사랑채부터 시작한다</b> — 대문 두드리기부터 다시 따라가지 않고.
    ///
    /// 부르는 곳은 [이문록 ▸ 바로 시작 ▸ 사랑채] 하나뿐이다. 그 메뉴가
    /// <see cref="StartAt"/> 에 쪽지를 한 장 남기고 재생을 켜면, 여기가 그 쪽지를
    /// <b>읽으면서 지우고</b> 사람을 방 안에 내려놓는다.
    ///
    /// <b>왜 씬에 안 두고 제 발로 서는가</b> — 씬에 부품을 하나 얹으면 그것이 커밋에
    /// 남고, 언젠가 남의 손에서 켜진다. 이건 만드는 사람의 지름길이지 게임의 일부가
    /// 아니다. 그러니 씬을 안 건드리는 쪽이 옳다.
    ///
    /// <b>자리를 여기 적어 두지 않는다</b> — 방이 어디인지는
    /// <see cref="InteriorSceneSwap.RoomBox"/> 가 이미 알고 있다. 그 값을 읽어 쓴다.
    /// 자리를 두 곳에 적으면 방을 옮길 때 한 곳만 고치고 만다.
    ///
    /// 내려놓은 뒤에 하는 일 둘:
    ///   · <b>씬이 갈리기를 기다린다</b>. 실내는 따로 씬이라, 방 안에 들어선 것을
    ///     <see cref="InteriorSceneSwap"/> 이 알아채고 마당을 내리고 방을 올린다.
    ///     그 사이에 손을 대면 올라오는 중인 씬을 건드리게 된다.
    ///   · <b>복동을 앉힌다</b>. 그는 대문 앞에 서 있다 — 앞장서 안내한 적이 없으니
    ///     당연하다. 손님이 이미 방에 앉았는데 안내인이 마당에 서 있으면 그림이 어긋난다.
    /// </summary>
    public static class QuickStart
    {
        /// <summary>눈높이(m). 방바닥 위 이만큼에 눈을 둔다.</summary>
        private const float Eye = 1.55f;

        /// <summary>마주 앉는 상대에게서 물러나 서는 거리(m). 한 걸음 반쯤이다.</summary>
        private const float StandOff = 1.7f;

        /// <summary>씬이 갈리기를 기다리는 한도(초). 안 갈리면 포기하고 알린다.</summary>
        private const float Patience = 12f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
#if UNITY_EDITOR
            var where = StartAt.Take();
            if (where != StartAt.사랑채) return;

            var host = new GameObject("~바로시작") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(host);
            host.AddComponent<Runner>().StartCoroutine(Drop(host));
#endif
        }

        /// <summary>코루틴을 돌릴 몸통. 하는 일이 없으므로 빈 채로 둔다.</summary>
        private class Runner : MonoBehaviour { }

        private static IEnumerator Drop(GameObject host)
        {
            // <b>한 프레임으로는 모자란다.</b> PlayerStart 가 Awake 에서 카메라를 제자리로
            // 옮기고, InteriorSceneSwap 은 Start 에서 마당을 덧씌워 올린다. 그 둘이
            // 다 끝난 뒤라야 옮긴 자리가 안 되돌려진다.
            yield return null;
            yield return null;

            var swap = Object.FindFirstObjectByType<InteriorSceneSwap>();
            var cam = Camera.main;
            if (swap == null || cam == null)
            {
                Debug.LogWarning("[바로 시작] 사랑채로 못 갔다 — " +
                                 (swap == null ? "실내 갈아끼우개가" : "카메라가") + " 이 씬에 없다.");
                Object.Destroy(host);
                yield break;
            }

            var box = swap.RoomBox;
            if (box.size.sqrMagnitude < 0.01f)
            {
                Debug.LogWarning("[바로 시작] 방 넓이가 비어 있다. " +
                                 "[이문록 ▸ 사랑방 ▸ 방 넓이 재기] 를 한 번 눌러 주십시오.");
                Object.Destroy(host);
                yield break;
            }

            // 카메라가 달린 몸통을 옮긴다 — 헤드셋을 쓰면 카메라의 자리는 매 프레임
            // XR 이 다시 쓴다. 몸통을 옮겨야 둘 다 맞는다.
            var body = cam.transform.root;

            // <b>방 한가운데에 내려놓으면 안 된다.</b> 처음에 그렇게 했더니 눈앞이
            // 통째로 벽이었다 — 사랑채는 한 칸이 아니라 여러 칸이고, 넓이 상자는
            // 그 여러 칸을 한꺼번에 두른 것이라 <b>한가운데가 사람이 앉는 칸이 아니다</b>.
            //
            // 사람이 설 자리는 <b>복동이 앉은 자리에서 한 걸음 반 물러난 데</b>다.
            // 그 자리가 곧 마주 앉는 자리이고, 방이 몇 칸이든 거기는 늘 맞다.
            var spot = GameObject.Find("甲_보료자리");
            Vector3 aim = spot != null ? spot.transform.position : box.center;
            Vector3 back = box.center - aim;              // 방 안쪽으로 물러나는 쪽
            back.y = 0f;
            if (back.sqrMagnitude < 0.01f) back = Vector3.right;

            // box.max.y 가 방바닥(마루) 면이다. 넓이를 잰 도구가 마루 위로 얇게 떠 놓는다.
            // 여기 높이는 <b>어림</b>이면 된다 — 방이 올라온 뒤에 바닥을 짚어 다시 선다.
            var at = aim + back.normalized * StandOff;
            at.y = box.max.y + Eye;
            body.position = at;

            // 마주 보게 돌려세운다. 뒤돌아선 채로 시작하면 어디에 떨어졌는지부터 헤매게 된다.
            Vector3 d = aim - at;
            d.y = 0f;
            if (d.sqrMagnitude > 0.01f) body.rotation = Quaternion.LookRotation(d);

            // 각을 맞춰 주지 않으면 <b>마우스를 대는 순간 옛 각도로 홱 돌아간다</b> —
            // 걷기 부품이 yaw·pitch 를 제가 들고 있기 때문이다. 이미 한 번 밟은 함정이다.
            var fly = cam.GetComponent<DebugFlyCamera>();
            if (fly != null) fly.SyncAngles();

            DevLog.Note("[바로 시작] 사랑채 " + at.ToString("F2") + " 에 내려놓았다. 씬이 갈리기를 기다린다…");

            // 갈아끼우개는 Update 에서 방 안인지 보고 스스로 씬을 바꾼다. 그것을 기다린다.
            float waited = 0f;
            while (waited < Patience && !(swap.Inside && !swap.Busy))
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!swap.Inside)
            {
                Debug.LogWarning("[바로 시작] " + Patience + "초를 기다려도 실내로 안 갈렸다. " +
                                 "방 넓이(RoomBox)가 사람이 선 자리와 맞는지 보십시오.");
                Object.Destroy(host);
                yield break;
            }

            // 마루를 짚어 다시 선다 — 내려놓은 높이는 어림이었다.
            if (fly != null) fly.StandOnGround();

            // 복동을 방에 앉힌다. 이름으로 찾을 것 없이 부품 하나뿐이다.
            var bokdong = Object.FindFirstObjectByType<BokdongController>();
            if (bokdong != null) bokdong.SitDown();

            DevLog.Note("[바로 시작] 사랑채에 들었다" + (bokdong != null ? " · 복동도 앉혔다" : "") + ".");
            Object.Destroy(host);
        }
    }
}
