using System.Collections.Generic;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>헤드셋을 쓰면 도구가 얼굴에서 손으로 옮겨 간다.</b>
    ///
    /// 도구는 여태 <b>카메라의 자식</b>이었다. 화면으로 할 때는 그것이 옳다 — 손이
    /// 없으니 눈앞 어딘가에 띄워 두는 수밖에 없고, <see cref="HeldRig"/> 의 값들이
    /// 그 자리를 잡아 준다.
    ///
    /// 그런데 헤드셋을 쓰면 <b>카메라가 곧 목</b>이다. 그 자리 그대로 두면 등불이
    /// 코앞에 못 박혀 고개를 돌리는 대로 따라다니고, 컨트롤러를 아무리 휘둘러도
    /// 꿈쩍하지 않는다. 손에 든 물건이 아니라 <b>얼굴에 붙은 물건</b>이 된다.
    ///
    /// 그래서 손이 생기면 옮긴다.
    ///
    /// <b>왜 도구마다 안 하고 여기서 하나</b>: 헤드셋은 <b>늦게 선다</b>.
    /// <see cref="VRRig"/> 가 링크를 두드려 손이 생기기까지 몇 초가 걸리고, 실패하면
    /// 아예 안 생긴다. 도구마다 그 기다림을 따로 짜 두면 도구가 늘 때마다 잊는
    /// 자리가 하나씩 는다. 여기 한 군데가 목록을 들고 기다린다.
    ///
    /// 옮겨 놓은 뒤에는 <b>손을 안 댄다</b> — 자리를 매 칸 다시 쓰면 사람이 손을
    /// 움직여도 도구가 제자리로 튕겨 온다. 한 번 앉히고 그다음은 컨트롤러가 끈다.
    ///
    /// <b>딸린 일</b>: <see cref="InHand"/> 가 켜지면, 눈 기준으로 짜 둔 손짓들을
    /// 도구 쪽에서 스스로 꺼야 한다 — 등불을 「종이 뒤로 들어 올리는」 것 따위다.
    /// 손에 들었으면 그건 사람이 팔로 한다.
    /// </summary>
    public static class HeldHand
    {
        private struct Held
        {
            public Transform holder;
            public string toolId;
            public Transform model;
            public Transform home;        // 옮기기 전 부모(카메라)
            public Vector3 homePos;
            public Quaternion homeRot;
        }

        private static readonly List<Held> _all = new List<Held>();
        private static bool _moved;

        /// <summary>지금 도구가 <b>진짜 손</b>에 들려 있나. 눈 기준 손짓을 끌 때 본다.</summary>
        public static bool InHand { get { return _moved; } }

        /// <summary>
        /// 손에 태울 것을 적어 둔다. <see cref="HeldRig.Apply"/> 가 대신 불러 주므로
        /// 도구 쪽에서 따로 부를 일이 없다.
        /// </summary>
        public static void Register(Transform holder, string toolId, Transform model)
        {
            if (holder == null) return;
            for (int i = 0; i < _all.Count; i++)
                if (_all[i].holder == holder) return;      // 두 번 적지 않는다

            _all.Add(new Held
            {
                holder = holder,
                toolId = toolId,
                model = model,
                home = holder.parent,
                homePos = holder.localPosition,
                homeRot = holder.localRotation,
            });
        }

        /// <summary>
        /// 재생이 새로 시작될 때 목록을 비운다.
        ///
        /// <b>도메인 리로드가 꺼진 프로젝트</b>에서는 정적 값이 재생을 넘겨 산다.
        /// 지난 판의 홀더(이미 없어진 것)가 목록에 그대로 남아 있으면 다음 판에서
        /// 죽은 것을 만지게 된다. 이 프로젝트에서 이미 여러 번 밟은 자리다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { _all.Clear(); _moved = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var go = new GameObject("~손에들기") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Watcher>();
        }

        /// <summary>손이 생겼나 없어졌나만 지켜본다. 하는 일이 적으므로 안쪽에 둔다.</summary>
        private class Watcher : MonoBehaviour
        {
            private void Update()
            {
                var hand = Hand();
                if (hand != null && !_moved) { Move(hand); _moved = true; }
                else if (hand == null && _moved) { Home(); _moved = false; }
            }
        }

        /// <summary>도구를 쥘 손. 왼손잡이면 왼손이다.</summary>
        private static Transform Hand()
        {
            if (!VRRig.Active) return null;
            return HudSide.LeftHanded ? VRRig.LeftHand : VRRig.RightHand;
        }

        private static void Move(Transform hand)
        {
            int n = 0;
            for (int i = 0; i < _all.Count; i++)
            {
                var h = _all[i];
                if (h.holder == null) continue;

                // 옮기기 전에 지금 부모를 다시 적어 둔다 — 씬이 바뀌면서 카메라가
                // 딴것으로 갈렸을 수 있다.
                if (h.holder.parent != hand)
                {
                    h.home = h.holder.parent;
                    h.homePos = h.holder.localPosition;
                    h.homeRot = h.holder.localRotation;
                    _all[i] = h;
                }

                h.holder.SetParent(hand, false);

                HeldRig.Pose p;
                if (HeldRig.TryGetHand(h.toolId, out p))
                {
                    var pos = p.position;
                    if (HudSide.LeftHanded) pos.x = -pos.x;
                    h.holder.localPosition = pos;
                    h.holder.localRotation = Quaternion.Euler(p.euler);
                    if (h.model != null && p.scale > 0f) h.model.localScale = Vector3.one * p.scale;
                }
                else
                {
                    // 모르는 도구는 손아귀에 그냥 붙인다 — 눈 기준 값(눈앞 반 미터)을
                    // 손에 그대로 쓰면 컨트롤러에서 반 미터 떨어져 둥둥 뜬다.
                    h.holder.localPosition = Vector3.zero;
                    h.holder.localRotation = Quaternion.identity;
                }
                n++;
            }
            if (n > 0) Debug.Log("[손에들기] 도구 " + n + "개를 얼굴에서 손으로 옮겼다.");
        }

        private static void Home()
        {
            for (int i = 0; i < _all.Count; i++)
            {
                var h = _all[i];
                if (h.holder == null || h.home == null) continue;
                h.holder.SetParent(h.home, false);
                h.holder.localPosition = h.homePos;
                h.holder.localRotation = h.homeRot;
            }
        }
    }
}
