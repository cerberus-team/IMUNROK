using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Gyeonu.EditorTools
{
    /// <summary>
    /// 종막 장부 — ④ 찬장 문 떼어내기 · ⑤ 선반 짜기 · ⑦ 단서 소지품.
    /// (①~③ 재질·프리팹·한지 굽기는 <c>ArchiveLedgerBuilder.cs</c> 에 있다)
    /// </summary>
    public static partial class ArchiveLedgerBuilder
    {
        // 찬장 메시 로컬 좌표 (raw × 100 = m). X=폭 Y=깊이(앞이 음수) Z=높이
        const float 상칸z0 = 1.085f, 상칸z1 = 1.566f;
        const float 하칸z0 = 0.587f, 하칸z1 = 1.069f;
        /// <summary>
        /// **문널 자체**의 z 대역 (원본 메시를 훑어 실측). 칸(위)보다 좁다 —
        /// 칸 경계 0.558·0.603 / 1.051·1.102 / 1.549·1.597 중 가운데 두 값이 문널의 위아래 끝이고,
        /// 그 바깥은 문틀 가로대다. 대역을 칸으로 잡으면 가로대가 문에 딸려 나간다.
        /// </summary>
        const float 상문z0 = 1.102f, 상문z1 = 1.549f;
        const float 하문z0 = 0.603f, 하문z1 = 1.051f;
        /// <summary>
        /// **두 짝 문**이 차지하는 z 대역 — 아래 문널 밑끝부터 위 문널 윗끝까지 통째로.
        /// 가운데 가로대(1.051~1.102)도 문에 딸려 간다: 문을 열었을 때 앞이 막힘없이 트여야
        /// 장롱문처럼 읽히고, 안쪽 진열판도 가로대에 잘리지 않는다.
        /// 위·아래 가로대(0.558~0.603 / 1.549~1.597)는 문틀이므로 몸통에 남긴다.
        /// </summary>
        const float 문z0 = 0.603f, 문z1 = 1.549f;
        const float 문반폭 = 0.362f;

        // ── 사용자가 씬에 놓아 준 표시 (2026-08-25) ──────────
        // 「갈라지는 부분」 2개 · 「힌지」 4개를 찬장 로컬로 환산한 값. 위·아래 칸에 한 번씩
        // 찍혀 있고 X가 5mm 안쪽으로 일치하므로 **가름선 하나 · 힌지선 둘**로 읽는다.
        // ⚠️ 이 수치는 손대지 않는다. 세 번째 시도이고, 앞의 두 번은 내가 자리를 스스로
        //    잡다가 틀렸다. 문널의 실제 삼각형이 표시보다 몇 mm 넘칠 수 있으므로 **허용치만**
        //    준다 — 표시를 딱 자르는 선으로 쓰면 문널 가장자리가 잘려 다시 쪼개져 보인다.
        const float 힌지X_좌 = 0.3566f;    // 화면 왼쪽 (찬장 로컬 +X) — 표시 +0.3581 / +0.3551
        const float 힌지X_우 = -0.3672f;   // 화면 오른쪽 (찬장 로컬 −X) — 표시 −0.3647 / −0.3697
        const float 가름X = -0.0011f;      // 표시 −0.0024 / +0.0002
        const float 힌지Z = 0.3440f;       // 표시 0.3436 / 0.3437 (문 두께 한가운데)
        const float 문아래Y = 0.5997f;     // 아래 칸 표시의 밑끝
        const float 문위Y = 1.5469f;       // 위 칸 표시의 윗끝
        const float 표시허용 = 0.012f;

        const float 여는각 = 112f;       // 장롱문처럼 활짝
        /// <summary>문짝으로 인정할 깊이 한계 (메시 로컬 y). 문널은 −0.38~−0.31에 있고
        /// 뒷판·내부 상자는 +0.01 이상이라 −0.15에서 자르면 안전하게 갈린다.</summary>
        const float 문깊이한계 = -0.15f;

        // ── ④ 문짝 떼어내기 ──────────────────────────────────
        /// <summary>
        /// <c>SM_Pantry_Chest_01</c> 은 문짝까지 **한 덩어리로 구워진** 메시다. 여닫으려면
        /// 문짝을 삼각형 단위로 떼어 따로 메시를 만들고, 몸통에서는 그만큼 지워야 한다.
        ///
        /// ■ 두 짝으로 가른다 (2026-08-25 개정)
        ///   처음에는 칸마다(상·하 × 좌·우) 넷으로 떼었는데, 조각이 잘아 열 때 쪼개져 보였다.
        ///   지금은 **앞면을 가운데에서 세로로 한 번만** 가른다 — 위아래 칸을 아우르는 큰 널
        ///   두 짝이 바깥 끝단 경첩을 축으로 장롱문처럼 활짝 열린다.
        ///   가르는 조건: 세 꼭짓점이 모두 |x| ≤ 0.362, z ∈ [0.603, 1.549], 앞쪽(y ≤ −0.15).
        ///   좌·우는 무게중심의 x 부호로만 나눈다.
        ///   쇠붙이 메시 <c>_02</c> 는 전부 경첩과 손잡이라 x 부호로만 갈라 딸려 보낸다.
        ///
        /// ⚠️ 원본 FBX는 손대지 않는다. 새 메시를 굽고 씬의 MeshFilter만 갈아 끼운다.
        /// </summary>
        static FurnitureParts SplitChestDoors(GameObject chest)
        {
            var body = chest.transform.Find("SM_Pantry_Chest_01");
            var trim = chest.transform.Find("SM_Pantry_Chest_02");
            if (body == null) return null;

            // ⚠️ 이미 떼어 놓았어도 **매번 다시 자른다** (2026-08-25 수정).
            //    처음에는 "문짝 그룹이 있으면 그대로 쓴다"로 두었는데, 자르는 규칙을 고쳐도
            //    메시가 갱신되지 않아 고친 것이 반영되지 않았다. <see cref="OriginalMesh"/>가
            //    늘 원본 FBX에서 다시 읽으므로 몇 번을 돌려도 결과가 같다.
            var existing = chest.GetComponent<FurnitureParts>();
            var oldDoors = chest.transform.Find("문짝");
            if (oldDoors != null) Object.DestroyImmediate(oldDoors.gameObject);

            var srcBody = OriginalMesh(body.GetComponent<MeshFilter>().sharedMesh, "SM_Pantry_Chest_01");
            var srcTrim = trim != null ? OriginalMesh(trim.GetComponent<MeshFilter>().sharedMesh, "SM_Pantry_Chest_02") : null;

            var doorsRoot = new GameObject("문짝");
            doorsRoot.transform.SetParent(chest.transform, false);

            var leaves = new List<Transform>();
            var used = new HashSet<int>();
            var usedTrim = new HashSet<int>();

            // 속 상자 앞판 두 장 — **버리지 않는다.** 몸통 메시 안에서 서브메시만 갈라 두고,
            // 문이 열려 있는 동안 재질만 투명으로 바꾼다 (ChestFrontPane)
            var pane = FindShellFrontTriangles(srcBody);

            // 칸마다 떼던 시절의 메시(찬장문_00 …)를 치운다 — 이제 두 짝(찬장문_0·1)만 쓴다
            foreach (var g in AssetDatabase.FindAssets("찬장", new[] { 메시폴더 }))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                var nm = System.IO.Path.GetFileNameWithoutExtension(p);
                if ((nm.StartsWith("찬장문_") || nm.StartsWith("찬장쇠_")) && nm.Length > 5 && nm != "찬장쇠_나머지")
                    AssetDatabase.DeleteAsset(p);
            }

            // 좌·우 두 짝 — **표시한 가름선**에서 한 번만 가른다
            //   side 0 = 화면 왼쪽(찬장 로컬 +X, 가름선~힌지X_좌)
            //   side 1 = 화면 오른쪽(찬장 로컬 −X, 힌지X_우~가름선)
            float 밴드아래 = 문아래Y - 표시허용, 밴드위 = 문위Y + 표시허용;
            float 폭좌 = 힌지X_좌 + 표시허용, 폭우 = 힌지X_우 - 표시허용;
            for (int side = 0; side < 2; side++)
            {
                float x0 = side == 0 ? 가름X : 폭우, x1 = side == 0 ? 폭좌 : 가름X;
                var leafMesh = Extract(srcBody, x0, x1, 폭우, 폭좌, 밴드아래, 밴드위, 문깊이한계, true, used,
                    메시폴더 + "/찬장문_" + side + ".asset");
                if (leafMesh == null) continue;

                var leaf = new GameObject("문짝_" + (side == 0 ? "좌" : "우"));
                leaf.transform.SetParent(doorsRoot.transform, false);

                MakeChestPart(leaf.transform, "널", leafMesh, body.GetComponent<MeshRenderer>().sharedMaterial);
                AddDoorPanel(leaf.transform, leafMesh);
                if (srcTrim != null)
                {
                    // 쇠붙이는 경첩·손잡이뿐이라 세로를 넉넉히 잡아도 다른 것이 딸려 오지 않는다
                    var trimMesh = Extract(srcTrim, x0, x1, 폭우, 폭좌, 0.50f, 1.65f, 문깊이한계, false, usedTrim,
                        메시폴더 + "/찬장쇠_" + side + ".asset");
                    if (trimMesh != null)
                        MakeChestPart(leaf.transform, "쇠", trimMesh, trim.GetComponent<MeshRenderer>().sharedMaterial);
                }
                leaves.Add(leaf.transform);
            }

            // 몸통 — 문짝으로 간 삼각형을 뺀 나머지. 속 상자 앞판 두 장은 **서브메시 1**로 간다
            var bodyMr = body.GetComponent<MeshRenderer>();
            var woodMat = bodyMr.sharedMaterial;
            body.GetComponent<MeshFilter>().sharedMesh =
                ExtractRestSplit(srcBody, used, pane, 메시폴더 + "/찬장몸통.asset");
            bodyMr.sharedMaterials = new[] { woodMat, woodMat };   // 닫힌 상태 = 원본과 같은 재질
            if (trim != null && srcTrim != null)
                trim.GetComponent<MeshFilter>().sharedMesh =
                    ExtractRest(srcTrim, usedTrim, 메시폴더 + "/찬장쇠_나머지.asset");

            // 여닫이 배선 — 경첩은 **사용자가 표시한 자리** 그대로, 축은 찬장 로컬 수직(Y)
            var fp = existing != null ? existing : chest.AddComponent<FurnitureParts>();
            fp.displayName = "찬장";
            fp.duration = 1.0f;
            fp.parts = new List<FurnitureParts.Part>();
            foreach (var leaf in leaves)
            {
                bool right = leaf.name.EndsWith("우");
                // 화면 오른쪽 = 찬장 로컬 −X. 자유 모서리가 앞(+Z)으로 나오는 부호를 고른다.
                fp.parts.Add(new FurnitureParts.Part
                {
                    node = leaf,
                    axisInParent = Vector3.up,
                    pivotInParent = new Vector3(right ? 힌지X_우 : 힌지X_좌, 0f, 힌지Z),
                    openAngle = right ? -여는각 : 여는각,
                });
            }

            // 앞판 재질 전환기 — 문이 완전히 닫혀 있을 때만 나뭇결, 그 밖에는 투명
            var paneCtl = chest.GetComponent<ChestFrontPane>();
            if (paneCtl == null) paneCtl = chest.AddComponent<ChestFrontPane>();
            paneCtl.doors = fp;
            paneCtl.body = bodyMr;
            paneCtl.slot = 1;
            paneCtl.closedMaterial = woodMat;
            paneCtl.openMaterial = PaneCutoutMaterial(srcBody, pane, woodMat);
            return fp;
        }

        // 뚫을 자리 (찬장 로컬). 문 구멍보다 4mm 넓게 잡아 **이음매가 문틀 기둥·가로대 뒤로** 숨는다.
        const float 컷반폭 = 0.360f;
        const float 컷아래Y = 0.600f, 컷위Y = 1.552f;

        /// <summary>
        /// 문이 열려 있는 동안 앞판에 씌울 **알파 컷아웃 재질** (2026-08-25).
        ///
        /// ■ 왜 통째로 투명하면 안 되는가
        ///   앞판 한 장은 X ±0.654 로 문 구멍(±0.356)보다 1.8배 넓다. 통째로 비우면
        ///   **문 구멍 바깥 가장자리까지 뚫려** 찬장 옆구리가 비쳐 보인다.
        ///
        /// ■ 어떻게 문 구멍만 뚫는가
        ///   앞판의 UV가 위치의 **완전한 아핀 사상**이라(실측 오차 0), 문 구멍이 UV에서도
        ///   반듯한 사각형으로 떨어진다. 그 사각형만 알파 0인 베이스맵 사본을 굽고 알파 클립을 켠다.
        ///   RGB는 원본에서 **바이트 단위로 그대로 베끼고** 노멀맵은 원본 에셋을 그대로 쓰므로,
        ///   뚫리지 않은 가장자리는 원본과 같은 나뭇결·같은 음영으로 남는다.
        ///
        /// ⚠️ u 계수가 음수다(+x 가 작은 u 로 간다). 좌우가 뒤집히므로 사각형은 반드시
        ///    **네 모서리를 다 환산해 min/max** 로 잡는다 — 부호를 가정하면 반대쪽이 뚫린다.
        /// ⚠️ 원본 텍스처는 임포터를 고치지 않고 **파일에서 직접 읽는다**
        ///    (isReadable 을 켜면 gitignore 된 원본이 더럽혀진다).
        /// </summary>
        static Material PaneCutoutMaterial(Mesh src, HashSet<int> paneTris, Material wood)
        {
            string mp = 재질폴더 + "/M_찬장_앞판_열림.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (m == null) { m = new Material(wood); AssetDatabase.CreateAsset(m, mp); }
            m.shader = wood.shader;
            m.CopyPropertiesFromMaterial(wood);
            m.shaderKeywords = wood.shaderKeywords;   // _NORMALMAP 등을 그대로 물려받는다

            var mask = BakePaneMask(src, paneTris, wood);
            if (mask != null) m.SetTexture("_BaseMap", mask);
            m.SetTextureScale("_BaseMap", Vector2.one);
            m.SetTextureOffset("_BaseMap", Vector2.zero);

            // 알파 클립 — 투명 블렌드가 아니라 잘라내기라 정렬 문제가 없다
            m.SetFloat("_Surface", 0f);          // Opaque
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", 0.5f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "TransparentCutout");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>원본 베이스맵을 그대로 베끼고 문 구멍 자리만 알파 0으로 만든 사본을 굽는다.</summary>
        static Texture2D BakePaneMask(Mesh src, HashSet<int> paneTris, Material wood)
        {
            var srcTex = wood.GetTexture("_BaseMap") as Texture2D;
            if (srcTex == null) { Debug.LogWarning("[종막 장부] 찬장 베이스맵을 찾지 못했다"); return null; }
            string assetPath = AssetDatabase.GetAssetPath(srcTex);
            string diskPath = Application.dataPath + assetPath.Substring("Assets".Length);
            if (!System.IO.File.Exists(diskPath)) { Debug.LogWarning("[종막 장부] 원본 텍스처 파일 없음: " + diskPath); return null; }

            // ── 앞판의 UV ↔ 위치 아핀 계수를 **실측한다** (수치를 박아 두지 않는다)
            if (!SolvePaneUv(src, paneTris, out Vector3 cu, out Vector3 cv)) return null;

            // 네 모서리를 모두 환산해 min/max — 부호가 뒤집혀도 안전하다
            float umin = 9f, umax = -9f, vmin = 9f, vmax = -9f;
            foreach (float x in new[] { -컷반폭, 컷반폭 })
                foreach (float z in new[] { 컷아래Y, 컷위Y })
                {
                    float u = cu.x * x + cu.y * z + cu.z;
                    float vv = cv.x * x + cv.y * z + cv.z;
                    umin = Mathf.Min(umin, u); umax = Mathf.Max(umax, u);
                    vmin = Mathf.Min(vmin, vv); vmax = Mathf.Max(vmax, vv);
                }

            var read = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!read.LoadImage(System.IO.File.ReadAllBytes(diskPath)))
            { Object.DestroyImmediate(read); Debug.LogWarning("[종막 장부] 텍스처를 읽지 못했다"); return null; }

            int W = read.width, H = read.height;
            int x0 = Mathf.Clamp(Mathf.RoundToInt(umin * W), 0, W - 1);
            int x1 = Mathf.Clamp(Mathf.RoundToInt(umax * W), 0, W - 1);
            int y0 = Mathf.Clamp(Mathf.RoundToInt(vmin * H), 0, H - 1);
            int y1 = Mathf.Clamp(Mathf.RoundToInt(vmax * H), 0, H - 1);

            // RGB 는 바이트 단위로 그대로, 알파만 손댄다
            var px = read.GetPixels32();
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var c = px[y * W + x]; c.a = 0; px[y * W + x] = c;
                }
            // 나머지는 확실히 불투명으로 (원본에 알파가 없으면 이미 255다)
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    if (x < x0 || x > x1 || y < y0 || y > y1)
                    { var c = px[y * W + x]; c.a = 255; px[y * W + x] = c; }
            read.SetPixels32(px);
            read.Apply();

            string outPath = 텍스처폴더 + "/T_찬장앞판_마스크.png";
            System.IO.File.WriteAllBytes(Application.dataPath + outPath.Substring("Assets".Length), read.EncodeToPNG());
            Object.DestroyImmediate(read);

            AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(outPath);
            if (imp != null)
            {
                imp.textureType = TextureImporterType.Default;
                imp.sRGBTexture = true;
                imp.alphaSource = TextureImporterAlphaSource.FromInput;
                imp.alphaIsTransparency = false;   // 색 확산(dilate)으로 RGB가 바뀌지 않게
                imp.mipmapEnabled = true;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.maxTextureSize = Mathf.Max(W, H);
                imp.textureCompression = TextureImporterCompression.CompressedHQ;
                imp.SaveAndReimport();
            }
            Debug.Log("[종막 장부] 앞판 마스크 구움 — 뚫은 자리 픽셀 x " + x0 + "~" + x1 + ", y " + y0 + "~" + y1
                      + " / 텍스처 " + W + "x" + H);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
        }

        /// <summary>앞판의 uv = A·x + B·z + C 계수를 실측한다 (찬장 로컬 X·Y 기준).</summary>
        static bool SolvePaneUv(Mesh src, HashSet<int> paneTris, out Vector3 cu, out Vector3 cv)
        {
            cu = cv = Vector3.zero;
            var v = src.vertices; var t = src.triangles; var uv = src.uv;
            var idx = new List<int>();
            foreach (int ti in paneTris)
                for (int k = 0; k < 3; k++)
                    if (!idx.Contains(t[ti * 3 + k])) idx.Add(t[ti * 3 + k]);
            if (idx.Count < 3) return false;

            // 메시 로컬 (x, z) = 찬장 로컬 (X, Y)
            for (int a = 0; a < idx.Count; a++)
                for (int b = a + 1; b < idx.Count; b++)
                    for (int c = b + 1; c < idx.Count; c++)
                    {
                        Vector2 pa = new Vector2(v[idx[a]].x * 100f, v[idx[a]].z * 100f);
                        Vector2 pb = new Vector2(v[idx[b]].x * 100f, v[idx[b]].z * 100f);
                        Vector2 pc = new Vector2(v[idx[c]].x * 100f, v[idx[c]].z * 100f);
                        float det = (pb.x - pa.x) * (pc.y - pa.y) - (pc.x - pa.x) * (pb.y - pa.y);
                        if (Mathf.Abs(det) < 1e-5f) continue;
                        cu = Affine(pa, pb, pc, uv[idx[a]].x, uv[idx[b]].x, uv[idx[c]].x, det);
                        cv = Affine(pa, pb, pc, uv[idx[a]].y, uv[idx[b]].y, uv[idx[c]].y, det);
                        // 검산 — 나머지 정점이 같은 식을 따르는지
                        float worst = 0f;
                        foreach (int i in idx)
                        {
                            float px = v[i].x * 100f, pz = v[i].z * 100f;
                            worst = Mathf.Max(worst, Mathf.Abs(cu.x * px + cu.y * pz + cu.z - uv[i].x));
                            worst = Mathf.Max(worst, Mathf.Abs(cv.x * px + cv.y * pz + cv.z - uv[i].y));
                        }
                        if (worst > 1e-4f) { Debug.LogWarning("[종막 장부] 앞판 UV가 아핀이 아니다 (오차 " + worst + ")"); return false; }
                        return true;
                    }
            return false;
        }

        static Vector3 Affine(Vector2 pa, Vector2 pb, Vector2 pc, float fa, float fb, float fc, float det)
        {
            float A = ((fb - fa) * (pc.y - pa.y) - (fc - fa) * (pb.y - pa.y)) / det;
            float B = ((fc - fa) * (pb.x - pa.x) - (fb - fa) * (pc.x - pa.x)) / det;
            return new Vector3(A, B, fa - A * pa.x - B * pa.y);
        }

        /// <summary>
        /// 속 상자(몸통과 떨어져 있는 연결 성분)의 **앞면 두 삼각형** 번호.
        /// 법선이 −Y, 즉 방을 향하는 면이다. 원본 실측: 652 · 653 (찬장 로컬 Z = +0.332).
        /// </summary>
        static HashSet<int> FindShellFrontTriangles(Mesh src)
        {
            var result = new HashSet<int>();
            int shell = FindShellGroup(src, out System.Func<int, int> group);
            if (shell < 0) return result;
            var tri = src.triangles; var nor = src.normals;
            for (int i = 0; i < tri.Length / 3; i++)
            {
                if (group(i) != shell) continue;
                var n = (nor[tri[i * 3]] + nor[tri[i * 3 + 1]] + nor[tri[i * 3 + 2]]).normalized;
                if (n.y < -0.5f) result.Add(i);
            }
            return result;
        }

        /// <summary>
        /// 문짝에 **널판을 대어 준다** (2026-08-25).
        ///
        /// ⚠️ 원본 찬장의 문짝은 **속이 빈 테두리**다. 닫혔을 때 어둡게 보이던 판은 문이 아니라
        ///    바로 뒤에 선 **속 상자의 앞면**이었다. 그 상자를 걷어내면(문을 열면 안이 보여야 하므로
        ///    걷어내야 한다) 문짝이 그림틀처럼 뻥 뚫려 닫아도 속이 비쳐 보인다.
        ///    그래서 짝마다 널판을 하나씩 대어 준다 — 테두리 **뒤쪽**에 붙여 앞면의 문살은 그대로 남긴다.
        ///
        /// 좌표: 문짝 GameObject의 축은 찬장 로컬(X=폭, Y=높이, Z=앞)이고,
        /// 메시 로컬은 (X=폭, Y=깊이·앞이 음수, Z=높이)다. 그래서 x→x, z→y, −y→z 로 옮긴다.
        /// </summary>
        static void AddDoorPanel(Transform leaf, Mesh leafMesh)
        {
            var b = leafMesh.bounds;
            float cx = b.center.x * 100f;
            float cy = b.center.z * 100f;
            float cz = -b.center.y * 100f;
            float depth = b.size.y * 100f;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "널판";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(leaf, false);
            // 테두리 뒤쪽 — 앞면에서 두께의 3/4만큼 물러난 자리
            go.transform.localPosition = new Vector3(cx, cy, cz - depth * 0.34f);
            go.transform.localScale = new Vector3(b.size.x * 100f - 0.006f, b.size.z * 100f - 0.006f, 0.012f);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = LedgerWood("M_찬장_문널판", new Color(0.082f, 0.056f, 0.040f));
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static void MakeChestPart(Transform parent, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            // 원본 자식과 같은 자세 — 메시가 찬장 로컬 좌표에 그대로 앉는다
            go.transform.localRotation = Quaternion.Euler(270f, 0f, 0f);
            go.transform.localScale = Vector3.one * 100f;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
        }

        /// <summary>이미 잘라 둔 메시가 꽂혀 있어도 **원본 FBX 메시**를 찾아 온다 (멱등 재실행).</summary>
        static Mesh OriginalMesh(Mesh current, string subName)
        {
            if (current != null && AssetDatabase.GetAssetPath(current).EndsWith(".fbx")) return current;
            foreach (var g in AssetDatabase.FindAssets("SM_Pantry_Chest t:Model"))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(p))
                    if (o is Mesh m && m.name == subName) return m;
            }
            return current;
        }

        /// <summary>
        /// 영역 안 삼각형만 새 메시로. 쓴 삼각형 번호는 <paramref name="used"/>에 적어 둔다.
        ///
        /// ⚠️ **무게중심만 보면 안 된다** (2026-08-25 실측, 두 번 물렸다).
        ///    찬장 뒷판과 내부 상자에는 폭 1.22~1.31m짜리 거대한 삼각형이 있는데, 그 무게중심이
        ///    문짝 칸 한가운데에 떨어진다. 그대로 딸려 나가 문을 열면 그 큰 면이 함께 돌아
        ///    **찬장이 쪼개지는 것처럼** 보였다.
        ///    ① 깊이 한계(y)를 걸어 뒷면 것들을 걸러 냈지만, 내부 상자의 **앞면**(y −0.332)은
        ///       그 검사를 통과해 한 장씩 남았다.
        ///    ② 그래서 **세 꼭짓점이 모두** 칸 안에 있어야 문짝으로 친다. 문널 삼각형은 제 칸을
        ///       넘지 않고, 칸을 가로지르는 큰 면은 반드시 꼭짓점 하나가 밖으로 나간다.
        ///    좌·우를 가르는 것은 여전히 무게중심이다(두 문널은 x=0에서 딱 갈라진다).
        /// </summary>
        /// <summary>
        /// 속 상자의 **앞면 한 장만** 버린다 (2026-08-25 개정).
        ///
        /// 원본 메시에는 몸통(664삼각형)과 별개로 **12삼각형짜리 상자**(1.31 × 0.68 × 0.99)가
        /// 들어 있다. 문이 닫힌 찬장의 "어두운 속"을 흉내 내는 물건인데, 그 **앞면 한 장**이
        /// 위아래 칸 개구부를 통째로 덮어(x −0.65~0.65) 문을 열어도 닫힌 것처럼 보이게 한다.
        ///
        /// ⚠️ 그렇다고 **상자를 통째로 버리면 안 된다** — 옆·뒤·위아래 다섯 면이 찬장 속을
        ///    막아 주는 가림막이라, 없애면 문 옆으로 속이 뚫려 보인다(사용자 지적).
        ///    앞면(법선이 −Y, 즉 방을 향하는 면)만 골라 버리고 나머지는 그대로 둔다.
        ///    닫혔을 때의 어두운 판은 문짝마다 대는 널판(<see cref="AddDoorPanel"/>)이 대신한다.
        /// </summary>
        static void DropShellFrontFace(Mesh src, HashSet<int> used)
        {
            int shell = FindShellGroup(src, out System.Func<int, int> group);
            if (shell < 0) return;
            var v = src.vertices; var tri = src.triangles; var nor = src.normals;
            int dropped = 0, kept = 0;
            for (int i = 0; i < tri.Length / 3; i++)
            {
                if (group(i) != shell) continue;
                var n = (nor[tri[i * 3]] + nor[tri[i * 3 + 1]] + nor[tri[i * 3 + 2]]).normalized;
                if (n.y < -0.5f) { used.Add(i); dropped++; }   // 방을 향하는 면
                else kept++;
            }
            Debug.Log("[종막 장부] 속 상자 앞면 " + dropped + "삼각형 제거, 가림막 " + kept + "삼각형 유지");
        }

        /// <summary>몸통 말고 **다른 연결 성분**(= 속 상자)의 번호. 못 찾으면 −1.</summary>
        static int FindShellGroup(Mesh src, out System.Func<int, int> group)
        {
            var v = src.vertices; var tri = src.triangles;
            var weld = new Dictionary<string, int>();
            var rep = new int[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                string k = Mathf.RoundToInt(v[i].x * 200000f) + "_" + Mathf.RoundToInt(v[i].y * 200000f)
                         + "_" + Mathf.RoundToInt(v[i].z * 200000f);
                if (!weld.TryGetValue(k, out int r)) { r = i; weld[k] = i; }
                rep[i] = r;
            }
            var parent = new int[v.Length];
            for (int i = 0; i < v.Length; i++) parent[i] = i;
            System.Func<int, int> find = null;
            find = x => { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; };
            System.Action<int, int> uni = (a, b) => { int ra = find(rep[a]), rb = find(rep[b]); if (ra != rb) parent[ra] = rb; };
            for (int i = 0; i < tri.Length; i += 3) { uni(tri[i], tri[i + 1]); uni(tri[i + 1], tri[i + 2]); }

            var count = new Dictionary<int, int>();
            for (int i = 0; i < tri.Length / 3; i++)
            {
                int g = find(rep[tri[i * 3]]);
                count[g] = count.TryGetValue(g, out int c) ? c + 1 : 1;
            }
            int biggest = -1, best = -1, other = -1;
            foreach (var kv in count) if (kv.Value > best) { best = kv.Value; biggest = kv.Key; }
            foreach (var kv in count) if (kv.Key != biggest) other = kv.Key;
            group = i => find(rep[tri[i * 3]]);
            return other;
        }

        /// <summary>
        /// (안 씀 — 참고용) 찬장 안을 **통째로 막고 있는 속 상자**를 버린다.
        ///
        /// 원본 메시에는 몸통(664삼각형)과 별개로 **12삼각형짜리 상자**(1.31 × 0.68 × 0.99)가 들어
        /// 있다. 문이 닫힌 찬장의 "어두운 속"을 값싸게 흉내 내는 물건인데, 그 **앞면 한 장**이
        /// 위아래 칸 개구부를 통째로 덮는다(x −0.65~0.65, z 0.59~1.58). 문짝을 아무리 정확히
        /// 떼어내도 이 판이 뒤에 그대로 서 있어 **문이 열려도 닫힌 것처럼** 보였다 —
        /// 문짝을 통째로 숨겨 봐도 그대로여서 원인이 잘 안 보였다.
        /// 우리는 그 자리에 진열판을 따로 세우므로 이 상자는 쓸 데가 없다.
        ///
        /// 찾는 법: 몸통 메시의 연결 성분 중 **가장 큰 것이 아닌 것**. 형태·크기를 조건으로
        /// 걸면 에셋이 바뀔 때 조용히 빗나가지만, "몸통 말고 나머지"는 뜻이 분명하다.
        /// </summary>
        static void DropInteriorShell(Mesh src, HashSet<int> used)
        {
            var v = src.vertices; var tri = src.triangles;
            var weld = new Dictionary<string, int>();
            var rep = new int[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                string k = Mathf.RoundToInt(v[i].x * 200000f) + "_" + Mathf.RoundToInt(v[i].y * 200000f)
                         + "_" + Mathf.RoundToInt(v[i].z * 200000f);
                if (!weld.TryGetValue(k, out int r)) { r = i; weld[k] = i; }
                rep[i] = r;
            }
            var parent = new int[v.Length];
            for (int i = 0; i < v.Length; i++) parent[i] = i;
            System.Func<int, int> find = null;
            find = x => { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; };
            System.Action<int, int> uni = (a, b) => { int ra = find(rep[a]), rb = find(rep[b]); if (ra != rb) parent[ra] = rb; };
            for (int i = 0; i < tri.Length; i += 3) { uni(tri[i], tri[i + 1]); uni(tri[i + 1], tri[i + 2]); }

            var count = new Dictionary<int, int>();
            for (int i = 0; i < tri.Length / 3; i++)
            {
                int g = find(rep[tri[i * 3]]);
                count[g] = count.TryGetValue(g, out int c) ? c + 1 : 1;
            }
            int biggest = -1, best = -1;
            foreach (var kv in count) if (kv.Value > best) { best = kv.Value; biggest = kv.Key; }

            int dropped = 0;
            for (int i = 0; i < tri.Length / 3; i++)
                if (find(rep[tri[i * 3]]) != biggest) { used.Add(i); dropped++; }
            if (dropped > 0) Debug.Log("[종막 장부] 찬장 속 상자 " + dropped + "삼각형을 걷어냈다 (몸통 " + best + ")");
        }

        static Mesh Extract(Mesh src, float x0, float x1, float spanMin, float spanMax,
                            float z0, float z1, float yMax, bool rejectFlatEdge,
                            HashSet<int> used, string path)
        {
            const float eps = 0.002f;
            var v = src.vertices; var tri = src.triangles;
            var cand = new List<int>();
            float lo = Mathf.Min(spanMin, spanMax), hi = Mathf.Max(spanMin, spanMax);
            float sideLo = Mathf.Min(x0, x1), sideHi = Mathf.Max(x0, x1);

            // ① 문 전체 범위 안에 **세 꼭짓점이 모두** 든 앞면 삼각형만 후보로
            for (int i = 0; i < tri.Length / 3; i++)
            {
                if (used.Contains(i)) continue;
                var a = v[tri[i * 3]] * 100f; var b = v[tri[i * 3 + 1]] * 100f; var c2 = v[tri[i * 3 + 2]] * 100f;
                bool inside = true;
                foreach (var p in new[] { a, b, c2 })
                    if (p.x < lo - eps || p.x > hi + eps || p.z < z0 || p.z > z1 || p.y > yMax) { inside = false; break; }
                if (!inside) continue;
                var cen = (a + b + c2) / 3f;
                if (cen.x > sideLo && cen.x < sideHi) cand.Add(i);
            }
            if (cand.Count == 0) return null;

            // ② 문널의 **실제 위아래 끝**을 후보에서 재고, 그 끝에 딱 붙은 납작한 삼각형을 버린다.
            //    그것은 문틀 가로대의 윗면·아랫면이지 문널이 아니다 (문널의 납작한 면은 홈 자리인
            //    안쪽에만 있다). ⚠️ 표시 대역의 경계로 재면 안 된다 — 표시는 손으로 놓은 것이라
            //    실제 널 끝과 몇 mm 어긋나고, 그러면 가로대 면이 문에 딸려 가 구멍이 남는다.
            float realLo = float.MaxValue, realHi = float.MinValue;
            foreach (int i in cand)
                for (int k = 0; k < 3; k++)
                {
                    float z = v[tri[i * 3 + k]].z * 100f;
                    realLo = Mathf.Min(realLo, z); realHi = Mathf.Max(realHi, z);
                }

            var take = new List<int>();
            foreach (int i in cand)
            {
                if (rejectFlatEdge)
                {
                    var a = v[tri[i * 3]] * 100f; var b = v[tri[i * 3 + 1]] * 100f; var c2 = v[tri[i * 3 + 2]] * 100f;
                    float zmin = Mathf.Min(a.z, Mathf.Min(b.z, c2.z));
                    float zmax = Mathf.Max(a.z, Mathf.Max(b.z, c2.z));
                    if (zmax - zmin < 0.001f &&
                        (Mathf.Abs(zmin - realLo) < eps || Mathf.Abs(zmin - realHi) < eps)) continue;
                }
                take.Add(i); used.Add(i);
            }
            return take.Count == 0 ? null : Rebuild(src, take, path);
        }

        static Mesh ExtractRest(Mesh src, HashSet<int> used, string path)
        {
            var take = new List<int>();
            for (int i = 0; i < src.triangles.Length / 3; i++) if (!used.Contains(i)) take.Add(i);
            return Rebuild(src, take, path);
        }

        /// <summary>
        /// 남은 삼각형으로 몸통을 짓되, <paramref name="pane"/>에 든 것만 **서브메시 1**로 뺀다.
        ///
        /// 버리는 것이 아니다 — 삼각형도 정점도 전부 그대로 있고, 인덱스가 두 뭉치로 나뉠 뿐이다.
        /// 그래야 그 두 장에만 다른 재질을 씌워 문이 열렸을 때 비쳐 보이게 할 수 있다.
        /// </summary>
        static Mesh ExtractRestSplit(Mesh src, HashSet<int> used, HashSet<int> pane, string path)
        {
            var main = new List<int>(); var second = new List<int>();
            for (int i = 0; i < src.triangles.Length / 3; i++)
            {
                if (used.Contains(i)) continue;
                (pane.Contains(i) ? second : main).Add(i);
            }
            return Rebuild(src, main, second, path);
        }

        static Mesh Rebuild(Mesh src, List<int> tris, string path) => Rebuild(src, tris, null, path);

        /// <summary>
        /// 고른 삼각형으로 새 메시를 짓는다. <paramref name="second"/>를 주면 그것만 서브메시 1로.
        /// 정점 좌표·법선·UV는 원본에서 **그대로 베낀다** — 다시 계산하지 않는다.
        /// </summary>
        static Mesh Rebuild(Mesh src, List<int> tris, List<int> second, string path)
        {
            // ⚠️ **정점 속성을 하나도 빠뜨리면 안 된다** (2026-08-25 실측).
            //    처음엔 위치·법선·UV만 베꼈는데, 찬장 재질 M_JL_찬장01 은 노멀맵을 쓴다
            //    (`_NORMALMAP` 키워드). 탄젠트가 없으면 접선 공간이 무너져 **같은 재질인데도
            //    음영이 통째로 달라진다** — 닫힌 찬장이 원본보다 훨씬 밝게 떴다.
            //    탄젠트·UV2~4·정점색까지 원본에서 그대로 옮긴다.
            var sv = src.vertices; var sn = src.normals; var stan = src.tangents;
            var su = src.uv; var su2 = src.uv2; var su3 = src.uv3; var su4 = src.uv4;
            var scol = src.colors; var st = src.triangles;

            var map = new Dictionary<int, int>();
            var v = new List<Vector3>(); var n = new List<Vector3>(); var tg = new List<Vector4>();
            var u = new List<Vector2>(); var u2 = new List<Vector2>(); var u3 = new List<Vector2>(); var u4 = new List<Vector2>();
            var col = new List<Color>();
            var idxA = new List<int>(); var idxB = new List<int>();

            System.Action<List<int>, List<int>> emit = (source, into) =>
            {
                if (source == null) return;
                foreach (int t in source)
                    for (int k = 0; k < 3; k++)
                    {
                        int o = st[t * 3 + k];
                        if (!map.TryGetValue(o, out int ni))
                        {
                            ni = v.Count; map[o] = ni;
                            v.Add(sv[o]);
                            if (sn.Length > o) n.Add(sn[o]);
                            if (stan.Length > o) tg.Add(stan[o]);
                            if (su.Length > o) u.Add(su[o]);
                            if (su2.Length > o) u2.Add(su2[o]);
                            if (su3.Length > o) u3.Add(su3[o]);
                            if (su4.Length > o) u4.Add(su4[o]);
                            if (scol.Length > o) col.Add(scol[o]);
                        }
                        into.Add(ni);
                    }
            };
            emit(tris, idxA);
            emit(second, idxB);

            var mesh = new Mesh { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            mesh.SetVertices(v);
            if (n.Count == v.Count) mesh.SetNormals(n);
            if (tg.Count == v.Count) mesh.SetTangents(tg);
            if (u.Count == v.Count) mesh.SetUVs(0, u);
            if (u2.Count == v.Count) mesh.SetUVs(1, u2);
            if (u3.Count == v.Count) mesh.SetUVs(2, u3);
            if (u4.Count == v.Count) mesh.SetUVs(3, u4);
            if (col.Count == v.Count) mesh.SetColors(col);
            mesh.subMeshCount = idxB.Count > 0 ? 2 : 1;
            mesh.SetTriangles(idxA, 0);
            if (idxB.Count > 0) mesh.SetTriangles(idxB, 1);
            if (n.Count != v.Count) mesh.RecalculateNormals();
            if (tg.Count != v.Count) mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            return StoreMesh(mesh, path);
        }

        /// <summary>제거 메뉴 — 찬장을 원본 메시로 되돌린다.</summary>
        static void RestoreChest()
        {
            var chest = GameObject.Find(찬장경로);
            if (chest == null) return;
            var doors = chest.transform.Find("문짝");
            if (doors != null) Object.DestroyImmediate(doors.gameObject);
            var fp = chest.GetComponent<FurnitureParts>();
            if (fp != null) Object.DestroyImmediate(fp);
            var pane = chest.GetComponent<ChestFrontPane>();
            if (pane != null) Object.DestroyImmediate(pane);
            foreach (string sub in new[] { "SM_Pantry_Chest_01", "SM_Pantry_Chest_02" })
            {
                var t = chest.transform.Find(sub);
                if (t == null) continue;
                var mf = t.GetComponent<MeshFilter>();
                var orig = OriginalMesh(null, sub);
                if (orig != null) mf.sharedMesh = orig;
                // 서브메시를 갈랐던 것도 되돌린다 — 재질 칸 하나로
                var mr = t.GetComponent<MeshRenderer>();
                if (mr != null && mr.sharedMaterials.Length > 1)
                    mr.sharedMaterials = new[] { mr.sharedMaterials[0] };
            }
        }

        // ── ⑤ 선반·자리 ─────────────────────────────────────
        static LedgerPuzzle BuildBoard(GameObject chest,
                                       Dictionary<int, GameObject> pieces,
                                       Dictionary<int, GameObject> labels,
                                       Dictionary<int, GameObject> records,
                                       Dictionary<int, GameObject> tags,
                                       FurnitureParts doors)
        {
            var old = GameObject.Find(루트이름);
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject(루트이름);
            root.transform.SetParent(chest.transform, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;

            // ⚠️ 찬장 원본 재질을 그대로 쓰면 안 된다 — 그 텍스처는 **아틀라스**라, 네모 상자의
            //    0~1 UV가 판자·창살이 뒤섞인 자리를 통째로 물어 온다(뒷판에 검은 살대 숲이 섰다, 실측).
            //    민무늬 재질 둘로 나눈다: 뒷판은 어둡게, 선반틀은 밝게 — 기물이 배경에서 떠오른다.
            var wood = LedgerWood("M_장부_선반", new Color(0.223f, 0.145f, 0.098f));
            var backWood = LedgerWood("M_장부_뒷판", new Color(0.118f, 0.078f, 0.055f));

            // 진열판 — 뒷판 + 선반널 + 칸막이
            var shelf = new GameObject("진열판");
            shelf.transform.SetParent(root.transform, false);
            // 뒷판은 한 단 물러나 있다 — 그래야 표찰 홈이 그 앞에 **패인 자국**으로 보인다.
            // (같은 평면에 두었더니 홈이 뒷판에 묻혀 머리 줄이 그냥 빈 판으로 보였다, Play 실측)
            // 속 상자를 걷어냈으므로 뒷판이 개구부를 **빈틈없이** 덮어야 한다 — 폭을 안쪽 벽까지
            Board(shelf.transform, "뒷판", 0f, 1.075f, 판Z - 0.018f, 안쪽폭 + 0.020f, 1.000f, 0.012f, backWood);
            // ⚠️ 선반널은 **찬장 안쪽 벽까지** 이어져야 한다 (2026-08-25 수정).
            //    ① 처음엔 폭 0.60을 화면 x −0.06에 두었는데 **부호가 반대**였다. 격자 네 열의
            //       가운데는 화면 x **+0.06** 이라, 널이 0.12m 밀려 오른쪽 끝에 닿지 못하고
            //       왼쪽으로는 행머리 칸 위로 삐져나와 **끊어져 보였다.**
            //    ② 이제 아예 안쪽 개구부 전체(폭 0.716, 가운데 0)를 덮는다 — 행머리 칸까지
            //       한 장으로 이어져 널이 끊기는 자리가 없다. 행받침은 이것에 흡수돼 없앴다.
            for (int r = 0; r < 3; r++)
                Board(shelf.transform, "선반널_" + r, 0f, 행y[r] - 칸높이 * 0.5f, 판Z + 0.020f, 안쪽폭, 0.010f, 선반깊이, wood);
            Board(shelf.transform, "선반널_머리", 0f, 열머리y - 0.045f, 판Z + 0.020f, 안쪽폭, 0.010f, 선반깊이, wood);
            // 칸막이도 널과 같은 깊이로 — 앞뒤가 어긋나면 격자가 겹쳐 보인다
            for (int c = 0; c <= 4; c++)
                Board(shelf.transform, "칸막이_" + c, 격자좌 + c * 칸너비, 1.28f, 판Z + 0.020f, 0.008f, 0.36f, 선반깊이, wood);

            // 표찰이 들어갈 자리를 **비어 있을 때도** 알아보게 — 한 단 파인 자국을 새긴다.
            // 없으면 머리 줄이 그냥 빈 판으로 보여 무엇을 채우라는 것인지 읽히지 않는다.
            // ⚠️ 홈을 뒷판보다 **어둡게** 잡으면 어두운 판 위의 더 어두운 자국이라 아예 안 보인다
            //    (Play 실측). 뒷판보다 밝은 회갈색이라야 "표찰이 빠진 자리"로 읽힌다.
            var notch =LedgerWood("M_장부_홈", new Color(0.285f, 0.232f, 0.170f));
            for (int c = 0; c < LedgerData.Cols; c++)
                Board(shelf.transform, "머리홈_열_" + c, ColX(c), 열머리y, 판Z - 0.008f, 0.112f, 0.068f, 0.008f, notch);
            for (int r = 0; r < LedgerData.Rows; r++)
                Board(shelf.transform, "머리홈_행_" + r, 행머리x, 행y[r], 판Z - 0.008f, 0.112f, 0.068f, 0.008f, notch);

            // 판 뿌리 — 자리와 조각이 매달린다
            var board = new GameObject("판");
            board.transform.SetParent(root.transform, false);

            var slots = new Dictionary<string, LedgerSlot>();
            for (int r = 0; r < LedgerData.Rows; r++)
                for (int c = 0; c < LedgerData.Cols; c++)
                    slots["칸" + (r * LedgerData.Cols + c)] =
                        Slot(board.transform, "자리_칸_" + r + c, LedgerSlot.Kind.칸, r * LedgerData.Cols + c,
                             ColX(c), 행y[r]);
            for (int c = 0; c < LedgerData.Cols; c++)
                slots["열" + c] = Slot(board.transform, "자리_열_" + c, LedgerSlot.Kind.열머리, c, ColX(c), 열머리y);
            for (int r = 0; r < LedgerData.Rows; r++)
                slots["행" + r] = Slot(board.transform, "자리_행_" + r, LedgerSlot.Kind.행머리, r, 행머리x, 행y[r]);

            float[] poolX = { -0.27f, -0.09f, 0.09f, 0.27f, -0.18f, 0f, 0.18f };
            float[] poolY = { 1.00f, 1.00f, 1.00f, 1.00f, 0.86f, 0.86f, 0.86f };
            for (int i = 0; i < 7; i++)
                slots["보관" + i] = Slot(board.transform, "자리_보관_" + i, LedgerSlot.Kind.보관, i, poolX[i], poolY[i]);

            for (int i = 0; i < LedgerData.Records.Length; i++)
                slots["대조" + i] = Slot(board.transform, "자리_대조_" + i, LedgerSlot.Kind.대조, i, TrayX(i), 대조y);

            // 조각 — 기물 12, 표찰 7
            var placed = new List<LedgerPiece>();
            foreach (var def in LedgerData.Pieces)
            {
                if (!pieces.TryGetValue(def.id, out var pf)) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, board.transform);
                placed.Add(go.GetComponent<LedgerPiece>());
            }
            var labelPieces = new List<LedgerPiece>();
            for (int i = 0; i < 7; i++)
            {
                if (!labels.TryGetValue(i, out var pf)) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, board.transform);
                labelPieces.Add(go.GetComponent<LedgerPiece>());
            }

            // 무작위 시작 — 기물은 3×4 격자에 섞어 놓고, 표찰은 아래 칸에 흩는다
            var rng = new System.Random(씨앗);
            var cellKeys = Enumerable.Range(0, 12).OrderBy(_ => rng.Next()).ToList();
            for (int i = 0; i < placed.Count; i++) Seat(placed[i], slots["칸" + cellKeys[i]]);
            var poolKeys = Enumerable.Range(0, 7).OrderBy(_ => rng.Next()).ToList();
            for (int i = 0; i < labelPieces.Count; i++) Seat(labelPieces[i], slots["보관" + poolKeys[i]]);

            // 2단계 살림 — 기록 글판 · 대조대 · 처리 딱지
            var recRoot = new GameObject("기록");
            recRoot.transform.SetParent(root.transform, false);
            for (int i = 0; i < records.Count; i++)
            {
                records[i].transform.SetParent(recRoot.transform, false);
                records[i].transform.localPosition = new Vector3(-TrayX(i), 기록y, 판Z + 0.004f);
            }
            var trayRoot = new GameObject("대조대");
            trayRoot.transform.SetParent(root.transform, false);
            for (int i = 0; i < LedgerData.Records.Length; i++)
            {
                Board(trayRoot.transform, "대조받침_" + i, TrayX(i), 대조y - 0.055f, 판Z + 0.028f, 0.20f, 0.010f, 0.056f, wood);
                Board(trayRoot.transform, "대조홈_" + i, TrayX(i), 대조y, 판Z - 0.008f, 0.185f, 0.105f, 0.008f,
                      LedgerWood("M_장부_홈", new Color(0.285f, 0.232f, 0.170f)));
            }

            var tagRoot = new GameObject("처리딱지");
            tagRoot.transform.SetParent(root.transform, false);
            var tagList = new List<GameObject>();
            for (int i = 0; i < tags.Count; i++)
            {
                tags[i].transform.SetParent(tagRoot.transform, false);
                tags[i].transform.localPosition = new Vector3(-TrayX(i), 딱지y, 판Z + 0.006f);
                tagList.Add(tags[i]);
            }

            // 1단계 전용 — 표찰이 얹혀 있는 얕은 선반 둘 (2단계에는 그 자리를 기록·대조대가 쓴다)
            var pool = new GameObject("보관선반");
            pool.transform.SetParent(root.transform, false);
            Board(pool.transform, "보관받침_0", 0f, 1.00f - 0.038f, 판Z + 0.024f, 0.68f, 0.010f, 0.048f, wood);
            Board(pool.transform, "보관받침_1", 0f, 0.86f - 0.038f, 판Z + 0.024f, 0.68f, 0.010f, 0.048f, wood);

            // 조사등 — 포커스 중에만 켠다. 서고는 어두워 그냥 두면 기물이 검은 덩어리로만 보인다.
            // ⚠️ 점광원으로 하면 방까지 환해져 찬장만 도드라진다. 판을 겨눈 좁은 스포트라야 한다.
            var lamp = new GameObject("조사등");
            lamp.transform.SetParent(root.transform, false);
            lamp.transform.localPosition = new Vector3(0f, 1.20f, 판Z + 0.82f);
            lamp.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // 판 쪽(-Z 로컬)을 본다
            var light = lamp.AddComponent<Light>();
            light.type = LightType.Spot;
            light.spotAngle = 78f;
            light.range = 1.9f;
            light.intensity = 3.4f;
            light.color = new Color(1f, 0.94f, 0.84f);
            light.shadows = LightShadows.None;   // 선반틀이 격자무늬 그림자를 드리운다
            light.enabled = false;

            // 포커스 기준점 두 개 — 단계마다 보는 범위가 다르다
            // 담아야 할 세로 범위로 거리를 정한다 (화각 38° → 절반 높이 = 거리 × 0.3443)
            //   1단계: 열머리 위끝 1.536 ~ 보관 아래끝 0.829  → 가운데 1.183, 여유 30%
            //   2단계: 열머리 위끝 1.536 ~ 딱지 아래끝 0.592  → 가운데 1.064, 여유 30%
            var a1 = new GameObject("기준_배열").transform; a1.SetParent(root.transform, false);
            a1.localPosition = new Vector3(0f, 1.183f, 판Z);
            var a2 = new GameObject("기준_대조").transform; a2.SetParent(root.transform, false);
            a2.localPosition = new Vector3(0f, 1.064f, 판Z);
            //   3단계: 대조대(0.67~0.77)와 처리 딱지(0.59~0.66)만 — 넉 자를 크게 읽힌다
            var a3 = new GameObject("기준_처리").transform; a3.SetParent(root.transform, false);
            a3.localPosition = new Vector3(0f, 0.697f, 판Z);

            // 퍼즐 본체
            var puz = root.AddComponent<LedgerPuzzle>();
            puz.displayName = "수령의 장부";
            puz.boardRoot = board.transform;
            puz.chestDoors = doors;
            puz.recordSheet = recRoot;
            puz.trayMarks = trayRoot;
            puz.handlingTags = tagList;
            puz.poolShelf = pool;
            puz.focusLight = light;
            puz.focusAnchor = a2;
            puz.arrangeAnchor = a1;
            puz.revealAnchor = a3;
            puz.arrangeDistance = 1.36f;
            puz.focusDistance = 1.78f;
            puz.revealDistance = 0.72f;
            puz.transitionTime = 0.6f;
            puz.dimStrength = 0.72f;

            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1.075f, 판Z + 0.02f);
            box.size = new Vector3(0.72f, 0.98f, 0.08f);

            var src = root.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 1f;

            return puz;
        }

        static Material LedgerWood(string name, Color c)
        {
            string mp = 재질폴더 + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, mp); }
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", 0.11f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static float ColX(int c) => 격자좌 + (c + 0.5f) * 칸너비;
        static float TrayX(int i) => -0.235f + i * 0.235f;

        /// <summary>자리 하나. 화면 기준 x를 찬장 로컬 X로 뒤집어 놓는다
        /// (찬장이 방을 향해 돌아 앉아 있어 로컬 +X가 화면 왼쪽이다).</summary>
        static LedgerSlot Slot(Transform parent, string name, LedgerSlot.Kind kind, int index, float sx, float sy)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(-sx, sy, 판Z);
            var s = go.AddComponent<LedgerSlot>();
            s.kind = kind; s.index = index;
            return s;
        }

        static void Seat(LedgerPiece p, LedgerSlot s)
        {
            p.Capture();
            s.occupant = p;
            p.slot = s;
            p.transform.SetParent(s.transform.parent, false);
            p.transform.localPosition = s.transform.localPosition + new Vector3(0f, 0f, p.restLift);
            p.transform.localRotation = Quaternion.identity;
        }

        static void Board(Transform parent, string name, float sx, float sy, float z,
                          float w, float h, float d, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(-sx, sy, z);
            go.transform.localScale = new Vector3(w, h, d);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
