using UnityEditor;
using UnityEngine;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// 조사청의 기능을 <b>실제로 보이는 물건</b>에 잇는다. 메뉴: [이문록 ▸ 조사청 배선 잇기]
    ///
    /// 왜 필요했나: 조사청이 두 개였다. 원점(0,0,0) 언저리에 회색 상자로 지은 프로토타입이
    /// 있고 — 사건 큐브·선반·기록대·봉서함 — 스크립트는 전부 그쪽에 붙어 있었다. 그런데
    /// 눈에 보이는 조사청은 광풍각 안이고, PlayerStart 가 카메라를 그리로 옮긴다.
    /// 그래서 실행하면 플레이어는 광풍각에 서 있고 작동하는 물건은 삼백 미터 밖에 있었다.
    /// 아무것도 누를 수 없는 방이었던 것이다.
    ///
    /// 이 생성기가 하는 일 셋:
    ///   ① 기능을 진짜 물건으로 옮긴다 — 사건 선택은 사건판의 문서 셋으로, 기록대는 문갑으로.
    ///   ② 세계 상태(창호가 밝아 오는 연출)에 달빛·창호·창빛을 걸어 준다.
    ///   ③ 도구 선반을 방 안에 세우고, 집으면 도구벨트로 들어가게 한다.
    ///
    /// 회색 프로토타입은 지우지 않고 <c>_옛프로토타입</c> 아래로 모아 꺼 둔다.
    /// 값(색·씬 이름)이 거기 들어 있어, 잘못 옮긴 것이 있으면 켜서 대조할 수 있다.
    ///
    /// 여러 번 눌러도 같은 결과가 되게 짰다(있으면 쓰고 없으면 만든다).
    /// </summary>
    public static class HubWiring
    {
        private const string RoomName = "조사청_실내";
        private const string OldName = "_옛프로토타입";
        private const string ToolDataDir = "Assets/_Project/Data/Tools";

        /// <summary>
        /// 물건을 놓을 <b>기준</b>. 세간(문갑·사건판)은 원본 광풍각 안에 있으므로 기준도
        /// 그쪽이다. 지어 둔 방은 마음에 들 때까지 옆에 빼두는 미리보기라, 그걸 기준으로
        /// 놓으면 방을 옮길 때 봉서함까지 딸려가 조사청이 텅 빈다.
        /// 원본이 없으면 지은 방으로 물러선다.
        /// </summary>
        private static Transform Reference()
        {
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name != "소쇄원_정원") continue;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Gwangpunggak_Pavilion") return t;
            }
            var room = GameObject.Find(RoomName);
            return room != null ? room.transform : null;
        }

        /// <summary>세간이 모여 있는 묶음. 새로 놓는 물건도 여기로 넣는다.</summary>
        private static Transform PropRoot()
        {
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == "조사청_소품") return root.transform;
            return null;
        }

        [MenuItem("이문록/조사청 배선 잇기")]
        public static void Wire()
        {
            var room = GameObject.Find(RoomName);
            if (room == null)
            {
                EditorUtility.DisplayDialog("이문록",
                    "먼저 [이문록 ▸ 조사청 실내 짓기] 로 방을 지어 주세요.", "확인");
                return;
            }

            int done = 0;
            done += WireCaseDocuments() ? 1 : 0;
            done += WireRecordStand(room.transform) ? 1 : 0;
            done += WireBongseoBox(room.transform) ? 1 : 0;
            done += WireWorldState(room.transform) ? 1 : 0;
            done += WireJournalFont() ? 1 : 0;
            done += PlaceToolsOnChest() ? 1 : 0;
            done += MoveTutorialNote(room.transform) ? 1 : 0;
            ParkOldPrototype();

            EditorSceneMarkDirty();
            Debug.Log($"[조사청] 배선 {done}군데를 이었습니다.");
        }

        private static void EditorSceneMarkDirty()
        {
            var s = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(s);
        }

        // ── ① 기능을 진짜 물건으로 ────────────────────

        /// <summary>사건판에 붙은 문서 셋이 곧 사건 선택이다. 회색 큐브가 하던 일을 넘겨받는다.</summary>
        private static bool WireCaseDocuments()
        {
            var pile = GameObject.Find("문서 더미");
            if (pile == null) { Debug.LogWarning("[조사청] '문서 더미'를 못 찾았습니다."); return false; }

            (string name, CaseId id, string scene)[] map =
            {
                ("문서0", CaseId.Case1_Onggojip, "Onggojip"),
                ("문서1", CaseId.Case2_Seocheon, "Seocheon"),
                ("문서2", CaseId.Case3_Gyeonu,   "Gyeonu"),
            };

            foreach (var (name, id, scene) in map)
            {
                var t = pile.transform.Find(name);
                if (t == null) { Debug.LogWarning($"[조사청] {name} 없음"); continue; }

                // 누르려면 레이가 맞을 것이 있어야 한다. 문서는 콜라이더가 없었다.
                if (t.GetComponent<Collider>() == null)
                {
                    var bc = Undo.AddComponent<BoxCollider>(t.gameObject);
                    var r = t.GetComponent<Renderer>();
                    if (r != null)
                    {
                        // 종잇장이라 얇아서 그대로면 겨냥이 어렵다. 앞뒤로만 조금 두껍게.
                        var size = t.InverseTransformVector(r.bounds.size);
                        bc.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y),
                                              Mathf.Max(Mathf.Abs(size.z), 0.25f));
                    }
                }

                var cube = t.GetComponent<CaseCube>();
                if (cube == null) cube = Undo.AddComponent<CaseCube>(t.gameObject);

                var so = new SerializedObject(cube);
                so.FindProperty("_caseId").enumValueIndex = (int)id;
                so.FindProperty("_caseSceneName").stringValue = scene;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            return true;
        }

        /// <summary>기록대는 문갑이 맡는다 — 판결패가 그 위에 쌓인다.</summary>
        private static bool WireRecordStand(Transform room)
        {
            var chest = GameObject.Find("문갑");
            if (chest == null) { Debug.LogWarning("[조사청] '문갑'을 못 찾았습니다."); return false; }

            var stand = chest.GetComponent<RecordStand>();
            if (stand == null) stand = Undo.AddComponent<RecordStand>(chest.gameObject);

            var anchor = chest.transform.Find("StackAnchor");
            if (anchor == null)
            {
                var go = new GameObject("StackAnchor");
                Undo.RegisterCreatedObjectUndo(go, "기록대 기준점");
                go.transform.SetParent(chest.transform, false);
                anchor = go.transform;
            }
            // 문갑 <b>윗면</b>에 얹어야 한다. 물건 원점은 대개 바닥이라 그대로 두면
            // 판결패가 문갑 속에 파묻힌다.
            var rends = chest.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                anchor.position = new Vector3(b.center.x, b.max.y + 0.01f, b.center.z);
            }

            var so = new SerializedObject(stand);
            so.FindProperty("_stackAnchor").objectReferenceValue = anchor;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        /// <summary>봉서함은 대신할 물건이 아직 없다. 회색 상자를 방 안으로 들여놓는다.</summary>
        private static bool WireBongseoBox(Transform room)
        {
            var box = Object.FindFirstObjectByType<BongseoBox>(FindObjectsInactive.Include);
            if (box == null) return false;

            var basis = Reference();
            if (basis == null) return false;

            Undo.RecordObject(box.transform, "봉서함 자리");
            // <b>먼저 세간 묶음으로 옮긴다.</b> 봉서함은 옛 프로토타입(Zone_BongseoBox)의
            // 자식이라, 자리만 옮겨 두면 뒤에서 프로토타입을 끌 때 같이 꺼져 버린다.
            // 한 번 그렇게 되어 방 안에 봉서함이 없었다.
            var props = PropRoot();
            Undo.SetTransformParent(box.transform, props != null ? props : basis, "봉서함을 세간으로");
            // 가운데 방, 사건판 옆. 왕의 명이 닿는 자리이니 사건판과 한 눈에 들어와야 한다.
            box.transform.position = basis.TransformPoint(new Vector3(1.05f, 0.71f + 0.25f, 1.05f));
            box.transform.rotation = basis.rotation;
            box.transform.localScale = new Vector3(0.5f, 0.5f, 0.35f);
            box.name = "봉서함";
            box.gameObject.SetActive(true);
            return true;
        }

        // ── ② 세계 상태 ───────────────────────────────

        /// <summary>
        /// 세 사건을 다 풀면 창호가 밝아 오는 연출. 달빛·창호·창빛이 비어 있어
        /// 여태 아무 일도 일어나지 않았다.
        /// </summary>
        private static bool WireWorldState(Transform room)
        {
            var ws = Object.FindFirstObjectByType<WorldStateController>(FindObjectsInactive.Include);
            if (ws == null) return false;

            var sun = GameObject.Find("달빛(Directional Light)");
            // 창호는 남쪽 문 한 짝을 쓴다. 밝아 오는 것을 눈으로 볼 자리는 거기다.
            var leaf = room.Find("구조/문/문_중방_남/문_중방_남_1")
                       ?? room.Find("구조/문/문_중방_남/문_중방_남_0");

            var lightGo = GameObject.Find("창빛");
            if (lightGo == null)
            {
                lightGo = new GameObject("창빛");
                Undo.RegisterCreatedObjectUndo(lightGo, "창빛");
                var l = lightGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 9f;
                l.color = new Color(1f, 0.93f, 0.78f);
                l.intensity = 0f;      // 닫힘 상태에서 시작. WorldState 가 올린다.
            }
            // 문 바깥에 두어야 빛이 창호를 통해 들어오는 것처럼 보인다.
            var basis = Reference() ?? room;
            lightGo.transform.position = basis.TransformPoint(new Vector3(0f, 2.0f, -2.6f));

            var so = new SerializedObject(ws);
            if (sun != null) so.FindProperty("_sun").objectReferenceValue = sun.GetComponent<Light>();
            if (leaf != null) so.FindProperty("_windowRenderer").objectReferenceValue = leaf.GetComponent<Renderer>();
            so.FindProperty("_windowLight").objectReferenceValue = lightGo.GetComponent<Light>();
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        /// <summary>수첩에 한글 폰트를 물린다. 비워 두면 빌드에서 글자가 깨진다.</summary>
        private static bool WireJournalFont()
        {
            var view = Object.FindFirstObjectByType<JournalView>(FindObjectsInactive.Include);
            if (view == null) return false;

            var font = AssetDatabase.LoadAssetAtPath<Font>(
                "Assets/_Project/_Common/Art/Fonts/ChosunCentennial_otf.otf");
            if (font == null) return false;

            var so = new SerializedObject(view);
            so.FindProperty("_font").objectReferenceValue = font;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        // ── ③ 도구 익히기 ────────────────────────────

        /// <summary>
        /// 문갑 위에 도구를 실제 모양으로 올려 둔다. 누르면 <see cref="ToolTutorial"/> 이
        /// 눈앞으로 들어 올려 쓰는 법을 짚어 준다.
        ///
        /// 회색 상자를 선반에 얹어 두었던 것을 걷어낸다 — 무엇인지 알아볼 수 없는 물건은
        /// 집고 싶지도 않고, 집어 봐야 무엇에 쓰는지도 모른다.
        /// 벨트(ToolbeltHud)가 씬에 없으면 같이 만든다.
        /// </summary>
        private static bool PlaceToolsOnChest()
        {
            var belt = Object.FindFirstObjectByType<ToolbeltHud>(FindObjectsInactive.Include);
            if (belt == null)
            {
                var go = new GameObject("_도구벨트");
                Undo.RegisterCreatedObjectUndo(go, "도구벨트");
                belt = go.AddComponent<ToolbeltHud>();
            }

            var chest = GameObject.Find("문갑");
            if (chest == null) { Debug.LogWarning("[조사청] '문갑'을 못 찾았습니다."); return false; }

            // 도구 넷을 얹으려면 문갑이 좁다. 원래 세간이라 크기를 바꿔도 되는 물건이다.
            Undo.RecordObject(chest.transform, "문갑 키우기");
            chest.transform.localScale = Vector3.one * 1.5f;

            // 앞서 지은 선반은 걷어낸다.
            var room = GameObject.Find(RoomName);
            if (room != null)
            {
                var shelf = room.transform.Find("도구선반");
                if (shelf != null) Undo.DestroyObjectImmediate(shelf.gameObject);
            }

            var holder = chest.transform.Find("도구");
            if (holder != null) Undo.DestroyObjectImmediate(holder.gameObject);
            var group = new GameObject("도구");
            Undo.RegisterCreatedObjectUndo(group, "도구 놓기");
            group.transform.SetParent(chest.transform, false);

            // 문갑 윗면을 잰다. 물건 원점은 대개 바닥이라 그대로 얹으면 파묻힌다.
            var b = WorldBounds(chest);

            (string tool, string model, float size, string[] steps)[] set =
            {
                ("Tool_journal.asset", "서책", 0.20f, new[]{
                    "수첩이다. 찾은 단서가 여기에 저절로 적힌다.",
                    "손에 들고 누르면 펼쳐진다. 심문 중에는 적힌 단서를 골라 들이밀 수도 있다.",
                }),
                ("Tool_map.asset", "두루마리", 0.30f, new[]{
                    "지도다. 지금 선 자리와 가야 할 곳이 그려져 있다.",
                    "손에 들고 누르면 펼쳐지고, 다시 누르면 말린다.",
                }),
                ("Tool_lantern.asset", "등불", 0.28f, new[]{
                    "등불이다. 든 사람의 앞만 밝힌다.",
                    "빛이 닿아야 드러나는 것이 있다 — 재에 남은 자국 같은 것.",
                }),
                ("Tool_magnify.asset", "돋보기", 0.24f, new[]{
                    "돋보기다. 작은 것을 크게 본다.",
                    "물건에 가까이 대고 들여다보면, 맨눈으로는 못 읽던 글자가 드러난다.",
                }),
            };

            int n = set.Length;
            for (int i = 0; i < n; i++)
            {
                var (toolFile, modelName, size, steps) = set[i];
                var def = AssetDatabase.LoadAssetAtPath<ToolDef>($"{ToolDataDir}/{toolFile}");
                if (def == null) { Debug.LogWarning($"[조사청] 도구 정의 없음: {toolFile}"); continue; }

                var go = MakeToolModel(modelName, def.displayName);
                if (go == null) continue;
                Undo.RegisterCreatedObjectUndo(go, "도구 모형");
                go.transform.SetParent(group.transform, true);

                FitTo(go, size);

                // 문갑 윗면에 한 줄로. 긴 쪽을 따라 고르게 벌린다.
                float t = (n == 1) ? 0.5f : i / (float)(n - 1);
                Vector3 along = chest.transform.right * (b.size.x > b.size.z ? 1f : 0f)
                              + chest.transform.forward * (b.size.z >= b.size.x ? 1f : 0f);
                float span = Mathf.Max(b.size.x, b.size.z) * 0.72f;
                Vector3 center = new Vector3(b.center.x, b.max.y, b.center.z);
                go.transform.position = center + along * ((t - 0.5f) * span) + Vector3.up * size * 0.35f;
                go.transform.rotation = chest.transform.rotation;

                var col = go.GetComponent<BoxCollider>();
                if (col == null) col = go.AddComponent<BoxCollider>();
                var gb = WorldBounds(go);
                col.center = go.transform.InverseTransformPoint(gb.center);
                col.size = new Vector3(gb.size.x / Mathf.Max(0.001f, go.transform.lossyScale.x),
                                       gb.size.y / Mathf.Max(0.001f, go.transform.lossyScale.y),
                                       gb.size.z / Mathf.Max(0.001f, go.transform.lossyScale.z));

                var tut = go.GetComponent<ToolTutorial>();
                if (tut == null) tut = go.AddComponent<ToolTutorial>();
                tut.Tool = def;
                tut.Steps = steps;
            }
            return true;
        }

        /// <summary>
        /// 도구 모양 하나를 마련한다. 셋은 이미 있는 것을 쓴다 —
        /// 수첩은 방에 놓인 서책을, 지도는 말린 두루마리를 그대로 쓴다.
        /// 등불·돋보기는 제 모델이 따로 있다(Art/Tools).
        /// </summary>
        private static GameObject MakeToolModel(string kind, string label)
        {
            GameObject go = null;
            switch (kind)
            {
                case "서책":
                {
                    var src = GameObject.Find("서책");
                    if (src != null) go = Object.Instantiate(src);
                    break;
                }
                case "두루마리":
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                        "Assets/_Project/_Common/Prefabs/두루마리.prefab");
                    if (prefab != null)
                    {
                        go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                        // 말린 채로 놓는다. 펼친 종이가 문갑 아래로 흘러내리면 안 된다.
                        var roll = go.GetComponentInChildren<ScrollUnroll>(true);
                        if (roll != null) roll.SetInstant(0f);
                    }
                    break;
                }
                case "등불":
                    go = LoadModel("Assets/_Project/Art/Tools/Lantern");
                    break;
                case "돋보기":
                    go = LoadModel("Assets/_Project/Art/Tools/Magnifier");
                    break;
            }

            if (go == null) { Debug.LogWarning($"[조사청] 도구 모양을 못 만들었습니다: {kind}"); return null; }
            go.name = "도구_" + label;
            go.SetActive(true);
            return go;
        }

        /// <summary>폴더에서 첫 모델(fbx)을 찾아 놓는다. 파일 이름이 길고 자주 바뀌어 폴더로 찾는다.</summary>
        private static GameObject LoadModel(string folder)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset != null) return (GameObject)PrefabUtility.InstantiatePrefab(asset);
            }
            return null;
        }

        /// <summary>가장 긴 변이 이만큼(m) 되게 줄인다. 받아온 모델은 크기가 제각각이다.</summary>
        private static void FitTo(GameObject go, float longest)
        {
            var b = WorldBounds(go);
            float now = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (now <= 0.0001f) return;
            go.transform.localScale *= longest / now;
        }

        private static Bounds WorldBounds(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.1f);
            var b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>도구 연습 쪽지도 방 안으로. 선반 곁에 붙여야 도구를 쥔 김에 눌러 본다.</summary>
        private static bool MoveTutorialNote(Transform room)
        {
            var note = Object.FindFirstObjectByType<InspectableNote>(FindObjectsInactive.Include);
            if (note == null) return false;
            var basis = Reference();
            if (basis == null) return false;
            Undo.RecordObject(note.transform, "쪽지 자리");
            note.transform.position = basis.TransformPoint(new Vector3(2.9f, 1.55f, 1.35f));
            note.transform.rotation = basis.rotation;
            note.gameObject.SetActive(true);
            return true;
        }

        // ── 옛 프로토타입 치우기 ──────────────────────

        /// <summary>
        /// 원점의 회색 상자들을 한자리에 모아 꺼 둔다. 지우지 않는 것은, 옮긴 값이
        /// 틀렸을 때 대조할 원본이 그것뿐이기 때문이다.
        /// </summary>
        private static void ParkOldPrototype()
        {
            // 꺼 둔 것은 GameObject.Find 로 안 잡힌다 — 그대로 두면 누를 때마다
            // 빈 묶음이 하나씩 새로 생긴다. 루트를 직접 훑는다.
            GameObject park = null;
            foreach (var r in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (r.name == OldName) { park = r; break; }
            if (park == null)
            {
                park = new GameObject(OldName);
                Undo.RegisterCreatedObjectUndo(park, "옛 프로토타입");
            }
            park.SetActive(true);   // 껐다 켜야 자식을 옮겨 담을 수 있다

            foreach (var n in new[] { "Zone_CaseBoard", "Zone_ToolShelf", "Zone_RecordStand", "Zone_BongseoBox" })
            {
                var go = GameObject.Find(n);
                if (go == null || go == park) continue;
                Undo.SetTransformParent(go.transform, park.transform, "옛 프로토타입으로");
            }
            park.SetActive(false);
        }
    }
}
