using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>도구를 관아로 옮긴다.</b> 메뉴: [이문록 ▸ 관아 ▸ ⑰ 도구를 관아로 옮긴다]
    ///
    /// 2막에서 등불과 돋보기가 유난히 겉돌던 데에는 까닭이 하나 더 있었다 —
    /// <b>손에 들 물건이 아예 없다</b>. 벨트에는 등불과 돋보기가 올라 있는데,
    /// 관아 씬의 카메라 밑에는 유척 하나뿐이다. 소품은 1막(Onggojip)과 조사청에만 있다.
    ///
    /// 그러면 이렇게 된다. <see cref="MagnifierLens"/> 는 소품을 못 찾으면
    /// <b>제가 흉내를 빚어 눈앞에 띄운다</b>(원래는 소품이 없을 때도 렌즈는 되게 하려는
    /// 배려였다). 그래서 화면에는 무언가 뜨는데 그것이 여러분이 만든 놋쇠 돋보기가 아니다.
    /// 등불은 <see cref="LanternController"/> 가 아예 없으니 종이 뒤로 넘어가지도 않는다.
    ///
    /// 1막 씬을 곁에 열어 그 카메라 밑에 달린 소품을 <b>그대로</b> 가져온다.
    /// 자리와 기울기는 손대지 않는다 — 1막에서 맞춰 둔 드는 자세가 곧 참값이다.
    ///
    /// 이미 있으면 건드리지 않는다. 두 번 눌러도 두 벌이 안 선다.
    /// </summary>
    public static class GwanaTools
    {
        private const string Act1 = "Assets/_Project/Onggojip/Scenes/Onggojip.unity";

        /// <summary>옮겨 올 도구.</summary>
        private static readonly string[] Want = { "lantern", "magnify" };

        [MenuItem("이문록/관아/⑰ 도구를 관아로 옮긴다")]
        public static void Run()
        {
            var here = SceneManager.GetActiveScene();
            if (!here.name.Contains("Gwana"))
            {
                Debug.LogWarning("[관아] 관아 씬을 열고 누르십시오(지금은 " + here.name + ").");
                return;
            }

            var dst = MainCam(here);
            if (dst == null) { Debug.LogWarning("[관아] 관아 씬에서 카메라를 못 찾았습니다."); return; }

            var log = new System.Text.StringBuilder("[관아] 도구를 손에 들려 보낸다\n");

            // 이미 든 것.
            //
            // <b>등불은 HeldToolModel 이 아니다.</b> 제 부품(LanternController)이 불빛까지
            // 함께 쥐고 있어서 그쪽으로 갈라져 있다. 처음에 HeldToolModel 만 훑었더니
            // "1막 카메라 밑에도 없다"고 나왔는데, 있는데 다른 이름으로 서 있었던 것이다.
            var have = new HashSet<string>();
            foreach (var h in dst.GetComponentsInChildren<HeldToolModel>(true)) have.Add(h.ToolId);
            if (dst.GetComponentInChildren<LanternController>(true) != null) have.Add("lantern");

            var need = new List<string>();
            foreach (var w in Want) if (!have.Contains(w)) need.Add(w);
            if (need.Count == 0)
            {
                log.AppendLine("  · 옮길 것이 없다 — 등불도 돋보기도 이미 카메라 밑에 있다");
                Debug.Log(log.ToString());
                return;
            }

            var other = EditorSceneManager.OpenScene(Act1, OpenSceneMode.Additive);
            try
            {
                var src = MainCam(other);
                if (src == null) { log.AppendLine("  ※ 1막 씬에서 카메라를 못 찾았다"); return; }

                foreach (var h in src.GetComponentsInChildren<HeldToolModel>(true))
                {
                    if (!need.Contains(h.ToolId)) continue;
                    Bring(h.transform, h.ToolId, dst, log);
                    need.Remove(h.ToolId);
                }
                if (need.Contains("lantern"))
                {
                    var lamp = src.GetComponentInChildren<LanternController>(true);
                    if (lamp != null) { Bring(lamp.transform, "lantern", dst, log); need.Remove("lantern"); }
                }
                foreach (var miss in need)
                    log.AppendLine("  ※ " + miss + " 는 1막 카메라 밑에도 없다");
            }
            finally
            {
                EditorSceneManager.CloseScene(other, true);
            }

            EditorSceneManager.MarkSceneDirty(here);
            Debug.Log(log.ToString());
        }

        /// <summary>소품 하나를 자리와 기울기 그대로 옮긴다.</summary>
        private static void Bring(Transform src, string id, Camera dst, System.Text.StringBuilder log)
        {
            var copy = Object.Instantiate(src.gameObject);             // 활성 씬(관아)에 뜬다
            copy.name = src.gameObject.name;
            Undo.RegisterCreatedObjectUndo(copy, "도구 옮기기");
            copy.transform.SetParent(dst.transform, false);
            copy.transform.localPosition = src.localPosition;
            copy.transform.localRotation = src.localRotation;
            copy.transform.localScale = src.localScale;
            copy.SetActive(src.gameObject.activeSelf);
            log.AppendLine("  · " + id + " — " + copy.name
                         + " 을 1막에서 그대로 옮겼다 (자리 " + copy.transform.localPosition.ToString("F3") + ")");
        }

        /// <summary>그 씬의 주 카메라. 태그가 안 붙어 있으면 아무 카메라나 집는다.</summary>
        private static Camera MainCam(Scene scene)
        {
            Camera any = null;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var c in root.GetComponentsInChildren<Camera>(true))
                {
                    if (c.CompareTag("MainCamera")) return c;
                    if (any == null) any = c;
                }
            return any;
        }
    }
}
