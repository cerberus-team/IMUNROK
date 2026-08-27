using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using IMUNROK.Common;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// 사랑방 창호를 <b>닫아 세우고 다시 짠다</b>. 메뉴: [이문록 ▸ 사랑방 창호 다시 짜기]
    ///
    /// 왜 따로 있나 — 창호가 두 가지로 어긋나 있었다.
    ///
    /// ① <b>경첩이 문짝 한가운데에 박혔다.</b> 문짝은 원점이 제 한가운데가 아니라 한쪽으로
    ///    반 장 치우쳐 있는데, 경첩 자리를 변환 위치에서 재는 바람에 한쪽 무리는 바깥
    ///    모서리에 제대로 걸리고 반대쪽 무리는 제 한가운데에 걸렸다. 그 문짝들은 열어도
    ///    자리가 그대로고 몸만 도는 회전문이 되었다.
    ///
    /// ② <b>열린 자세가 닫힘으로 굳었다.</b> 창호를 다시 짤 때 지금 서 있는 자세를 그대로
    ///    닫힌 자세로 삼았다. 문 하나가 열린 채로 저장돼 있으면 그 열린 자세가 닫힘이 되고,
    ///    다음에 또 열면 그만큼 더 돌아간다. 사랑방문_0 이 그렇게 90도 자리에서 175도까지
    ///    밀려나 있었다.
    ///
    /// 그래서 이 도구는 <b>먼저 닫는다</b>. 닫혔는지는 눈이 아니라 자로 안다 — 창호는 벽에
    /// 붙어 있을 때 벽을 따라 넓고 벽을 가로질러 얇다. 그 반대면 열린 것이므로 되돌린다.
    /// </summary>
    public static class SarangbangDoorRepair
    {
        private const string PrefabPath = "Assets/_Project/Onggojip/Prefabs/사랑채_실내.prefab";

        [MenuItem("이문록/사랑방 창호 다시 짜기")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[창호] 재생 중입니다 — 재생을 끄고 다시 누르십시오.");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null) { Debug.LogError("[창호] 실내 프리팹을 못 열었습니다."); return; }

            var log = new System.Text.StringBuilder();
            try
            {
                Transform doorGroup = null;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == "문") { doorGroup = t; break; }
                if (doorGroup == null) { Debug.LogWarning("[창호] 문 묶음을 못 찾음"); return; }

                int closed = Unbuild(doorGroup, log);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                log.Append("문짝 ").Append(closed).Append("장을 닫아 제자리에 세웠습니다. ")
                   .Append("이제 [사랑채 실내 정리] 를 누르면 경첩이 바깥 모서리에 다시 걸립니다.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            Debug.Log("[창호] " + log);
        }

        /// <summary>짜 놓은 창호를 풀되, 문짝은 <b>닫힌 자세로</b> 되돌려 내놓는다.</summary>
        private static int Unbuild(Transform group, System.Text.StringBuilder log)
        {
            var holders = new List<Transform>();
            foreach (Transform c in group)
                if (c.name.StartsWith("사랑방문_")) holders.Add(c);

            int n = 0;
            foreach (var holder in holders)
            {
                var dc = holder.GetComponent<DoorController>();
                var angles = ReadAngles(dc);

                int i = 0;
                var hinges = new List<Transform>();
                foreach (Transform h in holder) hinges.Add(h);

                foreach (var hinge in hinges)
                {
                    if (hinge.childCount == 0) { i++; continue; }
                    var leaf = hinge.GetChild(0);
                    float angle = i < angles.Count ? angles[i] : 85f;

                    // 벽을 따라 넓고 가로질러 얇아야 닫힌 것이다. 반대면 되돌린다.
                    if (IsOpen(leaf))
                    {
                        hinge.localRotation = hinge.localRotation * Quaternion.Euler(0f, -angle, 0f);
                        if (IsOpen(leaf))      // 반대로 돌린 것이면 두 배로 되돌린다
                            hinge.localRotation = hinge.localRotation * Quaternion.Euler(0f, 2f * angle, 0f);
                        log.Append("  닫음: ").Append(leaf.name).Append("\n");
                    }
                    n++;
                    i++;
                }

                // 닫힌 자세 그대로 꺼낸다
                foreach (var hinge in hinges)
                    while (hinge.childCount > 0) hinge.GetChild(0).SetParent(group, true);
                Object.DestroyImmediate(holder.gameObject);
            }
            return n;
        }

        /// <summary>
        /// 열려 있나. 창호는 벽(z = −12.75)을 따라 늘어서므로, 닫혔으면 X 로 넓고 Z 로 얇다.
        /// </summary>
        private static bool IsOpen(Transform leaf)
        {
            var r = leaf.GetComponent<Renderer>();
            if (r == null) return false;
            var s = r.bounds.size;
            return s.z > s.x;
        }

        private static List<float> ReadAngles(DoorController dc)
        {
            var list = new List<float>();
            if (dc == null) return list;
            var so = new SerializedObject(dc);
            var arr = so.FindProperty("_leaves");
            for (int i = 0; i < arr.arraySize; i++)
                list.Add(arr.GetArrayElementAtIndex(i).FindPropertyRelative("swingAngle").floatValue);
            return list;
        }
    }
}
