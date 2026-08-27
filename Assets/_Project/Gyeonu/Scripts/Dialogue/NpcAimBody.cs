using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 조준 몸통 맞춤 (2026-08-27). NPC의 <see cref="CapsuleCollider"/> 를 <b>실제로 화면에 보이는
    /// 자세</b>에 맞춰 늘린다.
    ///
    /// ■ 왜 필요한가 — 수령에게 말을 걸 수 없던 이유
    ///   설치기(<c>NpcSetup.Capsule</c>)는 자세를 이름으로 가른다: 기본 자세 이름에 <c>Sit</c>·<c>Fall</c>
    ///   이 들어가면 "앉은 사람"이라 보고 1.35m, 아니면 1.95m를 준다. 그런데 <c>Magistrate_SittingIdle</c>
    ///   은 <b>이름과 달리 몸이 서 있을 때만큼 높다</b> — 실측 1.49m(월드 3.88~5.37), 캡슐 위끝은 5.01.
    ///   <b>머리와 갓 0.36m가 조준 몸통 밖으로 삐져나온다.</b> 사람은 상대의 얼굴을 겨누므로,
    ///   눈에 보이는 얼굴을 겨눈 광선이 아무것도 맞히지 않고 뒷벽으로 지나갔다. 가슴께를 겨눠야만
    ///   조준이 됐다 — 그래서 "수령만 대화창이 안 뜬다".
    ///   (같은 함정의 앞선 사례: <c>FestivalMerchant_SitTalk</c> 이 이름과 달리 서 있는 자세였다.)
    ///
    /// ■ 이름 대신 몸을 잰다
    ///   스킨 메시를 <b>지금 자세 그대로</b> 구워(<see cref="SkinnedMeshRenderer.BakeMesh"/>) 위·아래 끝을
    ///   재고, 캡슐이 그 몸을 덮도록 늘린다. 애니메이션 클립 이름을 믿지 않으므로 새 인물·새 모션이
    ///   들어와도 따로 손댈 곳이 없다.
    ///
    /// ■ ⚠️ 늘리기만 한다, 줄이지 않는다
    ///   지금 조준이 되는 사람은 그대로 두려는 것이다. 구운 치수가 캡슐보다 작아도 무시한다 —
    ///   쓰러진 선아처럼 몸이 낮게 눕는 자세에서 캡슐을 몸에 딱 맞추면, 지금 되던 조준이
    ///   좁아지는 쪽으로 바뀔 수 있다. 이 고침은 <b>못 맞히던 것을 맞히게</b> 만들 뿐이어야 한다.
    ///
    /// ■ ⚠️ 반지름은 건드리지 않는다
    ///   캡슐은 조준 판정이자 <b>몸통 충돌</b>이다. 옆으로 넓히면 플레이어가 대화 거리까지
    ///   다가서지 못하고 밀려난다 (상인의 <c>SitDrinking</c> 은 팔을 벌려 폭이 1m를 넘는다).
    /// </summary>
    public static class NpcAimBody
    {
        /// <summary>구울 그릇 하나를 돌려 쓴다 — 자세가 바뀔 때마다 새로 만들면 쓰레기가 쌓인다.</summary>
        static Mesh scratch;

        /// <summary>몸 위아래로 이만큼(m) 여유를 둔다. 갓 테두리처럼 얇은 끝을 겨눠도 걸리게.</summary>
        const float Margin = 0.04f;

        /// <returns>실제로 캡슐을 늘렸으면 true.</returns>
        public static bool Fit(GameObject go, CapsuleCollider cap)
        {
            if (go == null || cap == null) return false;

            var skins = go.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (skins.Length == 0) return false;

            if (scratch == null) scratch = new Mesh { name = "조준몸통_임시" };

            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var skin in skins)
            {
                if (skin.sharedMesh == null) continue;
                // ⚠️ useScale 을 <b>켜야</b> 한다. 이 모델들은 FBX 안이 cm 단위라, 끄고 구우면
                //    100배짜리 좌표가 나온다 (실측: 1.71m 사람이 171 로 찍혀 캡슐이 하늘까지 늘어났다).
                skin.BakeMesh(scratch, true);
                var b = scratch.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x,
                                             (i & 2) == 0 ? b.min.y : b.max.y,
                                             (i & 4) == 0 ? b.min.z : b.max.z);
                    float y = go.transform.InverseTransformPoint(skin.transform.TransformPoint(corner)).y;
                    if (y < lo) lo = y;
                    if (y > hi) hi = y;
                }
            }
            if (hi <= lo) return false;

            lo -= Margin;
            hi += Margin;

            float half = cap.height * 0.5f;
            float nowLo = cap.center.y - half, nowHi = cap.center.y + half;
            float newLo = Mathf.Min(nowLo, lo), newHi = Mathf.Max(nowHi, hi);
            if (newHi - newLo <= cap.height + 0.001f) return false;      // 늘릴 것이 없다

            cap.height = newHi - newLo;
            cap.center = new Vector3(cap.center.x, (newLo + newHi) * 0.5f, cap.center.z);
            return true;
        }
    }
}
