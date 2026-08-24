using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 손에 든 도구가 <b>어디에 어떻게 들리는가</b> — 씬이 아니라 여기 한 군데에 적는다.
    ///
    /// <b>왜 여기로 옮겼나</b>: 자리를 씬에 적어 두었더니 사건마다 따로 놀았다.
    /// 옹고집전에서는 등불이 오른손(+0.28)에 들려 있는데 조사청에서는 <b>왼손</b>(-0.17)에
    /// 들려 있었고, 돋보기는 옹고집전에만 기울기(-45, 0, 90)가 있고 조사청에는 없었다.
    /// 같은 도구가 방마다 다른 손에 들리면 그건 도구가 아니라 소품이다. 무엇보다
    /// 한쪽을 고쳐도 다른 쪽은 그대로라, 고칠 때마다 두 번씩 고쳐야 한다.
    ///
    /// 도구를 드는 자세는 <b>사건의 사정</b>이 아니라 <b>사람의 사정</b>이다.
    /// 오른손잡이가 등불을 드는 자리는 옹고집전이든 조사청이든 같다. 그러니 씬에
    /// 흩어 둘 것이 아니라 공통으로 두고, 방마다 달라야 할 까닭이 생기면 그때
    /// <see cref="HeldToolModel"/> 쪽에서 따로 끄면 된다.
    ///
    /// 값은 <b>옹고집전에서 맞춰 둔 것</b>을 그대로 옮겨 왔다 — 그쪽이 먼저 손으로
    /// 맞춘 것이고, 실제로 헤드셋을 쓰고 본 자리다.
    ///
    /// <b>왼손잡이</b>: <see cref="HudSide.LeftHanded"/> 가 켜지면 x 를 뒤집는다.
    /// 도구벨트가 이미 그 값을 보고 좌우를 바꾸므로, 손에 든 것만 반대편에 있으면
    /// 몸이 어긋난다.
    /// </summary>
    public static class HeldRig
    {
        /// <summary>도구 하나가 손에 들리는 자리와 기울기(카메라 기준).</summary>
        public struct Pose
        {
            public Vector3 position;
            public Vector3 euler;
        }

        /// <summary>
        /// 도구 id 로 자리를 찾는다. 모르는 도구면 <paramref name="pose"/> 는 손대지 않고 false.
        ///
        /// 팀원이 도구를 하나 더 만들면 여기 한 줄을 보태면 된다 — 그러면 모든 사건에서
        /// 같은 자리에 들린다. 안 보태도 씬에 맞춰 둔 자리로 그냥 돈다.
        /// </summary>
        public static bool TryGet(string toolId, out Pose pose)
        {
            switch (toolId)
            {
                // 등불 — 오른손에 낮게 들고 앞을 비춘다. 팔을 뻗지 않는다(0.55m).
                case "lantern":
                    pose = new Pose { position = new Vector3(0.28f, -0.28f, 0.55f), euler = Vector3.zero };
                    return true;

                // 돋보기 — 오른손에 쥐고 자루가 아래로 가게 눕힌다. 눈에 댈 때
                // 유리가 바로 서도록 미리 90도 돌려 둔 것이다.
                case "magnify":
                    pose = new Pose { position = new Vector3(0.22f, -0.20f, 0.32f), euler = new Vector3(-45f, 0f, 90f) };
                    return true;
            }
            pose = default(Pose);
            return false;
        }

        /// <summary>
        /// 이 매단 자리에 공통 자세를 앉힌다. 아는 도구가 아니면 아무 일도 안 한다
        /// (씬에 맞춰 둔 자리가 그대로 남는다).
        /// </summary>
        public static void Apply(Transform holder, string toolId)
        {
            if (holder == null) return;
            Pose p;
            if (!TryGet(toolId, out p)) return;

            Vector3 pos = p.position;
            if (HudSide.LeftHanded) pos.x = -pos.x;   // 왼손잡이는 좌우를 뒤집는다
            holder.localPosition = pos;
            holder.localRotation = Quaternion.Euler(p.euler);
        }
    }
}
