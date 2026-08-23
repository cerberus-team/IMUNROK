using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// 조사청 마무리 셋. 메뉴: [이문록 ▸ 조사청 ▸ 마무리(안개·솎기·돋보기)]
    ///
    /// <b>① 먼 데는 안개로 덮는다</b>: 들판이 190m 짜리 판 한 장이라 가장자리가 하늘과
    /// 맞닿는 곧은 선으로 보인다. 밤인데 지평선만 또렷한 것이 어색하고, 무엇보다
    /// 먼 데까지 다 보이니 <b>멀리 심은 것을 지울 수가 없다</b>. 안개를 깔면 먼 데가
    /// 흐려지고, 흐려지면 그 안의 것은 없어도 된다.
    ///
    /// <b>② 안개가 덮는 것은 솎아 낸다</b>: <see cref="PruneBeyond"/> m 너머의 나무·꽃·
    /// 풀을 걷는다. 보이지도 않는 것을 그리는 값이 아깝다. 바위는 남긴다 — 낮고
    /// 어두워 안개에 먼저 묻히므로 솎아도 아낄 것이 적고, 지평선 언저리에 뭔가
    /// 있어야 판이 비어 보이지 않는다.
    ///
    /// <b>③ 조사청에도 돋보기 소품을 세운다</b>: 이 씬에는 손에 드는 돋보기 모델이
    /// 없었다. 그래서 <see cref="MagnifierLens"/> 가 소품을 못 찾고 <b>제가 원판과 테와
    /// 자루를 빚어</b> 쓴다 — 눈앞에 허연 돋보기 비슷한 것이 떠 있던 까닭이 이것이다.
    /// 옹고집 껍데기 씬과 같은 자세로 소품을 달아 준다.
    ///
    /// ★플레이를 멈추고 실행할 것. 실행 중에는 씬을 고칠 수 없다.
    /// </summary>
    public static class HubPolish
    {
        private const string RoomName = "조사청_실내";
        private const string FieldName = "조사청_들판";
        private const string FbxPath = "Assets/_Project/Art/Tools/Magnifier/Magnifier_Tassel.fbx";

        /// <summary>이 너머의 초목은 걷는다(m).</summary>
        private const float PruneBeyond = 58f;

        // 안개 — 밤이라 하늘빛에 가깝게. 너무 밝으면 들판이 우유가 된다.
        private static readonly Color FogColor = new Color(0.085f, 0.105f, 0.145f);
        private const float FogStart = 26f;
        private const float FogEnd = 92f;

        [MenuItem("이문록/조사청/마무리(안개·솎기·돋보기)")]
        private static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[마무리] 플레이를 멈추고 다시 실행하세요.");
                return;
            }

            var room = GameObject.Find(RoomName);
            if (room == null) { Debug.LogError("[마무리] " + RoomName + " 을 못 찾았습니다."); return; }

            var log = new StringBuilder();
            Fog(log);
            Prune(room.transform.position, log);
            Magnifier(log);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(room.scene);
            Debug.Log("[마무리]\n" + log);
        }

        private static void Fog(StringBuilder log)
        {
            // RenderSettings 는 씬에 딸린 값이라 되돌리기가 안 걸린다. 마음에 안 들면
            // Lighting 창에서 Fog 를 끄면 된다.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogStartDistance = FogStart;
            RenderSettings.fogEndDistance = FogEnd;
            log.AppendLine("   안개: " + FogStart + "m 부터 " + FogEnd + "m 까지 · 빛깔 " + FogColor.ToString("F3"));
        }

        /// <summary>안개 너머의 초목을 걷는다. 바위는 남긴다.</summary>
        private static void Prune(Vector3 center, StringBuilder log)
        {
            var field = GameObject.Find(FieldName);
            if (field == null) { log.AppendLine("   ✘ 들판이 없어 솎지 못했습니다."); return; }

            center.y = 0f;
            int cut = 0;
            long saved = 0;
            foreach (var group in new[] { "나무", "꽃", "풀" })
            {
                var g = field.transform.Find(group);
                if (g == null) continue;

                var doomed = new List<GameObject>();
                foreach (Transform t in g)
                {
                    var p = t.position; p.y = 0f;
                    if (Vector3.Distance(p, center) <= PruneBeyond) continue;
                    doomed.Add(t.gameObject);
                    foreach (var mf in t.GetComponentsInChildren<MeshFilter>(true))
                    {
                        var m = mf.sharedMesh; if (m == null) continue;
                        for (int s = 0; s < m.subMeshCount; s++) saved += (long)m.GetIndexCount(s) / 3;
                    }
                }
                foreach (var d in doomed) Undo.DestroyObjectImmediate(d);
                cut += doomed.Count;
                log.AppendLine("   " + group + " 에서 " + doomed.Count + "포기를 걷었습니다");
            }
            log.AppendLine("   솎아 낸 것 " + cut + "포기 · " + saved.ToString("N0") + " 삼각형");
        }

        /// <summary>
        /// 손에 드는 돋보기 소품. 옹고집 껍데기 씬과 같은 자세로 단다 —
        /// 두 씬에서 같은 물건이 같은 각도로 손에 잡혀야 한 게임으로 느껴진다.
        /// </summary>
        private static void Magnifier(StringBuilder log)
        {
            var cam = Camera.main;
            if (cam == null) { log.AppendLine("   ✘ 메인 카메라가 없어 소품을 못 달았습니다."); return; }

            foreach (var h in cam.GetComponentsInChildren<HeldToolModel>(true))
                if (h.ToolId == "magnify") { log.AppendLine("   돋보기 소품은 이미 있습니다: " + h.name); return; }

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbx == null) { log.AppendLine("   ✘ " + FbxPath + " 를 못 찾았습니다(아트는 공유폴더에 있습니다)."); return; }

            var holder = new GameObject("_돋보기");
            Undo.RegisterCreatedObjectUndo(holder, "조사청 돋보기 소품");
            holder.transform.SetParent(cam.transform, false);
            holder.transform.localPosition = new Vector3(0.220f, -0.200f, 0.320f);
            holder.transform.localRotation = Quaternion.Euler(315f, 0f, 90f);

            var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, holder.transform);
            model.name = "돋보기_술_모델";
            model.transform.localPosition = Vector3.zero;
            // FBX 축을 세우는 회전. 옹고집 씬에 박혀 있는 값을 그대로 옮긴 것이다.
            model.transform.localRotation = new Quaternion(0.5f, 0.5f, 0.5f, -0.5f);
            model.transform.localScale = Vector3.one * 0.933f;

            var held = Undo.AddComponent<HeldToolModel>(holder);
            var so = new SerializedObject(held);
            so.FindProperty("_toolId").stringValue = "magnify";
            so.FindProperty("_model").objectReferenceValue = model;
            so.ApplyModifiedProperties();

            log.AppendLine("   돋보기 소품을 세웠습니다 — 이제 흰 원판 대신 놋쇠 돋보기가 잡힙니다");
        }
    }
}
