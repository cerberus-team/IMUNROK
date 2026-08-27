using UnityEditor;
using UnityEngine;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// 문을 <b>눈으로 보면서</b> 맞추는 인스펙터.
    ///
    /// 문이 비뚤게 열리는 까닭은 거의 늘 셋 중 하나인데, 인스펙터의 숫자만 봐서는
    /// 어느 것인지 알 수 없다:
    ///
    ///   ① <b>경첩이 문짝 한가운데에 있다</b> → 문이 열리는 게 아니라 제자리에서 돈다.
    ///      경첩은 문짝의 <i>바깥 모서리</i>에 있어야 한다.
    ///   ② <b>각도의 부호가 같다</b> → 두 짝이 같은 쪽으로 돌아 서로를 지나간다.
    ///      마주 여는 두 짝은 부호가 반대여야 한다(+85 / −85).
    ///   ③ <b>지금 자세가 이미 열린 자세다</b> → 그것을 '닫힘'으로 삼으니 열 때마다
    ///      그만큼 더 돌아간다. 사랑방문_0 이 90도에서 175도까지 밀려났던 것이 이 경우다.
    ///
    /// 그래서 이 인스펙터는 <b>지금 씬에서 문을 여닫아 보고</b>, 잘못된 것을 그 자리에서
    /// 고치는 단추를 준다. 재생을 켤 필요가 없다.
    ///
    /// 쓰는 차례:
    ///   1. 문 오브젝트를 고른다 → 씬 뷰에 경첩이 노란 점, 문짝 한가운데가 파란 점으로 보인다.
    ///   2. [열어보기] 를 눌러 어떻게 열리는지 본다. 이상하면 [닫아보기] 로 되돌린다.
    ///   3. 제자리에서 돌면 → 그 짝의 [경첩 ← 왼쪽] 또는 [오른쪽 →] 를 눌러 경첩을 모서리로 옮긴다.
    ///   4. 반대로 열리면 → [방향 뒤집기].
    ///   5. 열린 채로 굳었으면 → [경첩 각도 0으로] 로 반듯하게 세우고 다시 맞춘다.
    /// </summary>
    [CustomEditor(typeof(DoorController))]
    public class DoorControllerEditor : UnityEditor.Editor
    {
        private bool _previewOpen;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var door = (DoorController)target;
            var leaves = serializedObject.FindProperty("_leaves");

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("문 맞추기", EditorStyles.boldLabel);

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("재생 중입니다. 여기서 고친 것은 재생을 끄면 사라집니다.",
                                        MessageType.Warning);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(_previewOpen ? "닫아보기" : "열어보기", GUILayout.Height(28)))
                    Preview(door, leaves, !_previewOpen);
                if (GUILayout.Button("경첩 각도 0으로", GUILayout.Height(28)))
                    Straighten(door, leaves);
            }
            EditorGUILayout.LabelField(_previewOpen ? "지금 '열린' 자세로 보고 있습니다" : "지금 '닫힌' 자세입니다",
                                       EditorStyles.miniLabel);

            EditorGUILayout.Space(6);
            for (int i = 0; i < leaves.arraySize; i++)
            {
                var el = leaves.GetArrayElementAtIndex(i);
                var pivot = el.FindPropertyRelative("pivot").objectReferenceValue as Transform;
                var angle = el.FindPropertyRelative("swingAngle");

                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.LabelField($"{i}번 짝 — {(pivot == null ? "경첩 없음" : pivot.name)}   ({angle.floatValue:0}도)",
                                               EditorStyles.miniBoldLabel);
                    if (pivot == null) continue;

                    var leaf = FirstRenderer(pivot);
                    if (leaf == null)
                    {
                        EditorGUILayout.LabelField("문짝(렌더러)을 못 찾음 — 경첩 밑에 문짝이 들어 있어야 합니다",
                                                   EditorStyles.miniLabel);
                        continue;
                    }

                    float off = HingeOffset(pivot, leaf);
                    EditorGUILayout.LabelField(
                        off < 0.03f
                            ? "⚠ 경첩이 문짝 한가운데에 있습니다 — 이대로면 제자리에서 돕니다"
                            : $"경첩이 한가운데에서 {off:0.00}m 비껴 있습니다 (문짝 폭의 절반쯤이면 알맞습니다)",
                        EditorStyles.miniLabel);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("경첩 ← 왼쪽 모서리")) MoveHinge(pivot, leaf, -1);
                        if (GUILayout.Button("오른쪽 모서리 →")) MoveHinge(pivot, leaf, +1);
                        if (GUILayout.Button("방향 뒤집기", GUILayout.Width(90)))
                        {
                            if (_previewOpen) Preview(door, leaves, false);
                            angle.floatValue = -angle.floatValue;
                            serializedObject.ApplyModifiedProperties();
                        }
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        // ── 미리보기 ──

        /// <summary>
        /// 지금 자세를 '닫힘'으로 삼고 각도만큼 돌려 본다. 되돌릴 때 같은 만큼 되돌리므로
        /// 자세는 보존된다 — 미리보기가 저장돼 열린 채로 굳는 사고를 막는다.
        /// </summary>
        private void Preview(DoorController door, SerializedProperty leaves, bool open)
        {
            for (int i = 0; i < leaves.arraySize; i++)
            {
                var el = leaves.GetArrayElementAtIndex(i);
                var pivot = el.FindPropertyRelative("pivot").objectReferenceValue as Transform;
                if (pivot == null) continue;
                float a = el.FindPropertyRelative("swingAngle").floatValue;
                Undo.RecordObject(pivot, "문 미리보기");
                pivot.localRotation = pivot.localRotation * Quaternion.Euler(0f, open ? a : -a, 0f);
                EditorUtility.SetDirty(pivot);
            }
            _previewOpen = open;
            SceneView.RepaintAll();
        }

        /// <summary>경첩을 반듯하게 세운다(열린 자세가 닫힘으로 굳은 것을 푸는 단추).</summary>
        private void Straighten(DoorController door, SerializedProperty leaves)
        {
            for (int i = 0; i < leaves.arraySize; i++)
            {
                var pivot = leaves.GetArrayElementAtIndex(i).FindPropertyRelative("pivot").objectReferenceValue as Transform;
                if (pivot == null) continue;
                Undo.RecordObject(pivot, "경첩 반듯하게");
                pivot.localRotation = Quaternion.identity;
                EditorUtility.SetDirty(pivot);
            }
            _previewOpen = false;
            SceneView.RepaintAll();
        }

        // ── 경첩 옮기기 ──

        /// <summary>
        /// 경첩을 문짝의 한쪽 모서리로 옮긴다. 문짝은 <b>제자리에 그대로 둔다</b> —
        /// 경첩만 옮기고 문짝의 로컬 자리를 그만큼 반대로 밀어 상쇄한다.
        /// </summary>
        private void MoveHinge(Transform pivot, Renderer leaf, int side)
        {
            var b = leaf.bounds;
            bool alongX = b.size.x >= b.size.z;      // 문짝이 어느 축을 따라 넓은가
            float half = (alongX ? b.size.x : b.size.z) * 0.5f;

            Vector3 target = b.center;
            if (alongX) target.x += side * half; else target.z += side * half;
            target.y = pivot.position.y;             // 높이는 건드리지 않는다

            Vector3 delta = target - pivot.position;
            if (delta.sqrMagnitude < 1e-8f) return;

            Undo.RecordObject(pivot, "경첩 옮기기");
            var kids = new Transform[pivot.childCount];
            for (int i = 0; i < kids.Length; i++) kids[i] = pivot.GetChild(i);
            foreach (var k in kids) Undo.RecordObject(k, "경첩 옮기기");

            // 자식들의 월드 자리를 기억했다가 그대로 되돌린다
            var keep = new Vector3[kids.Length];
            var keepRot = new Quaternion[kids.Length];
            for (int i = 0; i < kids.Length; i++) { keep[i] = kids[i].position; keepRot[i] = kids[i].rotation; }

            pivot.position = target;
            for (int i = 0; i < kids.Length; i++) { kids[i].position = keep[i]; kids[i].rotation = keepRot[i]; }

            EditorUtility.SetDirty(pivot);
            SceneView.RepaintAll();
        }

        private static Renderer FirstRenderer(Transform pivot)
        {
            foreach (var r in pivot.GetComponentsInChildren<Renderer>(true))
                if (r.enabled) return r;
            return null;
        }

        private static float HingeOffset(Transform pivot, Renderer leaf)
        {
            var c = leaf.bounds.center;
            var p = pivot.position;
            return Vector2.Distance(new Vector2(c.x, c.z), new Vector2(p.x, p.z));
        }

        // ── 씬 뷰 ──

        private void OnSceneGUI()
        {
            var leaves = serializedObject.FindProperty("_leaves");
            for (int i = 0; i < leaves.arraySize; i++)
            {
                var el = leaves.GetArrayElementAtIndex(i);
                var pivot = el.FindPropertyRelative("pivot").objectReferenceValue as Transform;
                if (pivot == null) continue;
                float a = el.FindPropertyRelative("swingAngle").floatValue;

                Handles.color = Color.yellow;
                Handles.SphereHandleCap(0, pivot.position, Quaternion.identity, 0.06f, EventType.Repaint);
                Handles.Label(pivot.position + Vector3.up * 0.12f, $"경첩 {i} ({a:0}도)");

                var leaf = FirstRenderer(pivot);
                if (leaf == null) continue;

                Handles.color = new Color(0.3f, 0.6f, 1f);
                Handles.SphereHandleCap(0, leaf.bounds.center, Quaternion.identity, 0.05f, EventType.Repaint);
                Handles.DrawDottedLine(pivot.position, leaf.bounds.center, 3f);

                // 열리는 쪽을 부채꼴로 그린다 — 부호가 같은 두 짝은 여기서 바로 보인다
                Vector3 arm = leaf.bounds.center - pivot.position;
                arm.y = 0f;
                if (arm.sqrMagnitude < 1e-6f) continue;
                Handles.color = new Color(1f, 0.55f, 0.1f, 0.25f);
                Handles.DrawSolidArc(pivot.position, Vector3.up, arm.normalized, a, arm.magnitude);
            }
        }
    }
}
