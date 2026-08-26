using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 소지품 시험용 자산·배치 생성기 (2026-08-23). 전부 **멱등** — 여러 번 눌러도 같은 결과다.
    ///
    /// ■ 무엇을 만드나
    ///   1) 모델 프리팹  Prefabs/Items/서책_아버지유품.prefab
    ///      Joseon Library의 SM_CCJ_BookA(FBX 내장 재질이 이미 URP Lit + BaseMap 연결됨)를
    ///      우리 폴더의 프리팹으로 감쌌다. 원본 폴더는 손대지 않는다.
    ///   2) 소지품 정의   Resources/GyeonuItems/Item_C4_아버지유품서책.asset
    ///      Resources 아래에 두는 까닭 — 디버그 획득·세이브 복원이 id로 찾아야 한다.
    ///   3) 씬 배치       선아집 아버지 방 문갑 위 (실측 상면 1.467)
    ///
    /// ■ 왜 프리팹에 ItemPickup을 미리 안 붙였나
    ///   상세 보기가 같은 프리팹을 복제해 무대에 올린다. ItemPickup.Awake는 "이미 지닌
    ///   물건이면 자기를 감춘다" — 미리 붙여 두면 **상세 보기 복제본이 켜지자마자 사라진다**.
    ///   그래서 씬에 놓을 때만 배치 스크립트가 붙인다.
    /// </summary>
    public static class InventorySetup
    {
        const string Root = "Tools/이문록/소지품/";

        const string BookFbx = "Assets/Joseon Library/gyeongbokgung-cheonchujeon-book/source/SM_CCJ_BookA.fbx";
        const string ItemFolder = "Assets/_Project/Gyeonu/Prefabs/Items";
        const string ResFolder = "Assets/_Project/Gyeonu/Resources/GyeonuItems";
        const string PrefabPath = ItemFolder + "/서책_아버지유품.prefab";
        const string AssetPath = ResFolder + "/Item_C4_아버지유품서책.asset";

        const string PickupRoot = "선아집_습득물";
        const string PickupName = "습득_아버지유품서책";
        const string ChestName = "반닫이";

        /// <summary>
        /// 반닫이 **안쪽 바닥** — 실측 y 1.087 (2026-08-24).
        ///
        /// 어떻게 쟀나: SkinnedMeshRenderer를 BakeMesh로 구워 임시 MeshCollider를 올리고
        /// 궤 속 12개 지점에서 아래로 레이캐스트 → 전부 1.087, 법선 (0,1,0)으로 평평했다.
        /// ⚠️ Renderer.bounds(0.935~1.681)는 스킨드 메시라 **작성자가 넣어 둔 넉넉한 상자**다 —
        ///    그걸로 바닥을 잡으면 0.15m 파묻힌다. 실제 기하는 x 4.140~4.568로 더 얕다.
        /// ⚠️ Physics.RaycastAll은 **콜라이더 하나당 한 번만** 맞힌다 — 궤처럼 겹겹인 메시에서
        ///    RaycastAll로 안쪽 면을 찾으려 하면 바깥 껍데기만 잡히고 빈손으로 돌아온다.
        ///
        /// 앞판은 y 1.37 언저리에서 앞으로 내려앉는다 — 그 아래 0.28m가 궤 속 우물이라
        /// 닫혀 있으면 서책이 통째로 가려지고, 열면 위에서 내려다보인다.
        /// </summary>
        static readonly Vector3 BookSpot = new Vector3(4.395f, 1.087f, -2.330f);
        const float BookYaw = -6f;

        // ───────────────────────────────────────────────────────
        [MenuItem(Root + "시험 아이템 만들기 (아버지의 유품 서책)", priority = 100)]
        public static InventoryItem CreateTestItem()
        {
            EnsureFolder(ItemFolder);
            EnsureFolder(ResFolder);

            var prefab = BuildBookPrefab();
            if (prefab == null) return null;

            var item = AssetDatabase.LoadAssetAtPath<InventoryItem>(AssetPath);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<InventoryItem>();
                AssetDatabase.CreateAsset(item, AssetPath);
            }

            item.itemId = "C4";
            item.displayName = "아버지의 유품 서책";
            item.description =
                "선아의 아버지가 남긴 서책. 겉장은 손때로 반들거리고, 아래쪽 모서리가 닳아 있다.\n\n" +
                "속장에는 밤하늘의 별자리를 짚은 그림과, 날짜로 보이는 숫자가 잇달아 적혀 있다. " +
                "글씨는 급하게 쓴 듯 기울었고, 몇 줄은 먹이 번져 읽기 어렵다.\n\n" +
                "마지막 장에는 다른 필체로 짧게 덧붙어 있다.\n" +
                "  「칠석 전날, 물이 가장 낮을 때. 다리 아래로.」\n\n" +
                "(임시 설명 — 실제 단서 문안이 정해지면 이 글만 바꾸면 된다. " +
                "여기 적힌 글이 길어져도 휠로 굴려 읽을 수 있는지 확인하려고 일부러 길게 두었다.)";
            item.modelPrefab = prefab;
            // 책이 평평히 누워 있어 정면에서 보면 종잇장 옆면만 보인다 — 겉장이 보이도록 눕혀 세운다
            item.previewEuler = new Vector3(-62f, 22f, 0f);
            item.previewZoom = 1.15f;   // 그림틀을 조금 더 채우게
            // 서책은 **쓰임 버튼이 없다** — 옆에 글이 다 나오므로 누를 것이 없다.
            // 지도라면 usable=true + useLabel="펼쳐보기", 열쇠라면 "사용하기"로 채우면 버튼이 생긴다.
            item.usable = false;
            item.useLabel = "";
            item.useNotReadyHint = "";
            item.pickupVerb = "살피기";        // 자리별 동사는 ItemPickup.verbOverride가 덮는다
            item.autoShowOnPickup = true;
            item.journalKey = "father_book";
            item.journalText = "선아의 아버지가 남긴 서책 — 별자리 그림과 날짜가 적혀 있다.";

            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[소지품] 시험 아이템 준비 완료 — {AssetPath}");
            Selection.activeObject = item;
            return item;
        }

        /// <summary>FBX를 우리 폴더 프리팹으로 감싼다. 조준용 박스 콜라이더까지 맞춰 넣는다.</summary>
        static GameObject BuildBookPrefab()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(BookFbx);
            if (fbx == null)
            {
                Debug.LogError("[소지품] 서책 원본이 없다 — " + BookFbx +
                               "\n  (Joseon Library 팩은 gitignore다. 각자 임포트해야 한다)");
                return null;
            }

            var temp = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            PrefabUtility.UnpackPrefabInstance(temp, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            temp.name = "서책_아버지유품";
            temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            foreach (var c in temp.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);

            // 조준이 잘 걸리도록 실제 책보다 조금 넉넉한 박스 (책은 21×3.6×30cm로 작다)
            var rends = temp.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                var box = temp.AddComponent<BoxCollider>();
                box.center = temp.transform.InverseTransformPoint(b.center);
                box.size = b.size + new Vector3(0.04f, 0.06f, 0.04f);
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        // ───────────────────────────────────────────────────────
        [MenuItem(Root + "선아집 아버지 방 반닫이 안에 서책 놓기", priority = 101)]
        public static void PlaceInSeonaHouse()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name != SeonaHouseLayout.SceneName)
            {
                EditorUtility.DisplayDialog("소지품",
                    $"{SeonaHouseLayout.SceneName} 씬을 연 뒤에 눌러라.\n(지금 열린 씬: {scene.name})", "알겠다");
                return;
            }

            var item = AssetDatabase.LoadAssetAtPath<InventoryItem>(AssetPath) ?? CreateTestItem();
            if (item == null || item.modelPrefab == null) return;

            // 습득물은 가구(기물)와 섞지 않는다 — 나중에 통째로 확인·정리하기 쉽게 따로 뿌리를 둔다
            var root = GameObject.Find(PickupRoot);
            if (root == null) root = new GameObject(PickupRoot);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var old = root.transform.Find(PickupName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var go = (GameObject)PrefabUtility.InstantiatePrefab(item.modelPrefab);
            go.name = PickupName;
            go.transform.SetParent(root.transform, false);
            go.transform.rotation = Quaternion.Euler(0f, BookYaw, 0f);

            // 바닥면을 반닫이 안쪽 바닥에 정확히 얹는다 (프리팹 피벗이 어디든 실측 바운즈로 맞춘다)
            go.transform.position = BookSpot;
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                go.transform.position += new Vector3(0f, BookSpot.y - b.min.y, 0f);
            }

            var pickup = go.AddComponent<ItemPickup>();
            pickup.item = item;
            pickup.verbOverride = "꺼내기";      // 궤 안에 든 물건
            // 이름도, 조작 안내도 붙이지 않는다 — 조준하면 "꺼내기"만 뜨고,
            // 무엇이었는지는 꺼낸 뒤 상세 창에서 처음 안다.
            pickup.revealName = false;
            pickup.pickupToast = "";

            // 반닫이가 열려 있을 때만 집힌다 — 닫혀 있으면 조준해도 아무것도 안 뜬다
            var chest = FindProp(ChestName);
            if (chest != null) pickup.insideFurniture = chest.GetComponent<FurnitureParts>();
            if (pickup.insideFurniture == null)
                Debug.LogWarning("[소지품] 반닫이의 FurnitureParts를 못 찾았다 — 서책이 닫힌 채로도 집힌다");

            EditorUtility.SetDirty(go);
            EditorSceneManagerMarkDirty(scene);
            Selection.activeGameObject = go;
            Debug.Log($"[소지품] 반닫이 안에 '{item.displayName}' 배치 — {go.transform.position:F3} " +
                      $"(바닥 {BookSpot.y:F3}, 문구 '{pickup.Prompt}')");
        }

        /// <summary>선아집_소품/기물 아래의 가구를 이름으로 찾는다.</summary>
        static GameObject FindProp(string name)
        {
            var root = GameObject.Find(SeonaHouseLayout.PropRoot);
            if (root == null) return null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
            return null;
        }

        static void EditorSceneManagerMarkDirty(Scene scene) =>
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
