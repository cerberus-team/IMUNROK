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
        /// <summary>도구 하나가 손에 들리는 자리와 기울기(카메라 기준), 그리고 <b>크기</b>.</summary>
        public struct Pose
        {
            public Vector3 position;
            public Vector3 euler;

            /// <summary>
            /// 손에 든 모델의 크기(모델 오브젝트의 localScale).
            ///
            /// <b>크기도 자세다.</b> 자리와 기울기만 맞추고 크기를 씬에 맡겨 두었더니
            /// 조사청의 등불이 옹고집전 것의 <b>절반</b>(8 대 21)이었고, 같은 방
            /// 문갑에 놓인 등불(19.5)보다도 작았다 — 집어 든 물건이 놓여 있던
            /// 물건보다 작아지는 꼴이다. 0 이면 씬에 맞춰 둔 크기를 그대로 둔다.
            /// </summary>
            public float scale;
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
                // 크기는 <b>문갑에 놓인 그 등불</b>(19.5)에 맞춘다. 집어 들었더니
                // 물건이 작아지면 든 것이 아니라 바뀐 것이 된다.
                case "lantern":
                    pose = new Pose { position = new Vector3(0.28f, -0.28f, 0.55f), euler = Vector3.zero, scale = 19.5f };
                    return true;

                // 돋보기 — 오른손에 쥐고 자루가 아래로 가게 눕힌다. 눈에 댈 때
                // 유리가 바로 서도록 미리 90도 돌려 둔 것이다.
                //
                // 크기를 0.933 에서 <b>0.68</b> 로 줄이고 한 뼘 더 밀어냈다(0.32 → 0.36).
                // 여태 40cm 짜리 돋보기를 눈에서 32cm 앞에 들고 있어 <b>화면의 반</b>을
                // 가렸다 — 손에 든 물건이 아니라 방패로 보였다. 28cm 를 36cm 앞에
                // 두면 손에 쥔 것으로 읽힌다.
                case "magnify":
                    pose = new Pose { position = new Vector3(0.21f, -0.17f, 0.36f), euler = new Vector3(-45f, 0f, 90f), scale = 0.68f };
                    return true;

                // 마패 — 오른손에 쥐고 <b>얼굴이 이쪽을 보게</b> 돌려 둔다. 새겨진 말이
                // 안 보이면 그것은 마패가 아니라 쇳조각이다. 내보이는 물건이므로
                // 등불보다 조금 높고 조금 가깝다.
                //
                // 크기는 1 — 모델을 <b>진짜 치수</b>(8.7 × 11cm)로 들여왔다. 여태
                // 도구들이 씬마다 다른 배율로 들려 있던 까닭이 모델이 제 크기가
                // 아니어서였으므로, 새로 들이는 것은 파일에서부터 맞춰 둔다.
                case "mapae":
                    pose = new Pose { position = new Vector3(0.20f, -0.13f, 0.36f), euler = new Vector3(12f, 195f, 5f), scale = 1f };
                    return true;

                // 유척 — 놋쇠 자. 앞으로 곧게 뻗으면 끝만 보이므로 <b>비스듬히</b> 뉜다.
                // 자는 눈금을 읽는 물건이라 길이가 보여야 한다.
                //
                // -28도로는 <b>모자랐다</b>. 자는 2cm × 1.4cm × 31cm 짜리 각봉인데,
                // 그 각도로는 31cm 가 거의 앞으로 뻗어 눈에는 <b>손가락만 한 막대</b>
                // 하나로 줄어들었다 — "유척이 이상하게 보인다"던 것이 이것이다.
                // 눈금을 읽는 물건이니 <b>길이가 가로로 놓여야</b> 한다. -72도로 눕히면
                // 31cm 가 시야를 가로질러, 각진 몸과 눈금이 다 보인다.
                case "yucheok":
                    pose = new Pose { position = new Vector3(0.17f, -0.16f, 0.38f), euler = new Vector3(-12f, -72f, 6f), scale = 1f };
                    return true;
            }
            pose = default(Pose);
            return false;
        }

        /// <summary>
        /// 이 매단 자리에 공통 자세를 앉힌다. 아는 도구가 아니면 아무 일도 안 한다
        /// (씬에 맞춰 둔 자리가 그대로 남는다).
        ///
        /// <paramref name="model"/> 을 주면 <b>크기까지</b> 맞춘다. 매단 자리가 아니라
        /// 모델에 거는 까닭: 매단 자리를 키우면 그 밑에 같이 달린 빛의 거리(range)까지
        /// 배로 늘어난다 — 등불 하나가 방 두 개를 밝히게 된다.
        /// </summary>
        public static void Apply(Transform holder, string toolId, Transform model = null)
        {
            if (holder == null) return;
            Pose p;
            if (!TryGet(toolId, out p)) return;

            Vector3 pos = p.position;
            if (HudSide.LeftHanded) pos.x = -pos.x;   // 왼손잡이는 좌우를 뒤집는다
            holder.localPosition = pos;
            holder.localRotation = Quaternion.Euler(p.euler);

            if (model != null && p.scale > 0f) model.localScale = Vector3.one * p.scale;
        }
    }
}
