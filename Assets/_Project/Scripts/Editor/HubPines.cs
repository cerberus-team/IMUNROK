using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>조사청을 솔숲으로 두른다.</b>
    /// 메뉴: [이문록 ▸ 조사청 ▸ 솔숲 두르기] / [… 솔숲 걷기]
    ///
    /// 조사청에는 진작 나무 열여덟 그루가 있었는데 <b>들판에 흩어져</b> 있었다.
    /// 흩어진 나무는 배경이지 <b>둘레</b>가 아니다. 마당에 서서 고개를 돌리면
    /// 백오십 미터짜리 빈 들판이 그대로 보여, 관아 한 채가 허허벌판에 놓인 것처럼
    /// 읽힌다. 조사청은 <b>가려진 곳</b>이라야 한다 — 어사가 사람들 눈을 피해
    /// 드나드는 자리다.
    ///
    /// <b>새 나무를 들이지 않는다.</b> 이미 이 씬이 쓰는 그 팩의 그 프리팹을
    /// 그대로 심는다. 종을 섞으면 한 숲에 두 세계가 되고, 무엇보다 그 나무들에는
    /// <b>LOD 와 빌보드</b>가 달려 있어 멀어지면 판 한 장으로 떨어진다 —
    /// 열여섯 그루를 더 심어도 늘 다 그리지는 않는다.
    ///
    /// 굽은 줄기에 층진 수관인 <b>3_2·3_3</b> 만 쓴다. 3_1 은 둥근 떨기라
    /// 솔로 안 읽힌다.
    ///
    /// 두 번 눌러도 두 벌이 안 선다. 마음에 안 들면 [솔숲 걷기] 로 통째로 걷는다.
    /// </summary>
    public static class HubPines
    {
        private const string HubPath = "Assets/_Project/Scenes/Core/HubScene.unity";
        private const string GroupName = "조사청_솔숲";

        private const string Pine2 = "Assets/Fristy stylize Modular Assets 2/Prefabs/3_2_Tree.prefab";
        private const string Pine3 = "Assets/Fristy stylize Modular Assets 2/Prefabs/3_3_Tree.prefab";

        /// <summary>몇 그루. 늘리면 그만큼 빽빽해지고 그만큼 무거워진다.</summary>
        private const int Count = 18;

        /// <summary>마당 한가운데에서 이만큼 떨어진 고리에 심는다(m).</summary>
        private const float Inner = 16f, Outer = 23f;

        [MenuItem("이문록/조사청/솔숲 두르기")]
        public static void Plant() { Run(true); }

        [MenuItem("이문록/조사청/솔숲 걷기")]
        public static void Clear() { Run(false); }

        private static void Run(bool plant)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[솔숲] 재생을 멈추고 다시 누르십시오.");
                return;
            }

            var scene = SceneManager.GetActiveScene();
            if (scene.path != HubPath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(HubPath, OpenSceneMode.Single);
            }

            var log = new System.Text.StringBuilder(plant ? "[솔숲] 두르기\n" : "[솔숲] 걷기\n");

            // 이미 심은 것이 있으면 걷고 다시 심는다 — 두 벌이 서면 숲이 아니라 덤불이다
            GameObject old = null;
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == GroupName) old = root;
            if (old != null)
            {
                Object.DestroyImmediate(old);
                log.AppendLine("── 먼저 심었던 것을 걷었다");
            }

            if (!plant)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log(log.ToString());
                return;
            }

            // 마당 한가운데와 바닥 높이를 <b>있는 것에서</b> 잰다. 조사청 배치는
            // 손으로 맞춰 둔 것이라, 숫자를 새로 정하면 그 손이 지워진다.
            Bounds yard = new Bounds();
            bool got = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name.IndexOf("마당") < 0) continue;
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                { if (!got) { yard = r.bounds; got = true; } else yard.Encapsulate(r.bounds); }
            }
            if (!got)
            {
                Debug.LogWarning(log + "── 마당을 못 찾아 자리를 못 잡는다.");
                return;
            }

            var group = new GameObject(GroupName);
            EditorSceneManager.MoveGameObjectToScene(group, scene);

            var a = AssetDatabase.LoadAssetAtPath<GameObject>(Pine2);
            var b = AssetDatabase.LoadAssetAtPath<GameObject>(Pine3);
            if (a == null && b == null)
            {
                Debug.LogWarning(log + "── 솔 프리팹이 없다(아트는 공유 폴더에 있다).");
                Object.DestroyImmediate(group);
                return;
            }

            // 씨를 못 박는다. 누가 눌러도 같은 숲이 서야 팀원과 같은 그림을 본다.
            var rnd = new System.Random(19700117);
            int planted = 0;
            for (int i = 0; i < Count; i++)
            {
                float turn = (i + (float)rnd.NextDouble() * 0.55f) / Count * Mathf.PI * 2f;
                float far = Mathf.Lerp(Inner, Outer, (float)rnd.NextDouble());
                var at = new Vector3(yard.center.x + Mathf.Cos(turn) * far,
                                     yard.center.y + 30f,
                                     yard.center.z + Mathf.Sin(turn) * far);

                float groundY = yard.min.y;
                RaycastHit hit;
                if (Physics.Raycast(at, Vector3.down, out hit, 60f, ~0, QueryTriggerInteraction.Ignore))
                    groundY = hit.point.y;

                var pf = (a != null && (b == null || rnd.Next(2) == 0)) ? a : b;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, group.transform);
                go.transform.position = new Vector3(at.x, groundY, at.z);
                go.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);

                // 크기를 흩는다. 같은 키로 늘어서면 나무가 아니라 울타리로 보인다.
                float k = 0.85f + (float)rnd.NextDouble() * 0.55f;
                go.transform.localScale = new Vector3(k, k * (0.95f + (float)rnd.NextDouble() * 0.25f), k);
                planted++;
            }

            long tris = 0;
            foreach (var r in group.GetComponentsInChildren<Renderer>(true))
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;
            }

            Selection.activeGameObject = group;
            EditorUtility.SetDirty(group);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            log.AppendLine("── " + planted + "그루를 " + Inner + "~" + Outer + "m 고리에 심었다");
            log.AppendLine("   마당 한가운데 " + yard.center.ToString("F1") + " · 바닥은 광선으로 짚었다");
            log.AppendLine("   더한 삼각형 " + tris.ToString("N0") + "개(LOD 다 합쳐서 — 멀면 빌보드로 떨어진다)");
            log.AppendLine("   마음에 안 들면 [이문록 ▸ 조사청 ▸ 솔숲 걷기]");
            Debug.Log(log.ToString());
        }
    }
}
