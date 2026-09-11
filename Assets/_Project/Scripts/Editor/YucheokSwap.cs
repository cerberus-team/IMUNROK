using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>유척의 몸만 갈아 끼운다.</b>
    ///
    /// 유척은 씬에 <c>_유척</c> 이라는 <b>껍데기</b>로 서 있고, 그 껍데기가
    /// <see cref="HeldToolModel"/>(드는 자세)와
    /// <see cref="BrassRuler"/>(대어 재는 짓)를 지닌다. 모델은 그 <b>자식</b>일
    /// 뿐이다. 그러므로 모델을 바꾸는 데 껍데기를 건드릴 까닭이 없다 — 자식만 갈아 끼우고
    /// <c>HeldToolModel._model</c> 이 새 자식을 가리키게 하면 끝이다.
    ///
    /// <b>왜 손으로 안 하고 도구인가.</b> 씬에 박힌 것은 <b>모델 프리팹 인스턴스</b>라,
    /// 그 안쪽 조각들의 fileID 가 obj 와 fbx 사이에 다르다. 텍스트로 바꿔 치면 참조가
    /// 통째로 끊긴다. 유니티 안에서 바꿔야 옳다.
    ///
    /// <b>크기는 옛 몸에 맞춘다.</b> Meshy 가 뽑아 준 것은 제 단위로 나오므로 그대로 놓으면
    /// 자가 기둥만 해지거나 티끌만 해진다. 옛 몸의 <b>가장 긴 변</b>을 재어 새 몸을 거기
    /// 맞춰 줄인다 — 자의 길이가 곧 이 물건의 뜻이니 길이를 지키는 것이 맞다.
    /// </summary>
    public static class YucheokSwap
    {
        private const string NewModelPath = "Assets/_Project/Art/Tools/유척/유척.fbx";

        [MenuItem("이문록/유척을 새 모델로 갈아 끼우기")]
        public static void Swap()
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(NewModelPath);
            if (src == null)
            {
                EditorUtility.DisplayDialog("유척",
                    "새 모델을 못 찾았습니다:\n" + NewModelPath +
                    "\n\n파일은 있는데 이 말이 뜨면 아직 불러들이는 중입니다. 잠시 뒤 다시 누르십시오.", "예");
                return;
            }

            int done = 0, skipped = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                foreach (var root in scene.GetRootGameObjects())
                foreach (var held in root.GetComponentsInChildren<HeldToolModel>(true))
                {
                    if (held.ToolId != "yucheok") { continue; }

                    var so = new SerializedObject(held);
                    var modelProp = so.FindProperty("_model");
                    var old = modelProp.objectReferenceValue as GameObject;
                    if (old == null) { skipped++; continue; }
                    if (old.name == src.name) { skipped++; continue; }   // 이미 갈아 끼웠다

                    // 옛 몸의 자리와 크기를 받아 둔다
                    var t = old.transform;
                    Vector3 lp = t.localPosition;
                    Quaternion lr = t.localRotation;
                    bool wasOn = old.activeSelf;
                    float oldLongest = LongestSide(old);

                    // 새 몸을 같은 껍데기 밑에 앉힌다
                    var fresh = (GameObject)PrefabUtility.InstantiatePrefab(src, held.transform);
                    Undo.RegisterCreatedObjectUndo(fresh, "유척 갈아 끼우기");
                    fresh.transform.SetLocalPositionAndRotation(lp, lr);
                    fresh.transform.localScale = Vector3.one;

                    float newLongest = LongestSide(fresh);
                    if (oldLongest > 0.0001f && newLongest > 0.0001f)
                        fresh.transform.localScale = Vector3.one * (oldLongest / newLongest);

                    fresh.name = src.name;
                    fresh.SetActive(wasOn);

                    // 껍데기가 새 몸을 가리키게
                    Undo.RecordObject(held, "유척 갈아 끼우기");
                    modelProp.objectReferenceValue = fresh;
                    so.ApplyModifiedProperties();

                    Undo.DestroyObjectImmediate(old);
                    EditorSceneManager.MarkSceneDirty(scene);
                    done++;

                    Debug.Log("[유척] " + scene.name + " / " + Path(held.transform) +
                              " — 옛 몸 " + oldLongest.ToString("F3") + "m → 새 몸을 " +
                              fresh.transform.localScale.x.ToString("F4") + " 배로 줄여 같은 길이로 맞췄다.");
                }
            }

            EditorUtility.DisplayDialog("유척",
                done + " 자리를 갈아 끼웠습니다." + (skipped > 0 ? ("\n" + skipped + " 자리는 건너뛰었습니다(몸이 없거나 이미 새 것).") : "") +
                "\n\n씬 저장을 잊지 마십시오.", "예");
        }

        /// <summary>그려지는 몸의 가장 긴 변(월드 기준).</summary>
        private static float LongestSide(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return 0f;
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        }

        private static string Path(Transform t)
        {
            string s = t.name;
            for (var p = t.parent; p != null; p = p.parent) s = p.name + "/" + s;
            return s;
        }
    }
}
