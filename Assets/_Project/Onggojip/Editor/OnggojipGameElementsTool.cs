using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using IMUNROK.Common;

namespace IMUNROK.Onggojip.Editor
{
    /// <summary>
    /// 1부(옹씨댁)에 얹을 "게임 요소" 마커를 만드는 도구.
    ///  · 집(김명관고택)은 그대로 두고, 그 위에 올릴 몇 개(플레이어 시작·단서·인물)만 생성.
    ///  · 다 "게임요소" 그룹 밑에 모이고, 처음엔 한 곳(원점 근처)에 모여 나온다.
    ///  · 사용자는 각 마커를 실제 건물 위로 "드래그"만 하면 됨(님이 직접 배치).
    ///
    /// 단서 마커(문갑·장부·아궁이·행랑궤)에는 InspectableNote가 붙어,
    /// 살펴보면 해당 단서(J08/J09/J13/J15)가 공통 수첩에 자동 기록된다.
    /// 인물 마커는 색깔 상자(나중에 심문 연결).
    ///
    /// 메뉴: [이문록 ▸ 옹고집: 1부 게임요소(플레이어·단서·인물) 만들기]. 실행 후 Ctrl+S.
    /// </summary>
    public static class OnggojipGameElementsTool
    {
        private const string GroupName = "게임요소";

        [MenuItem("이문록/옹고집: 1부 게임요소(플레이어·단서·인물) 만들기")]
        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == GroupName)
                {
                    EditorUtility.DisplayDialog("이문록",
                        $"이미 '{GroupName}'가 있습니다.\n지우고 다시 실행하거나, 그 안의 마커를 옮겨 쓰세요.", "확인");
                    return;
                }

            var group = new GameObject(GroupName);

            // 마커들을 원점 근처에 한 줄로(찾기 쉽게). 나중에 실제 건물 위로 드래그.
            float x = -6f;

            // 플레이어 시작(캡슐) — 여기에 Main Camera를 맞춤
            var player = MakeMarker(group.transform, PrimitiveType.Capsule, "플레이어_시작(여기에 카메라)",
                new Vector3(x, 1f, 0), new Vector3(0.6f, 1f, 0.6f), new Color(0.3f, 0.7f, 1f));
            x += 2f;

            // 단서 마커(살펴보면 수첩 기록)
            x = MakeClue(group.transform, x, "문갑",   "J08", "자물쇠가 부서지고 비어 있다. 스무 해 잠겨 있던 것이 최근 열렸다.");
            x = MakeClue(group.transform, x, "장부",   "J09", "필적이 한 달 전후로 뚜렷이 바뀌어 있다.");
            x = MakeClue(group.transform, x, "아궁이", "J13", "타다 만 서찰 조각이 재 속에 남아 있다.");
            x = MakeClue(group.transform, x, "행랑궤", "J15", "밑바닥에서 속량(贖良) 문서가 나온다.");

            // 인물 마커(색깔 상자)
            x = MakeNpc(group.transform, x, "甲_가짜",        new Color(0.85f, 0.4f, 0.35f));
            x = MakeNpc(group.transform, x, "乙_진짜옹덕구",  new Color(0.9f, 0.8f, 0.4f));
            x = MakeNpc(group.transform, x, "마름",           new Color(0.6f, 0.6f, 0.55f));
            x = MakeNpc(group.transform, x, "늙은하인",       new Color(0.5f, 0.5f, 0.5f));
            x = MakeNpc(group.transform, x, "마을사람",       new Color(0.6f, 0.6f, 0.6f));

            Selection.activeGameObject = group;
            SceneView.FrameLastActiveSceneView();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorUtility.DisplayDialog("이문록",
                "게임요소 마커를 만들었습니다!\n\n" +
                "· 파란 캡슐 = 플레이어 시작 위치\n" +
                "· 노란/빨강 상자 = 인물, 흰 상자 = 단서(살펴보면 수첩 기록)\n\n" +
                "이 마커들을 Scene뷰에서 실제 건물 위로 '드래그'해 배치하세요.\n" +
                "다 놓으면 Ctrl+S. (어디에 놓을지는 채팅으로 안내할게요)", "확인");
        }

        private static float MakeClue(Transform parent, float x, string name, string key, string body)
        {
            var go = MakeMarker(parent, PrimitiveType.Cube, name,
                new Vector3(x, 0.6f, 3f), new Vector3(0.5f, 1f, 0.5f), new Color(0.95f, 0.95f, 0.9f));
            var note = go.AddComponent<InspectableNote>();
            var so = new SerializedObject(note);
            so.FindProperty("_title").stringValue = name;
            so.FindProperty("_body").stringValue = body;
            so.FindProperty("_recordClue").boolValue = true;
            so.FindProperty("_clueCase").enumValueIndex = (int)CaseId.Case1_Onggojip;
            so.FindProperty("_clueKey").stringValue = key;
            so.FindProperty("_clueText").stringValue = $"[{key}] {body}";
            so.ApplyModifiedPropertiesWithoutUndo();
            return x + 1.4f;
        }

        private static float MakeNpc(Transform parent, float x, string name, Color color)
        {
            MakeMarker(parent, PrimitiveType.Cube, name,
                new Vector3(x, 0.9f, 6f), new Vector3(0.6f, 1.8f, 0.6f), color);
            return x + 1.4f;
        }

        private static GameObject MakeMarker(Transform parent, PrimitiveType type, string name,
            Vector3 pos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            if (r != null)
            {
                var block = new MaterialPropertyBlock();
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", color);
                r.SetPropertyBlock(block);
            }
            return go;
        }
    }
}
