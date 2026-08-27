using System.Text;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// 문갑에 놓인 도구 넷을 갈무리한다. 메뉴: [이문록 ▸ 조사청 ▸ 문갑의 도구 갈무리]
    ///
    /// <b>도구는 넷이 아니라 둘이다</b>. 여태 문갑에 수첩·지도·돋보기·등불 넷을 늘어놓고
    /// 넷 다 익히게 했는데, 수첩과 지도는 <b>익히고 말고 할 물건이 아니다</b> —
    /// 수첩은 늘 지니고 다니며 아무 때나 펴는 것(I)이고, 지도는 챕터마다 그냥 열리는
    /// 것(M)이다. 벨트에 칸을 차지하고 앉을 까닭이 없다. 손에 <b>들고</b> 무언가에
    /// <b>대는</b> 물건만 벨트에 오른다 — 지금은 등불과 돋보기 둘이고, 팀원이 도구를
    /// 하나 더 만들면 그때 늘어난다. 그 둘은 문갑에서 <b>치운다</b> — 놓여 있다는 것만으로
    /// "이것도 집어야 하는 물건"으로 읽히고, 짚을 것이 넷이면 익혀야 할 둘이 묻힌다.
    ///
    /// <b>말만 읽고 끝나지 않게</b>: 돋보기와 등불에는 <b>해 볼 과제</b>와 함께
    /// <b>예시 증거를 쥐여 준다</b>. 말끝에 「事目」 한 장이 저절로 손에 펴지고,
    /// 그 종이에 돋보기를 대야 · 그 종이를 등불에 비춰야 다 익힌 것이 된다.
    /// 종이를 찾아오라고 시키지 않는 까닭은, 물건을 찾아 방을 헤매다 보면 정작
    /// 배우려던 도구가 뒷전이 되기 때문이다.
    ///
    /// 사목에 적힌 세 줄이 곧 도구 쓰는 법이다 — 안내문을 따로 띄우는 대신
    /// <b>조사청의 규칙</b>으로 적어 두었다. 읽는 것과 배우는 것이 한 장에서 끝난다.
    ///
    /// 그 사목은 두 장으로 굽는다. 겉장과, 배접 속에 끼운 장이 비쳐 보이는 장.
    /// 등불을 들면 뒷장이 앞장 위로 배어 나온다(<see cref="LanternReveal"/>).
    ///
    /// ★플레이를 멈추고 실행할 것. 다시 눌러도 덧나지 않는다.
    /// </summary>
    public static class HubTools
    {
        private const string RoomName = "조사청_실내";
        private const string ToolsPath = "조사청_소품/문갑/도구";
        private const string SheetName = "연습_사목";

        private const string DocDir = "Assets/_Project/Art/Props/Textures/";
        private const string PlainDoc = DocDir + "T_Doc_Yeonseup.png";
        private const string LitDoc = DocDir + "T_Doc_Yeonseup_lit.png";
        private const string MatPath = "Assets/_Project/_Common/Materials/M_조사청_사목.mat";

        /// <summary>사목을 놓을 자리(방 기준). 문갑 윗면 가운데, 도구 넷의 앞줄이다.</summary>
        private static readonly Vector3 SheetAt = new Vector3(0.45f, 1.437f, -0.86f);
        private const float SheetSize = 0.30f;

        [MenuItem("이문록/조사청/문갑의 도구 갈무리")]
        private static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[문갑] 플레이를 멈추고 다시 실행하세요.");
                return;
            }

            var room = GameObject.Find(RoomName);
            var tools = GameObject.Find(ToolsPath);
            if (room == null || tools == null)
            {
                Debug.LogError("[문갑] " + ToolsPath + " 을 못 찾았습니다.");
                return;
            }

            var log = new StringBuilder();
            var sheet = Sheet(room.transform, tools.transform, log);

            // 수첩과 지도는 <b>치운다</b>. 익히고 말고 할 물건이 아닌데 문갑에 놓여 있으면,
            // 놓여 있다는 것만으로 "이것도 집어야 하는 물건"으로 읽힌다. 이름줄만 남겨
            // 두어도 짚을 것이 넷이라, 정작 익혀야 할 둘이 묻힌다.
            Remove(tools.transform, "도구_수첩", log);
            Remove(tools.transform, "도구_지도", log);

            Practice(tools.transform, "도구_돋보기", sheet,
                     "예시로 한 장 드리겠소. 돋보기를 눈에 대고(오른쪽 단추) 글자를 키워 보시오.", log);
            Practice(tools.transform, "도구_등불", sheet,
                     "같은 종이요. 이번엔 등불을 들고 비춰 보시오 — 겹 사이의 것이 배어 나올 것이오.", log);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(room.scene);
            Debug.Log("[문갑]\n" + log);
        }

        /// <summary>익히지 않는 물건은 문갑에서 치운다.</summary>
        private static void Remove(Transform tools, string name, StringBuilder log)
        {
            var t = tools.Find(name);
            if (t == null) { log.AppendLine("   " + name + " — 이미 없습니다"); return; }
            Undo.DestroyObjectImmediate(t.gameObject);
            log.AppendLine("   " + name + " — 문갑에서 치웠습니다");
        }

        /// <summary>익히는 도구에 해 볼 과제와 <b>쥐여 줄 예시 증거</b>를 붙인다.</summary>
        private static void Practice(Transform tools, string name, InspectableNote example,
                                     string task, StringBuilder log)
        {
            var t = tools.Find(name);
            if (t == null) { log.AppendLine("   ✘ " + name + " 없음"); return; }
            var tut = t.GetComponent<ToolTutorial>();
            if (tut == null) { log.AppendLine("   ✘ " + name + " 에 익히기가 없습니다"); return; }

            var so = new SerializedObject(tut);
            so.FindProperty("_practice").stringValue = task;
            so.FindProperty("_example").objectReferenceValue = example;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tut);
            log.AppendLine("   " + name + " — 과제와 예시 증거를 붙였습니다");
        }

        /// <summary>
        /// 문갑에 事目 한 장을 깐다. 종이는 <b>두께가 없다</b> — 판때기를 눕히면 옆에서
        /// 봤을 때 나뭇조각이 된다. 대신 뒤에서 보면 사라지므로, 문갑 위에 눕혀 두고
        /// 위에서만 보게 한다.
        /// </summary>
        private static InspectableNote Sheet(Transform room, Transform tools, StringBuilder log)
        {
            var plain = AssetDatabase.LoadAssetAtPath<Texture2D>(PlainDoc);
            var lit = AssetDatabase.LoadAssetAtPath<Texture2D>(LitDoc);
            if (plain == null)
            {
                log.AppendLine("   ✘ " + PlainDoc + " 이 없습니다 — Tools/DocBaker/make_docs.ps1 을 한 번 돌리세요.");
                return null;
            }

            var old = tools.Find(SheetName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = SheetName;
            Undo.RegisterCreatedObjectUndo(go, "연습 사목 두기");
            go.transform.SetParent(tools, false);
            go.transform.position = room.TransformPoint(SheetAt);
            go.transform.rotation = room.rotation * Quaternion.Euler(90f, 6f, 0f);

            // 문갑은 남의 모델이라 제 배율이 따로 있다(2.9배쯤). 그 밑에 붙이면서
            // 로컬 배율에 0.30 을 넣었더니 세상에서는 0.88m 짜리 대자보가 되었다.
            // 어느 밑에 붙든 세상에서 30cm 이도록, 부모 배율로 나눠 준다.
            var ps = tools.lossyScale;
            go.transform.localScale = new Vector3(
                SheetSize / Mathf.Max(0.0001f, ps.x),
                SheetSize / Mathf.Max(0.0001f, ps.y),
                SheetSize / Mathf.Max(0.0001f, ps.z));

            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat == null)
            {
                var lit3 = Shader.Find("Universal Render Pipeline/Lit");
                mat = new Material(lit3);
                AssetDatabase.CreateAsset(mat, MatPath);
            }
            mat.SetTexture("_BaseMap", plain);
            mat.SetFloat("_Smoothness", 0.05f);
            mat.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(mat);
            go.GetComponent<Renderer>().sharedMaterial = mat;

            // 방에는 <b>보이지 않는다</b>.
            //
            // 문갑 위에 실제로 깔아 두었더니 짚을 것이 하나 더 늘었다. 문서는 앞으로
            // 필요할 때 위에서 내려올 물건이지 문갑에 늘 놓여 있을 물건이 아니다.
            // 그래서 이것은 <b>내용만 지닌 그릇</b>으로 남긴다 — 익히기가 이 그릇을
            // 붙들고 있다가, 과제를 낼 때 그 안의 종이를 손에 펴 준다.
            // 지우지 않고 남기는 까닭은, 지우면 익히기가 붙들 종이가 없어지기 때문이다.
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;
            var mc = go.GetComponent<MeshCollider>();
            if (mc != null) Object.DestroyImmediate(mc);

            var note = go.AddComponent<InspectableNote>();
            var so = new SerializedObject(note);
            so.FindProperty("_page").objectReferenceValue = plain;
            so.FindProperty("_title").stringValue = "事目";
            so.FindProperty("_body").stringValue = "";
            so.FindProperty("_canOpen").boolValue = true;
            so.FindProperty("_maxTouchDistance").floatValue = 2.5f;
            // 잔글씨 — 돋보기로 끝까지 읽어야 뜬다. 읽히는 것이 <b>돋보기에 대한 말</b>인
            // 것은 일부러다. 지금 하고 있는 일이 무엇인지 그 자리에서 알게 된다.
            so.FindProperty("_fineText").stringValue =
                "細字用鏡而讀 — 잔글씨는 돋보기로 읽는다. 지금 그 말대로 한 것이다.";
            // 배접 속 — 등불에 비춰야 배어 나온다
            so.FindProperty("_litPage").objectReferenceValue = lit;
            so.FindProperty("_litText").stringValue =
                "見而不疑是謂盲 — 보고도 의심하지 않으면 그것을 눈멀었다 한다.";
            so.FindProperty("_recordClue").boolValue = false;   // 연습이다. 수첩에 적지 않는다
            so.FindProperty("_clueNeedsMagnifier").boolValue = false;
            so.FindProperty("_clueNeedsLantern").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(note);

            log.AppendLine("   " + SheetName + " 를 문갑에 깔았습니다"
                           + (lit == null ? "  (배접 속 장이 없어 등불에는 글만 뜹니다)" : "  (겉장 + 배접 속 장)"));
            return note;
        }
    }
}
