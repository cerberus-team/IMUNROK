using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// 2막 관아 세트를 씬에 놓는다. 메뉴: [이문록 ▸ 관아 ▸ …]
    ///
    /// 팀 전달본(GwanaSet_FOR_TEAM)은 <b>씬이 아니다</b>. 씬 파일이 한 장도 들어 있지
    /// 않고, 들어 있는 것은 프리팹 둘 — 외삼문·담·문지기 한 벌(PF_GwanaGateSet)과
    /// 동헌 한 채(PF_Donheon) — 뿐이다. 그러니 관아 씬을 <b>갈아 끼우는</b> 것이 아니라,
    /// 여태 회색 상자로 서 있던 자리에 <b>건물을 세우는</b> 일이다.
    ///
    /// 지금 Onggojip_Gwana 씬에 들어 있는 것은 바닥 평면 두 장과 조사 대상(입안대장·
    /// 호구단자·호적대장·환곡대장), 자리 표식(甲_자리·乙_자리·아내·늙은하인_소환)이다.
    /// <b>그것들은 건드리지 않는다.</b> 건물이 없어서 회색 상자였던 것이지, 배치가
    /// 틀려서 회색 상자였던 것이 아니다.
    ///
    /// 두 프리팹의 사이 간격은 전달본이 재어 둔 값을 그대로 쓴다. 둘 다 항등으로 놓으면
    /// 밑면이 Y=0 에 앉으므로, 게이트를 원점에 두고 동헌만 그만큼 띄우면 원본 구도가 된다.
    /// </summary>
    public static class GwanaSetup
    {
        /// <summary>
        /// 관아 한 벌이 사는 곳. 전달본은 Models·_Import·Gate_Hwaryeongjeon·
        /// Wall_SlitBlocker·Seocheon 다섯 군데로 흩어져 들어왔는데, 앞의 넷은 작업 중
        /// 임시 폴더였고 Seocheon 은 아직 비어 있던 딴 사건 폴더였다. 관아는 사건마다
        /// 다시 나오는 무대라 사건 폴더가 아니라 공통 자리에 한 벌만 둔다.
        /// 사건별로 모양을 고칠 때는 이걸 복사하지 말고 그 사건 폴더에 프리팹
        /// 배리언트를 뜬다 — 아트 원본은 330MB 라 두 벌째부터 값이 비싸다.
        /// </summary>
        private const string SetRoot = "Assets/_Project/_Common/Sets/Gwana";

        private const string GatePath = SetRoot + "/Prefabs/PF_GwanaGateSet.prefab";
        private const string DonheonPath = SetRoot + "/Prefabs/PF_Donheon.prefab";
        private const string RootName = "관아세트";

        /// <summary>동헌은 게이트 기준 +X 13.42m, 9.6cm 낮은 지면에 앉아 있었다(전달본 실측).</summary>
        private static readonly Vector3 DonheonOffset = new Vector3(13.4201f, -0.09599f, -0.066223f);

        // ── 놓기 ──

        [MenuItem("이문록/관아/① 세트 놓기")]
        public static void Place()
        {
            var gate = AssetDatabase.LoadAssetAtPath<GameObject>(GatePath);
            var donheon = AssetDatabase.LoadAssetAtPath<GameObject>(DonheonPath);
            if (gate == null || donheon == null)
            {
                Debug.LogError("[관아] 프리팹을 못 찾았습니다. 전달본이 들어와 있는지 보십시오:\n  "
                               + GatePath + "\n  " + DonheonPath);
                return;
            }

            // 두 번 눌러도 두 벌이 서지 않게, 있던 것은 지우고 새로 세운다.
            var old = GameObject.Find(RootName);
            if (old != null) Undo.DestroyObjectImmediate(old);

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "관아 세트");
            root.transform.position = Vector3.zero;

            var g = (GameObject)PrefabUtility.InstantiatePrefab(gate, root.transform);
            g.transform.localPosition = Vector3.zero;
            g.transform.localRotation = Quaternion.identity;

            var d = (GameObject)PrefabUtility.InstantiatePrefab(donheon, root.transform);
            d.transform.localPosition = DonheonOffset;
            d.transform.localRotation = Quaternion.identity;

            Selection.activeGameObject = root;
            EditorSceneManagerMarkDirty();
            Debug.Log("[관아] 세트를 놓았습니다.\n" + Report(root));
        }

        // ── 검사 ──

        [MenuItem("이문록/관아/② 놓은 것 살펴보기")]
        public static void Check()
        {
            var root = GameObject.Find(RootName);
            if (root == null) { Debug.LogWarning("[관아] '" + RootName + "' 이 씬에 없습니다. 먼저 ① 을 누르십시오."); return; }
            Debug.Log("[관아] 살펴본 바\n" + Report(root));
        }

        /// <summary>
        /// 전달본이 "이대로 두라"고 적어 둔 것들이 그대로인지 센다.
        ///
        /// 셋 다 조용히 어긋나는 것들이라 눈으로는 못 잡는다 —
        /// 담 한 짝을 잘못 켜면 담이 이중으로 겹치고, 문지기 X 배율이 양수로 돌아가면
        /// 두 사람이 같은 손에 삼지창을 들며, 마개가 꺼지면 담 이음선에 관통 슬릿이 뚫린다.
        /// </summary>
        private static string Report(GameObject root)
        {
            var sb = new StringBuilder();

            int rend = 0, tri = 0, verts = 0;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(false))
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                rend++; tri += mf.sharedMesh.triangles.Length / 3; verts += mf.sharedMesh.vertexCount;
            }
            sb.AppendLine("  보이는 렌더러 " + rend + " · 삼각형 " + tri.ToString("N0") + " · 정점 " + verts.ToString("N0"));

            // 동쪽 담 — 통짜 하나만 켜져 있어야 한다
            sb.AppendLine("  동쪽 담  " + OnOff(root, "Wall_E", true)
                          + OnOff(root, "Wall_E_L", false)
                          + OnOff(root, "Wall_E_R", false)
                          + OnOff(root, "Wall_E_Cap_L", false)
                          + OnOff(root, "Wall_E_Cap_R", false));

            // 마개 — 담을 껐다 켰다 하면 그 밑에서 같이 꺼진다
            int patchOn = 0, patchOff = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("Patch_")) continue;
                if (t.gameObject.activeInHierarchy) patchOn++; else patchOff++;
            }
            sb.AppendLine("  마개  켜짐 " + patchOn + " · 꺼짐 " + patchOff + "   (전달본 기준 27 / 7)");

            // 문지기 — 미러는 X 배율 음수로 낸다
            var s = FindDeep(root, "NPC_Gatekeeper_S");
            var n = FindDeep(root, "NPC_Gatekeeper_N");
            sb.AppendLine("  문지기  남 X배율 " + (s != null ? s.localScale.x.ToString("F0") : "<없음>")
                          + (s != null && s.localScale.x < 0f ? " ✔" : " ✘ 음수여야 미러가 산다")
                          + "  ·  북 X배율 " + (n != null ? n.localScale.x.ToString("F0") : "<없음>"));

            // 콜라이더 — 값을 아끼려고 프리미티브만 쓴다
            int box = root.GetComponentsInChildren<BoxCollider>(true).Length;
            int cap = root.GetComponentsInChildren<CapsuleCollider>(true).Length;
            int mesh = root.GetComponentsInChildren<MeshCollider>(true).Length;
            sb.AppendLine("  콜라이더  Box " + box + " · Capsule " + cap + " · Mesh " + mesh
                          + (mesh == 0 ? " ✔" : " ✘ MeshCollider 는 붙이지 않기로 되어 있다"));

            var b = WorldBounds(root);
            sb.AppendLine("  차지하는 넓이 " + b.size.x.ToString("F1") + " x " + b.size.z.ToString("F1")
                          + " m · 높이 " + b.size.y.ToString("F1") + " m · 밑면 y=" + b.min.y.ToString("F3"));
            return sb.ToString();
        }

        // ── 회색 상자를 세트에 맞추기 ──

        /// <summary>
        /// 회색 상자로 잡아 둔 두 구역을 세운 건물에 맞춘다.
        ///
        /// 심문 구역은 x=12 에 있는데 동헌은 x=13.42 에 선다. 1.42m 차이는 눈으로는
        /// 잘 안 보이지만, 甲과 乙이 마루 끝에 걸터앉게 되는 거리다.
        /// 문서고 구역의 바닥 평면은 8x8m 라 40x35m 마당 한가운데 섬처럼 떠 있게 된다.
        ///
        /// ★되돌리기(Ctrl+Z)가 듣는다. 마음에 안 들면 그냥 되돌리십시오.
        /// </summary>
        [MenuItem("이문록/관아/③ 회색 상자를 세트에 맞추기")]
        public static void FitGreybox()
        {
            var root = GameObject.Find(RootName);
            if (root == null) { Debug.LogWarning("[관아] 먼저 ① 세트 놓기 를 하십시오."); return; }

            var sb = new StringBuilder();
            var zone2 = GameObject.Find("Zone_2_동헌(심문)");
            if (zone2 != null)
            {
                Undo.RecordObject(zone2.transform, "구역 맞추기");
                var p = zone2.transform.position;
                zone2.transform.position = new Vector3(DonheonOffset.x, p.y, p.z);
                sb.AppendLine("  심문 구역 x " + p.x.ToString("F2") + " → " + DonheonOffset.x.ToString("F2") + " (동헌 자리)");
            }

            // 마당 바닥을 담이 두르는 넓이만큼 넓힌다. 유니티 Plane 은 배율 1 이 10m 다.
            var zone1 = GameObject.Find("Zone_1_관아문서고");
            if (zone1 != null)
            {
                var floor = zone1.transform.Find("Floor");
                if (floor != null)
                {
                    Undo.RecordObject(floor, "마당 넓히기");
                    var b = WorldBounds(root);
                    floor.localScale = new Vector3(b.size.x / 10f, 1f, b.size.z / 10f);
                    sb.AppendLine("  마당 바닥 " + (b.size.x).ToString("F1") + " x " + (b.size.z).ToString("F1") + " m 로 넓힘");
                }
            }

            // 두 구역의 바닥이 같은 자리에 겹쳐 있다(둘 다 월드 원점). 한 장이면 된다.
            var zone2Floor = zone2 != null ? zone2.transform.Find("Floor") : null;
            if (zone2Floor != null && zone2Floor.gameObject.activeSelf)
            {
                Undo.RecordObject(zone2Floor.gameObject, "겹친 바닥 끄기");
                zone2Floor.gameObject.SetActive(false);
                sb.AppendLine("  심문 구역의 바닥 한 장을 껐다 — 문서고 바닥과 같은 자리에 겹쳐 있었다");
            }

            EditorSceneManagerMarkDirty();
            Debug.Log("[관아] 회색 상자를 맞췄습니다.\n" + sb);
        }

        // ── 텍스처 ──

        /// <summary>
        /// 텍스처를 1024 로 낮추고 ASTC 로 굽는다.
        ///
        /// 전달본 텍스처는 45장 305MB, 그중 39장이 2048x2048 이다. 그대로 두면
        /// 압축 전 기준으로 텍스처 메모리만 800MB 를 넘어 퀘스트에서는 켜지지도 않는다.
        /// 1024 + ASTC 로 내리면 25MB 안팎이 된다.
        ///
        /// ★원본 파일은 건드리지 않는다. 임포터 설정만 바꾸므로 언제든 되돌릴 수 있다.
        /// </summary>
        [MenuItem("이문록/관아/④ 텍스처 1024·ASTC 로 낮추기")]
        public static void ShrinkTextures()
        {
            var folders = new List<string>();
            foreach (var f in new[]
                     {
                         SetRoot + "/Gate/Textures",
                         SetRoot + "/Donheon/Textures",
                         SetRoot + "/Gatekeeper",
                     })
                if (AssetDatabase.IsValidFolder(f)) folders.Add(f);

            if (folders.Count == 0) { Debug.LogWarning("[관아] 텍스처 폴더를 못 찾았습니다."); return; }

            var guids = AssetDatabase.FindAssets("t:Texture2D", folders.ToArray());
            int done = 0;
            long before = 0;
            var sb = new StringBuilder();
            string projRoot = System.IO.Path.GetDirectoryName(Application.dataPath);

            // 한 장씩 다시 굽지 않는다 — 2048 짜리 마흔 장을 낱개로 reimport 하면
            // 에디터가 몇 분씩 멈춘다. 설정만 죄다 바꿔 두고 마지막에 한 번에 굽는다.
            try
            {
                AssetDatabase.StartAssetEditing();
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (ti == null) continue;

                    string full = System.IO.Path.Combine(projRoot, path);
                    if (System.IO.File.Exists(full)) before += new System.IO.FileInfo(full).Length;

                    ti.maxTextureSize = 1024;
                    ti.textureCompression = TextureImporterCompression.Compressed;
                    ti.mipmapEnabled = true;

                    // 안드로이드(퀘스트) 쪽은 따로 박아 준다. 기본값에 맡기면 ETC2 로 굽는다.
                    var and = new TextureImporterPlatformSettings
                    {
                        name = "Android",
                        overridden = true,
                        maxTextureSize = 1024,
                        format = TextureImporterFormat.ASTC_6x6,
                        compressionQuality = 50,
                        textureCompression = TextureImporterCompression.Compressed,
                    };
                    ti.SetPlatformTextureSettings(and);

                    // SetDirty 만으로는 .meta 만 더러워지고 텍스처는 2048 인 채로 남는다.
                    // 그 뒤 Refresh(ForceUpdate) 를 불러도 마찬가지다 — 임포터가
                    // "다시 구워라"는 말을 못 듣는다. SaveAndReimport 가 그 말이다.
                    ti.SaveAndReimport();
                    done++;
                }
            }
            finally
            {
                // StartAssetEditing 로 묶어 두었으므로 위의 SaveAndReimport 들은
                // 예약만 되어 있다. 여기서 한 번에 굽는다.
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            sb.AppendLine("  텍스처 " + done + "장을 1024 · ASTC 6x6 으로 다시 구웠습니다.");
            sb.AppendLine("  디스크 원본은 그대로 " + (before / 1048576.0).ToString("F1") + " MB 입니다 —");
            sb.AppendLine("  줄어드는 것은 빌드에 실리는 크기와 실행 중 텍스처 메모리입니다.");
            Debug.Log("[관아] 텍스처를 낮췄습니다.\n" + sb);
        }

        // ── 잔손 ──

        private static string OnOff(GameObject root, string name, bool want)
        {
            var t = FindDeep(root, name);
            if (t == null) return "\n     " + name + " <없음>";
            bool on = t.gameObject.activeSelf;
            return "\n     " + name.PadRight(14) + (on ? "켜짐" : "꺼짐") + (on == want ? " ✔" : " ✘ " + (want ? "켜야" : "꺼야") + " 한다");
        }

        private static Transform FindDeep(GameObject root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        private static Bounds WorldBounds(GameObject root)
        {
            var rs = root.GetComponentsInChildren<Renderer>(false);
            if (rs.Length == 0) return new Bounds(root.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        private static void EditorSceneManagerMarkDirty()
        {
            var s = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(s);
        }
    }
}
