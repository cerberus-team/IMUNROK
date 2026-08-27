using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 타공 비밀지도(A1) 저작기 (2026-08-23). 멱등 — 여러 번 눌러도 같은 것이 나온다.
    ///
    /// ■ 무엇을 만드나
    ///   1) 종이 메시   Art/Models/Items/비밀지도_종이.asset
    ///      3등분·2등분으로 접혔던 자국이 남은 완만한 굴곡 + 들린 가장자리 + 삭은 테두리.
    ///      앞면(지도 그림)과 뒷면·옆면(한지)을 **서브메시로 갈라** 재질을 따로 준다 —
    ///      인벤토리에서 360도 돌려 보므로 뒤로 넘어가도 빈 데가 없어야 한다.
    ///   2) 별빛 길     Art/Models/Items/비밀지도_별빛길.asset (메시 12토막이 한 파일에)
    ///      첨부 지도에 그어진 길을 그대로 딴 경로. 관측실 천장 별과 **같은 셰이더**
    ///      (IMUNROK/별_가산)라 빛의 계열이 같다. 빨간 선은 자리 참고일 뿐 색은 별빛이다.
    ///   3) 타공        같은 경로 위에 뚫린 구멍 자국. 불이 없을 때도 종이에 남아 있다.
    ///   4) 프리팹      Prefabs/Items/비밀지도.prefab
    ///   5) 소지품 정의 Resources/GyeonuItems/Item_A1_타공비밀지도.asset
    ///
    /// ■ 원본 그림
    ///   SecretMap.png는 **gitignore**다 (팀원이 따로 받는다). 여기서는 읽기만 하고
    ///   손대지 않는다. 없으면 재질의 BaseMap만 비고 나머지는 그대로 만들어진다.
    ///
    /// ■ 왜 메시를 직접 굽나
    ///   접힌 자국·들린 가장자리·삭은 테두리는 판판한 Quad로는 안 나온다. 그렇다고 모델링
    ///   툴을 왕복하면 수치가 코드에 안 남는다. 격자 하나 굽는 정도라 스크립트가 낫다 —
    ///   값을 고치고 메뉴를 다시 누르면 끝이고, diff에 이유가 남는다.
    /// </summary>
    public static class SecretMapBuilder
    {
        const string Root = "Tools/이문록/소지품/";

        const string MapTexPath = "Assets/_Project/Gyeonu/Art/Textures/Items/SecretMap.png";
        const string GlowTexPath = "Assets/_Project/Gyeonu/Art/Textures/Observatory/T_관측실_별글로우.png";
        const string TexDir = "Assets/_Project/Gyeonu/Art/Textures/Items";
        const string MatDir = "Assets/_Project/Gyeonu/Art/Materials/Items";
        const string MeshDir = "Assets/_Project/Gyeonu/Art/Models/Items";
        const string PrefabDir = "Assets/_Project/Gyeonu/Prefabs/Items";
        const string ResDir = "Assets/_Project/Gyeonu/Resources/GyeonuItems";

        const string PaperMeshPath = MeshDir + "/비밀지도_종이.asset";
        const string PathMeshPath = MeshDir + "/비밀지도_별빛길.asset";
        const string HoleMeshPath = MeshDir + "/비밀지도_타공.asset";
        const string PrefabPath = PrefabDir + "/비밀지도.prefab";
        const string ItemPath = ResDir + "/Item_A1_타공비밀지도.asset";

        // ── 종이 치수 ──────────────────────────────────────
        const float PxW = 1672f, PxH = 941f;      // 원본 그림 픽셀 (16:9)
        const float W = 0.62f;                    // 펼친 가로 (m) — 두 손에 들리는 크기
        const float H = W * PxH / PxW;            // 0.349
        const float T = 0.0016f;                  // 두께. 얇되 옆면이 보이기는 해야 한다
        const int NX = 49, NZ = 29;               // 굴곡을 담을 격자

        // ── 별빛 길 ────────────────────────────────────────
        const int Segments = 12;                  // 앞에서 뒤로 차례로 밝히려고 나눠 둔 토막
        const float StarsPerMeter = 430f;
        const float LatHalf = 0.0055f;            // 길 반폭 (m) — 이만큼 좌우로 흩어진다
        const float HoleSpacing = 0.0085f;        // 구멍 간격 (m)
        /// <summary>소지품 상세 창(밝은 미리보기 무대)에서 쓰는 별빛 배수. 관측실 연출은 1로 되돌린다.</summary>
        const float PreviewBrightness = 6.0f;
        /// <summary>길이 밝을 때 종이에 먹이는 배수. 가산 별빛이 흰 종이에 묻히지 않게 자리를 내준다.</summary>
        const float PreviewPaperDim = 0.62f;

        /// <summary>
        /// 첨부된 지도에 그어진 길 — **원본 그림 픽셀 좌표**(1672×941, y는 위에서 아래).
        ///
        /// 왜 픽셀로 적나: 그림 위에서 눈으로 짚은 값이라 그림과 같은 좌표계로 남겨야
        /// 나중에 대조·수정이 된다. 종이 크기가 바뀌어도 이 표는 그대로 쓴다.
        /// (참고 그림의 빨간색은 자리를 알려 주려고 그은 것이다 — 색은 쓰지 않는다)
        /// </summary>
        static readonly Vector2[] PathPx =
        {
            new Vector2(1032f, 161f),   // 위쪽 마을 어귀에서 시작
            new Vector2(1068f, 178f), new Vector2(1088f, 208f), new Vector2(1092f, 245f),
            new Vector2(1078f, 285f), new Vector2(1050f, 318f), new Vector2(1012f, 348f),
            new Vector2( 990f, 372f), new Vector2( 988f, 396f), new Vector2(1012f, 416f),
            new Vector2(1050f, 432f), new Vector2(1085f, 452f), new Vector2(1103f, 478f),
            new Vector2(1100f, 510f), new Vector2(1078f, 540f), new Vector2(1044f, 566f),
            new Vector2(1005f, 586f), new Vector2( 962f, 600f), new Vector2( 918f, 610f),
            new Vector2( 876f, 620f),   // 못 위 다리께
            new Vector2( 836f, 634f), new Vector2( 792f, 650f), new Vector2( 752f, 668f),
            new Vector2( 718f, 690f), new Vector2( 700f, 713f), new Vector2( 704f, 732f),
            new Vector2( 730f, 744f), new Vector2( 778f, 750f), new Vector2( 830f, 750f),
            new Vector2( 876f, 744f), new Vector2( 918f, 736f), new Vector2( 952f, 742f),
            new Vector2( 988f, 760f), new Vector2(1026f, 782f), new Vector2(1068f, 802f),
            new Vector2(1112f, 815f), new Vector2(1156f, 816f), new Vector2(1198f, 806f),
            new Vector2(1236f, 795f), new Vector2(1262f, 790f),   // 아래쪽 마을에서 끝
        };

        // ═══════════════════════════════════════════════════
        [MenuItem(Root + "비밀지도 만들기 (타공 지도 + 별빛 길)", priority = 110)]
        public static InventoryItem Build()
        {
            EnsureFolder(TexDir); EnsureFolder(MatDir); EnsureFolder(MeshDir);
            EnsureFolder(PrefabDir); EnsureFolder(ResDir);

            var mapTex = AssetDatabase.LoadAssetAtPath<Texture2D>(MapTexPath);
            if (mapTex == null)
                Debug.LogWarning("[비밀지도] 그림을 못 찾았다 — " + MapTexPath +
                                 "\n  (gitignore 대상이라 각자 받아 넣어야 한다. 일단 무지 종이로 만든다)");

            var paper = SavePaper();
            var starMeshes = SaveStarPath();
            var holes = SaveHoles();

            var matFront = MatPaperFront(mapTex);
            var matBack = MatPaperBack();
            var matStar = MatStar();
            var matHole = MatHole();

            var prefab = BuildPrefab(paper, starMeshes, holes, matFront, matBack, matStar, matHole);
            var item = BuildItem(prefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[비밀지도] 준비 완료 — 종이 {W:F2}×{H:F2}m, 별 {LastStarCount}개 " +
                      $"({Segments}토막), 구멍 {LastHoleCount}개\n  {PrefabPath}\n  {ItemPath}");
            Selection.activeObject = item;
            return item;
        }

        static int LastStarCount, LastHoleCount;

        // ═══════════════════════════════════════════════════
        //  종이
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// 접혔다 펴진 종이의 높낮이 (m). 세로 접선 2개(3등분) + 가로 접선 1개(2등분)로
        /// 칸마다 번갈아 부풀고, 가장자리는 오래 접혀 있던 종이처럼 살짝 들린다.
        /// </summary>
        static float Relief(float u, float v)
        {
            float pu = u * 3f; int cu = Mathf.Clamp((int)pu, 0, 2); float fu = pu - cu;
            float hx = Mathf.Sin(fu * Mathf.PI) * ((cu & 1) == 0 ? 1f : -1f) * 0.0055f;

            float pv = v * 2f; int cv = Mathf.Clamp((int)pv, 0, 1); float fv = pv - cv;
            float hz = Mathf.Sin(fv * Mathf.PI) * ((cv & 1) == 0 ? -1f : 1f) * 0.0045f;

            // 가장자리 말림 — 테두리에서 3.5cm 안쪽까지 서서히
            float d = Mathf.Min(Mathf.Min(u, 1f - u) * W, Mathf.Min(v, 1f - v) * H);
            float e = Mathf.Clamp01(1f - d / 0.035f);
            float curl = e * e * (0.0085f + 0.0060f * Mathf.PerlinNoise(u * 9.3f + 4.1f, v * 7.1f + 2.7f));

            return hx + hz + curl;
        }

        static Mesh SavePaper()
        {
            int n = NX * NZ;
            var mid = new Vector3[n];
            var uv = new Vector2[n];

            for (int j = 0; j < NZ; j++)
                for (int i = 0; i < NX; i++)
                {
                    float u = i / (NX - 1f), v = j / (NZ - 1f);
                    float x = (u - 0.5f) * W, z = (v - 0.5f) * H;

                    // 삭은 테두리 — 바깥 줄만 안쪽으로 들쭉날쭉 밀어 넣는다.
                    // UV도 함께 밀어야 그림이 눌리지 않고 **잘려 나간 것**처럼 보인다.
                    bool rim = i == 0 || i == NX - 1 || j == 0 || j == NZ - 1;
                    if (rim)
                    {
                        float amt = 0.0005f + 0.0032f * Mathf.PerlinNoise(x * 38f + 13.7f, z * 38f + 5.1f);
                        if (i == 0) x += amt; else if (i == NX - 1) x -= amt;
                        if (j == 0) z += amt; else if (j == NZ - 1) z -= amt;
                        u = x / W + 0.5f; v = z / H + 0.5f;
                    }

                    int k = j * NX + i;
                    mid[k] = new Vector3(x, Relief(u, v), z);
                    uv[k] = new Vector2(u, v);
                }

            var verts = new List<Vector3>(n * 2 + 400);
            var uvs = new List<Vector2>(n * 2 + 400);
            var front = new List<int>(n * 6);
            var backSide = new List<int>(n * 6 + 900);

            // 앞면 (법선 +Y) — 지도 그림
            for (int k = 0; k < n; k++) { verts.Add(mid[k] + Vector3.up * (T * 0.5f)); uvs.Add(uv[k]); }
            for (int j = 0; j < NZ - 1; j++)
                for (int i = 0; i < NX - 1; i++)
                {
                    int a = j * NX + i, b = a + 1, c = a + NX, d = c + 1;
                    front.Add(a); front.Add(c); front.Add(b);
                    front.Add(c); front.Add(d); front.Add(b);
                }

            // 뒷면 (법선 -Y) — 한지. 감는 순서를 뒤집는다
            int off = verts.Count;
            for (int k = 0; k < n; k++)
            {
                verts.Add(mid[k] - Vector3.up * (T * 0.5f));
                uvs.Add(new Vector2(1f - uv[k].x, uv[k].y));   // 뒤에서 보면 좌우가 뒤집힌다
            }
            for (int j = 0; j < NZ - 1; j++)
                for (int i = 0; i < NX - 1; i++)
                {
                    int a = off + j * NX + i, b = a + 1, c = a + NX, d = c + 1;
                    backSide.Add(a); backSide.Add(b); backSide.Add(c);
                    backSide.Add(c); backSide.Add(b); backSide.Add(d);
                }

            // 옆면 — 테두리를 한 바퀴 돌며 앞뒤를 잇는다.
            // 정점을 새로 만든다: 앞뒤 것을 재활용하면 법선 평균이 앞면 가장자리까지 흐려
            // 테두리에 거뭇한 띠가 생긴다.
            var ring = new List<int>();
            for (int i = 0; i < NX; i++) ring.Add(i);                          // 아래 변
            for (int j = 1; j < NZ; j++) ring.Add(j * NX + NX - 1);            // 오른 변
            for (int i = NX - 2; i >= 0; i--) ring.Add((NZ - 1) * NX + i);     // 위 변
            for (int j = NZ - 2; j >= 1; j--) ring.Add(j * NX);                // 왼 변

            for (int r = 0; r < ring.Count; r++)
            {
                int k0 = ring[r], k1 = ring[(r + 1) % ring.Count];
                Vector3 f0 = mid[k0] + Vector3.up * (T * 0.5f), b0 = mid[k0] - Vector3.up * (T * 0.5f);
                Vector3 f1 = mid[k1] + Vector3.up * (T * 0.5f), b1 = mid[k1] - Vector3.up * (T * 0.5f);

                int s = verts.Count;
                verts.Add(f0); verts.Add(f1); verts.Add(b0); verts.Add(b1);
                for (int q = 0; q < 4; q++) uvs.Add(new Vector2(r / (float)ring.Count, q < 2 ? 1f : 0f));

                // 감는 방향은 **계산해서** 정한다. 테두리를 도는 순서만으로 맞히려 들면
                // 좌표계 방향을 한 번은 반드시 헷갈린다 — 바깥쪽을 향하는지 확인하고 뒤집는다.
                Vector3 nrm = Vector3.Cross(f1 - f0, b0 - f0);
                Vector3 outward = new Vector3(f0.x + f1.x, 0f, f0.z + f1.z) * 0.5f;
                bool flip = Vector3.Dot(nrm, outward) < 0f;
                if (flip) { backSide.Add(s); backSide.Add(s + 2); backSide.Add(s + 1); backSide.Add(s + 1); backSide.Add(s + 2); backSide.Add(s + 3); }
                else { backSide.Add(s); backSide.Add(s + 1); backSide.Add(s + 2); backSide.Add(s + 1); backSide.Add(s + 3); backSide.Add(s + 2); }
            }

            var mesh = new Mesh { name = "비밀지도_종이" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(front, 0);
            mesh.SetTriangles(backSide, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return SaveMesh(mesh, PaperMeshPath);
        }

        // ═══════════════════════════════════════════════════
        //  별빛 길 · 타공
        // ═══════════════════════════════════════════════════

        /// <summary>픽셀 좌표를 종이 위 지점으로. 앞면보다 조금 띄워 z-파이팅을 피한다.</summary>
        static Vector3 OnPaper(Vector2 px, float lift)
        {
            float u = px.x / PxW, v = 1f - px.y / PxH;      // 그림은 y가 아래로 간다
            return new Vector3((u - 0.5f) * W, Relief(u, v) + T * 0.5f + lift, (v - 0.5f) * H);
        }

        /// <summary>꺾인 점들을 캣멀롬으로 부드럽게 이어 촘촘한 점열로 만든다.</summary>
        static List<Vector3> SmoothPath(float lift)
        {
            var raw = new List<Vector3>(PathPx.Length);
            foreach (var p in PathPx) raw.Add(OnPaper(p, lift));

            var outPts = new List<Vector3>(raw.Count * 8);
            for (int i = 0; i < raw.Count - 1; i++)
            {
                Vector3 p0 = raw[Mathf.Max(0, i - 1)], p1 = raw[i];
                Vector3 p2 = raw[i + 1], p3 = raw[Mathf.Min(raw.Count - 1, i + 2)];
                for (int s = 0; s < 8; s++)
                    outPts.Add(CatmullRom(p0, p1, p2, p3, s / 8f));
            }
            outPts.Add(raw[raw.Count - 1]);
            return outPts;
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t
                 + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        /// <summary>점열을 누적거리로 훑을 수 있게 만든다.</summary>
        static float[] Cumulative(List<Vector3> pts)
        {
            var d = new float[pts.Count];
            for (int i = 1; i < pts.Count; i++) d[i] = d[i - 1] + Vector3.Distance(pts[i - 1], pts[i]);
            return d;
        }

        static void Sample(List<Vector3> pts, float[] dist, float d, out Vector3 pos, out Vector3 fwd)
        {
            int i = 1;
            while (i < pts.Count - 1 && dist[i] < d) i++;
            float seg = Mathf.Max(1e-5f, dist[i] - dist[i - 1]);
            float t = Mathf.Clamp01((d - dist[i - 1]) / seg);
            pos = Vector3.Lerp(pts[i - 1], pts[i], t);
            fwd = pts[i] - pts[i - 1]; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-8f) fwd = Vector3.forward; else fwd.Normalize();
        }

        /// <summary>
        /// 별 메시 12토막. 관측실 천장 별과 같은 정점 규약을 따른다 —
        /// COLOR = 색조(LDR), TEXCOORD1 = (반짝임 위상, 속도 지터, HDR 세기, 미사용).
        /// </summary>
        static Mesh[] SaveStarPath()
        {
            var pts = SmoothPath(0.0011f);
            var dist = Cumulative(pts);
            float total = dist[pts.Count - 1];
            int count = Mathf.Max(Segments * 4, Mathf.RoundToInt(total * StarsPerMeter));
            LastStarCount = count;

            var vs = new List<Vector3>[Segments];
            var us = new List<Vector2>[Segments];
            var tw = new List<Vector4>[Segments];
            var cs = new List<Color>[Segments];
            var ts = new List<int>[Segments];
            for (int s = 0; s < Segments; s++)
            {
                vs[s] = new List<Vector3>(); us[s] = new List<Vector2>();
                tw[s] = new List<Vector4>(); cs[s] = new List<Color>(); ts[s] = new List<int>();
            }

            for (int i = 0; i < count; i++)
            {
                float d = (i + 0.5f) / count * total;
                Sample(pts, dist, d, out Vector3 center, out Vector3 fwd);

                float r1 = Hash(d * 71.3f), r2 = Hash(d * 37.1f + 11f), r3 = Hash(d * 19.7f + 23f);

                // 옆으로 흩어 놓아야 자로 그은 선이 아니라 흘러가는 빛으로 읽힌다
                var right = new Vector3(-fwd.z, 0f, fwd.x);
                float lat = (r1 + r2 - 1f) * LatHalf;
                Vector3 pos = center + right * lat;

                // ⚠️ 크기·세기는 천장 별에서 그대로 가져오면 **안 된다** (2026-08-23 실측).
                //    천장 별은 5m 밖 검은 돔 위에 있고, 이것은 0.6m 앞 **밝은 종이 위**에 있다.
                //    처음엔 천장 값(작은 별 0.004m·세기 0.5)을 그대로 썼더니 종이 위에서
                //    점 몇 개만 겨우 보였다 — 별 텍스처는 심이 반지름의 1/7뿐이라 4mm 별의
                //    심은 화면에서 1픽셀도 못 채운다. 여린 별까지 보이게 올려야 **이어진
                //    빛줄기**로 읽힌다. 밝은 별은 그 위에 얹히는 악센트다.
                float size, hdr;
                if (r3 > 0.972f) { size = Mathf.Lerp(0.0240f, 0.0340f, r1); hdr = Mathf.Lerp(5.5f, 8.0f, r2); }
                else if (r3 > 0.82f) { size = Mathf.Lerp(0.0140f, 0.0200f, r1); hdr = Mathf.Lerp(3.0f, 4.5f, r2); }
                else { size = Mathf.Lerp(0.0070f, 0.0120f, r1); hdr = Mathf.Lerp(1.4f, 2.4f, r2); }
                // 중심에서 멀수록 여리게 — 가운데가 심지처럼 밝아진다
                float edge = 1f - Mathf.Abs(lat) / LatHalf;
                hdr *= Mathf.Lerp(0.45f, 1f, edge * edge);

                Color tint;
                if (r2 > 0.93f) tint = new Color(1.00f, 0.86f, 0.70f);
                else if (r2 > 0.62f) tint = new Color(0.78f, 0.86f, 1.00f);
                else if (r2 > 0.34f) tint = new Color(0.88f, 0.82f, 1.00f);
                else tint = Color.white;

                int s = Mathf.Clamp(i * Segments / count, 0, Segments - 1);
                int b = vs[s].Count;
                float h = size * 0.5f;
                vs[s].Add(pos + new Vector3(-h, 0f, -h));
                vs[s].Add(pos + new Vector3(h, 0f, -h));
                vs[s].Add(pos + new Vector3(-h, 0f, h));
                vs[s].Add(pos + new Vector3(h, 0f, h));
                us[s].Add(new Vector2(0, 0)); us[s].Add(new Vector2(1, 0));
                us[s].Add(new Vector2(0, 1)); us[s].Add(new Vector2(1, 1));
                var t4 = new Vector4(r1, r2, hdr, 0f);
                for (int q = 0; q < 4; q++) { tw[s].Add(t4); cs[s].Add(tint); }
                ts[s].Add(b + 0); ts[s].Add(b + 2); ts[s].Add(b + 1);
                ts[s].Add(b + 2); ts[s].Add(b + 3); ts[s].Add(b + 1);
            }

            var meshes = new Mesh[Segments];
            for (int s = 0; s < Segments; s++)
            {
                var m = new Mesh { name = $"비밀지도_별빛길_{s:00}" };
                m.SetVertices(vs[s]);
                m.SetUVs(0, us[s]);
                m.SetUVs(1, tw[s]);
                m.SetColors(cs[s]);
                m.SetTriangles(ts[s], 0);
                m.RecalculateBounds();
                meshes[s] = m;
            }
            return SaveMeshes(meshes, PathMeshPath);
        }

        /// <summary>
        /// 같은 경로에 뚫린 구멍 자국 — 불이 없을 때 지도에 남아 있는 것.
        /// **앞뒤 양쪽에** 찍는다. 뚫린 구멍이니 뒤집어 봐도 있어야 한다 — 인벤토리에서
        /// 360도 돌려 보는 물건이라 한쪽만 있으면 대번에 들킨다.
        /// </summary>
        static Mesh SaveHoles()
        {
            var front = SmoothPath(0.0004f);
            var dist = Cumulative(front);
            float total = dist[front.Count - 1];
            int count = Mathf.Max(4, Mathf.RoundToInt(total / HoleSpacing));
            LastHoleCount = count;

            var verts = new List<Vector3>(count * 8);
            var uvs = new List<Vector2>(count * 8);
            var tris = new List<int>(count * 12);

            for (int i = 0; i < count; i++)
            {
                float d = (i + 0.5f) / count * total;
                Sample(front, dist, d, out Vector3 pos, out Vector3 _);
                float r = Hash(d * 53.7f + 7f);
                float h = Mathf.Lerp(0.0011f, 0.0017f, r);      // 지름 2.2~3.4mm

                // 앞면 (법선 +Y)
                int b = verts.Count;
                verts.Add(pos + new Vector3(-h, 0f, -h));
                verts.Add(pos + new Vector3(h, 0f, -h));
                verts.Add(pos + new Vector3(-h, 0f, h));
                verts.Add(pos + new Vector3(h, 0f, h));
                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
                uvs.Add(new Vector2(0, 1)); uvs.Add(new Vector2(1, 1));
                tris.Add(b + 0); tris.Add(b + 2); tris.Add(b + 1);
                tris.Add(b + 2); tris.Add(b + 3); tris.Add(b + 1);

                // 뒷면 (법선 -Y) — 종이 두께만큼 내려가고 감는 순서를 뒤집는다
                Vector3 back = pos - new Vector3(0f, T + 0.0008f, 0f);
                int k = verts.Count;
                verts.Add(back + new Vector3(-h, 0f, -h));
                verts.Add(back + new Vector3(h, 0f, -h));
                verts.Add(back + new Vector3(-h, 0f, h));
                verts.Add(back + new Vector3(h, 0f, h));
                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
                uvs.Add(new Vector2(0, 1)); uvs.Add(new Vector2(1, 1));
                tris.Add(k + 0); tris.Add(k + 1); tris.Add(k + 2);
                tris.Add(k + 2); tris.Add(k + 1); tris.Add(k + 3);
            }

            var mesh = new Mesh { name = "비밀지도_타공" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return SaveMesh(mesh, HoleMeshPath);
        }

        static float Hash(float x)
        {
            float s = Mathf.Sin(x * 127.1f) * 43758.5453f;
            return s - Mathf.Floor(s);
        }

        // ═══════════════════════════════════════════════════
        //  재질
        // ═══════════════════════════════════════════════════

        static Material MatPaperFront(Texture2D tex)
        {
            var m = EnsureMat("M_비밀지도_앞", "Universal Render Pipeline/Lit");
            if (tex != null) m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.06f);      // 종이는 거의 반사가 없다
            m.SetFloat("_Metallic", 0f);

            // 아주 여린 자체 발광. 혼상 점등 뒤의 관측실은 **거의 암흑**이라
            // 빛에만 기대면 손에 든 지도가 통째로 검어진다 (2026-08-15 앰비언트 재하향 이후).
            // 연출 쪽에서 작은 등을 딸려 보내지만, 그것만으로는 가장자리가 묻힌다.
            if (tex != null) m.SetTexture("_EmissionMap", tex);
            // ⚠️ 알파를 1로 둘 것. `Color.white * 0.13f`는 알파까지 0.13이 되고,
            //    ⚠️ globalIlluminationFlags를 None으로 두면 **_EMISSION 키워드가 지워진다**
            //       (2026-08-23 실측 — 재질을 저장하면 URP 후처리가 GI 플래그를 보고 키워드를
            //       다시 세우는데, None이면 발광이 없는 것으로 보고 꺼 버린다. 값은 남아 있고
            //       화면에서만 안 빛나 원인이 잘 안 보인다). RealtimeEmissive로 둔다 —
            //       실시간 GI가 꺼진 프로젝트라 광원으로 새어 나가지는 않는다.
            m.SetColor("_EmissionColor", new Color(0.13f, 0.13f, 0.13f, 1f));
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material MatPaperBack()
        {
            var m = EnsureMat("M_비밀지도_뒤", "Universal Render Pipeline/Lit");
            m.SetTexture("_BaseMap", HanjiTex());
            m.SetColor("_BaseColor", new Color(0.847f, 0.804f, 0.694f));   // 연한 한지색
            m.SetTextureScale("_BaseMap", new Vector2(3f, 2f));
            m.SetFloat("_Smoothness", 0.04f);
            m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material MatStar()
        {
            var shader = Shader.Find("IMUNROK/별_가산");
            if (shader == null)
            {
                Debug.LogError("[비밀지도] IMUNROK/별_가산 셰이더가 없다 — 별빛 길이 제대로 안 뜬다");
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            var m = EnsureMat("M_비밀지도_별빛", shader.name);
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(GlowTexPath));
            m.SetFloat("_Intensity", 0f);          // 기본은 꺼짐 — 켜는 쪽이 MPB로 올린다
            m.SetFloat("_TwinkleAmp", 0.24f);      // 천장 별(0.12)보다 또렷하게 — 손안에서 보므로
            m.SetFloat("_TwinkleSpeed", 1.6f);
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material MatHole()
        {
            var m = EnsureMat("M_비밀지도_타공", "Universal Render Pipeline/Unlit");
            m.SetTexture("_BaseMap", DotTex());
            m.SetColor("_BaseColor", new Color(0.10f, 0.09f, 0.08f, 0.80f));
            // 투명 설정 — URP Unlit은 키워드·블렌드를 손으로 다 세워 줘야 한다
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_AlphaClip", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material EnsureMat(string name, string shaderName)
        {
            string path = $"{MatDir}/{name}.mat";
            var shader = Shader.Find(shaderName);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader) m.shader = shader;
            return m;
        }

        /// <summary>한지 결 — 은은한 섬유 줄무늬. 뒷장이 밋밋한 색판으로 보이지 않게.</summary>
        static Texture2D HanjiTex()
        {
            const int res = 256;
            string path = TexDir + "/T_비밀지도_한지.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    // 가로로 길게 늘인 잡음 = 뜬 종이의 섬유결.
                    // ⚠️ 처음엔 x*0.35로 잡았더니 결이 아니라 **잡음 줄무늬**로 보였다 —
                    //    이 텍스처는 0.2m마다 반복되므로 화면에서 매우 촘촘해진다.
                    //    주파수를 낮추고 진폭도 절반으로 줄여야 종이로 읽힌다 (2026-08-23).
                    float fib = Mathf.PerlinNoise(x * 0.085f, y * 0.014f) * 0.55f
                              + Mathf.PerlinNoise(x * 0.030f + 31f, y * 0.006f + 7f) * 0.30f
                              + Mathf.PerlinNoise(x * 0.012f + 5f, y * 0.012f + 19f) * 0.15f;
                    float v = Mathf.Clamp01(0.86f + (fib - 0.5f) * 0.16f);
                    tex.SetPixel(x, y, new Color(v, v * 0.985f, v * 0.955f, 1f));
                }
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>구멍 하나 — 가장자리가 부드러운 둥근 점.</summary>
        static Texture2D DotTex()
        {
            const int res = 32;
            string path = TexDir + "/T_비밀지도_구멍.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            float c = (res - 1) * 0.5f;
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    float dx = (x - c) / c, dy = (y - c) / c;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(a * 2.4f));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.alphaIsTransparency = true;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ═══════════════════════════════════════════════════
        //  프리팹 · 소지품 정의
        // ═══════════════════════════════════════════════════

        static GameObject BuildPrefab(Mesh paper, Mesh[] starMeshes, Mesh holes,
                                      Material front, Material back, Material star, Material hole)
        {
            var root = new GameObject("비밀지도");
            var mf = root.AddComponent<MeshFilter>();
            mf.sharedMesh = paper;
            var mr = root.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { front, back };   // 서브메시 0=앞, 1=뒤·옆

            // 조준용 상자 — 실제 종이보다 조금 넉넉하게 (씬에 놓을 때 ItemPickup이 쓴다)
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0f, 0f);
            box.size = new Vector3(W + 0.02f, 0.030f, H + 0.02f);

            var holeGo = new GameObject("타공");
            holeGo.transform.SetParent(root.transform, false);
            holeGo.AddComponent<MeshFilter>().sharedMesh = holes;
            var hr = holeGo.AddComponent<MeshRenderer>();
            hr.sharedMaterial = hole;
            NoLighting(hr);

            var pathGo = new GameObject("별빛길");
            pathGo.transform.SetParent(root.transform, false);
            var comp = pathGo.AddComponent<SecretMapStarPath>();

            var rends = new Renderer[starMeshes.Length];
            for (int s = 0; s < starMeshes.Length; s++)
            {
                var go = new GameObject($"구간_{s:00}");
                go.transform.SetParent(pathGo.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = starMeshes[s];
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = star;
                NoLighting(r);
                r.enabled = false;                  // 평소에는 보이지 않는다
                rends[s] = r;
            }
            comp.segments = rends;

            // **길을 밝히기 전에는 구멍뿐, 밝힌 뒤에는 지도에 새겨진 것처럼 남는다** (2026-08-23 개정).
            // 처음엔 이 칸을 비워 두었다 — "주머니 속에서 빛나면 약속이 깨진다"는 이유였다.
            // 그러나 약속은 "혼상의 빛을 받아야 **처음** 이어진다"는 것이지, 한 번 드러난 길이
            // 다시 사라진다는 것이 아니다. 본 것은 사라지지 않는다.
            comp.visibleFlag = GyeonuWorld.F_타공지도_길밝힘;

            // ⚠️ 소지품 미리보기 무대는 점광원 셋(6.0/2.2/3.0)이 종이를 0.78까지 끌어올린다.
            //    가산 별빛은 바탕이 밝으면 묻히므로 상세 창 기준으로 배수를 올려 둔다.
            //    거의 암흑인 관측실 연출은 SecretMapReveal이 이 값을 1로 되돌려 쓴다.
            comp.brightness = PreviewBrightness;
            comp.paperRenderer = mr;
            comp.paperDimWhenLit = PreviewPaperDim;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static void NoLighting(Renderer r)
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        static InventoryItem BuildItem(GameObject prefab)
        {
            var item = AssetDatabase.LoadAssetAtPath<InventoryItem>(ItemPath);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<InventoryItem>();
                AssetDatabase.CreateAsset(item, ItemPath);
            }

            item.itemId = SecretMapUse.ItemId;         // "A1"
            item.displayName = "타공 비밀지도";
            item.description =
                "견우에게서 받은 지도. 성하리 일대가 먹으로 그려져 있고, 종이는 여러 번 접혔다 " +
                "펴진 자국으로 결이 갈라져 있다.\n\n" +
                "그림 자체는 여느 지도와 다를 것이 없다. 다만 어느 한 줄을 따라 바늘로 뚫은 듯한 " +
                "작은 구멍이 성글게 이어져 있다. 빛에 비추어 보아야 무엇인지 알 수 있을 듯하다.\n\n" +
                "가장자리는 삭아 부스러지고, 뒷장에는 아무것도 적혀 있지 않다.\n\n" +
                "(임시 설명 — 실제 문안이 정해지면 이 글만 바꾸면 된다)";
            item.modelPrefab = prefab;
            // 앞면이 +Y다. X를 -90도 돌리면 앞면이 -Z(미리보기 카메라 쪽)를 본다.
            // Z에 살짝 기울기를 주어 손에 든 것처럼 — 축 순서(ZXY)라 그림만 3도 돈다.
            item.previewEuler = new Vector3(-90f, 0f, -3f);
            item.previewZoom = 1.45f;                  // 가로가 긴 물건이라 정사각 그림틀에 여백이 남는다 — 더 채운다
            item.usable = true;
            item.useLabel = "펼쳐보기";
            item.useNotReadyHint = "펼쳐 보아도 성글게 뚫린 구멍뿐이다. 지금은 아무것도 보이지 않는다.";
            item.pickupVerb = "받기";                  // 견우가 건네주는 물건
            item.autoShowOnPickup = true;
            item.journalKey = "secret_map";
            item.journalText = "견우에게 받은 타공 비밀지도 — 한 줄을 따라 바늘 구멍이 이어져 있다.";
            item.worldFlag = GyeonuWorld.F_비밀지도획득;

            EditorUtility.SetDirty(item);
            return item;
        }

        // ═══════════════════════════════════════════════════
        //  저장 도우미
        // ═══════════════════════════════════════════════════

        static Mesh SaveMesh(Mesh mesh, string path)
        {
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old != null)
            {
                // 같은 에셋을 덮어써야 프리팹·씬의 참조가 끊기지 않는다 (GUID 유지)
                old.Clear();
                EditorUtility.CopySerialized(mesh, old);
                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(old);
                return old;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>여러 메시를 한 파일에 담는다 (별빛 길 12토막이 폴더를 어지르지 않게).</summary>
        static Mesh[] SaveMeshes(Mesh[] meshes, string path)
        {
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(meshes[0], path);
            for (int i = 1; i < meshes.Length; i++) AssetDatabase.AddObjectToAsset(meshes[i], path);
            // ⚠️ 여기서 ImportAsset을 부르면 안 된다 — 방금 붙인 부속 메시들이 아직 디스크에
            //    없는 상태로 다시 읽어 들여 "Importer generated inconsistent result" 오류가 난다
            //    (2026-08-23 실측). 먼저 저장해서 디스크와 메모리를 맞춘다.
            AssetDatabase.SaveAssets();
            return meshes;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
