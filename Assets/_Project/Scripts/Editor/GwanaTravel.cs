using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using IMUNROK.Common;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>관아 안을 지도로 오간다.</b> 메뉴: [이문록 ▸ 관아 ▸ ⑬ 지도로 오가게]
    ///
    /// 관아 담 안이 <b>37 × 27m</b> 다. 대문에서 동헌까지 스물세 걸음, 문서고에서
    /// 동헌까지 스무 걸음인데 그 사이에 아무것도 없다. 한 번 걸으면 넓은 관아지만,
    /// 대장을 읽고 동헌으로 갔다가 대조하러 서고로 돌아오는 일을 네 번 하면 그건
    /// 그냥 <b>빈 마당을 여덟 번 가로지르는 것</b>이다.
    ///
    /// <b>마당을 좁히지 않는 까닭</b>: 담을 안으로 밀면 132 조각을 다 옮겨야 하고,
    /// 무엇보다 관아는 원래 넓다 — 좁히면 관아처럼 안 보인다. 넓이는 두고 오가는
    /// 수고만 덜어 낸다.
    ///
    /// <b>세 군데</b>를 둔다 — 대문 앞(마당) · 문서고 · 동헌 대청.
    /// 지금 서 있는 데는 흐리게 뜨고 눌리지 않는다.
    ///
    /// <b>처음 한 번은 걸어야 한다.</b> 잠겨 있다가 <b>서리가 길을 다 일러 준 뒤에</b>
    /// 풀린다. 어디가 어디인지 모르는 채로 이름만 눌러 건너뛰면 관아가 세 칸짜리
    /// 메뉴가 된다. 한 번 걸어 본 데라야 지도가 지도다.
    ///
    /// 두 번 눌러도 두 벌이 안 놓인다.
    /// </summary>
    public static class GwanaTravel
    {
        // 셋 다 <b>발이 닿는 바닥 자리</b>다(눈높이는 판이 얹는다).
        // 문서고는 궤가 놓인 안쪽을 보게, 동헌은 교의 옆에 서게 잡았다.
        private static readonly (string 이름, Vector3 pos, float yaw, float 여기)[] Stops =
        {
            ("마당(대문)", new Vector3(-13.50f, 0.00f,  0.00f),  90f, 7f),
            ("문서고",     new Vector3( -1.50f, 0.82f, -6.20f), 180f, 8f),
            ("동헌 대청",  new Vector3( 14.10f, 2.21f, -1.60f), 250f, 6f),
        };

        [MenuItem("이문록/관아/⑬ 지도로 오가게")]
        public static void Run()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.name.Contains("Gwana"))
            {
                Debug.LogWarning("[관아] 관아 씬을 열고 누르십시오(지금은 " + scene.name + ").");
                return;
            }

            var log = new System.Text.StringBuilder("[관아] 지도로 오가게\n");

            var go = Find(scene, "_오갈데");
            if (go == null)
            {
                go = new GameObject("_오갈데");
                SceneManager.MoveGameObjectToScene(go, scene);
                Undo.RegisterCreatedObjectUndo(go, "오갈 데");
                log.AppendLine("  · 오갈 데 판을 놓았다");
            }
            var board = go.GetComponent<TravelBoard>();
            if (board == null) board = Undo.AddComponent<TravelBoard>(go);

            // 자리 표식을 놓고 딛히는지 재 본다
            Physics.SyncTransforms();
            var spots = new Transform[Stops.Length];
            for (int i = 0; i < Stops.Length; i++)
            {
                var s = Stops[i];
                var mark = Find(scene, "갈곳_" + s.이름);
                if (mark == null)
                {
                    mark = new GameObject("갈곳_" + s.이름);
                    SceneManager.MoveGameObjectToScene(mark, scene);
                    Undo.RegisterCreatedObjectUndo(mark, "오갈 데");
                }
                mark.transform.position = s.pos;
                mark.transform.rotation = Quaternion.Euler(0f, s.yaw, 0f);
                spots[i] = mark.transform;

                RaycastHit h;
                bool ok = Physics.Raycast(s.pos + Vector3.up * 1.5f, Vector3.down, out h, 4f, ~0, QueryTriggerInteraction.Ignore);
                log.AppendLine("  · " + s.이름.PadRight(10) + s.pos.ToString("F2")
                             + (ok ? "   딛는 것: " + h.collider.name + " y=" + h.point.y.ToString("F2")
                                     + (Mathf.Abs(h.point.y - s.pos.y) > 0.12f ? "   ※ 표식과 " + (h.point.y - s.pos.y).ToString("F2") + " 어긋남" : "")
                                   : "   ※ 밑에 딛을 것이 없다"));
            }

            var so = new SerializedObject(board);
            var list = so.FindProperty("_stops");
            list.arraySize = Stops.Length;
            for (int i = 0; i < Stops.Length; i++)
            {
                var e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("이름").stringValue = Stops[i].이름;
                e.FindPropertyRelative("자리").objectReferenceValue = spots[i];
                e.FindPropertyRelative("여기").floatValue = Stops[i].여기;
            }
            so.FindProperty("_openAtStart").boolValue = false;   // 서리가 풀어 준다
            var key = so.FindProperty("_key");
            int m = System.Array.IndexOf(key.enumNames, "M");
            if (m >= 0) key.enumValueIndex = m;
            so.ApplyModifiedPropertiesWithoutUndo();

            Usher(scene, board, log);
            Map(scene, log);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// 서리가 인사를 마치면 지도가 풀리게 잇는다.
        ///
        /// 이 신호를 서리에게 맡기는 것은 <b>말이 되기 때문</b>이다 — 길을 일러 준
        /// 사람이 곧 길을 열어 준 사람이다. 따로 조건을 두면 언제 풀렸는지 모른다.
        /// </summary>
        private static void Usher(Scene scene, TravelBoard board, System.Text.StringBuilder log)
        {
            var usher = Object.FindFirstObjectByType<GwanaUsher>();
            if (usher == null) { log.AppendLine("  ※ 서리가 없다 — ⑨ 를 먼저 누르십시오"); return; }

            var so = new SerializedObject(usher);
            var ev = so.FindProperty("_onDone");
            var calls = ev.FindPropertyRelative("m_PersistentCalls.m_Calls");

            // 이미 걸려 있으면 두 벌로 걸지 않는다
            for (int i = 0; i < calls.arraySize; i++)
            {
                var c = calls.GetArrayElementAtIndex(i);
                if (c.FindPropertyRelative("m_Target").objectReferenceValue == board)
                { log.AppendLine("  · 서리 → 지도 풀기는 이미 걸려 있다"); return; }
            }

            calls.arraySize++;
            var call = calls.GetArrayElementAtIndex(calls.arraySize - 1);
            call.FindPropertyRelative("m_Target").objectReferenceValue = board;
            call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue =
                typeof(TravelBoard).AssemblyQualifiedName;
            call.FindPropertyRelative("m_MethodName").stringValue = "Unlock";
            call.FindPropertyRelative("m_Mode").enumValueIndex = 1;   // Void
            call.FindPropertyRelative("m_CallState").enumValueIndex = 2;   // RuntimeOnly
            so.ApplyModifiedPropertiesWithoutUndo();
            log.AppendLine("  · 서리가 말을 마치면 지도가 풀리게 이었다");
        }

        /// <summary>
        /// 옛 지도판(MapView)은 뗀다.
        ///
        /// 그림 한 장을 띄우기만 하는 것이라, 같은 M 키에 둘이 물려 있으면 눌렀을 때
        /// 그림과 판이 겹쳐 뜬다. 관아에서는 오갈 데 판이 그 자리를 대신한다.
        /// </summary>
        private static void Map(Scene scene, System.Text.StringBuilder log)
        {
            var map = Object.FindFirstObjectByType<MapView>();
            if (map == null || map.gameObject.scene != scene) return;
            Undo.DestroyObjectImmediate(map);
            log.AppendLine("  · 옛 지도판(MapView)을 뗐다 — 같은 M 키에 둘이 물려 있었다");
        }

        private static GameObject Find(Scene scene, string name)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t.gameObject;
            return null;
        }
    }
}
