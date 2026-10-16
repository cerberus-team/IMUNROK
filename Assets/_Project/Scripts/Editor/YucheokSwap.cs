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

                    // <b>이름으로 가리면 안 된다.</b> obj 와 fbx 는 같은 파일 이름에서 나와
                    // 씬에 선 뿌리 이름이 둘 다 「유척」이다 — 이름만 보면 옛 몸이 박혀
                    // 있는데도 「이미 새 것」으로 건너뛰어, 도구를 눌러도 아무 일이 없다.
                    // 어느 <b>파일</b>에서 나온 것인지를 본다.
                    var origin = PrefabUtility.GetCorrespondingObjectFromSource(old);
                    if (origin != null && AssetDatabase.GetAssetPath(origin) == NewModelPath)
                    { skipped++; continue; }   // 이미 갈아 끼웠다

                    // 옛 몸의 자리와 크기를 받아 둔다
                    var t = old.transform;
                    Vector3 lp = t.localPosition;
                    Quaternion lr = t.localRotation;
                    bool wasOn = old.activeSelf;
                    float oldLongest = LongestSide(old);
                    var oldBox = LocalBox(old, held.transform);
                    Vector3 oldCenter = oldBox.center;
                    int oldAxis = LongAxis(oldBox.size);

                    // 새 몸을 같은 껍데기 밑에 앉힌다
                    var fresh = (GameObject)PrefabUtility.InstantiatePrefab(src, held.transform);
                    Undo.RegisterCreatedObjectUndo(fresh, "유척 갈아 끼우기");
                    fresh.transform.SetLocalPositionAndRotation(lp, lr);

                    // <b>제 배율을 지우지 않는다.</b> 여기서 Vector3.one 으로 깎았더니
                    // 모델이 제 뿌리에 지니고 온 100배가 함께 날아갔다 — 자가 3mm 가 된다.
                    // 프리팹이 들고 온 크기에서 <b>모자라거나 넘치는 만큼만</b> 곱한다.
                    float newLongest = LongestSide(fresh);
                    if (oldLongest > 0.0001f && newLongest > 0.0001f)
                        fresh.transform.localScale *= (oldLongest / newLongest);

                    // <b>누운 축을 맞춘다.</b> 옛 몸과 새 몸이 같은 축으로 길다는 보장이
                    // 없다 — Meshy 것은 X 로 길고 옛 obj 는 Z 로 길었다. 그대로 두면
                    // 자를 가로로 쥔 꼴이 된다. 긴 축이 같은 쪽을 보게 돌린다.
                    AlignLongAxis(fresh, oldAxis);

                    // <b>꼭지 자리도 맞춘다.</b> 옛 obj 는 한쪽 끝이 원점이고 새 fbx 는
                    // 가운데가 원점이라, 돌리기만 하면 자가 반 자루만큼 밀려 난다.
                    // 옛 몸이 차지하던 상자의 한가운데로 민다.
                    var box = LocalBox(fresh, held.transform);
                    if (box.size.sqrMagnitude > 0f)
                        fresh.transform.localPosition += (oldCenter - box.center);

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

        /// <summary>
        /// 그려지는 몸의 가장 긴 변.
        ///
        /// <b>월드 AABB 를 쓰면 안 된다.</b> 유척은 껍데기가 (344, 332, 4)도로 비스듬히
        /// 뉘어 있어서, 돌아간 막대를 감싸는 상자는 막대보다 <b>짧다</b> — 0.310m 짜리
        /// 자가 0.282m 로 읽혔다. 그 값으로 새 몸을 맞추면 자가 9% 짧아진다.
        /// 메시 자체를 재고 배율만 곱한다.
        /// </summary>
        private static float LongestSide(GameObject go)
        {
            float best = 0f;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                Vector3 s = mf.sharedMesh.bounds.size, l = mf.transform.lossyScale;
                best = Mathf.Max(best, Mathf.Max(s.x * Mathf.Abs(l.x),
                                 Mathf.Max(s.y * Mathf.Abs(l.y), s.z * Mathf.Abs(l.z))));
            }
            return best;
        }

        /// <summary>몸이 <paramref name="frame"/> 자리에서 차지하는 상자.</summary>
        private static Bounds LocalBox(GameObject go, Transform frame)
        {
            bool first = true;
            var acc = new Bounds();
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                var toFrame = frame.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                for (int c = 0; c < 8; c++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents,
                        new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                    var p = toFrame.MultiplyPoint3x4(corner);
                    if (first) { acc = new Bounds(p, Vector3.zero); first = false; }
                    else acc.Encapsulate(p);
                }
            }
            return acc;
        }

        /// <summary>가장 긴 축 — 0=x, 1=y, 2=z.</summary>
        private static int LongAxis(Vector3 size)
        {
            if (size.x >= size.y && size.x >= size.z) return 0;
            return size.y >= size.z ? 1 : 2;
        }

        /// <summary>긴 축이 <paramref name="wantAxis"/> 를 보도록 90도 돌린다.</summary>
        private static void AlignLongAxis(GameObject go, int wantAxis)
        {
            var t = go.transform;
            for (int tries = 0; tries < 3; tries++)
            {
                int have = LongAxis(LocalBox(go, t.parent).size);
                if (have == wantAxis) return;
                if (have == 0 && wantAxis == 2) { t.localRotation *= Quaternion.Euler(0f, -90f, 0f); continue; }
                if (have == 2 && wantAxis == 0) { t.localRotation *= Quaternion.Euler(0f, 90f, 0f); continue; }
                if (have == 0 && wantAxis == 1) { t.localRotation *= Quaternion.Euler(0f, 0f, 90f); continue; }
                if (have == 1 && wantAxis == 0) { t.localRotation *= Quaternion.Euler(0f, 0f, -90f); continue; }
                if (have == 1 && wantAxis == 2) { t.localRotation *= Quaternion.Euler(90f, 0f, 0f); continue; }
                if (have == 2 && wantAxis == 1) { t.localRotation *= Quaternion.Euler(-90f, 0f, 0f); continue; }
                return;
            }
        }

        private static string Path(Transform t)
        {
            string s = t.name;
            for (var p = t.parent; p != null; p = p.parent) s = p.name + "/" + s;
            return s;
        }
    }
}
