using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 혼천의 여덟 방위 퍼즐 저작기 (2026-08-23). 멱등 — 여러 번 눌러도 같은 결과다.
    ///
    /// ■ 무엇을 하나
    ///   1) 소지품 정의  Resources/GyeonuItems/Item_HONCHEON_MEMO_선아의관측수기.asset
    ///   2) 씬 배치      작업실 table03 위의 **Book08** 에 ItemPickup을 붙인다.
    ///      새 책을 놓지 않는다 — 이미 그 자리에 있던 책 하나를 집을 수 있게 만들 뿐이라
    ///      가구·소품 배치가 바뀌지 않는다. 집으면 ItemPickup이 그 자리를 비운다.
    ///   3) 퍼즐 부착    혼천의 루트에 <see cref="HoncheonuiPuzzle"/> + 정답 표
    ///
    /// ■ 책장 넘김 연출은 하지 않는다 (2026-08-23 조사 결과)
    ///   프로젝트의 책 에셋을 전부 훑었다 — Book01~14, SM_CCJ_BookA/B/C, SM_CCJ_OpenBook까지
    ///   **모두 렌더러 1개 · 서브메시 1개 · 스킨 없음 · 애니메이션 클립 없음**이다.
    ///   페이지가 분리된 모델이 하나도 없어 넘길 것이 없다. 지시대로 시도하지 않고
    ///   소지품 설명 글로만 간다.
    ///
    /// ■ 왜 Book08인가
    ///   table03 상판(y −2.227) 위의 책은 넷이고, 셋은 (22.05, 33.55)에 포개 쌓인 더미다.
    ///   Book08만 (21.55, 33.30)에 홀로 놓여 있어 집어 가도 더미에 구멍이 남지 않는다.
    /// </summary>
    public static class HoncheonuiPuzzleBuilder
    {
        const string Root = "Tools/이문록/소지품/";
        const string ResDir = "Assets/_Project/Gyeonu/Resources/GyeonuItems";
        const string ItemPath = ResDir + "/Item_HONCHEON_MEMO_선아의관측수기.asset";
        const string SceneName = "Gyeonu_Observatory";
        const string PrefabDir = "Assets/_Project/Gyeonu/Prefabs/Items";
        const string PrefabPath = PrefabDir + "/선아_관측수기.prefab";

        const string MemoId = "HONCHEON_MEMO";
        const string BookName = "Book08";
        static readonly Vector3 BookSpot = new Vector3(21.550f, -2.207f, 33.300f);

        /// <summary>정답 — 고리 순서(자오·적도·황도·소1·소2·소3)대로 방위 번호.
        /// 0=北 1=北東 2=東 3=南東 4=南 5=南西 6=西 7=北西</summary>
        static readonly int[] Answer = { 4, 2, 7, 6, 0, 3 };

        const string MemoText =
            "해가 가장 높이 오를 때, 자오의 고리는 그 빛을 정면으로 받는다.\n" +
            "적도의 고리는 하루의 첫 빛을 따른다.\n" +
            "첫째 작은 고리는 적도의 고리와 등을 맞댄다.\n" +
            "둘째 작은 고리는 자오의 고리와 등을 맞댄다.\n" +
            "황도의 고리는 저무는 빛과 북녘의 붙박이별 사이를 가른다.\n" +
            "마지막 작은 고리는 황도의 고리와 마주 선다.";

        // ═══════════════════════════════════════════════════
        [MenuItem(Root + "혼천의 퍼즐 준비 (선아의 관측 수기 + 여덟 방위)", priority = 120)]
        public static void Build()
        {
            var item = CreateItem();
            if (item == null) return;

            var scene = SceneManager.GetActiveScene();
            if (scene.name != SceneName)
            {
                EditorUtility.DisplayDialog("혼천의 퍼즐",
                    $"소지품 정의는 만들었다.\n씬 작업(책 아이템화·퍼즐 부착)은 {SceneName} 씬을 연 뒤에 다시 눌러라.\n" +
                    $"(지금 열린 씬: {scene.name})", "알겠다");
                Selection.activeObject = item;
                return;
            }

            int placed = AttachPickup(item);
            int wired = AttachPuzzle();

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[혼천의 퍼즐] 준비 완료 — 수기 배치 {placed}건, 퍼즐 부착 {wired}건\n  {ItemPath}");
            Selection.activeObject = item;
        }

        // ── 소지품 정의 ───────────────────────────────────
        static InventoryItem CreateItem()
        {
            EnsureFolder(ResDir);
            var item = AssetDatabase.LoadAssetAtPath<InventoryItem>(ItemPath);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<InventoryItem>();
                AssetDatabase.CreateAsset(item, ItemPath);
            }

            item.itemId = MemoId;
            item.displayName = "선아의 관측 수기";
            item.description = MemoText;
            // 상세 창에서 돌려 볼 책. 씬에 놓여 있던 그 책(Book08)의 메시를 그대로 감싼다 —
            // 집어 든 물건과 화면에 뜨는 물건이 달라 보이면 안 된다.
            // ⚠️ 모델을 안 주면 상세 창 왼쪽 그림틀이 **빈 회색 상자**로 남는다 (2026-08-23 실측).
            item.modelPrefab = BuildBookPrefab();
            // ⚠️ 이 책 메시는 **로컬 Z가 두께**다 (로컬 bounds 0.29 × 0.21 × 0.04 실측).
            //    겉장이 이미 로컬 −Z를 보고 있어 미리보기 카메라와 마주 선다 — 거의 세우지 않는다.
            //    C4 서책(누운 책)처럼 −62°씩 눕히면 책등만 보인다.
            item.previewEuler = new Vector3(-12f, 18f, 0f);
            item.previewZoom = 1.25f;
            item.usable = false;          // 쓰임 버튼 없음 — 읽으면 끝이다
            item.useLabel = "";
            item.useNotReadyHint = "";
            item.pickupVerb = "살피기";
            item.autoShowOnPickup = true;
            item.journalKey = "honcheonui_memo";
            item.journalText = "선아의 관측 수기 — 혼천의 여섯 고리를 어느 방위에 두어야 하는지 적혀 있다.";
            item.worldFlag = GyeonuWorld.F_혼천의메모;

            EditorUtility.SetDirty(item);
            return item;
        }

        /// <summary>
        /// 씬의 Book08이 쓰는 메시·재질을 우리 폴더 프리팹으로 감싼다. 원본 에셋은 손대지 않는다.
        /// 씬이 안 열려 있으면(정의만 만드는 경우) 이미 구워 둔 프리팹을 그대로 쓴다.
        /// </summary>
        static GameObject BuildBookPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var src = FindBook();
            if (src == null) return existing;               // 씬 밖에서 부른 경우

            var mf = src.GetComponent<MeshFilter>();
            var mr = src.GetComponent<MeshRenderer>();
            if (mf == null || mr == null) return existing;

            EnsureFolder(PrefabDir);
            var temp = new GameObject("선아_관측수기");
            temp.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            temp.AddComponent<MeshRenderer>().sharedMaterials = mr.sharedMaterials;
            temp.transform.localScale = src.transform.lossyScale;

            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        // ── 씬: 책 하나를 집을 수 있게 ─────────────────────
        static int AttachPickup(InventoryItem item)
        {
            var book = FindBook();
            if (book == null)
            {
                Debug.LogWarning($"[혼천의 퍼즐] table03 위의 {BookName} 을 못 찾았다 — 수기를 놓지 못했다");
                return 0;
            }

            var pickup = book.GetComponent<ItemPickup>();
            if (pickup == null) pickup = book.AddComponent<ItemPickup>();
            pickup.item = item;
            pickup.verbOverride = "살피기";     // 상 위에 놓인 물건
            pickup.revealName = false;          // 무엇인지는 집어서 상세로 봐야 안다
            pickup.pickupToast = "";

            // 얇은 책이라 조준이 잘 안 걸린다 — 조금 넉넉한 상자를 준다
            var col = book.GetComponent<BoxCollider>();
            if (col == null) col = book.AddComponent<BoxCollider>();
            var r = book.GetComponent<Renderer>();
            if (r != null)
            {
                col.center = book.transform.InverseTransformPoint(r.bounds.center);
                var s = r.bounds.size + new Vector3(0.04f, 0.06f, 0.04f);
                var ls = book.transform.lossyScale;
                col.size = new Vector3(s.x / Mathf.Max(1e-4f, ls.x),
                                       s.y / Mathf.Max(1e-4f, ls.y),
                                       s.z / Mathf.Max(1e-4f, ls.z));
            }

            EditorUtility.SetDirty(book);
            return 1;
        }

        static GameObject FindBook()
        {
            GameObject best = null;
            float bestD = 0.25f;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name != BookName) continue;
                float d = Vector3.Distance(t.position, BookSpot);
                if (d < bestD) { bestD = d; best = t.gameObject; }
            }
            return best;
        }

        // ── 씬: 퍼즐 부착 ─────────────────────────────────
        static int AttachPuzzle()
        {
            var focus = Object.FindFirstObjectByType<HoncheonuiFocusRings>(FindObjectsInactive.Include);
            if (focus == null)
            {
                Debug.LogWarning("[혼천의 퍼즐] 씬에 혼천의가 없다 — Tools ▸ 이문록 ▸ 혼천의 생성 먼저");
                return 0;
            }

            var pz = focus.GetComponent<HoncheonuiPuzzle>();
            if (pz == null) pz = focus.gameObject.AddComponent<HoncheonuiPuzzle>();
            pz.rings = focus;
            pz.answer = (int[])Answer.Clone();
            pz.requireFlag = GyeonuWorld.F_혼천의메모;
            pz.solvedFlag = GyeonuWorld.F_혼천의퍼즐;
            focus.puzzle = pz;

            // ⚠️ 문구는 **여기서 반드시 다시 써 준다.** 컴포넌트의 기본값을 고쳐도 씬에 이미
            //    직렬화된 옛 문자열이 이긴다 — 코드만 고치고 화면에서 옛 문구를 보며 한참 헤맸다
            //    (2026-08-23). 빌더가 값을 쥐고 있어야 메뉴 한 번으로 씬이 따라온다.
            pz.lockedHint = "고리가 여섯 겹으로 얽혀 있다. 어느 것부터 손대야 할지 알 수 없다.";
            pz.solvedNow = "여섯 고리가 한꺼번에 물린다. 혼천의가 멎었다.";
            pz.solvedAfter = "고리가 이르는 대로 하늘이 섰다. 이제 안쪽 방의 혼상을 맞출 차례다.";

            // 혼상 쪽 문구·회전량도 같은 이유로 빌더가 쥔다
            var orb = Object.FindFirstObjectByType<HonsangFocusOrb>(FindObjectsInactive.Include);
            if (orb != null)
            {
                orb.turnToLearn = 150f;
                orb.notReadyMessage = "구는 묵직하게 돌아갈 뿐이다. 어느 쪽으로 얼마나 돌려야 할지 알 수가 없다.";
                orb.needLightMessage = "빛이 필요하다. 작업실의 촛대를 가져와야겠다.";
                EditorUtility.SetDirty(orb);
            }

            EditorUtility.SetDirty(focus);
            EditorUtility.SetDirty(pz);

            // 정답을 사람이 읽을 수 있게 한 줄 남긴다 (검증용)
            var sb = new System.Text.StringBuilder("[혼천의 퍼즐] 정답 — ");
            for (int i = 0; i < Answer.Length && i < focus.ringNames.Length; i++)
                sb.Append(focus.ringNames[i]).Append(' ').Append(CompassNames.Ko[Answer[i]]).Append("  ");
            Debug.Log(sb.ToString());
            return 1;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
