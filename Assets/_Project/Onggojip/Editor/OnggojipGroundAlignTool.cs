using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IMUNROK.Onggojip.Editor
{
    /// <summary>
    /// "집이 바닥에서 떠 있다 / 파묻혔다"를 자동으로 맞추는 도구.
    ///
    /// 원리:
    ///  · 집(김명관고택)의 모든 렌더러를 훑어 "제일 낮은 지점(밑면)"을 구한다.
    ///  · 바닥판(Cube)이 있으면 → 바닥판을 위/아래로 옮겨 그 윗면이 집 밑면에 딱 닿게 한다.
    ///  · 바닥판이 없으면 → 집 전체를 내려/올려서 밑면이 y=0 에 오게 한다.
    ///
    /// 값을 몰라도 알아서 계산하므로 눈대중이 필요 없다. 실행 후 Ctrl+S.
    /// 메뉴: [이문록 ▸ 옹고집: 바닥 높이 자동 맞추기].
    /// </summary>
    public static class OnggojipGroundAlignTool
    {
        private const string GroupName = "김명관고택";

        [MenuItem("이문록/옹고집: 바닥 높이 자동 맞추기")]
        public static void Align()
        {
            var scene = EditorSceneManager.GetActiveScene();

            var group = FindInScene(scene, GroupName);
            if (group == null)
            {
                EditorUtility.DisplayDialog("이문록",
                    $"'{GroupName}' 를 못 찾았습니다.\n먼저 집을 합쳤는지 확인하세요.", "확인");
                return;
            }

            // 바닥판 후보(집 안/밖 어디든): 이름에 Cube/바닥/Ground/Floor 포함
            var ground = FindGround(scene, group);

            // 집 밑면 계산(바닥판은 제외)
            Renderer groundRend = ground != null ? ground.GetComponentInChildren<Renderer>() : null;
            if (!TryGetHouseBounds(group, ground, out Bounds house))
            {
                EditorUtility.DisplayDialog("이문록",
                    "집에서 렌더러(보이는 물체)를 못 찾았습니다.", "확인");
                return;
            }

            string msg;
            if (ground != null && groundRend != null)
            {
                // 바닥판 윗면을 집 밑면에 맞춤
                float groundTop = groundRend.bounds.max.y;
                float delta = house.min.y - groundTop;
                ground.transform.position += new Vector3(0, delta, 0);
                msg = $"바닥판('{ground.name}')을 {delta:0.###} 만큼 옮겨\n집 밑면(y={house.min.y:0.###})에 딱 맞췄습니다.";
            }
            else
            {
                // 바닥판이 없으면 집 밑면을 y=0 으로
                float delta = -house.min.y;
                group.transform.position += new Vector3(0, delta, 0);
                msg = $"바닥판이 없어 집 전체를 {delta:0.###} 만큼 옮겨\n밑면을 y=0 에 맞췄습니다.";
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorUtility.DisplayDialog("이문록", msg + "\n\nCtrl+S로 저장하세요.", "확인");
        }

        /// <summary>집 밑면(bounds). 바닥판 렌더러는 계산에서 제외.</summary>
        private static bool TryGetHouseBounds(GameObject group, GameObject ground, out Bounds bounds)
        {
            bounds = default;
            bool has = false;
            var renders = group.GetComponentsInChildren<Renderer>();
            foreach (var r in renders)
            {
                if (ground != null && (r.gameObject == ground || r.transform.IsChildOf(ground.transform)))
                    continue; // 바닥판 제외
                if (!has) { bounds = r.bounds; has = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return has;
        }

        /// <summary>바닥판으로 보이는 오브젝트 찾기(넓고 얇은 것 우선).</summary>
        private static GameObject FindGround(UnityEngine.SceneManagement.Scene scene, GameObject group)
        {
            // 1) 이름으로
            string[] keys = { "Cube", "바닥", "Ground", "Floor" };
            foreach (var r in group.GetComponentsInChildren<Renderer>())
                foreach (var k in keys)
                    if (r.gameObject.name.Contains(k)) return r.gameObject;

            // 2) 루트 레벨(집 밖에 따로 있는 바닥판)
            foreach (var root in scene.GetRootGameObjects())
                foreach (var k in keys)
                    if (root.name.Contains(k)) return root;

            return null;
        }

        private static GameObject FindInScene(UnityEngine.SceneManagement.Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
                var t = root.transform.Find(name);
                if (t != null) return t.gameObject;
            }
            return null;
        }
    }
}
