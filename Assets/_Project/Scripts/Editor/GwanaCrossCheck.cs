using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using IMUNROK.Common;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>서고에서 찾은 것을 집에서 가져온 것과 맞대게 한다.</b>
    /// 메뉴: [이문록 ▸ 관아 ▸ ⑩ 대장에 종이 붙이고 맞대보기 표 놓기]
    ///
    /// 두 가지를 한다.
    ///
    /// <b>㉠ 대장 넷에 종이를 붙인다.</b> 궤를 헤집으면 대장이 나오는데 <b>펼칠 것이
    /// 없었다</b> — InspectableNote 의 <c>_page</c> 가 넷 다 비어 있어서, 집어 들어도
    /// 제목과 한 줄 설명만 뜨고 종이는 안 열렸다. 서고까지 와서 뒤진 보람이 문장 한 줄인
    /// 셈이다. 이제 G01~G04 의 실제 문서를 붙인다(Tools/DocBaker 에서 구운 것).
    ///
    /// <b>㉡ 맞대보기 표를 놓는다.</b> 2막의 알맹이가 여기 있다. 서고에서 찾는 종이는
    /// <b>혼자서는 아무 말도 안 한다</b> — 관아 호적대장에 "복동, 왼팔 안쪽 데인 자국"이
    /// 적혀 있다는 것만으로는 그냥 옛 기록이다. 집에서 본 호적(J12)과 겹쳐 놓아야
    /// "그 복동이 지금 저기 서 있다"가 되고, 겹치는 순간에야 소매를 걷으라 이를 수 있다.
    ///
    /// <b>넷 다 짝이 있다</b>
    ///   G01 + J12 → 원본에는 한 줄이 더 있다(파기). <b>여기서 손목 심문이 열린다</b>
    ///   G02 + J13 → 관아 것과 집 것이 같은 손이다. 위조가 확정된다
    ///   G03 + J11 → 별급문기가 관의 책에도 그대로다. 면천 기록은 없다 — 복동은 종이다
    ///   G04 + J01 → 한 달간 소작료 인하. 마을이 甲을 주인으로 아는 값이 숫자로 남았다
    ///
    /// 두 번 눌러도 두 벌이 안 놓인다.
    /// </summary>
    public static class GwanaCrossCheck
    {
        private const string Tex = "Assets/_Project/Onggojip/Art/Textures/";

        /// <summary>대장 이름 → 붙일 종이 · 수첩에 적힐 글.</summary>
        private struct Ledger
        {
            public string 이름;      // 씬의 오브젝트
            public string 종이;      // 텍스처 파일 이름
            public string key;
            public string 글;        // ※ OnggojipClues.All 과 한 벌이어야 한다
            public string 겉;        // 집어 들었을 때 한 줄
        }

        // ※ 아래 '글'은 IMUNROK.Onggojip.OnggojipClues.All 의 G01~G04 와 같아야 한다.
        //   (편집기 어셈블리가 사건 어셈블리를 참조하지 않아 여기 옮겨 적는다.
        //    표를 고칠 때는 반드시 두 곳을 함께 고칠 것 — 한동안 두 벌이었던 적이 있다)
        private static readonly Ledger[] Ledgers =
        {
            new Ledger { 이름 = "호적대장", 종이 = "T_Doc_G01_Hojeok_Wonbon", key = "G01",
                         글 = "관아 호적대장 원본 — \"노 복동, 왼팔 안쪽 데인 자국 두 치 남짓\"",
                         겉 = "삼 년 전 식년의 호적대장. 관의 책이라 손글씨가 아니다." },
            new Ledger { 이름 = "호구단자", 종이 = "T_Doc_G02_Hogu_Wonbon", key = "G02",
                         글 = "관아가 받은 호구단자 — 집에서 나온 것과 같은 필적",
                         겉 = "올해 식년에 이 집에서 올린 호구단자. 관은 받아 적기만 했다." },
            new Ledger { 이름 = "입안대장", 종이 = "T_Doc_G03_Ipan", key = "G03",
                         글 = "입안 대장 — 별급문기 사본, 수취인 \"종 복동\"",
                         겉 = "관이 문서를 인정해 준 기록. 별급문기의 사본이 여기 있다." },
            new Ledger { 이름 = "환곡대장", 종이 = "T_Doc_G04_Hwansang", key = "G04",
                         글 = "환곡·소작 대장 — 한 달간 소작료 인하",
                         겉 = "환곡과 소작을 적은 대장. 한 달 전 숫자가 한 번 꺾인다." },
        };

        [MenuItem("이문록/관아/⑩ 대장에 종이 붙이고 맞대보기 표 놓기")]
        public static void Run()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.name.Contains("Gwana"))
            {
                Debug.LogWarning("[관아] 관아 씬을 열고 누르십시오(지금은 " + scene.name + ").");
                return;
            }

            var log = new System.Text.StringBuilder("[관아] 서고를 잇는다\n");
            Pages(scene, log);
            Table(scene, log);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(log.ToString());
        }

        /// <summary>대장 넷에 실제 종이를 붙인다.</summary>
        private static void Pages(Scene scene, System.Text.StringBuilder log)
        {
            foreach (var L in Ledgers)
            {
                var note = FindNote(scene, L.이름);
                if (note == null) { log.AppendLine("  ※ " + L.이름 + " 을 못 찾았다 — ⑦ 을 먼저 누르십시오"); continue; }

                var page = AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + L.종이 + ".png");
                if (page == null)
                {
                    log.AppendLine("  ※ " + L.종이 + ".png 이 없다 — [이문록 ▸ 에셋: 사건 문서 텍스처 굽기] 를 먼저");
                    continue;
                }

                var so = new SerializedObject(note);
                so.FindProperty("_page").objectReferenceValue = page;
                so.FindProperty("_canOpen").boolValue = true;
                so.FindProperty("_recordClue").boolValue = true;
                so.FindProperty("_clueKey").stringValue = L.key;
                so.FindProperty("_clueText").stringValue = L.글;
                so.FindProperty("_body").stringValue = L.겉;
                so.ApplyModifiedPropertiesWithoutUndo();
                log.AppendLine("  · " + L.이름 + " 에 종이를 붙였다 (" + L.종이 + ")  → " + L.key);
            }
        }

        /// <summary>맞대보기 표.</summary>
        private static void Table(Scene scene, System.Text.StringBuilder log)
        {
            var go = FindGo(scene, "_대조");
            if (go == null)
            {
                go = new GameObject("_대조");
                SceneManager.MoveGameObjectToScene(go, scene);
                Undo.RegisterCreatedObjectUndo(go, "맞대보기 표");
                log.AppendLine("  · 맞대보기 표를 놓았다");
            }
            var cc = go.GetComponent<CrossCheck>();
            if (cc == null) cc = Undo.AddComponent<CrossCheck>(go);

            var rows = new[]
            {
                new[] { "호적 — 파기", "G01", "J12",
                        "집에서 본 호적과 같은 책인데 <b>원본에는 한 줄이 더 있다</b>.\n" +
                        "「노 복동 — 왼팔 안쪽 데인 자국 *두 치 남짓*」\n" +
                        "스무 해 전 관리가 적어 둔 것이라 아무도 손댈 수 없었다.", "", "" },

                new[] { "호구단자 — 같은 손", "G02", "J13",
                        "관아가 받아 둔 것과 집에서 나온 것이 <b>같은 손</b>이다.\n" +
                        "관리는 맨 끝 「도부(到付)」 한 줄만 썼다 — 나머지는 *이 집 사람이 썼다*.", "", "" },

                new[] { "별급문기 — 관의 책", "G03", "J11",
                        "보료 밑의 별급문기가 관의 책에도 그대로 있다.\n" +
                        "수취인은 「오래 부린 종 복동」, 그리고 「면천 없음」 —\n" +
                        "재산은 받았으되 *종에서 풀려난 적은 없다*.", "", "" },

                new[] { "소작료 — 한 달", "G04", "J01",
                        "한 달 전부터 소작료가 서른 말에서 *스무 말*로 내렸다.\n" +
                        "마을이 甲을 주인으로 아는 값이 여기 숫자로 남아 있다.", "", "" },
            };

            var so = new SerializedObject(cc);
            so.FindProperty("_case").enumValueIndex = 0;          // Case1_Onggojip
            so.FindProperty("_speaker").stringValue = "";
            so.FindProperty("_hint").stringValue = "맞대어 보니 —";
            so.FindProperty("_beat").floatValue = 0.9f;
            var list = so.FindProperty("_pairs");
            list.arraySize = rows.Length;
            for (int i = 0; i < rows.Length; i++)
            {
                var e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("이름").stringValue = rows[i][0];
                e.FindPropertyRelative("이쪽").stringValue = rows[i][1];
                e.FindPropertyRelative("저쪽").stringValue = rows[i][2];
                e.FindPropertyRelative("맞대면").stringValue = rows[i][3];
                e.FindPropertyRelative("새단서key").stringValue = rows[i][4];
                e.FindPropertyRelative("새단서").stringValue = rows[i][5];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            log.AppendLine("  · 맞댈 짝 " + rows.Length + " 벌을 적었다 (G01+J12 · G02+J13 · G03+J11 · G04+J01)");
        }

        // ── 잔손 ──

        private static InspectableNote FindNote(Scene scene, string name)
        {
            foreach (var n in Resources.FindObjectsOfTypeAll<InspectableNote>())
                if (n.name == name && n.gameObject.scene == scene) return n;
            return null;
        }

        private static GameObject FindGo(Scene scene, string name)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t.gameObject;
            return null;
        }
    }
}
