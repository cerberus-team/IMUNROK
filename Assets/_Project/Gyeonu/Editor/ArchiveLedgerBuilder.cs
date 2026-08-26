using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Gyeonu.EditorTools
{
    /// <summary>
    /// 종막 퍼즐 — 서고 찬장 속 **수령의 장부** 를 한 번에 짓는다 (2026-08-24). 멱등.
    ///   Tools ▸ 이문록 ▸ 서고 ▸ 종막 장부 퍼즐 만들기 / 제거
    ///
    /// 하는 일
    ///   ① 임포트한 기물 12종의 **재질을 물린다** (Meshy FBX는 텍스처가 안 붙어 온다)
    ///   ② 기물 프리팹 12개 — 실측 바운즈로 눕는 자세·크기·콜라이더를 정한다
    ///   ③ 표찰 7장 · 아버지 기록 3칸 · 처리 딱지 3장 — 한지 텍스처를 굽고 얇은 판에 입힌다
    ///   ④ 찬장 <c>SM_Pantry_Chest</c> 의 **문짝 넷을 메시에서 떼어** 여닫이로 만든다
    ///   ⑤ 찬장 속에 3×4 진열 선반을 짜고 자리(LedgerSlot) 스물아홉을 놓는다
    ///   ⑥ <see cref="LedgerPuzzle"/> 를 배선하고 기물·표찰을 **무작위로** 흩는다
    ///   ⑦ C1·C2·C3 소지품 정의와 창고방 서랍장의 최종 물증을 놓는다
    ///
    /// ⚠️ 원본 FBX는 건드리지 않는다. 문짝을 뗀 메시는 새 에셋으로 굽고 씬의 MeshFilter만
    ///    갈아 끼운다 — 제거 메뉴가 원본 메시로 되돌린다.
    /// ⚠️ 사용자가 손으로 배치한 서고 소품은 그대로 둔다. ArchiveFurnisher를 다시 돌리지 않는다.
    /// </summary>
    public static partial class ArchiveLedgerBuilder
    {
        const string 씬 = "Gyeonu_Observatory";
        const string 찬장경로 = "서고_소품/SM_Pantry_Chest";
        const string 루트이름 = "서고_장부";

        const string 기물폴더 = "Assets/_Project/Gyeonu/Art/Materials/Artifacts";
        const string 재질폴더 = "Assets/_Project/Gyeonu/Art/Materials/Ledger";
        const string 텍스처폴더 = "Assets/_Project/Gyeonu/Art/Textures/Ledger";
        const string 메시폴더 = "Assets/_Project/Gyeonu/Art/Models/Ledger";
        const string 프리팹폴더 = "Assets/_Project/Gyeonu/Prefabs/Ledger";
        const string 자료폴더 = "Assets/_Project/Gyeonu/Data/Ledger";
        // 기존 소지품 정의가 모여 있는 곳 — Resources 경로가 같아야 세이브 복원·디버그 지급이 찾는다
        const string 소지품폴더 = "Assets/_Project/Gyeonu/Resources/GyeonuItems";

        // ── 판 치수 (찬장 로컬 m). 화면 기준 sx는 오른쪽이 +, 찬장 로컬 X는 그 반대다 ──
        const float 판Z = 0.175f;        // 진열판 앞면
        const float 칸너비 = 0.1475f;
        const float 칸높이 = 0.12f;
        const float 격자좌 = -0.235f;    // 기물 열이 시작하는 화면 x
        const float 행머리x = -0.295f;
        const float 열머리y = 1.505f;
        static readonly float[] 행y = { 1.40f, 1.28f, 1.16f };
        // 아래 칸(하칸 0.587~1.069)의 2단계 살림 — 위에서부터 기록 글판 / 대조 자리 / 처리 딱지
        const float 기록y = 0.955f;
        const float 대조y = 0.752f;
        const float 딱지y = 0.638f;

        /// <summary>찬장 안쪽 개구부의 폭. 문널이 −0.352~+0.358, 문틀 기둥이 ±0.366에 있다(실측).</summary>
        const float 안쪽폭 = 0.716f;
        /// <summary>선반널·칸막이 깊이. 뒷판 앞면(판Z−0.012)부터 앞으로 나온다.</summary>
        const float 선반깊이 = 0.070f;

        const float 기물크기 = 0.100f;   // 가장 긴 변을 이 길이로 맞춘다
        const float 표찰너비 = 0.105f;
        const float 표찰높이 = 0.062f;

        const int 씨앗 = 20260824;

        // ─────────────────────────────────────────────────────
        [MenuItem("Tools/이문록/서고/종막 장부 퍼즐 만들기", false, 40)]
        public static void Build()
        {
            if (!GuardScene()) return;
            var chest = GameObject.Find(찬장경로);
            if (chest == null) { EditorUtility.DisplayDialog("장부", 찬장경로 + " 을 찾지 못했다.", "확인"); return; }

            EnsureFolders();
            try
            {
                EditorUtility.DisplayProgressBar("종막 장부", "기물 재질을 물린다", 0.05f);
                var mats = BuildArtifactMaterials();

                EditorUtility.DisplayProgressBar("종막 장부", "기물 프리팹을 굽는다", 0.2f);
                var piecePrefabs = BuildArtifactPrefabs(mats);

                EditorUtility.DisplayProgressBar("종막 장부", "표찰·기록을 굽는다", 0.45f);
                var labelPrefabs = BuildLabelPrefabs();
                var recordPlates = BuildRecordPlates();
                var tagPlates = BuildHandlingTags();

                // ⚠️ 프리팹을 씬에 심기 전에 반드시 한 번 디스크와 맞춘다 (2026-08-24 실측).
                //    제자리로 고쳐 쓴 메시 에셋은 SetDirty 상태로만 남아 있어, 그대로 두면
                //    프리팹이 파일에는 옳은 GUID를 적어 두고도 **메모리에서는 NULL**로 뜬다.
                //    표찰 일곱 장이 통째로 안 보이는데 파일을 열어 보면 멀쩡해 원인이 잘 안 보인다.
                AssetDatabase.SaveAssets();
                foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { 프리팹폴더 }))
                    AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(g), ImportAssetOptions.ForceUpdate);

                EditorUtility.DisplayProgressBar("종막 장부", "찬장 문을 떼어 낸다", 0.6f);
                var doors = SplitChestDoors(chest);

                EditorUtility.DisplayProgressBar("종막 장부", "선반을 짠다", 0.75f);
                var root = BuildBoard(chest, piecePrefabs, labelPrefabs, recordPlates, tagPlates, doors);

                EditorUtility.DisplayProgressBar("종막 장부", "소지품을 놓는다", 0.9f);
                BuildClueItems(root);

                RemoveDoorMarkers();

                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
                Debug.Log("[종막 장부] 완료 — " + 루트이름);
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        [MenuItem("Tools/이문록/서고/종막 장부 퍼즐 제거", false, 41)]
        public static void Remove()
        {
            if (!GuardScene()) return;
            var old = GameObject.Find(루트이름);
            if (old != null) Object.DestroyImmediate(old);
            RestoreChest();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            Debug.Log("[종막 장부] 제거했다.");
        }

        /// <summary>
        /// 문짝 자리를 일러 주려고 씬에 놓아 둔 표시(「갈라지는 부분」·「힌지」)를 치운다.
        /// 그 값은 <c>ArchiveLedgerBuilder_Board.cs</c> 위쪽 상수에 옮겨 적어 두었으므로
        /// 표시가 없어도 같은 자리에 다시 만들어진다.
        /// </summary>
        static void RemoveDoorMarkers()
        {
            var doomed = new List<GameObject>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name.Contains("갈라지는 부분") || t.name == "힌지" || t.name.StartsWith("힌지 "))
                    doomed.Add(t.gameObject);
            foreach (var g in doomed) Object.DestroyImmediate(g);
            if (doomed.Count > 0) Debug.Log("[종막 장부] 표시용 오브젝트 " + doomed.Count + "개를 치웠다");
        }

        static bool GuardScene()
        {
            var s = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (s.name == 씬) return true;
            EditorUtility.DisplayDialog("장부", "이 도구는 " + 씬 + " 씬에서만 쓴다.\n지금: " + s.name, "확인");
            return false;
        }

        static void EnsureFolders()
        {
            foreach (var p in new[] { 재질폴더, 텍스처폴더, 메시폴더, 프리팹폴더, 자료폴더, 소지품폴더 })
            {
                var parts = p.Split('/');
                string cur = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = cur + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
                    cur = next;
                }
            }
        }

        // ── ① 기물 재질 ──────────────────────────────────────
        /// <summary>
        /// Meshy FBX는 **재질에 텍스처가 안 붙어 온다** (내장 Material.001의 _BaseMap이 비어 있다).
        /// 원본 폴더 규칙상 임포터를 고치지 않고, 옆에 놓인 PNG를 물린 재질을 우리 폴더에 새로 만든다.
        ///
        /// ⚠️ Meshy의 metallic_roughness는 glTF 규약(G=거칠기, B=금속)이라 URP Lit의
        ///    _MetallicGlossMap(R=금속, A=매끄러움)과 채널이 다르다. 굳이 변환하지 않고
        ///    재질 종류(쇠/유리)별 상수로 둔다 — 베이스 텍스처에 이미 광택·그늘이 그려져 있다.
        /// </summary>
        static Dictionary<int, Material> BuildArtifactMaterials()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var result = new Dictionary<int, Material>();
            foreach (var def in LedgerData.Pieces)
            {
                string dir = 기물폴더 + "/" + def.id;
                var modelPath = AssetDatabase.FindAssets("t:Model", new[] { dir })
                                             .Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
                if (modelPath == null) { Debug.LogWarning("[종막 장부] 기물 " + def.id + " 모델 없음"); continue; }
                string folder = System.IO.Path.GetDirectoryName(modelPath).Replace("\\", "/");
                string bn = System.IO.Path.GetFileNameWithoutExtension(modelPath);

                string mp = 재질폴더 + "/M_기물_" + def.id.ToString("00") + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(mp);
                if (mat == null) { mat = new Material(lit); AssetDatabase.CreateAsset(mat, mp); }
                mat.shader = lit;
                mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + bn + ".png"));
                mat.SetColor("_BaseColor", Color.white);
                if (def.touch == LedgerTouch.유리)
                {
                    mat.SetFloat("_Metallic", 0.05f);
                    mat.SetFloat("_Smoothness", 0.72f);
                }
                else
                {
                    mat.SetFloat("_Metallic", 0.55f);
                    mat.SetFloat("_Smoothness", 0.34f);
                }
                EditorUtility.SetDirty(mat);
                result[def.id] = mat;
            }
            AssetDatabase.SaveAssets();
            return result;
        }

        // ── ② 기물 프리팹 ────────────────────────────────────
        static Dictionary<int, GameObject> BuildArtifactPrefabs(Dictionary<int, Material> mats)
        {
            var result = new Dictionary<int, GameObject>();
            foreach (var def in LedgerData.Pieces)
            {
                string dir = 기물폴더 + "/" + def.id;
                var modelPath = AssetDatabase.FindAssets("t:Model", new[] { dir })
                                             .Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
                if (modelPath == null) continue;
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                var mesh = src.GetComponentInChildren<MeshFilter>().sharedMesh;

                var root = new GameObject("기물_" + def.id.ToString("00") + "_" + def.name);
                var model = new GameObject("모델");
                model.transform.SetParent(root.transform, false);
                model.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = model.AddComponent<MeshRenderer>();
                if (mats.TryGetValue(def.id, out var m)) mr.sharedMaterial = m;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // 찬장 속 작은 기물

                // 눕는 자세 — **가장 얇은 축을 앞(+Z)으로, 가장 긴 축을 가로(X)로**.
                // 축이든 고리든 조각이든 이 규칙 하나로 "가장 넓은 면이 보이게" 눕는다.
                var b = mesh.bounds;
                var q = LieFlat(b.size, out Vector3 shown);
                float scale = 기물크기 / Mathf.Max(1e-4f, shown.x);
                // 세로가 칸을 넘치면 세로 기준으로 다시 맞춘다
                if (shown.y * scale > 칸높이 * 0.80f) scale = 칸높이 * 0.80f / shown.y;

                model.transform.localRotation = q;
                model.transform.localScale = Vector3.one * scale;
                // 바운즈 중심을 기물 뿌리 원점으로 — 자리에 앉히면 칸 한가운데 온다
                model.transform.localPosition = -(q * b.center) * scale;

                var box = root.AddComponent<BoxCollider>();
                box.isTrigger = true;                       // 보행·물리에 끼어들지 않는다
                box.size = new Vector3(Mathf.Max(shown.x * scale, 0.02f),
                                       Mathf.Max(shown.y * scale, 0.02f),
                                       Mathf.Max(shown.z * scale, 0.02f));

                var piece = root.AddComponent<LedgerPiece>();
                piece.isLabel = false;
                piece.id = def.id;
                piece.inspectItem = BuildInspectItem(def, modelPath, q);

                string pp = 프리팹폴더 + "/기물_" + def.id.ToString("00") + ".prefab";
                var saved = PrefabUtility.SaveAsPrefabAsset(root, pp);
                Object.DestroyImmediate(root);
                result[def.id] = saved;
            }
            AssetDatabase.SaveAssets();
            return result;
        }

        /// <summary>
        /// 바운즈 크기만 보고 **가장 넓은 면이 앞을 보도록** 눕히는 회전.
        /// 돌려준 <paramref name="shown"/>은 눕힌 뒤의 (가로, 세로, 두께).
        /// </summary>
        static Quaternion LieFlat(Vector3 size, out Vector3 shown)
        {
            int iMin = 0, iMax = 0;
            for (int i = 1; i < 3; i++)
            {
                if (size[i] < size[iMin]) iMin = i;
                if (size[i] > size[iMax]) iMax = i;
            }
            int iMid = 3 - iMin - iMax;
            if (iMin == iMax) { iMin = 2; iMax = 0; iMid = 1; }     // 정육면체에 가까운 경우

            var m = Matrix4x4.identity;
            m.SetColumn(iMax, Vector3.right);
            m.SetColumn(iMid, Vector3.up);
            m.SetColumn(iMin, Vector3.forward);
            // 왼손잡이가 되면 거울상이 된다 — 두께 축을 뒤집어 오른손 좌표계로 되돌린다
            if (Vector3.Dot(Vector3.Cross(m.GetColumn(0), m.GetColumn(1)), m.GetColumn(2)) < 0f)
                m.SetColumn(iMin, -Vector3.forward);

            shown = new Vector3(size[iMax], size[iMid], size[iMin]);
            return Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1));
        }

        /// <summary>확대 조사에 쓸 소지품 정의 — 소지품 목록에는 들어가지 않는다.</summary>
        static InventoryItem BuildInspectItem(LedgerPieceDef def, string modelPath, Quaternion lieFlat)
        {
            string p = 자료폴더 + "/조사_기물_" + def.id.ToString("00") + ".asset";
            var it = AssetDatabase.LoadAssetAtPath<InventoryItem>(p);
            if (it == null) { it = ScriptableObject.CreateInstance<InventoryItem>(); AssetDatabase.CreateAsset(it, p); }
            it.itemId = "LEDGER_" + def.id.ToString("00");
            it.displayName = def.name;
            it.description = "";
            it.modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(프리팹폴더 + "/기물_" + def.id.ToString("00") + ".prefab");
            // ⚠️ 미리보기 카메라는 무대 원점에서 **-Z에 서서 +Z를 본다** — 즉 모델의 뒷면을 본다.
            //    기물은 앞면(+Z)이 선반 밖을 향하도록 눕혀 두었으므로 반 바퀴 돌려야 한다.
            //    단순한 취향 문제가 아니다: 특징 문구가 "오른쪽 아래에 눌린 자국"처럼 좌우를
            //    가리키므로, 뒷면을 보여 주면 좌우가 뒤집혀 글과 실물이 어긋난다.
            it.previewEuler = new Vector3(0f, 180f, 0f);
            it.previewZoom = 1f;
            it.usable = false;
            it.autoShowOnPickup = false;
            EditorUtility.SetDirty(it);
            return it;
        }

        // ── ③ 표찰·기록·딱지 ────────────────────────────────
        static Dictionary<int, GameObject> BuildLabelPrefabs()
        {
            var result = new Dictionary<int, GameObject>();
            for (int i = 0; i < 7; i++)
            {
                bool isMark = i < 4;
                string text = isMark ? ((LedgerMark)i).ToString() : ((LedgerSite)(i - 4)).ToString();
                string tp = 텍스처폴더 + "/T_표찰_" + i + ".png";
                HanjiTextBaker.Bake(tp, 320, 190,
                    new[] { new HanjiTextBaker.Line(text, 88) },
                    seed: 씨앗 + i * 37, border: true, seal: false);

                var mat = MakePaperMaterial("M_표찰_" + i, tp);
                var go = MakePlate("표찰_" + i + "_" + text, 표찰너비, 표찰높이, 0.004f, mat);
                var piece = go.AddComponent<LedgerPiece>();
                piece.isLabel = true;
                piece.id = i;
                piece.labelIsMark = isMark;
                var box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(표찰너비, 표찰높이, 0.012f);

                string pp = 프리팹폴더 + "/표찰_" + i + ".prefab";
                var saved = PrefabUtility.SaveAsPrefabAsset(go, pp);
                Object.DestroyImmediate(go);
                result[i] = saved;
            }
            return result;
        }

        static Dictionary<int, GameObject> BuildRecordPlates()
        {
            var result = new Dictionary<int, GameObject>();
            for (int i = 0; i < LedgerData.Records.Length; i++)
            {
                var rec = LedgerData.Records[i];
                var lines = new List<HanjiTextBaker.Line>
                {
                    new HanjiTextBaker.Line(rec.name, 46, new Color(0.098f, 0.082f, 0.075f)) { gap = 22 },
                };
                foreach (var t in rec.shortTraits) lines.Add(new HanjiTextBaker.Line(t, 38) { gap = 14 });

                string tp = 텍스처폴더 + "/T_기록_" + i + ".png";
                HanjiTextBaker.Bake(tp, 460, 460, lines, seed: 씨앗 + 500 + i * 13, border: true);
                var mat = MakePaperMaterial("M_기록_" + i, tp);
                var go = MakePlate("기록_" + i, 0.225f, 0.225f, 0.004f, mat);
                result[i] = go;   // 씬에 바로 붙인다 (프리팹으로 만들 이유가 없다)
            }
            return result;
        }

        static Dictionary<int, GameObject> BuildHandlingTags()
        {
            var result = new Dictionary<int, GameObject>();
            int i = 0;
            foreach (var def in LedgerData.Answers())
            {
                // ⚠️ 한자로 적으면 읽을 수가 없다 (2026-08-25 수정). 3단계 추론은 이 넉 자를
                //    **읽어서** 「셋 다 같은 손, 같은 곳」임을 알아채는 것이므로, 못 읽으면
                //    퍼즐이 통째로 무너진다. 조선 문서의 결은 한지·먹빛·주칠 관인이 맡고
                //    글자는 한국어로 적는다.
                string tp = 텍스처폴더 + "/T_딱지_" + i + ".png";
                var 주칠 = new Color(0.616f, 0.176f, 0.137f);
                HanjiTextBaker.Bake(tp, 430, 210, new[]
                {
                    new HanjiTextBaker.Line("처리 — " + def.handler, 54, 주칠) { gap = 16 },
                    new HanjiTextBaker.Line("보낸 곳 — " + def.destination, 54, 주칠),
                }, seed: 씨앗 + 900 + i * 7, border: true);
                var mat = MakePaperMaterial("M_딱지_" + i, tp);
                var go = MakePlate("딱지_" + i, 0.209f, 0.102f, 0.003f, mat);
                result[i] = go;
                i++;
            }
            return result;
        }

        static Material MakePaperMaterial(string name, string texPath)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            string mp = 재질폴더 + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (mat == null) { mat = new Material(lit); AssetDatabase.CreateAsset(mat, mp); }
            mat.shader = lit;
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 0.08f);   // 한지는 광택이 없다
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>앞면(+Z)에만 그림이 오는 얇은 판. 종이·목패 어디에나 쓴다.</summary>
        static GameObject MakePlate(string name, float w, float h, float t, Material front)
        {
            var go = new GameObject(name);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = PlateMesh(name, w, h, t);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { front, EdgeMaterial() };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        static Material edgeMat;
        static Material EdgeMaterial()
        {
            if (edgeMat != null) return edgeMat;
            string mp = 재질폴더 + "/M_판_옆면.mat";
            edgeMat = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (edgeMat == null)
            {
                edgeMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(edgeMat, mp);
            }
            edgeMat.SetColor("_BaseColor", new Color(0.62f, 0.56f, 0.45f));
            edgeMat.SetFloat("_Smoothness", 0.05f);
            EditorUtility.SetDirty(edgeMat);
            return edgeMat;
        }

        /// <summary>서브메시 0 = 앞면(그림), 1 = 뒤·옆면. 비밀지도 종이와 같은 구성.</summary>
        static Mesh PlateMesh(string name, float w, float h, float t)
        {
            string mp = 메시폴더 + "/" + name + "_판.asset";
            float x = w * 0.5f, y = h * 0.5f, z = t * 0.5f;
            var v = new List<Vector3>(); var uv = new List<Vector2>();
            var front = new List<int>(); var rest = new List<int>();

            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, bool isFront)
            {
                int i0 = v.Count;
                v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                // ⚠️ 앞면은 법선이 **+Z**다. +Z 쪽에서 보는 카메라의 화면 오른쪽은 월드 −X이므로,
                //    u가 +X를 따라 커지면 글씨가 **좌우로 뒤집혀** 보인다 (표찰의 「구름」이
                //    「믈두」로 읽혔다, Play 실측). 앞면만 u를 뒤집어 준다.
                if (isFront)
                {
                    uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(0, 0));
                    uv.Add(new Vector2(0, 1)); uv.Add(new Vector2(1, 1));
                }
                else
                {
                    uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0));
                    uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
                }
                var list = isFront ? front : rest;
                // ⚠️ 네 귀퉁이를 **바깥에서 볼 때 반시계**로 적어 두었으므로 감는 순서는 0-1-2 / 0-2-3.
                //    뒤집어 적으면 앞면이 컬링돼 종이 대신 옆면 재질(민무늬)만 보인다 — 실측으로 물린 자리다.
                list.AddRange(new[] { i0, i0 + 1, i0 + 2, i0, i0 + 2, i0 + 3 });
            }
            Quad(new Vector3(-x, -y, z), new Vector3(x, -y, z), new Vector3(x, y, z), new Vector3(-x, y, z), true);
            Quad(new Vector3(x, -y, -z), new Vector3(-x, -y, -z), new Vector3(-x, y, -z), new Vector3(x, y, -z), false);
            Quad(new Vector3(-x, -y, -z), new Vector3(-x, -y, z), new Vector3(-x, y, z), new Vector3(-x, y, -z), false);
            Quad(new Vector3(x, -y, z), new Vector3(x, -y, -z), new Vector3(x, y, -z), new Vector3(x, y, z), false);
            Quad(new Vector3(-x, y, z), new Vector3(x, y, z), new Vector3(x, y, -z), new Vector3(-x, y, -z), false);
            Quad(new Vector3(-x, -y, -z), new Vector3(x, -y, -z), new Vector3(x, -y, z), new Vector3(-x, -y, z), false);

            var mesh = new Mesh { name = name + "_판" };
            mesh.SetVertices(v); mesh.SetUVs(0, uv);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(front, 0); mesh.SetTriangles(rest, 1);
            mesh.RecalculateNormals();   // ⚠️ 단독 생성 메시는 노멀이 없다 — 반드시 직접 계산
            mesh.RecalculateBounds();

            return StoreMesh(mesh, mp);
        }

        /// <summary>
        /// 만든 메시를 에셋에 넣는다. 이미 있으면 **그 에셋을 제자리에서 고쳐 쓴다**.
        ///
        /// ⚠️ 지웠다 다시 만들면 안 된다 (2026-08-24 실측). 같은 프레임 안에서
        ///    <c>DeleteAsset</c> → <c>CreateAsset</c> 을 같은 경로에 하면 새 메시가 에셋으로
        ///    앉지 못하고, 그 뒤 저장한 프리팹의 MeshFilter가 **NULL**이 된다.
        ///    첫 빌드는 멀쩡하고 두 번째 빌드부터 표찰이 통째로 사라져 원인이 잘 안 보인다.
        /// </summary>
        static Mesh StoreMesh(Mesh built, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(built, path); return built; }

            existing.Clear();
            existing.name = built.name;
            existing.SetVertices(built.vertices);
            // ⚠️ 정점 속성을 하나라도 빠뜨리면 안 된다 — 특히 **탄젠트**. 노멀맵을 쓰는 재질에서
            //    탄젠트가 없으면 같은 재질인데도 음영이 달라진다 (찬장에서 실측).
            int n = built.vertexCount;
            if (built.normals.Length == n) existing.SetNormals(built.normals);
            if (built.tangents.Length == n) existing.SetTangents(built.tangents);
            if (built.uv.Length == n) existing.SetUVs(0, built.uv);
            if (built.uv2.Length == n) existing.SetUVs(1, built.uv2);
            if (built.uv3.Length == n) existing.SetUVs(2, built.uv3);
            if (built.uv4.Length == n) existing.SetUVs(3, built.uv4);
            if (built.colors.Length == n) existing.SetColors(built.colors);
            existing.subMeshCount = built.subMeshCount;
            for (int i = 0; i < built.subMeshCount; i++) existing.SetTriangles(built.GetTriangles(i), i);
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(built);
            return existing;
        }
    }
}
