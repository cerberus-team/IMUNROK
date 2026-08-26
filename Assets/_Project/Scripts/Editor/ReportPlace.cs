using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>장계 자리를 놓는다.</b> 메뉴: [이문록 ▸ 관아 ▸ ⑳ 장계 자리]
    ///
    /// 사건이 끝나는 자리다. 신문하던 교의에서 <b>일어나 옆으로 옮겨 앉는</b> 그
    /// 한 걸음이 마지막 장면을 연다 — 물어보는 일이 끝났고 이제 적는 일이라는 것을,
    /// 자리를 바꾸는 것으로 말한다.
    ///
    /// <b>대청 안에 둔다.</b> 신문도 여기서 하고 장계도 여기서 쓴다. 어사가 고을에
    /// 와서 하는 일이 다 한 지붕 아래라야, 이 집을 나가고 드는 것이 곧 사건의 마디가 된다.
    /// 자리는 어사 교의(15.30, z 0)에서 <b>옆으로 2.9m</b>, 동벽을 등지고 대청을 향한다.
    ///
    /// <b>여기 세우는 것은 임시 소품이다.</b> 서안과 의자는 따로 만들어 넣으실 테니,
    /// 이 도구는 <b>자리와 부품</b>만 잡아 둔다 — 뿌리(서안_자리) 밑에 모델을 넣고
    /// 임시 소품을 지우면 그대로 이어진다. 앉는 높이·앉는 자리·장계·필요한 단서는
    /// 모델이 바뀌어도 안 흔들린다.
    ///
    /// 함께 하는 것 둘:
    ///   · <b>장계 자산</b>(<see cref="CaseReport"/>)을 만들어 채운다. 말과 답은
    ///     인스펙터에서 고치라고 만든 것이다 — 어느 것이 참인지는 이야기를 쓰는 쪽이
    ///     정할 일이지 이 도구가 정할 일이 아니다.
    ///   · <b>마패</b>를 도구벨트에 넣는다. 장계는 마패로 봉해야 서므로, 벨트에 없으면
    ///     다 채워 놓고도 끝을 못 맺는다.
    ///
    /// 두 번 눌러도 두 벌이 안 서고, <b>이미 있는 장계는 안 덮어쓴다</b>.
    /// </summary>
    public static class ReportPlace
    {
        private const float Maru = 2.21f;

        /// <summary>서안이 놓이는 자리. 어사 교의에서 옆으로 2.9m, 동벽을 등진다.</summary>
        private static readonly Vector3 Desk = new Vector3(15.60f, Maru, 2.90f);

        /// <summary>앉는 자리. 서안 뒤(동벽 쪽)에서 대청을 향해 앉는다.</summary>
        private static readonly Vector3 Seat = new Vector3(16.30f, Maru, 2.90f);

        private const string FormPath = "Assets/_Project/Onggojip/Data/장계_옹고집.asset";

        [MenuItem("이문록/관아/⑳ 장계 자리")]
        public static void Run()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.name.Contains("Gwana"))
            {
                Debug.LogWarning("[관아] 관아 씬을 열고 누르십시오(지금은 " + scene.name + ").");
                return;
            }
            var log = new System.Text.StringBuilder("[관아] 장계 자리\n");
            var form = Form(log);
            Place(scene, form, log);
            Mapae(log);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(log.ToString());
        }

        // ── ㉠ 장계 ───────────────────────────────

        /// <summary>
        /// 옹고집 사건의 장계. <b>이미 있으면 안 덮어쓴다</b> — 말을 고쳐 둔 것을
        /// 도구가 도로 지워 버리면 고칠 마음이 안 난다.
        /// </summary>
        private static CaseReport Form(System.Text.StringBuilder log)
        {
            var have = AssetDatabase.LoadAssetAtPath<CaseReport>(FormPath);
            if (have != null) { log.AppendLine("  · 장계가 이미 있다 — 안 건드린다 (" + FormPath + ")"); return have; }

            var f = ScriptableObject.CreateInstance<CaseReport>();
            f.사건 = CaseId.Case1_Onggojip;
            f.머리말 = "臣이 옹진현에 이르러 살피니,";
            f.맺음말 = "삼가 아뢰옵나이다.";

            f.빈칸 = new[]
            {
                // ① 누구인가. 이 한 칸이 판결을 통째로 정한다.
                new CaseReport.Blank
                {
                    앞 = "", 뒤 = "이(가) 옹덕구를 사칭하고 가산을 차지하였으며",
                    물음 = "가산을 차지한 자가 누구인가.",
                    보기 = new[]
                    {
                        new CaseReport.Choice { 말 = "甲", 맞음 = true },
                        new CaseReport.Choice { 말 = "乙" },
                        new CaseReport.Choice { 말 = "아내" },
                        new CaseReport.Choice { 말 = "마름" },
                        new CaseReport.Choice { 말 = "복동" },
                    },
                },
                // ② 증좌. 캐낸 것만 뜬다 — 이 목록이 곧 조사의 성적표다.
                new CaseReport.Blank
                {
                    앞 = "그 증좌는", 뒤 = "이라",
                    물음 = "무엇으로 그것을 아는가. (캐낸 것만 쓸 수 있다)",
                    보기 = new[]
                    {
                        new CaseReport.Choice { 말 = "왼팔에 흉터가 없음",     필요단서 = "G08", 맞음 = true },
                        new CaseReport.Choice { 말 = "왼팔 흉터가 대장과 같음", 필요단서 = "G07" },
                        new CaseReport.Choice { 말 = "입안대장의 긁어낸 자리",       필요단서 = "G03" },
                        new CaseReport.Choice { 말 = "호적대장에 적힌 것",           필요단서 = "G01" },
                        new CaseReport.Choice { 말 = "호구단자의 필적",             필요단서 = "G02" },
                        new CaseReport.Choice { 말 = "환곡대장의 셈",               필요단서 = "G04" },
                    },
                },
                // ③ 곁가지 하나 — 조사하지 않으면 아예 모르고 지나갈 사람.
                new CaseReport.Blank
                {
                    앞 = "또", 뒤 = "은(는) 면천된 몸이나 문서가 고쳐져 아직 종으로 있으니",
                    물음 = "장부가 고쳐져 신분을 잃은 자가 누구인가.",
                    보기 = new[]
                    {
                        new CaseReport.Choice { 말 = "복동",     필요단서 = "G03", 맞음 = true },
                        new CaseReport.Choice { 말 = "늙은하인" },
                        new CaseReport.Choice { 말 = "마름" },
                    },
                },
                // ④ 처분. 사람을 맞혔을 때 판결의 결을 정한다.
                new CaseReport.Blank
                {
                    앞 = "마땅히", 뒤 = "하여야 할 것이옵니다",
                    물음 = "어찌 처결하시라 아뢸 것인가.",
                    보기 = new[]
                    {
                        new CaseReport.Choice { 말 = "가짜를 내치고 가산을 돌려주게", 맞음 = true, 판결 = Verdict.Truth },
                        new CaseReport.Choice { 말 = "사정을 헤아려 매를 감하게",     맞음 = true, 판결 = Verdict.Mercy },
                        new CaseReport.Choice { 말 = "둘을 다 두어 화목케",           판결 = Verdict.Mercy },
                    },
                },
            };

            f.참끝 = "가짜는 옹진 밖으로 내쳐졌다. 옹덕구는 제 이름을 되찾았고, " +
                    "복동은 그해 가을 <b>제 이름으로</b> 호적에 올랐다.";
            f.반끝 = "판결은 섰다. 다만 증좌가 성글어, 고을에는 아직도 " +
                    "<b>어느 쪽이 참이었느냐</b>는 말이 남았다.";
            f.헛끝 = "가짜가 옹덕구의 자리에 그대로 앉았다. 진짜는 제 집 문간에서 쫓겨났고, " +
                    "복동은 <b>여태 종이다</b>.";
            f.이문록줄 = "옹진현 옹고집 — {판결}";

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FormPath));
            AssetDatabase.CreateAsset(f, FormPath);
            AssetDatabase.SaveAssets();
            log.AppendLine("  · 장계를 만들었다 — " + FormPath);
            log.AppendLine("    ※ 어느 보기가 참인지(맞음)는 이야기를 쓰는 쪽이 정할 일이다. 인스펙터에서 고치면 된다");
            return f;
        }

        // ── ㉡ 자리 ───────────────────────────────

        private static void Place(Scene scene, CaseReport form, System.Text.StringBuilder log)
        {
            var root = Find(scene, "서안_자리");
            if (root == null)
            {
                root = new GameObject("서안_자리");
                SceneManager.MoveGameObjectToScene(root, scene);
                Undo.RegisterCreatedObjectUndo(root, "서안 자리");
                log.AppendLine("  · 서안 자리를 놓았다 — 대청 안, 어사 교의에서 옆으로 2.9m");
            }
            root.transform.SetPositionAndRotation(Desk, Quaternion.Euler(0f, 270f, 0f));  // 대청(−x)을 향한다
            root.transform.localScale = Vector3.one;

            // 임시 소품. 진짜 서안·의자가 오면 이 셋만 지우면 된다.
            var wood = Wood();
            var temp = root.transform.Find("임시소품");
            if (temp == null)
            {
                var t = new GameObject("임시소품");
                t.transform.SetParent(root.transform, false);
                Undo.RegisterCreatedObjectUndo(t, "임시 소품");
                temp = t.transform;
            }
            Part(temp, "상판", new Vector3(0f, 0.68f, 0f), new Vector3(1.05f, 0.05f, 0.56f), wood);
            Part(temp, "다리_앞", new Vector3(0f, 0.33f, -0.24f), new Vector3(1.00f, 0.66f, 0.05f), wood);
            Part(temp, "다리_뒤", new Vector3(0f, 0.33f, 0.24f), new Vector3(1.00f, 0.66f, 0.05f), wood);
            Part(temp, "의자_좌판", new Vector3(0.70f, 0.42f, 0f), new Vector3(0.48f, 0.05f, 0.46f), wood);
            Part(temp, "의자_등", new Vector3(0.94f, 0.68f, 0f), new Vector3(0.06f, 0.52f, 0.44f), wood);

            // 앉는 자리
            var seatT = root.transform.Find("앉는자리");
            if (seatT == null)
            {
                var s = new GameObject("앉는자리");
                s.transform.SetParent(root.transform, false);
                Undo.RegisterCreatedObjectUndo(s, "앉는 자리");
                seatT = s.transform;
            }
            seatT.SetPositionAndRotation(Seat, Quaternion.Euler(0f, 270f, 0f));

            // 누를 수 있게 — 서안 앞에 서면 잡히는 상자
            var col = root.GetComponent<BoxCollider>();
            if (col == null) col = Undo.AddComponent<BoxCollider>(root);
            col.center = new Vector3(0f, 0.45f, 0f);
            col.size = new Vector3(1.20f, 0.90f, 0.70f);

            var desk = root.GetComponent<ReportDesk>();
            if (desk == null) desk = Undo.AddComponent<ReportDesk>(root);
            // 넷을 다 캐야 앉는다. 반쯤 캐고 앉으면 빈칸에 쓸 말이 뜨지도 않아,
            // 제가 무엇을 못 했는지도 모르는 채 헛장계를 봉하게 된다.
            desk.Setup(form, seatT, new[] { "G01", "G03", "G07", "G08" });
            EditorUtility.SetDirty(desk);
            log.AppendLine("  · 앉는 자리 " + Seat.ToString("F2") + " — G01·G03·G07·G08 을 다 캐야 앉는다");
            log.AppendLine("  · 진짜 서안·의자가 오면 '임시소품' 만 지우고 그 밑에 넣으면 된다");
        }

        // ── ㉢ 마패 ───────────────────────────────

        /// <summary>장계는 마패로 봉해야 선다. 벨트에 없으면 끝을 못 맺는다.</summary>
        private static void Mapae(System.Text.StringBuilder log)
        {
            var def = AssetDatabase.LoadAssetAtPath<ToolDef>("Assets/_Project/Data/Tools/Tool_mapae.asset");
            if (def == null) { log.AppendLine("  ※ Tool_mapae 를 못 찾았다"); return; }
            var belt = Object.FindFirstObjectByType<ToolbeltHud>();
            if (belt == null) { log.AppendLine("  ※ 도구벨트가 씬에 없다"); return; }

            var so = new SerializedObject(belt);
            var arr = so.FindProperty("_tools");
            for (int i = 0; i < arr.arraySize; i++)
                if (arr.GetArrayElementAtIndex(i).objectReferenceValue == def)
                { log.AppendLine("  · 마패는 이미 벨트에 있다"); return; }
            arr.arraySize++;
            arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = def;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(belt);
            log.AppendLine("  · 마패를 도구벨트에 넣었다 — 이 물건에 드디어 할 일이 생겼다");
        }

        // ── 잔손 ─────────────────────────────────

        private static Material _wood;

        private static Material Wood()
        {
            if (_wood != null) return _wood;
            foreach (var g in AssetDatabase.FindAssets("t:Material MI_KoreanWood"))
            {
                _wood = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
                if (_wood != null) return _wood;
            }
            return null;
        }

        private static void Part(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var t = parent.Find(name);
            GameObject go;
            if (t == null)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                go.transform.SetParent(parent, false);
                Undo.RegisterCreatedObjectUndo(go, "임시 가구");
                var c = go.GetComponent<Collider>();
                if (c != null) Object.DestroyImmediate(c);      // 뿌리 상자 하나로 받는다
            }
            else go = t.gameObject;
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = scale;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null && mat != null) mr.sharedMaterial = mat;
        }

        private static GameObject Find(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == name) return root;
            return null;
        }
    }
}
