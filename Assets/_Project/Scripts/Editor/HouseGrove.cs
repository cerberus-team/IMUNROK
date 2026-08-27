using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>1막 고택 둘레에도 숲을 두른다.</b> 관아에 두른 것(㉑)의 1막 짝이다.
    /// 메뉴: [이문록 ▸ 옹고집 ▸ 고택 둘레에 숲 두르기]
    ///
    /// 관아와 다른 점이 셋이라 도구를 따로 낸다.
    ///
    ///   ① <b>땅이 이미 있다.</b> 관아는 담 밖이 허공이라 풀밭을 깔아야 했지만, 고택은
    ///      <c>Cube</c> 한 장(113×138)이 y −1.67 로 사방에 깔려 있다. 새로 안 깐다.
    ///   ② <b>집이 두 배 넘게 크다.</b> 관아가 38×28 인데 고택은 <b>74×76</b> 이다.
    ///      둘레가 300m 를 넘어, 관아와 같은 2.2m 간격으로 심으면 1,200 그루가 되어
    ///      삼각형이 260만 늘어난다 — 고택은 그 자체로 이미 467만이다. 그래서 사이를
    ///      <b>3.4m</b> 로 벌린다. 빽빽함은 좀 덜해도 담 너머로 보이는 그림은 같다.
    ///   ③ <b>하늘은 안 건드린다.</b> 1막은 한밤중이라 관아의 맑은 아침을 씌우면
    ///      이야기가 깨진다. 나무만 심는다.
    ///
    /// <b>집이 어디까지인지는 재서 안다.</b> 고택 뿌리를 통째로 재면 사방에 깔린 바닥
    /// 판(113×138)과 먼거리 배경(56×49)까지 들어와 <b>온 세상이 집</b>이 되어 나무를
    /// 한 그루도 못 심는다. 그래서 <b>너무 넓은 것은 빼고</b> 잰다.
    ///
    /// <b>대문 앞은 비운다.</b> 고택 대문은 남쪽(−z) x −7.5 에 있다.
    ///
    /// 두 번 눌러도 두 벌이 안 선다. 심는 자리는 못 박은 씨앗에서 나온다.
    /// </summary>
    public static class HouseGrove
    {
        private const string Shell = "Assets/_Project/Onggojip/Scenes/Onggojip.unity";
        private const string Yard  = "Assets/_Project/Onggojip/Scenes/Onggojip_마당.unity";
        private const string YardName = "Onggojip_마당";

        private const string Aspen  = "Assets/Soswaewon/Prefabs/Environments/SM_Aspen.prefab";
        private const string Bamboo = "Assets/Soswaewon/Prefabs/Environments/SM_Bamboo.prefab";
        private const string Maple  = "Assets/Soswaewon/Prefabs/Environments/SM_Maple.prefab";
        private const string Willow = "Assets/Soswaewon/Prefabs/Environments/SM_Willow.prefab";

        private const string GroveName = "고택_바깥숲";

        /// <summary>고택 바깥 땅의 높이.</summary>
        private const float Ground = -1.67f;

        /// <summary>담 밖으로 숲이 뻗는 너비(m) · 나무 사이(m) · 집에서 떨어뜨릴 거리(m).</summary>
        private const float Belt = 9f, Step = 3.4f, Clear = 2f;

        /// <summary>대문(남쪽 −z, x −7.5) 앞으로 비워 둘 길.</summary>
        private const float GateX = -7.5f, RoadHalf = 7f;

        /// <summary>이보다 넓은 것은 집이 아니라 바닥 판·먼거리 배경이다.</summary>
        private const float TooWide = 55f, TooDeep = 45f;

        /// <summary>비싼 나무는 마릿수를 못 박는다. 나머지는 다 사시나무다.</summary>
        private const int MaxBamboo = 10, MaxMaple = 3, MaxWillow = 3;

        [MenuItem("이문록/옹고집/고택 둘레에 숲 두르기")]
        public static void Run()
        {
            // 껍데기와 마당을 겹쳐 연다 — 집은 마당 쪽에 들어 있다.
            var open = SceneManager.GetSceneByName(YardName);
            if (!open.isLoaded)
            {
                EditorSceneManager.OpenScene(Shell, OpenSceneMode.Single);
                open = EditorSceneManager.OpenScene(Yard, OpenSceneMode.Additive);
            }

            var log = new System.Text.StringBuilder("[옹고집] 고택 둘레에 숲을 두른다\n");
            var house = House(log);
            Plant(open, house, log);

            EditorSceneManager.MarkSceneDirty(open);
            Debug.Log(log.ToString());
        }

        /// <summary>지은 것만 재서 합친다 — 바닥 판과 먼거리 배경은 뺀다.</summary>
        private static Bounds House(System.Text.StringBuilder log)
        {
            bool any = false; var b = new Bounds(); int n = 0, skip = 0;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                var s = r.bounds.size;
                if (s.x > TooWide || s.z > TooDeep) { skip++; continue; }
                n++;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            if (!any) b = new Bounds(new Vector3(-3f, 0f, 1f), new Vector3(74f, 10f, 76f));
            b.Expand(new Vector3(Clear * 2f, 0f, Clear * 2f));
            log.AppendLine("── 집이 차지한 자리 " + b.size.x.ToString("F1") + "m × " + b.size.z.ToString("F1")
                         + "m  (잰 것 " + n + "개, 너무 넓어 뺀 것 " + skip + "개)");
            return b;
        }

        private static void Plant(Scene scene, Bounds house, System.Text.StringBuilder log)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == GroveName) Undo.DestroyObjectImmediate(root);

            var group = new GameObject(GroveName);
            Undo.RegisterCreatedObjectUndo(group, "고택 바깥숲");
            SceneManager.MoveGameObjectToScene(group, scene);

            var aspen  = AssetDatabase.LoadAssetAtPath<GameObject>(Aspen);
            var bamboo = AssetDatabase.LoadAssetAtPath<GameObject>(Bamboo);
            var maple  = AssetDatabase.LoadAssetAtPath<GameObject>(Maple);
            var willow = AssetDatabase.LoadAssetAtPath<GameObject>(Willow);
            if (aspen == null) { log.AppendLine("── 나무를 못 찾았다: " + Aspen); return; }

            var rng = new System.Random(20260826);
            float x0 = house.min.x - Belt, x1 = house.max.x + Belt;
            float z0 = house.min.z - Belt, z1 = house.max.z + Belt;

            int na = 0, nb = 0, nm = 0, nw = 0; long tri = 0;

            for (float x = x0; x <= x1; x += Step)
                for (float z = z0; z <= z1; z += Step)
                {
                    float px = x + (float)(rng.NextDouble() - 0.5) * Step * 0.8f;
                    float pz = z + (float)(rng.NextDouble() - 0.5) * Step * 0.8f;

                    if (px > house.min.x && px < house.max.x && pz > house.min.z && pz < house.max.z) continue;
                    // 대문 앞길 — 남쪽으로 난 길이다
                    if (pz < house.min.z && Mathf.Abs(px - GateX) < RoadHalf) continue;

                    GameObject src = aspen; int kind = 0;
                    int roll = rng.Next(100);
                    if (roll < 4 && bamboo != null && nb < MaxBamboo) { src = bamboo; kind = 1; }
                    else if (roll < 6 && maple != null && nm < MaxMaple) { src = maple; kind = 2; }
                    else if (roll < 8 && willow != null && nw < MaxWillow) { src = willow; kind = 3; }

                    var t = (GameObject)PrefabUtility.InstantiatePrefab(src, group.transform);
                    t.transform.position = new Vector3(px, Ground, pz);
                    t.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                    float s = 0.75f + (float)rng.NextDouble() * 0.5f;
                    t.transform.localScale = new Vector3(s, s, s);

                    // 담이 사람을 막으니 나무의 콜라이더는 짐만 된다.
                    foreach (var c in t.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

                    foreach (var mf in t.GetComponentsInChildren<MeshFilter>(true))
                        if (mf.sharedMesh != null) tri += mf.sharedMesh.triangles.Length / 3;

                    if (kind == 1) nb++; else if (kind == 2) nm++; else if (kind == 3) nw++; else na++;
                }

            log.AppendLine("── 숲 " + (na + nb + nm + nw) + "그루"
                         + " (사시나무 " + na + " · 대나무 " + nb + " · 단풍 " + nm + " · 버들 " + nw + ")");
            log.AppendLine("   삼각형 " + (tri / 1000) + "k 를 보탰다 — 사이 " + Step.ToString("F1") + "m, 띠 " + Belt.ToString("F0") + "m");
        }
    }
}
