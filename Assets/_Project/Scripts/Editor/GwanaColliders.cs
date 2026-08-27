using UnityEditor;
using UnityEngine;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>관아에 몸으로 막을 것을 붙인다.</b> 메뉴: [이문록 ▸ 관아 ▸ ⑧ 콜라이더 붙이기]
    ///
    /// 관아는 집이 다 서 있는데 <b>발밑을 받치는 것이 하나도 없었다</b>. 그림 265장에
    /// 콜라이더가 0개라, 지금 딛고 서는 것은 회색 상자 시절의 판판한 바닥(y=0) 하나뿐이다.
    /// 그래서 문서고에 들어서면 마루(0.82)가 아니라 0 에 서서 <b>무릎까지 잠기고</b>,
    /// 동헌 마루(2.21)에는 아예 올라갈 수가 없다.
    ///
    /// <b>무엇을 붙이고 무엇을 안 붙이나</b> — 다 붙이면 값이 비싸고, 안 붙이면 못 걷는다.
    /// 그래서 모양을 보고 고른다:
    ///
    ///  · <b>상자 모양</b>(삼각형 24 이하 — 문서고의 기단·기둥·마루·벽) → BoxCollider.
    ///    생김새 그대로라 어림이 아니고, 값도 거의 안 든다.
    ///  · <b>납작한 것</b>(문짝) → BoxCollider. 문 한 짝에 6,923 삼각형짜리 메시콜라이더를
    ///    씌울 까닭이 없다. 두께 10cm 짜리 판이니 상자가 곧 참값이다.
    ///  · <b>들어가야 하는 집</b>(동헌) → MeshCollider. 여기만은 상자로 싸면 <b>통째로 막혀</b>
    ///    안에 못 들어간다. 62,408 삼각형이 아깝지만 다른 길이 없다.
    ///  · <b>지붕</b> → 안 붙인다. 손이 닿지 않는 데다, 상자로 싸면 처마가 방을 막는다.
    ///  · <b>LOD 밑의 것</b> → 안 붙인다. 같은 담이 네 겹으로 들어 있어, 다 붙이면
    ///    콜라이더가 네 벌이 된다. 담은 이미 Wall_* 이 막고 있다.
    ///
    /// 두 번 눌러도 두 벌이 안 붙는다 — 있는 것은 건너뛴다.
    /// </summary>
    public static class GwanaColliders
    {
        /// <summary>이보다 삼각형이 적으면 상자로 본다(유니티 Cube 가 12개다).</summary>
        private const int BoxTriLimit = 24;

        /// <summary>가장 얇은 쪽이 이보다 얇으면 판때기로 본다 — 문짝·판벽.</summary>
        private const float FlatUnder = 0.35f;

        [MenuItem("이문록/관아/⑧ 콜라이더 붙이기")]
        public static void Attach()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!scene.name.Contains("Gwana"))
            {
                Debug.LogWarning("[콜라이더] 관아 씬을 열고 누르십시오(지금은 " + scene.name + ").");
                return;
            }

            int box = 0, mesh = 0, skipped = 0;
            var log = new System.Text.StringBuilder("[콜라이더] 관아에 붙였다\n");

            foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mr.gameObject.scene != scene) continue;
                if (mr.GetComponent<Collider>() != null) { skipped++; continue; }

                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) { skipped++; continue; }

                // 같은 담이 네 겹으로 들어 있다 — 다 붙이면 네 벌이 된다
                if (mr.GetComponentInParent<LODGroup>() != null) { skipped++; continue; }

                string n = mr.name;
                if (Named(n, "지붕") || Named(n, "Roof") || Named(n, "서까래") || Named(n, "도리"))
                { skipped++; continue; }

                // 내가 지어 넣은 것들은 뿌리에 이미 상자가 있다
                if (InsideNamed(mr.transform, "서가") || InsideNamed(mr.transform, "문서궤들"))
                { skipped++; continue; }
                // 자리 표식은 눈에 보이는 것이 아니라 표다
                if (mr.transform.parent != null && mr.transform.parent.name.StartsWith("Zone_"))
                { skipped++; continue; }

                var m = mf.sharedMesh;
                long tris = 0;
                for (int s = 0; s < m.subMeshCount; s++) tris += (long)m.GetIndexCount(s) / 3;

                Vector3 size = m.bounds.size;
                float thin = Mathf.Min(size.x, Mathf.Min(size.y, size.z));

                bool wantMesh = Named(n, "Donheon") || Named(n, "동헌");

                if (wantMesh)
                {
                    var mc = Undo.AddComponent<MeshCollider>(mr.gameObject);
                    mc.sharedMesh = m;
                    mc.convex = false;
                    mesh++;
                    log.AppendLine("  · " + n + " → 메시(" + tris.ToString("N0") + "삼각형) — 안에 들어가야 하는 집");
                }
                // <b>삼각형이 적다고 상자로 싸면 안 된다.</b> 창호(Paper_1)는 삼각형이
                // 열여섯인데 낱장이 14m 에 걸쳐 흩어져 있어서, 그 몸피를 상자로 싸면
                // <b>동헌 마루가 통째로 막힌다</b> — 실제로 그렇게 되어 마루 위를
                // 4.06m 높이로 걸어 다니게 됐다. 그러니 참말 상자인 것만 상자로 싼다.
                else if (tris == 12 && m.vertexCount <= 24)      // 유니티 Cube
                {
                    var bc = Undo.AddComponent<BoxCollider>(mr.gameObject);
                    bc.center = m.bounds.center;
                    bc.size = m.bounds.size;
                    box++;
                }
                else if (thin < FlatUnder && tris > 200)          // 문짝 — 판때기라 상자가 참값
                {
                    var bc = Undo.AddComponent<BoxCollider>(mr.gameObject);
                    bc.center = m.bounds.center;
                    bc.size = m.bounds.size;
                    box++;
                }
                else if (tris <= 2000)                            // 잔 것은 생김새대로
                {
                    var mc = Undo.AddComponent<MeshCollider>(mr.gameObject);
                    mc.sharedMesh = m; mc.convex = false;
                    mesh++;
                }
                else                                              // 무거운 조각상 따위
                {
                    var bc = Undo.AddComponent<BoxCollider>(mr.gameObject);
                    bc.center = m.bounds.center;
                    bc.size = m.bounds.size;
                    box++;
                }
            }

            log.AppendLine(Steps(scene));

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            log.AppendLine("  상자 " + box + "개, 메시 " + mesh + "개, 건너뜀 " + skipped + "개");
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// <b>동헌 마루로 오르는 댓돌.</b>
        ///
        /// 기단(1.65)에서 마루(2.21)까지가 <b>0.56m</b> 인데, 걸음이 넘어설 수 있는 턱은
        /// 0.40m 다(DebugFlyCamera._stepUp). 그래서 마루 앞 14m 가 통째로 <b>못 오르는
        /// 벽</b>이었다 — 동헌에 올라가 앉을 수가 없으니 심문을 시작할 수조차 없다.
        ///
        /// 턱을 낮추는 것이 아니라 <b>디딤돌을 하나 놓는다</b>. 턱을 높이면 1막에서
        /// 경상이며 문갑 위로 걸어 올라가던 일이 다시 생긴다. 그리고 실제 동헌도
        /// 마루 앞 한가운데에 댓돌을 두지, 열네 자를 죄 계단으로 두르지 않는다.
        ///
        /// 0.27m 씩 두 걸음이 된다: 1.65 → 1.92 → 2.21.
        /// </summary>
        private static string Steps(UnityEngine.SceneManagement.Scene scene)
        {
            const string Name = "동헌_댓돌";
            if (GameObject.Find(Name) != null) return "  · 댓돌은 이미 있다";

            var stone = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/_Common/Sets/Gwana/Donheon/Materials/M_Stone_Granite.mat");
            if (stone == null)
                stone = AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/_Project/Onggojip/Art/KimMyeonggwanHouse/Material/MI_StoneWall02A.mat");

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = Name;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            // 마루 앞 한가운데. 윗면 1.92, 기단(1.65) 위로 0.27 올라선다.
            go.transform.position = new Vector3(10.78f, 1.92f - 0.135f, 0f);
            go.transform.localScale = new Vector3(0.85f, 0.27f, 2.60f);
            go.GetComponent<Renderer>().sharedMaterial = stone;
            Undo.RegisterCreatedObjectUndo(go, "동헌 댓돌");
            return "  · 동헌_댓돌 을 놓았다 (윗면 1.92 — 1.65에서 두 걸음에 마루로)";
        }

        private static bool Named(string s, string k)
            => s.IndexOf(k, System.StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool InsideNamed(Transform t, string rootName)
        {
            for (var p = t; p != null; p = p.parent) if (p.name == rootName) return true;
            return false;
        }
    }
}
