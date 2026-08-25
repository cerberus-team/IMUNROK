using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>관아 문서고에 서가를 짓는다.</b> 메뉴: [이문록 ▸ 관아 ▸ ⑥ 서가 짓기]
    ///
    /// 문서고는 집만 서 있고 안이 텅 비어 있었다. 대장 넷이 허공에 회색 상자로 떠 있는
    /// 방이라, 들어서도 여기가 무엇을 두는 데인지 알 수가 없다.
    ///
    /// <b>왜 문갑이 아닌가</b>: 문갑은 사랑방 세간이다. 서고는 <b>격자 서가</b>다 —
    /// 기둥과 기둥 사이를 널로 층층이 질러 놓고 책을 세워 꽂는다. 규장각·장경각 사진이
    /// 다 그 꼴이고, 그 빼곡함 자체가 "여기 나라의 기록이 다 있다"는 말을 한다.
    ///
    /// <b>왜 메시를 구워 놓나</b>: 서가 한 칸에 책이 260권쯤 선다. 아홉 칸이면 2,300권인데
    /// 이것을 오브젝트로 낱낱이 두면 그리는 값이 2,300번이다. 헤드셋에서는 그것만으로
    /// 프레임이 죽는다. 그래서 한 칸을 <b>메시 한 장</b>으로 구워 둔다 — 아홉 칸이면
    /// 그리는 값이 아홉 번이고, 삼각형은 다 합쳐 3만이 채 안 된다.
    ///
    /// <b>책 등의 무늬</b>는 그림 한 장을 지어 세로로 서른두 칸을 나눠 쓴다. 책마다
    /// 다른 칸을 물리면 재질 하나로도 권마다 빛깔과 제첨(題簽)이 달라진다.
    /// 낱낱이 재질을 두면 그리는 값이 다시 늘어난다.
    ///
    /// 기둥을 피해 칸을 앉힌다 — 뒷벽 다섯, 옆벽 둘씩. 두 번 눌러도 두 벌이 안 선다.
    /// </summary>
    public static class SeogoShelves
    {
        private const string Root = "서가";
        private const string MeshDir = "Assets/_Project/_Common/Sets/Gwana/Donheon/Meshes";
        private const string MatDir = "Assets/_Project/_Common/Sets/Gwana/Donheon/Materials";
        private const string SpinePng = MatDir + "/T_서책_등.png";

        // 한 칸의 치수(m)
        private const float BayWidth = 2.05f;
        private const float Depth = 0.42f;
        private const float Height = 2.30f;
        private const int Levels = 5;
        private const float Board = 0.05f;   // 널 두께
        private const float Post = 0.07f;    // 옆기둥 굵기

        private const int SpineCols = 32;    // 책 등 무늬 칸 수

        [MenuItem("이문록/관아/⑥ 서가 짓기")]
        public static void Build()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!scene.name.Contains("Gwana"))
            {
                Debug.LogWarning("[서가] 관아 씬을 열고 누르십시오(지금은 " + scene.name + ").");
                return;
            }

            Transform seogo = null;
            foreach (var r in scene.GetRootGameObjects()) if (r.name == "문서고") seogo = r.transform;
            if (seogo == null) { Debug.LogError("[서가] '문서고' 를 못 찾았습니다."); return; }

            // 마루 윗면과 벽 안쪽을 재어 자리를 잡는다 — 숫자를 코드에 박지 않는다
            Bounds floor = default, back = default, west = default, east = default;
            bool okF = false, okB = false, okW = false, okE = false;
            foreach (var r in seogo.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name == "우물마루") { floor = r.bounds; okF = true; }
                else if (r.name == "벽_뒤") { back = r.bounds; okB = true; }
                else if (r.name == "벽_서") { west = r.bounds; okW = true; }
                else if (r.name == "벽_동") { east = r.bounds; okE = true; }
            }
            if (!okF || !okB || !okW || !okE) { Debug.LogError("[서가] 마루·벽을 못 찾았습니다."); return; }

            float y = floor.max.y;                       // 마루 윗면
            var spine = EnsureSpineTexture();
            // 처음에 MI_Wood01A(밝은 나무)로 짰더니 서가가 책보다 밝아, 빼곡한 책이
            // 오히려 그늘처럼 보였다. 사진의 서가는 나무가 짙고 <b>제첨만 희다</b>.
            var woodMat = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/Onggojip/Art/KimMyeonggwanHouse/Material/MI_Wood02A.mat");
            var bookMat = EnsureBookMaterial(spine);

            var old = GameObject.Find(Root);
            if (old != null) Undo.DestroyObjectImmediate(old);
            var root = new GameObject(Root);
            Undo.RegisterCreatedObjectUndo(root, "서가 짓기");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);

            System.IO.Directory.CreateDirectory(MeshDir);
            int made = 0;

            // ── 뒷벽: 기둥 사이 다섯 칸 ──
            float[] backX = { -6.8f, -4.4f, -2.0f, 0.4f, 2.8f };
            for (int i = 0; i < backX.Length; i++)
                made += Put(root.transform, "서가_뒤_" + i, i,
                            new Vector3(backX[i], y, back.max.z + Depth), 0f, woodMat, bookMat);

            // ── 옆벽: 기둥 사이 둘씩 ──
            float[] sideZ = { -9.8f, -6.8f };
            for (int i = 0; i < sideZ.Length; i++)
            {
                made += Put(root.transform, "서가_서_" + i, 10 + i,
                            new Vector3(west.max.x + Depth, y, sideZ[i]), 90f, woodMat, bookMat);
                made += Put(root.transform, "서가_동_" + i, 20 + i,
                            new Vector3(east.min.x - Depth, y, sideZ[i]), -90f, woodMat, bookMat);
            }

            EditorSceneManagerMarkDirty(scene);
            Debug.Log("[서가] " + made + "칸을 지었다. 마루 윗면 y=" + y.ToString("F2"));
        }

        /// <summary>
        /// <b>대장 넷을 궤에 담아 바닥에 놓고, 종이를 헤집어 찾게 한다.</b>
        /// 메뉴: [이문록 ▸ 관아 ▸ ⑦ 대장을 궤에 담기]
        ///
        /// 처음에는 궤를 <b>서가에 끼워</b> 두었다. 그런데 책 이천 권 사이에 궤 하나가
        /// 놓여 있으면 그것은 찾는 것이 아니라 <b>다른 것 하나를 골라내는</b> 일이다 —
        /// 눈에 띄는 순간 끝난다.
        ///
        /// 그래서 궤를 바닥에 따로 내려놓고, 안에 <b>종이 뭉치를 덮어</b> 둔다.
        /// 눌러 잡고 헤집어야 밑에서 대장이 나온다(<see cref="AshRake"/> — 아궁이 재를
        /// 헤집는 그 부품이다). 뒤진다는 것이 손으로 하는 일이 되고, 그동안 소리가 난다.
        ///
        /// 단서는 <b>궤가 주지 않는다</b>. 헤집으면 대장이 드러날 뿐이고, 그것을 눌러
        /// 읽어야 수첩에 적힌다 — 있던 InspectableNote 가 그대로 그 일을 한다.
        /// </summary>
        [MenuItem("이문록/관아/⑦ 대장을 궤에 담기")]
        public static void PutInBoxes()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!scene.name.Contains("Gwana")) { Debug.LogWarning("[궤] 관아 씬을 열고 누르십시오."); return; }

            var wood = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/Onggojip/Art/KimMyeonggwanHouse/Material/MI_Wood02A.mat");
            var book = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/M_서책.mat");
            var rustle = AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/_Project/_Common/Audio/SFX/서랍_긁힘.wav");
            var crate = EnsureMesh("문서궤", BuildCrate);
            var heapMesh = EnsureMesh("종이더미", BuildHeap);
            var ledger = EnsureMesh("대장책", BuildLedger);

            var oldRoot = GameObject.Find("문서궤들");
            if (oldRoot != null)
            {
                // 안에 옮겨 두었던 대장을 도로 꺼내 놓고 뿌리를 지운다(두 번 눌러도 안 겹치게)
                foreach (var note in oldRoot.GetComponentsInChildren<InspectableNote>(true))
                {
                    note.gameObject.SetActive(true);
                    note.transform.SetParent(null, true);
                }
                Undo.DestroyObjectImmediate(oldRoot);
            }
            var root = new GameObject("문서궤들");
            Undo.RegisterCreatedObjectUndo(root, "문서궤");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);

            float y = 0.82f;
            Transform seogo = null;
            foreach (var r in scene.GetRootGameObjects()) if (r.name == "문서고") seogo = r.transform;
            if (seogo != null)
                foreach (var r in seogo.GetComponentsInChildren<Renderer>(true))
                    if (r.name == "우물마루") y = r.bounds.max.y;

            // 서가와 기둥을 피해 바닥에 흩어 놓는다
            string[] names = { "호적대장", "호구단자", "입안대장", "환곡대장" };
            Vector3[] spot = {
                new Vector3(-6.10f, 0f, -9.55f), new Vector3(-2.30f, 0f, -9.90f),
                new Vector3( 0.90f, 0f, -9.30f), new Vector3( 2.40f, 0f, -6.60f),
            };
            float[] yaw = { 18f, -24f, 8f, -40f };

            int done = 0;
            for (int i = 0; i < names.Length; i++)
            {
                var note = GameObject.Find(names[i]);
                if (note == null) { Debug.LogWarning("[궤] 못 찾음: " + names[i]); continue; }

                var box = new GameObject("문서궤_" + names[i]);
                box.transform.SetParent(root.transform, false);
                box.transform.position = new Vector3(spot[i].x, y, spot[i].z);
                box.transform.rotation = Quaternion.Euler(0f, yaw[i], 0f);

                Piece(box.transform, "궤", crate, new[] { wood });
                var heapGo = Piece(box.transform, "종이더미", heapMesh, new[] { book });

                // 대장은 궤 안에 눕힌다. 있던 부품(InspectableNote·단서키)은 그대로 산다.
                note.transform.SetParent(box.transform, false);
                note.transform.localPosition = new Vector3(0f, 0.045f, 0f);
                note.transform.localRotation = Quaternion.Euler(0f, 6f, 0f);
                var mf = note.GetComponent<MeshFilter>(); if (mf == null) mf = note.AddComponent<MeshFilter>();
                mf.sharedMesh = ledger;
                var mr = note.GetComponent<MeshRenderer>(); if (mr == null) mr = note.AddComponent<MeshRenderer>();
                mr.sharedMaterials = new[] { book };
                var nbc = note.GetComponent<BoxCollider>(); if (nbc == null) nbc = note.AddComponent<BoxCollider>();
                nbc.center = new Vector3(0f, 0.035f, 0f);
                nbc.size = new Vector3(0.30f, 0.07f, 0.40f);
                note.SetActive(false);          // 헤집기 전에는 안 보인다

                var rake = box.AddComponent<AshRake>();
                var so = new SerializedObject(rake);
                so.FindProperty("_hinge").objectReferenceValue = heapGo.transform;
                so.FindProperty("_before").objectReferenceValue = heapGo;
                so.FindProperty("_after").objectReferenceValue = note;
                so.FindProperty("_liftEuler").vector3Value = new Vector3(-14f, 0f, 6f);
                so.FindProperty("_liftOffset").vector3Value = new Vector3(0.06f, 0.10f, -0.05f);
                so.FindProperty("_holdSeconds").floatValue = 1.05f;
                so.FindProperty("_catchAt").floatValue = 0.60f;
                so.FindProperty("_weight").floatValue = 0.22f;
                so.FindProperty("_revealWhileHolding").boolValue = true;
                so.FindProperty("_canPutBack").boolValue = true;
                so.FindProperty("_noise").floatValue = 0.30f;
                if (rustle != null) so.FindProperty("_sound").objectReferenceValue = rustle;
                so.FindProperty("_soundLoops").boolValue = true;
                so.FindProperty("_title").stringValue = "문서궤";
                so.FindProperty("_bodyBefore").stringValue = "종이 뭉치가 그득하다. 겉장만 봐서는 무엇인지 모른다.";
                so.FindProperty("_bodyAfter").stringValue = "종이 밑에서 *" + names[i] + "* 이 나온다.";
                so.FindProperty("_hint").stringValue = "(눌러 잡고 헤집기)";
                so.FindProperty("_recordClue").boolValue = false;   // 읽는 것은 대장이 한다
                so.ApplyModifiedProperties();

                var bc = box.AddComponent<BoxCollider>();
                bc.center = new Vector3(0f, 0.12f, 0f);
                bc.size = new Vector3(0.58f, 0.24f, 0.46f);
                done++;
            }
            EditorSceneManagerMarkDirty(scene);
            Debug.Log("[궤] 문서궤 " + done + "개를 바닥에 놓고 종이를 덮었다.");
        }

        private static GameObject Piece(Transform parent, string name, Mesh mesh, Material[] mats)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = mats;
            return go;
        }

        private static Mesh EnsureMesh(string name, System.Func<Mesh> make)
        {
            System.IO.Directory.CreateDirectory(MeshDir);
            string path = MeshDir + "/" + name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
            var m = make();
            m.name = name;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>뚜껑 없는 궤 — 안이 보여야 헤집을 것이 있다는 걸 안다.</summary>
        private static Mesh BuildCrate()
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            Rect u = new Rect(0f, 0f, 1f / (SpineCols + 1f), 1f);
            float w = 0.54f, d = 0.42f, h = 0.20f, th = 0.030f;
            Box(v, uv, t, new Vector3(0f, th * 0.5f, 0f), new Vector3(w, th, d), u, u);
            Box(v, uv, t, new Vector3(0f, h * 0.5f, d * 0.5f - th * 0.5f), new Vector3(w, h, th), u, u);
            Box(v, uv, t, new Vector3(0f, h * 0.5f, -d * 0.5f + th * 0.5f), new Vector3(w, h, th), u, u);
            Box(v, uv, t, new Vector3(w * 0.5f - th * 0.5f, h * 0.5f, 0f), new Vector3(th, h, d), u, u);
            Box(v, uv, t, new Vector3(-w * 0.5f + th * 0.5f, h * 0.5f, 0f), new Vector3(th, h, d), u, u);
            var m = new Mesh(); m.SetVertices(v); m.SetTriangles(t, 0); m.SetUVs(0, uv);
            m.RecalculateNormals(); m.RecalculateBounds(); return m;
        }

        /// <summary>궤를 덮은 종이 뭉치. 낱장이 어긋나게 겹쳐야 <b>헤집을 것</b>으로 보인다.</summary>
        private static Mesh BuildHeap()
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            Rect page = new Rect(0f, 0f, 1f / (SpineCols + 1f), 1f);
            var rnd = new System.Random(31);
            float y = 0.035f;
            for (int i = 0; i < 14; i++)
            {
                // 궤 안(0.48 x 0.36)에 들어앉아야 한다 — 처음에 크게 잡았더니 낱장이
                // 궤 벽을 뚫고 나왔다. 켜가 보이게 y 를 더 벌린다.
                float w = 0.245f + (float)rnd.NextDouble() * 0.085f;
                float d = 0.185f + (float)rnd.NextDouble() * 0.070f;
                float th = 0.009f + (float)rnd.NextDouble() * 0.013f;
                float jx = ((float)rnd.NextDouble() - 0.5f) * 0.055f;
                float jz = ((float)rnd.NextDouble() - 0.5f) * 0.045f;
                float rot = ((float)rnd.NextDouble() - 0.5f) * 0.62f;
                float cs = Mathf.Cos(rot), sn = Mathf.Sin(rot);
                Vector3 c = new Vector3(jx, y + th * 0.5f, jz);
                Vector3 ex = new Vector3(cs, 0f, sn) * (w * 0.5f);
                Vector3 ez = new Vector3(-sn, 0f, cs) * (d * 0.5f);
                Vector3 ey = new Vector3(0f, th * 0.5f, 0f);
                Quad(v, uv, t, c - ex - ez + ey, c + ex - ez + ey, c + ex + ez + ey, c - ex + ez + ey,
                     page.min, new Vector2(page.xMax, page.yMin), page.max, new Vector2(page.xMin, page.yMax));
                Quad(v, uv, t, c - ex - ez - ey, c - ex + ez - ey, c + ex + ez - ey, c + ex - ez - ey,
                     page.min, new Vector2(page.xMax, page.yMin), page.max, new Vector2(page.xMin, page.yMax));
                Quad(v, uv, t, c - ex - ez - ey, c + ex - ez - ey, c + ex - ez + ey, c - ex - ez + ey,
                     page.min, new Vector2(page.xMax, page.yMin), page.max, new Vector2(page.xMin, page.yMax));
                Quad(v, uv, t, c + ex + ez - ey, c - ex + ez - ey, c - ex + ez + ey, c + ex + ez + ey,
                     page.min, new Vector2(page.xMax, page.yMin), page.max, new Vector2(page.xMin, page.yMax));
                y += th * 0.80f;
            }
            var m = new Mesh(); m.SetVertices(v); m.SetTriangles(t, 0); m.SetUVs(0, uv);
            m.RecalculateNormals(); m.RecalculateBounds(); return m;
        }

        /// <summary>대장 한 권 — 모로 뉜 두툼한 선장본. 제첨이 위를 본다.</summary>
        private static Mesh BuildLedger()
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            Rect u = new Rect(0f, 0f, 1f / (SpineCols + 1f), 1f);
            Rect label = new Rect(5f / (SpineCols + 1f) + 0.004f, 0.60f, 1f / (SpineCols + 1f) - 0.008f, 0.22f);
            Box(v, uv, t, new Vector3(0f, 0.030f, 0f), new Vector3(0.28f, 0.060f, 0.38f), u, u);
            Box(v, uv, t, new Vector3(-0.055f, 0.062f, 0.055f), new Vector3(0.11f, 0.005f, 0.16f), label, label, 4);
            var m = new Mesh(); m.SetVertices(v); m.SetTriangles(t, 0); m.SetUVs(0, uv);
            m.RecalculateNormals(); m.RecalculateBounds(); return m;
        }

        /// <summary>한 칸을 세운다. 메시는 씬이 아니라 파일로 남긴다 — 씬 파일이 부풀지 않게.</summary>
        private static int Put(Transform parent, string name, int seed, Vector3 pos, float yaw,
                               Material wood, Material book)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            var mesh = BuildBay(seed);
            string path = MeshDir + "/" + name + ".asset";
            var had = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (had != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { wood, book };

            // 몸으로 막는다 — 서가를 뚫고 지나가면 방이 방이 아니다
            var bc = go.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, Height * 0.5f, -Depth * 0.5f);
            bc.size = new Vector3(BayWidth, Height, Depth);
            return 1;
        }

        /// <summary>
        /// 서가 한 칸을 메시 한 장으로 짓는다.
        /// 자리기준: x 는 폭, y 는 바닥에서 위로, z 는 0(방 쪽)에서 -Depth(벽 쪽).
        /// 서브메시 0 = 나무틀, 1 = 책.
        /// </summary>
        private static Mesh BuildBay(int seed)
        {
            var rnd = new System.Random(1000 + seed);
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var woodTris = new List<int>();
            var bookTris = new List<int>();

            // 나무는 무늬를 안 쓰므로 아무 칸이나 한 자리를 물린다
            Rect woodUv = new Rect(0f, 0f, 1f / (SpineCols + 1f), 1f);

            float half = BayWidth * 0.5f;
            float inner = half - Post;

            // ① 옆기둥 둘
            Box(v, uv, woodTris, new Vector3(-half + Post * 0.5f, Height * 0.5f, -Depth * 0.5f),
                new Vector3(Post, Height, Depth), woodUv, woodUv);
            Box(v, uv, woodTris, new Vector3(half - Post * 0.5f, Height * 0.5f, -Depth * 0.5f),
                new Vector3(Post, Height, Depth), woodUv, woodUv);

            // ② 층널 — 맨 아래와 맨 위를 넣어 Levels+1 장
            float clear = (Height - Board * (Levels + 1)) / Levels;
            var boardY = new float[Levels + 1];
            for (int i = 0; i <= Levels; i++) boardY[i] = i * (clear + Board) + Board * 0.5f;
            for (int i = 0; i <= Levels; i++)
                Box(v, uv, woodTris, new Vector3(0f, boardY[i], -Depth * 0.5f),
                    new Vector3(BayWidth - Post * 2f, Board, Depth), woodUv, woodUv);

            // ③ 뒷널 — 벽이 비쳐 보이지 않게 한 겹 친다
            Box(v, uv, woodTris, new Vector3(0f, Height * 0.5f, -Depth + 0.015f),
                new Vector3(BayWidth - Post * 2f, Height, 0.03f), woodUv, woodUv);

            // ④ 책 — 층마다 채운다. 세워 꽂은 줄과 <b>모로 뉜 무더기</b>를 섞는다.
            //
            // 사진의 선장본은 판자가 아니다. 종이를 접어 실로 꿰맨 것이라 <b>옆이 둥글고
            // 도톰하며</b>, 두께가 권마다 딴판이고, 꽂아 둔 줄이 반듯하지 않다.
            // 그리고 다 세워 두지도 않는다 — 몇 무더기는 모로 뉘어 쌓는다.
            for (int lv = 0; lv < Levels; lv++)
            {
                float baseY = boardY[lv] + Board * 0.5f;
                float x = -inner + 0.02f;
                float limit = inner - 0.02f;
                float stopAt = limit - (float)rnd.NextDouble() * 0.16f;

                while (x < stopAt - 0.05f)
                {
                    // 열에 두 번쯤은 모로 뉘어 쌓는다
                    if (rnd.NextDouble() < 0.20)
                    {
                        int pile = 3 + rnd.Next(5);
                        float pw = 0.19f + (float)rnd.NextDouble() * 0.05f;   // 뉜 책의 너비
                        if (x + pw > stopAt) break;
                        float yy = baseY;
                        for (int k = 0; k < pile; k++)
                        {
                            float th = 0.022f + (float)rnd.NextDouble() * 0.028f;
                            float pd = 0.20f + (float)rnd.NextDouble() * 0.05f;
                            float jx = ((float)rnd.NextDouble() - 0.5f) * 0.012f;
                            float jz = ((float)rnd.NextDouble() - 0.5f) * 0.016f;
                            int cc = 1 + rnd.Next(SpineCols);
                            Rect fc = new Rect(cc / (SpineCols + 1f), 0f, 1f / (SpineCols + 1f), 1f);
                            // 뉜 책은 <b>윗낯</b>에 표지가 보인다(+Y)
                            Box(v, uv, bookTris,
                                new Vector3(x + pw * 0.5f + jx, yy + th * 0.5f, -Depth + 0.05f + pd * 0.5f + jz),
                                new Vector3(pw, th, pd), fc, woodUv, 4);
                            yy += th;
                        }
                        x += pw + 0.02f;
                        continue;
                    }

                    // 세워 꽂은 책 — 두께가 권마다 다르고, 이따금 아주 도톰한 것이 낀다
                    float w = 0.024f + (float)rnd.NextDouble() * 0.026f;
                    if (rnd.NextDouble() < 0.16) w += 0.020f + (float)rnd.NextDouble() * 0.028f;
                    if (x + w > stopAt) break;
                    float h = 0.238f + (float)rnd.NextDouble() * 0.072f;
                    float d = 0.20f + (float)rnd.NextDouble() * 0.055f;
                    float lean = ((float)rnd.NextDouble() - 0.5f) * 0.10f;      // 라디안 — 반듯하지 않다
                    int col = 1 + rnd.Next(SpineCols);
                    Rect face = new Rect(col / (SpineCols + 1f), 0f, 1f / (SpineCols + 1f), 1f);

                    Volume(v, uv, bookTris,
                           new Vector3(x + w * 0.5f, baseY, -Depth + 0.04f + d * 0.5f),
                           new Vector3(w, h, d), lean, face, woodUv);
                    x += w + 0.0015f;
                }
            }

            var m = new Mesh { name = "서가칸" };
            if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v);
            m.subMeshCount = 2;
            m.SetTriangles(woodTris, 0);
            m.SetTriangles(bookTris, 1);
            m.SetUVs(0, uv);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>
        /// 상자 하나를 보탠다. <paramref name="front"/> 는 <b>방 쪽 낯</b>(+Z)에만 쓰고,
        /// 나머지 다섯 낯은 <paramref name="rest"/> 를 쓴다 — 책은 등만 보이면 되기 때문이다.
        /// </summary>
        private static void Box(List<Vector3> v, List<Vector2> uv, List<int> tris,
                                Vector3 c, Vector3 s, Rect front, Rect rest, int frontFace = 0)
        {
            Vector3 h = s * 0.5f;
            // 낯 여섯: +Z(앞), -Z, +X, -X, +Y, -Y
            Vector3[][] faces = {
                new[]{ new Vector3(-h.x,-h.y, h.z), new Vector3( h.x,-h.y, h.z), new Vector3( h.x, h.y, h.z), new Vector3(-h.x, h.y, h.z) },
                new[]{ new Vector3( h.x,-h.y,-h.z), new Vector3(-h.x,-h.y,-h.z), new Vector3(-h.x, h.y,-h.z), new Vector3( h.x, h.y,-h.z) },
                new[]{ new Vector3( h.x,-h.y, h.z), new Vector3( h.x,-h.y,-h.z), new Vector3( h.x, h.y,-h.z), new Vector3( h.x, h.y, h.z) },
                new[]{ new Vector3(-h.x,-h.y,-h.z), new Vector3(-h.x,-h.y, h.z), new Vector3(-h.x, h.y, h.z), new Vector3(-h.x, h.y,-h.z) },
                new[]{ new Vector3(-h.x, h.y, h.z), new Vector3( h.x, h.y, h.z), new Vector3( h.x, h.y,-h.z), new Vector3(-h.x, h.y,-h.z) },
                new[]{ new Vector3(-h.x,-h.y,-h.z), new Vector3( h.x,-h.y,-h.z), new Vector3( h.x,-h.y, h.z), new Vector3(-h.x,-h.y, h.z) },
            };
            for (int f = 0; f < 6; f++)
            {
                Rect r = (f == frontFace) ? front : rest;
                int b = v.Count;
                for (int i = 0; i < 4; i++) v.Add(c + faces[f][i]);
                uv.Add(new Vector2(r.xMin, r.yMin));
                uv.Add(new Vector2(r.xMax, r.yMin));
                uv.Add(new Vector2(r.xMax, r.yMax));
                uv.Add(new Vector2(r.xMin, r.yMax));
                // <b>감는 방향</b>. 처음에 (0,2,1)(0,3,2) 로 감았더니 낯이 다 안쪽을 보아,
                // 책 등에 무늬를 발라 놓고도 정작 눈에 보이는 것은 <b>뒤통수</b>였다 —
                // 그래서 아무리 밝혀도 시커멓기만 했다. 바깥을 보게 되감는다.
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
            }
        }

        /// <summary>
        /// <b>선장본 한 권.</b> 밑가운데(<paramref name="foot"/>)를 딛고 선다.
        ///
        /// 판판한 상자가 아니다. 종이를 접어 실로 꿰맨 책이라 <b>바깥쪽 접힌 옆이
        /// 둥글게 부르트고</b>, 위아래 마구리도 반듯하지 않다. 그래서 앞낯(+Z)을 세 쪽으로
        /// 갈라 가운데를 조금 내밀고, 위낯도 그만큼 앞으로 늘여 준다.
        /// <paramref name="lean"/> 만큼 기울여 꽂는다 — 반듯한 줄은 책꽂이가 아니라 벽돌담이다.
        /// </summary>
        private static void Volume(List<Vector3> v, List<Vector2> uv, List<int> tris,
                                   Vector3 foot, Vector3 s, float lean, Rect face, Rect rest)
        {
            float hw = s.x * 0.5f, h = s.y, hd = s.z * 0.5f;
            float bulge = s.x * 0.42f + 0.004f;      // 두꺼운 책일수록 더 부르튼다
            float cos = Mathf.Cos(lean), sin = Mathf.Sin(lean);

            // 밑가운데를 축으로 기울인다
            System.Func<float, float, float, Vector3> P = (px, py, pz) =>
                foot + new Vector3(px * cos - py * sin, px * sin + py * cos, pz);

            // 앞낯 옆모습(위에서 본 x-z 결): 가장자리는 들어가고 가운데가 나온다
            float[] xs = { -hw, -hw * 0.45f, hw * 0.45f, hw };
            float[] zs = { hd - 0.006f, hd + bulge, hd + bulge, hd - 0.006f };

            // ① 앞낯 세 쪽 — 표지 무늬가 여기 걸린다
            for (int i = 0; i < 3; i++)
            {
                float u0 = face.xMin + face.width * (i / 3f);
                float u1 = face.xMin + face.width * ((i + 1) / 3f);
                Quad(v, uv, tris,
                     P(xs[i], 0f, zs[i]), P(xs[i + 1], 0f, zs[i + 1]),
                     P(xs[i + 1], h, zs[i + 1]), P(xs[i], h, zs[i]),
                     new Vector2(u0, face.yMin), new Vector2(u1, face.yMin),
                     new Vector2(u1, face.yMax), new Vector2(u0, face.yMax));
            }
            // ② 뒷낯
            Quad(v, uv, tris, P(hw, 0f, -hd), P(-hw, 0f, -hd), P(-hw, h, -hd), P(hw, h, -hd),
                 rest.min, new Vector2(rest.xMax, rest.yMin), rest.max, new Vector2(rest.xMin, rest.yMax));
            // ③ 옆낯 둘
            Quad(v, uv, tris, P(hw, 0f, zs[3]), P(hw, 0f, -hd), P(hw, h, -hd), P(hw, h, zs[3]),
                 rest.min, new Vector2(rest.xMax, rest.yMin), rest.max, new Vector2(rest.xMin, rest.yMax));
            Quad(v, uv, tris, P(-hw, 0f, -hd), P(-hw, 0f, zs[0]), P(-hw, h, zs[0]), P(-hw, h, -hd),
                 rest.min, new Vector2(rest.xMax, rest.yMin), rest.max, new Vector2(rest.xMin, rest.yMax));
            // ④ 위낯 — 부르튼 앞을 따라 세 쪽
            for (int i = 0; i < 3; i++)
                Quad(v, uv, tris,
                     P(xs[i], h, zs[i]), P(xs[i + 1], h, zs[i + 1]),
                     P(xs[i + 1], h, -hd), P(xs[i], h, -hd),
                     rest.min, new Vector2(rest.xMax, rest.yMin), rest.max, new Vector2(rest.xMin, rest.yMax));
        }

        private static void Quad(List<Vector3> v, List<Vector2> uv, List<int> tris,
                                 Vector3 a, Vector3 b, Vector3 c, Vector3 d,
                                 Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            uv.Add(ua); uv.Add(ub); uv.Add(uc); uv.Add(ud);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }

        // ── 책 등 무늬 ──

        /// <summary>
        /// 책 등 그림을 짓는다. 세로로 <see cref="SpineCols"/>+1 칸이고, 맨 왼쪽 한 칸은
        /// <b>민민한 어둠</b>(책의 옆·위·아래 낯과 나무틀이 쓴다), 나머지가 등이다.
        /// 등마다 낡은 종이 빛깔이 다르고 위쪽에 제첨(題簽)이 한 장 붙는다.
        /// </summary>
        private static Texture2D EnsureSpineTexture()
        {
            var had = AssetDatabase.LoadAssetAtPath<Texture2D>(SpinePng);
            if (had != null) return had;

            System.IO.Directory.CreateDirectory(MatDir);
            const int colW = 32, H = 256;
            int W = colW * (SpineCols + 1);
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            var rnd = new System.Random(7);
            var px = new Color[W * H];

            // 맨 왼쪽 한 칸은 책의 <b>배와 마구리</b>가 쓴다. 처음에 시커멓게 두었더니
            // 모로 뉜 무더기가 검은 벽돌처럼 보였다 — 실제 선장본은 접힌 종이가
            // 그대로 드러나 <b>누렇게 밝다</b>. 표지보다 오히려 밝은 데다.
            Color dark = new Color(0.615f, 0.575f, 0.487f);
            for (int i = 0; i < px.Length; i++) px[i] = dark;

            for (int c = 1; c <= SpineCols; c++)
            {
                // 낡은 표지 — 짙은 갈색에서 잿빛까지
                float t = (float)rnd.NextDouble();
                // <b>처음에 너무 어둡게 잡았다.</b> 0.21~0.33 으로 두었더니 서고 안에서
                // 시커먼 덩어리로만 보였다 — 사진의 고서도 짙기는 하나, 그 짙음이 읽히는
                // 것은 <b>둘레의 나무가 밝고 제첨이 희기</b> 때문이다. 등 자체가 빛을
                // 못 받으면 책이 아니라 그림자가 된다.
                Color baseC = Color.Lerp(new Color(0.395f, 0.320f, 0.238f),
                                         new Color(0.605f, 0.548f, 0.462f), t);
                float labelTop = 0.62f + (float)rnd.NextDouble() * 0.16f;
                float labelH = 0.16f + (float)rnd.NextDouble() * 0.08f;
                bool hasLabel = rnd.NextDouble() > 0.06;   // 거의 다 제첨을 붙인다 — 그것이 읽히는 데다

                for (int y = 0; y < H; y++)
                {
                    float fy = (float)y / H;
                    for (int x = 0; x < colW; x++)
                    {
                        float fx = (float)x / colW;
                        // 세로결 + 얼룩
                        float n = Mathf.PerlinNoise(c * 13.7f + fx * 3f, fy * 22f) - 0.5f;
                        float grain = Mathf.PerlinNoise(c * 5.1f + fx * 40f, fy * 2f) - 0.5f;
                        Color col = baseC * (1f + n * 0.22f + grain * 0.10f);

                        // 가장자리는 손을 타 어둡다
                        float edge = Mathf.Min(fx, 1f - fx);
                        if (edge < 0.13f) col *= Mathf.Lerp(0.62f, 1f, edge / 0.13f);

                        // 제첨 — 위쪽에 붙인 흰 쪽지
                        if (hasLabel && fy > labelTop && fy < labelTop + labelH && fx > 0.18f && fx < 0.82f)
                        {
                            Color paper = new Color(0.885f, 0.855f, 0.775f) * (1f + n * 0.10f);
                            // 쪽지 한복판에 먹으로 한 줄
                            if (fx > 0.42f && fx < 0.58f && fy > labelTop + labelH * 0.15f && fy < labelTop + labelH * 0.85f)
                                paper *= 0.30f;
                            col = paper;
                        }
                        px[y * W + c * colW + x] = col;
                    }
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            System.IO.File.WriteAllBytes(SpinePng, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(SpinePng, ImportAssetOptions.ForceSynchronousImport);

            var ti = AssetImporter.GetAtPath(SpinePng) as TextureImporter;
            if (ti != null) { ti.wrapMode = TextureWrapMode.Clamp; ti.mipmapEnabled = true; ti.SaveAndReimport(); }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(SpinePng);
        }

        private static Material EnsureBookMaterial(Texture2D spine)
        {
            string p = MatDir + "/M_서책.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit");
                m = new Material(sh) { name = "M_서책" };
                AssetDatabase.CreateAsset(m, p);
            }
            m.SetTexture("_BaseMap", spine);
            m.SetTexture("_MainTex", spine);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.06f);   // 낡은 종이는 반들거리지 않는다
            m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();
            return m;
        }

        private static void EditorSceneManagerMarkDirty(UnityEngine.SceneManagement.Scene s)
            => UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(s);
    }
}
