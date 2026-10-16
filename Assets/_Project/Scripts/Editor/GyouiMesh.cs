using System.Collections.Generic;
using UnityEngine;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>교의(交椅)를 그물째 짓는다.</b> 어사가 앉아 판결하는 의자다.
    ///
    /// 꾸러미에 조선 의자가 없다 — 있는 것은 서양 집 의자(<c>BK_AlchemistHouse</c>)와
    /// 등받이 없는 걸상뿐이다. 그러니 짓는데, <b>큐브를 쌓아서는 안 된다</b>:
    /// 큐브는 여섯 면이 저마다 UV 0~1 이라 0.54m 짜리 좌판과 0.05m 짜리 다리에
    /// 같은 나뭇결 그림이 통째로 늘어나고 쭈그러들어, 한 의자가 대여섯 가지 나무로
    /// 보인다(동헌 돌계단에서 이미 겪었다).
    ///
    /// 그래서 <b>상자 여럿을 한 그물로 굽고 UV 는 면의 법선을 보고 딴다</b> —
    /// 어느 면이든 결이 같은 크기(<see cref="Tile"/>)로 앉는다.
    ///
    /// <b>재질은 이어 붙는 결이라야 한다.</b> 낙안읍성 평상 재질을 입혔더니 의자가
    /// 검은 얼룩투성이가 됐다 — 그것은 그 평상 하나를 위해 구운 <b>아틀라스</b>라
    /// 자리마다 딴 부위가 들어 있다. 화성행궁의 <c>MI_KoreanWood_*</c> 처럼 넓은 면에
    /// 쓰는 결을 골라야 한다.
    ///
    /// <b>치수는 몸에서 나온다.</b> 앉은 자세(Sitting_Idle)의 엉덩이가 바닥에서 0.55 이고
    /// 엉덩이뼈는 골반 속이라, 앉는 널은 <b>0.46</b> 이 맞다(<see cref="CourtSeat"/> 에서
    /// 걸상으로 이미 잰 값이다). 등받이 꼭대기 1.12, 팔걸이 0.73 — 앉은 사람의 팔꿈치 높이다.
    ///
    /// 만든 의자는 <b>제 앞(local +z)</b>을 본다. 뜰을 보게 하려면 뿌리를 yaw 270 으로.
    ///
    /// <b>콜라이더는 안 붙인다.</b> <see cref="PlayerSeat"/> 는 앉을 자리 밑으로 광선을
    /// 쏘아 바닥을 찾고 거기에 앉은 눈높이를 얹는데, 의자가 걸리면 눈이 좌판 위로
    /// 한 뼘 더 올라가 <b>선 키보다 높이</b> 앉는다. 막는 몸은 부르는 쪽에서 따로 붙인다.
    /// </summary>
    public static class GyouiMesh
    {
        /// <summary>좌판 윗면(바닥에서 m).</summary>
        public const float SeatTop = 0.46f;

        /// <summary>등받이 꼭대기.</summary>
        public const float Height = 1.12f;

        /// <summary>나뭇결 한 장의 크기(m).</summary>
        private const float Tile = 0.42f;

        private const float HW = 0.27f;    // 반너비(x)
        private const float HD = 0.24f;    // 반깊이(z)
        private const float Leg = 0.05f;
        private const float Plank = 0.05f;

        /// <summary>교의 한 벌. 상자 열넷을 한 그물로 굽는다.</summary>
        public static Mesh Build()
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();

            float lx = HW - Leg * 0.5f;        // 다리 한가운데 x
            float lz = HD - Leg * 0.5f;        // 다리 한가운데 z
            float legTop = SeatTop - Plank;    // 좌판 밑

            // 좌판
            Box(v, n, uv, t, new Vector3(0f, SeatTop - Plank * 0.5f, 0f), new Vector3(HW * 2f, Plank, HD * 2f));

            // 다리 넷
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    Box(v, n, uv, t, new Vector3(sx * lx, legTop * 0.5f, sz * lz), new Vector3(Leg, legTop, Leg));

            // 가로대 — 옆 둘, 앞뒤 둘. 높이를 어긋나게 두어야 서로 안 겹친다.
            for (int sx = -1; sx <= 1; sx += 2)
                Box(v, n, uv, t, new Vector3(sx * lx, 0.13f, 0f), new Vector3(0.035f, 0.035f, lz * 2f));
            for (int sz = -1; sz <= 1; sz += 2)
                Box(v, n, uv, t, new Vector3(0f, 0.19f, sz * lz), new Vector3(lx * 2f, 0.035f, 0.035f));

            // 등받이 기둥 둘
            float backZ = -lz;
            for (int sx = -1; sx <= 1; sx += 2)
                Box(v, n, uv, t, new Vector3(sx * lx, (SeatTop + Height) * 0.5f, backZ),
                    new Vector3(Leg, Height - SeatTop, Leg));

            // 등받이 — 아래 띠 · 위 널 · 사이의 널쪽
            Box(v, n, uv, t, new Vector3(0f, 0.72f, backZ), new Vector3(lx * 2f, 0.09f, 0.04f));
            Box(v, n, uv, t, new Vector3(0f, 1.045f, backZ), new Vector3(HW * 2f, 0.15f, 0.055f));
            Box(v, n, uv, t, new Vector3(0f, 0.8675f, backZ), new Vector3(0.16f, 0.205f, 0.03f));

            // 팔걸이 — 앞 기둥과 널
            for (int sx = -1; sx <= 1; sx += 2)
            {
                Box(v, n, uv, t, new Vector3(sx * lx, 0.585f, lz - 0.03f), new Vector3(0.045f, 0.25f, 0.045f));
                Box(v, n, uv, t, new Vector3(sx * lx, 0.732f, -0.005f), new Vector3(0.06f, 0.045f, lz * 2f + 0.03f));
            }

            var m = new Mesh();
            m.name = "교의";
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }

        /// <summary>상자 하나 — 면마다 따로 꼭짓점을 두어야 모서리가 안 뭉갠다.</summary>
        private static void Box(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t,
                                Vector3 c, Vector3 s)
        {
            Vector3 h = s * 0.5f;
            Vector3[] dirs =
            {
                Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back,
            };
            foreach (var d in dirs)
            {
                // 이 면의 네 귀퉁이 — 법선 축을 뺀 나머지 둘로 사각형을 그린다
                Vector3 a, b;
                if (Mathf.Abs(d.x) > 0.5f) { a = new Vector3(0f, 0f, 1f); b = new Vector3(0f, 1f, 0f); }
                else if (Mathf.Abs(d.y) > 0.5f) { a = new Vector3(1f, 0f, 0f); b = new Vector3(0f, 0f, 1f); }
                else { a = new Vector3(1f, 0f, 0f); b = new Vector3(0f, 1f, 0f); }

                Vector3 ha = Vector3.Scale(a, h), hb = Vector3.Scale(b, h), hn = Vector3.Scale(d, h);
                Vector3 p0 = c + hn - ha - hb;
                Vector3 p1 = c + hn + ha - hb;
                Vector3 p2 = c + hn + ha + hb;
                Vector3 p3 = c + hn - ha + hb;

                // 감김이 바깥을 보게. d 와 (a×b) 의 부호가 어긋나면 뒤집는다.
                bool flip = Vector3.Dot(Vector3.Cross(a, b), d) < 0f;
                int i0 = v.Count;
                v.Add(p0); v.Add(p1); v.Add(p2); v.Add(p3);
                for (int k = 0; k < 4; k++) n.Add(d);
                uv.Add(Plane(p0, d)); uv.Add(Plane(p1, d)); uv.Add(Plane(p2, d)); uv.Add(Plane(p3, d));
                if (flip) { t.Add(i0); t.Add(i0 + 2); t.Add(i0 + 1); t.Add(i0); t.Add(i0 + 3); t.Add(i0 + 2); }
                else { t.Add(i0); t.Add(i0 + 1); t.Add(i0 + 2); t.Add(i0); t.Add(i0 + 2); t.Add(i0 + 3); }
            }
        }

        /// <summary>
        /// 면이 가장 크게 향한 축을 빼고 나머지 둘로 UV 를 딴다 —
        /// 좌판 윗면은 (x,z), 앞뒤는 (x,y), 옆은 (z,y). 큐브의 0~1 UV 를 쓰면
        /// 면 크기에 따라 결이 늘거나 쭈그러든다.
        /// </summary>
        private static Vector2 Plane(Vector3 p, Vector3 nrm)
        {
            float ax = Mathf.Abs(nrm.x), ay = Mathf.Abs(nrm.y), az = Mathf.Abs(nrm.z);
            if (ay >= ax && ay >= az) return new Vector2(p.x, p.z) / Tile;
            if (ax >= az) return new Vector2(p.z, p.y) / Tile;
            return new Vector2(p.x, p.y) / Tile;
        }
    }
}
