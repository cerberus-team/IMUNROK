using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// 관아 마당을 좁히고 서고를 세운다. 메뉴: [이문록 ▸ 관아 ▸ ⑤ …]
    ///
    /// 왜 좁히나: 전달본 담이 두르는 안쪽이 37.4 x 34.0m 였다. 외삼문에서 동헌까지
    /// 22.6m 는 관아로 그럴듯하지만 <b>너비 34m</b>가 마당을 운동장으로 만든다.
    /// 여기서는 너비만 26m 로 줄인다(양옆 담을 z=±13 으로). 깊이는 건드리지 않는다 —
    /// 외삼문과 동헌은 전달본이 재어 둔 자리이고, 그 사이를 좁히면 두 채가 서로 밟는다.
    ///
    /// 서고는 <b>여기서 놓지 않는다</b>. 화성행궁 서리청을 세워 봤더니 492,430 삼각형이라
    /// 동헌의 여덟 배였다. 상자로 지은 문서고를 <see cref="GwanaSeogo"/> 가 대신 세운다.
    ///
    /// 왜 전달본 담을 끄고 새로 두르나: 전달본 담은 통짜 한 장이다(Wall_E 가 37.87m).
    /// 짧게 만들려면 배율을 줄여야 하는데 그러면 벽돌 무늬가 눌린다. 화성행궁 담장은
    /// 4.26m·2.16m 짜리 낱장이라 아무 길이나 이어 붙일 수 있다. 외삼문과 문지기는 그대로 둔다.
    ///
    /// <b>삼각형 값</b>: 담장 낱장 하나가 LOD0 에서 6,036 이다. 한 바퀴 113m 를 두르면
    /// 스물일곱 장, 16만이 된다. 그래서 이 도구는 두른 뒤 <see cref="RetuneLod"/> 로
    /// LOD 전환 문턱을 손본다 — 원본 문턱이 0.02 라 60m 밖까지 LOD0 을 쓴다. 담은
    /// 높이 2.14m 짜리고 대개 5m 밖에서 보므로 그렇게까지 촘촘할 까닭이 없다.
    ///
    /// ★되돌리기(Ctrl+Z)가 듣는다.
    /// </summary>
    public static class GwanaYard
    {
        private const string HH = "Assets/HwaseongHaenggung/Prefabs";
        private const string YardName = "관아마당_좁힌담";

        // ── 마당 치수. 여기 숫자 넷이 전부다 ──

        /// <summary>양옆 담까지의 거리(m). 전달본은 17.8 이었다.</summary>
        private const float HalfZ = 13.0f;
        /// <summary>뒷담의 x. 동헌 뒷면이 19.1 이라 그보다 조금 뒤.</summary>
        private const float BackX = 21.0f;
        /// <summary>앞담의 x. 외삼문 안쪽면이 -14.9 다.</summary>
        private const float FrontX = -15.5f;
        /// <summary>앞담에서 비워 둘 문 자리(±m). 외삼문이 z ±6.1 을 차지한다.</summary>
        private const float GateGap = 6.4f;

        private const float Tile1 = 4.26f;   // 긴 낱장
        private const float Tile2 = 2.16f;   // 짧은 낱장 — 자투리를 메운다

        // ── ⑤ ──

        [MenuItem("이문록/관아/⑤ 마당 좁히고 서고 놓기")]
        public static void Build()
        {
            var set = GameObject.Find("관아세트");
            if (set == null) { Debug.LogWarning("[관아] 먼저 ① 세트 놓기 를 하십시오."); return; }

            var t1 = AssetDatabase.LoadAssetAtPath<GameObject>(HH + "/Parts/SM_StraightStronewall_1.prefab");
            var t2 = AssetDatabase.LoadAssetAtPath<GameObject>(HH + "/Parts/SM_StraightStronewall_2.prefab");
            if (t1 == null || t2 == null)
            {
                Debug.LogError("[관아] 화성행궁 담장 프리팹을 못 찾았습니다. " + HH + "/Parts 밑을 보십시오.");
                return;
            }

            var sb = new StringBuilder();

            // 전달본 담을 접는다. 통짜라 길이를 못 줄이는 것들이다.
            // 외삼문(SM_Oisamun)과 문지기는 남긴다 — 그 둘은 자리가 맞다.
            int folded = 0;
            foreach (var t in set.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("Wall_")) continue;
                if (!t.gameObject.activeSelf) continue;
                Undo.RecordObject(t.gameObject, "전달본 담 접기");
                t.gameObject.SetActive(false);
                folded++;
            }
            sb.AppendLine("  전달본 담 " + folded + " 짝을 껐습니다(외삼문·문지기는 그대로).");

            var old = GameObject.Find(YardName);
            if (old != null) Undo.DestroyObjectImmediate(old);
            var root = new GameObject(YardName);
            Undo.RegisterCreatedObjectUndo(root, "마당 좁히기");

            // 한 바퀴. 낱장은 -Z 끝이 기준점이고 +Z 로 뻗으므로,
            // 뻗을 방향으로 돌려 놓고 시작점부터 채워 나간다.
            int tiles = 0;
            tiles += Run(root.transform, t1, t2, new Vector3(FrontX, 0f, -HalfZ), 90f, BackX - FrontX, "담_남");
            tiles += Run(root.transform, t1, t2, new Vector3(FrontX, 0f, HalfZ), 90f, BackX - FrontX, "담_북");
            tiles += Run(root.transform, t1, t2, new Vector3(BackX, 0f, -HalfZ), 0f, HalfZ * 2f, "담_뒤");
            tiles += Run(root.transform, t1, t2, new Vector3(FrontX, 0f, -HalfZ), 0f, HalfZ - GateGap, "담_앞남");
            tiles += Run(root.transform, t1, t2, new Vector3(FrontX, 0f, GateGap), 0f, HalfZ - GateGap, "담_앞북");
            sb.AppendLine("  담장 낱장 " + tiles + " 장으로 " + (HalfZ * 2f).ToString("F0") + " x "
                          + (BackX - FrontX).ToString("F0") + "m 를 둘렀습니다.");

            // 서고는 여기서 놓지 않는다. 화성행궁 서리청을 놓아 봤더니 492,430 삼각형이라
            // 동헌(62,408)의 여덟 배였고, 격자 뭉치기로는 162,177 에서 바닥이면서 모양이
            // 부서졌다. 그래서 ⑦ 이 상자로 짓는다(960 삼각형). 자세한 내력은 GwanaSeogo.cs.
            GwanaSeogo.Build();
            sb.AppendLine("  문서고는 ⑦ 로 상자로 지었습니다 — 서리청은 삼각형이 감당이 안 됐습니다.");

            RetuneLod(root, sb);

            // 마당 바닥을 새 넓이에 맞춘다. 유니티 Plane 은 배율 1 이 10m 다.
            var zone1 = GameObject.Find("Zone_1_관아문서고");
            var floor = zone1 != null ? zone1.transform.Find("Floor") : null;
            if (floor != null)
            {
                Undo.RecordObject(floor, "마당 바닥");
                floor.localScale = new Vector3((BackX - FrontX + 2f) / 10f, 1f, (HalfZ * 2f + 2f) / 10f);
                floor.position = new Vector3((FrontX + BackX) * 0.5f, floor.position.y, 0f);
                sb.AppendLine("  마당 바닥을 " + (BackX - FrontX + 2f).ToString("F0") + " x "
                              + (HalfZ * 2f + 2f).ToString("F0") + "m 로 맞췄습니다.");
            }

            // 조사 대상 넷은 원래 마당 한복판에 떠 있었고, 이제 그 자리는 문서고가 깔고 앉는다.
            // 서고 안이 제자리지만 실내는 씬을 따로 뗄 참이므로, 우선 서고 앞 툇마루 쪽으로 세워 둔다.
            string[] docs = { "호적대장", "호구단자", "입안대장", "환곡대장" };
            for (int i = 0; i < docs.Length; i++)
            {
                var t = zone1 != null ? zone1.transform.Find(docs[i]) : null;
                if (t == null) continue;
                Undo.RecordObject(t, "조사 대상 옮기기");
                t.position = new Vector3(-8.5f + i * 1.4f, 0.5f, -0.9f);
            }
            sb.AppendLine("  조사 대상 넷을 서고 앞으로 옮겼습니다 — 실내 씬을 뜨면 그 안으로 들어갈 것들입니다.");

            Selection.activeGameObject = root;
            EditorSceneManager_MarkDirty();
            Debug.Log("[관아] 마당을 좁혔습니다.\n" + sb + Measure());
        }

        // ── 낱장 잇기 ──

        /// <summary>
        /// 시작점에서 한 방향으로 담을 잇는다. 긴 낱장으로 채우고 자투리는 짧은 낱장으로,
        /// 그러고도 남으면 마지막 한 장만 배율로 늘여 맞춘다 — 한 장이라 무늬가 티나지 않는다.
        /// </summary>
        private static int Run(Transform parent, GameObject t1, GameObject t2,
                               Vector3 start, float yaw, float length, string label)
        {
            var group = new GameObject(label);
            group.transform.SetParent(parent, false);
            var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

            int n = 0;
            float done = 0f;
            while (length - done >= Tile1 - 0.01f)
            {
                Put(group.transform, t1, start + dir * done, yaw, 1f, ++n);
                done += Tile1;
            }
            while (length - done >= Tile2 - 0.01f)
            {
                Put(group.transform, t2, start + dir * done, yaw, 1f, ++n);
                done += Tile2;
            }
            float rest = length - done;
            if (rest > 0.05f)
            {
                Put(group.transform, t2, start + dir * done, yaw, rest / Tile2, ++n);
                done += rest;
            }
            return n;
        }

        private static void Put(Transform parent, GameObject prefab, Vector3 pos, float yaw, float stretch, int i)
        {
            var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            g.name = prefab.name + "_" + i.ToString("00");
            g.transform.position = pos;
            g.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (!Mathf.Approximately(stretch, 1f))
                g.transform.localScale = new Vector3(1f, 1f, stretch);
        }

        // ── LOD 문턱 ──

        /// <summary>
        /// 화성행궁 낱장은 LOD 를 네 단 들고 오는데 전환 문턱이 0.02 / 0.01 / 0.001 이다.
        /// 화면 높이의 2% 보다 크면 LOD0 이라는 뜻이라, 높이 2.14m 짜리 담이
        /// <b>60m 밖까지</b> 6,036 삼각형으로 그려진다. 마당은 26m 폭이다.
        ///
        /// 그래서 문턱을 올린다 — 5m 안쪽에서만 LOD0, 그 밖은 LOD1·2 로 내려간다.
        /// 담은 무늬가 노멀맵에 있어서 낱장 실루엣이 줄어도 눈에 잘 안 띈다.
        /// </summary>
        private static void RetuneLod(GameObject root, StringBuilder sb)
        {
            int touched = 0, before = 0, after = 0;
            foreach (var lg in root.GetComponentsInChildren<LODGroup>(true))
            {
                var lods = lg.GetLODs();
                if (lods.Length < 4) continue;
                before += Tri(lods[0]);
                lods[0].screenRelativeTransitionHeight = 0.30f;
                lods[1].screenRelativeTransitionHeight = 0.12f;
                lods[2].screenRelativeTransitionHeight = 0.04f;
                lods[3].screenRelativeTransitionHeight = 0.0f;
                Undo.RecordObject(lg, "LOD 문턱");
                lg.SetLODs(lods);
                after += Tri(lods[2]);
                touched++;
            }
            if (touched > 0)
                sb.AppendLine("  담장 " + touched + " 장의 LOD 문턱을 올렸습니다 — 5m 밖은 LOD1·2 로 내려갑니다"
                              + " (낱장 " + before / Mathf.Max(1, touched) + " → " + after / Mathf.Max(1, touched) + " 삼각형).");
        }

        private static int Tri(LOD lod)
        {
            int t = 0;
            foreach (var r in lod.renderers)
            {
                if (r == null) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) t += mf.sharedMesh.triangles.Length / 3;
            }
            return t;
        }

        // ── 재기 ──

        [MenuItem("이문록/관아/⑥ 마당 재기")]
        public static void MeasureMenu() { Debug.Log("[관아] 재어 본 바\n" + Measure()); }

        /// <summary>
        /// 지금 씬이 한 프레임에 무엇을 그리는지 센다.
        /// LOD 가 걸린 것은 <b>LOD0 기준</b>으로 세므로 실제보다 넉넉히 잡힌다 —
        /// 바짝 붙어 섰을 때의 최악값이라고 보면 된다.
        /// </summary>
        private static string Measure()
        {
            var sb = new StringBuilder();
            var seen = new HashSet<Renderer>();
            int lodTri = 0, lodDraw = 0;
            foreach (var lg in Object.FindObjectsByType<LODGroup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var lods = lg.GetLODs();
                if (lods.Length == 0) continue;
                foreach (var lod in lods)
                    foreach (var r in lod.renderers) if (r != null) seen.Add(r);
                foreach (var r in lods[0].renderers)
                {
                    if (r == null) continue;
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    lodTri += mf.sharedMesh.triangles.Length / 3;
                    lodDraw += Mathf.Max(mf.sharedMesh.subMeshCount, r.sharedMaterials.Length);
                }
            }

            int tri = 0, draw = 0, rend = 0;
            var mats = new HashSet<Material>();
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!r.enabled || seen.Contains(r)) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                rend++;
                tri += mf.sharedMesh.triangles.Length / 3;
                draw += Mathf.Max(mf.sharedMesh.subMeshCount, r.sharedMaterials.Length);
                foreach (var m in r.sharedMaterials) if (m != null) mats.Add(m);
            }
            sb.AppendLine("  LOD 없는 것  렌더러 " + rend + " · 삼각형 " + tri.ToString("N0") + " · 드로우 " + draw);
            sb.AppendLine("  LOD 있는 것  LOD0 기준 삼각형 " + lodTri.ToString("N0") + " · 드로우 " + lodDraw);
            sb.AppendLine("  합계  삼각형 " + (tri + lodTri).ToString("N0") + " · 드로우 " + (draw + lodDraw)
                          + "   (퀘스트 한 프레임 예산 20~50만)");
            return sb.ToString();
        }

        private static void EditorSceneManager_MarkDirty()
        {
            var s = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(s);
        }
    }
}
