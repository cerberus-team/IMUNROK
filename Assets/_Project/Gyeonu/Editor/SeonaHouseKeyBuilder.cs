using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Gyeonu.EditorTools
{
    /// <summary>
    /// 선아 집 열쇠 (2026-09-10) — 견우가 신뢰도 40에서 건네는 그 열쇠의 실물과 문 잠금.
    ///
    /// <code>
    ///   ① 재질   M_수령열쇠 를 복제해 나무색으로 (원본은 손대지 않는다)
    ///   ② 프리팹 수령열쇠.prefab 을 복제해 재질만 바꾼 선아집열쇠.prefab
    ///   ③ 소지품 Item_SEONA_HOUSE_KEY — worldFlag = F_선아집열쇠
    ///   ④ 문     마을 씬 '출구_선아집' 에 SeonaHouseGate 를 붙여 저장
    /// </code>
    /// 멱등 — 몇 번 눌러도 같은 결과다. 씬의 배치·가구는 건드리지 않는다.
    /// </summary>
    public static class SeonaHouseKeyBuilder
    {
        const string SrcPrefab = "Assets/_Project/Gyeonu/Prefabs/Items/수령열쇠.prefab";
        const string SrcMat    = "Assets/_Project/Gyeonu/Art/Materials/GwanaOffice/M_수령열쇠.mat";
        const string DstMat    = "Assets/_Project/Gyeonu/Art/Materials/Items/M_선아집열쇠.mat";
        const string DstPrefab = "Assets/_Project/Gyeonu/Prefabs/Items/선아집열쇠.prefab";
        const string DstItem   = "Assets/_Project/Gyeonu/Resources/GyeonuItems/Item_SEONA_HOUSE_KEY_선아집열쇠.asset";
        const string VillageScene = "Assets/_Project/Gyeonu/Scenes/Gyeonu.unity";

        public const string ItemId = "SEONA_HOUSE_KEY";

        [MenuItem("Tools/이문록/선아 집 ▸ 열쇠 · 문 잠금 만들기", priority = 350)]
        public static void Build()
        {
            var item = BuildItem();
            if (item == null) return;
            AttachGate();
            Debug.Log("[선아집열쇠] 완료 — 재질 " + DstMat + " / 프리팹 " + DstPrefab + " / 소지품 " + DstItem + " / 마을 씬 문 잠금.");
        }

        /// <summary>재질·프리팹·소지품 정의. 씬은 건드리지 않는다.</summary>
        public static InventoryItem BuildItem()
        {
            var srcPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SrcPrefab);
            var srcMat = AssetDatabase.LoadAssetAtPath<Material>(SrcMat);
            if (srcPrefab == null || srcMat == null)
            {
                Debug.LogError("[선아집열쇠] 원본이 없다 — 프리팹 " + (srcPrefab != null) + " / 재질 " + (srcMat != null) +
                               ". 먼저 집무실 렌즈 퍼즐 빌더로 수령 열쇠를 만들어야 한다.");
                return null;
            }

            // ① 재질 — 복제본에만 색을 입힌다. 텍스처는 그대로 두고 밑색으로 나무빛을 낸다.
            var mat = AssetDatabase.LoadAssetAtPath<Material>(DstMat);
            if (mat == null) { mat = new Material(srcMat); AssetDatabase.CreateAsset(mat, DstMat); }
            else mat.CopyPropertiesFromMaterial(srcMat);
            // ⚠️ 소지품 미리보기(InventoryPreview)는 점광 셋(세기 6·2.2·3)으로 밝게 비춘다 — 원본 텍스처가
            //    이미 밝은 상아빛이라 (0.46, 0.29, 0.15) 정도로는 화면에서 크림색으로 보였다 (2026-09-10 실측).
            //    미리보기에서도 나무로 읽히게 밑색을 한참 어둡게 잡는다.
            mat.SetColor("_BaseColor", new Color(0.20f, 0.10f, 0.04f, 1f));   // 손때 밴 참나무
            mat.SetFloat("_Metallic", 0.0f);
            mat.SetFloat("_Smoothness", 0.15f);
            // 점광 셋의 하이라이트·반사가 밑색을 덮어 희게 보인다 — 나무는 광택이 없으니 둘 다 끈다.
            mat.SetFloat("_SpecularHighlights", 0f);
            mat.SetFloat("_EnvironmentReflections", 0f);
            mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(mat);

            // ② 프리팹 — 원본 인스턴스를 풀어 재질만 바꿔 새 프리팹으로
            var tmp = (GameObject)PrefabUtility.InstantiatePrefab(srcPrefab);
            tmp.name = "선아집열쇠";
            PrefabUtility.UnpackPrefabInstance(tmp, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (var r in tmp.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
            var prefab = PrefabUtility.SaveAsPrefabAsset(tmp, DstPrefab);
            Object.DestroyImmediate(tmp);

            // ③ 소지품 정의
            var item = AssetDatabase.LoadAssetAtPath<InventoryItem>(DstItem);
            if (item == null) { item = ScriptableObject.CreateInstance<InventoryItem>(); AssetDatabase.CreateAsset(item, DstItem); }
            item.itemId = ItemId;
            item.displayName = "선아 집 열쇠";
            item.pickupVerb = "받기";
            item.description =
                "견우가 품에서 꺼내 건넨 나무 열쇠다. 손때가 배어 결이 매끈하다.\n\n" +
                "선아가 밤에 드나들 때 쓰던 것이라 했다. 문의 빗장을 안에서 걸어 두고도 " +
                "이 열쇠로 밖에서 젖힐 수 있게 되어 있다.\n\n" +
                "이제 선아 집에 들어갈 수 있다.";
            item.modelPrefab = prefab;
            item.previewEuler = new Vector3(-78f, 0f, 10f);
            item.previewZoom = 1.05f;
            item.usable = false;
            item.autoShowOnPickup = true;
            item.journalKey = "seona_house_key";
            item.journalText = "견우에게서 받은 선아 집 열쇠. 나무로 깎은 것이다.";
            item.worldFlag = GyeonuWorld.F_선아집열쇠;
            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssets();
            return item;
        }

        /// <summary>마을 씬의 '출구_선아집'에 SeonaHouseGate를 붙이고 저장한다. 다른 것은 건드리지 않는다.</summary>
        public static void AttachGate()
        {
            var active = SceneManager.GetActiveScene();
            bool opened = false;
            if (active.path != VillageScene)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                active = EditorSceneManager.OpenScene(VillageScene, OpenSceneMode.Single);
                opened = true;
            }
            var go = GameObject.Find(SeonaHouseGate.ExitName);
            if (go == null || go.GetComponent<SceneExit>() == null)
            {
                Debug.LogWarning("[선아집열쇠] 마을 씬에 '" + SeonaHouseGate.ExitName + "'(SceneExit) 이 없다 — 잠금을 붙이지 못했다.");
                return;
            }
            if (go.GetComponent<SeonaHouseGate>() == null)
            {
                Undo.AddComponent<SeonaHouseGate>(go);
                EditorSceneManager.MarkSceneDirty(active);
                EditorSceneManager.SaveScene(active);
                Debug.Log("[선아집열쇠] '" + SeonaHouseGate.ExitName + "' 에 SeonaHouseGate 를 붙여 저장했다" + (opened ? " (마을 씬을 열었다)" : "") + ".");
            }
            else Debug.Log("[선아집열쇠] 문 잠금은 이미 붙어 있다.");
        }
    }
}
