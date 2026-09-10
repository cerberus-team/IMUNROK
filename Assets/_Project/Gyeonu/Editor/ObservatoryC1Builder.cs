using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Gyeonu.EditorTools
{
    /// <summary>
    /// C1 아버지의 검수 기록 — 관측실 작업실 픽업 (2026-09-10).
    ///
    /// 기획안 「7. 배치 요약」은 C1을 관측실에서 얻는다고 적었는데, 씬에는 소지품 정의(Item_C1)만 있고
    /// 집을 물건이 없어 디버그 메뉴로만 얻어졌다. 그 탓에 서고 장부 2단계와 C2까지 막혔다.
    ///
    /// 자리는 <b>북벽 서랍장 위 초록 서책</b>(관측실_소품/소품/Book12, 촛대·백자 옆)이다.
    /// 작업탁자 위 선아의 관측 수기(Book08)와 5m 떨어져 있어 헷갈리지 않는다.
    /// 책은 옮기지 않는다 — 콜라이더와 <see cref="ItemPickup"/>만 얹는다. 멱등.
    /// </summary>
    public static class ObservatoryC1Builder
    {
        const string ScenePath = "Assets/_Project/Gyeonu/Scenes/Gyeonu_Observatory.unity";
        const string ItemPath = "Assets/_Project/Gyeonu/Resources/GyeonuItems/Item_C1_아버지의검수기록.asset";
        const string BookName = "Book12";
        static readonly Vector3 BookPos = new Vector3(18.55f, -1.32f, 35.95f);   // 북벽 서랍장 상판 위

        [MenuItem("Tools/이문록/관측실 ▸ C1 검수 기록 픽업 만들기", priority = 360)]
        public static void Build()
        {
            var active = SceneManager.GetActiveScene();
            if (active.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                active = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var item = AssetDatabase.LoadAssetAtPath<InventoryItem>(ItemPath);
            if (item == null) { Debug.LogError("[C1] 소지품 정의가 없다: " + ItemPath + " — 종막 장부 빌더를 먼저 돌려야 한다."); return; }

            var book = FindBook();
            if (book == null) { Debug.LogError("[C1] 북벽 서랍장 위 " + BookName + " 을 찾지 못했다 (" + BookPos + " 근처)."); return; }

            // 콜라이더 — 조준(레이캐스트)을 받으려면 있어야 한다. 렌더러 바운즈에 맞춘다.
            if (book.GetComponentInChildren<Collider>() == null)
            {
                var rs = book.GetComponentsInChildren<Renderer>();
                if (rs.Length > 0)
                {
                    Bounds b = rs[0].bounds;
                    for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                    var box = Undo.AddComponent<BoxCollider>(book);
                    box.center = book.transform.InverseTransformPoint(b.center);
                    Vector3 s = book.transform.InverseTransformVector(b.size);
                    box.size = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
                }
            }

            var pick = book.GetComponent<ItemPickup>();
            if (pick == null) pick = Undo.AddComponent<ItemPickup>(book);
            pick.item = item;
            pick.verbOverride = "살피기";
            pick.revealName = false;
            pick.insideFurniture = null;
            pick.requiredFlags = null;
            EditorUtility.SetDirty(pick);

            EditorSceneManager.MarkSceneDirty(active);
            EditorSceneManager.SaveScene(active);
            Debug.Log("[C1] " + book.name + " (" + book.transform.position + ") 에 검수 기록 픽업을 얹어 저장했다.");
        }

        /// <summary>이름이 같은 책이 여럿이라 자리로 가른다.</summary>
        static GameObject FindBook()
        {
            GameObject best = null; float bestD = 0.6f;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (t.name != BookName) continue;
                float d = Vector3.Distance(t.position, BookPos);
                if (d < bestD) { bestD = d; best = t.gameObject; }
            }
            return best;
        }
    }
}
