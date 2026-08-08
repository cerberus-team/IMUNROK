using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 마을(Gyeonu) 보행 콜라이더 (2026-08-08) — 은하담과 같은 방식, 전부 Box(투명, 렌더러 없음).
    ///
    /// 걸을 수 있는 곳: 지형(TerrainCollider 기존 ON), 집·담장·석벽(낙안 팩 내장 MeshCollider 기존),
    ///   관아 계단(팩 계단 콜라이더 위+뒷언덕 사면을 램프로 — 사면 최대 53°라 컨트롤러 한계 50° 초과).
    /// 막는 곳: 씬 경계 ±95(지형 ±100 안쪽, 남쪽 스폰 z-82 포함 — 외곽 언덕 너머·지형 끝 차단),
    ///   입구문(Ogongmun, 콜라이더 0) 좌우 기둥.
    /// 멱등 — 재실행 시 그룹 삭제 후 재생성. 스폰 마커는 읽기만 한다(사용자 배치 유지).
    /// </summary>
    public static class VillageWalkSetup
    {
        const string RootName = "성하리_보행콜라이더";
        const float Bound = 95f;

        [MenuItem("Tools/이문록/마을 보행 콜라이더 구축")]
        public static void Build()
        {
            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(RootName);

            // ── 관아 계단 뒷언덕: 사면 최대 53°(컨트롤러 경사 한계 50° 초과)라 램프 불가,
            //    지형 단면도 볼록해 직선 램프는 파고든다 → 지형을 따라가는 박스 계단
            //    (0.3m 간격, 단차 최대 ~0.4 = 스텝오프 0.5 이내) ──
            var terrain = Terrain.activeTerrain;
            int steps = 0;
            for (float z = 79.8f; z <= 89.6f; z += 0.3f)
            {
                float top = terrain.SampleHeight(new Vector3(-15.9f, 0f, z + 0.15f)) + terrain.transform.position.y + 0.06f;
                Box(root, "관아언덕_계단", new Vector3(-15.9f, top - 0.1f, z + 0.15f), new Vector3(4.5f, 0.2f, 0.34f));
                steps++;
            }
            Debug.Log("[마을보행] 관아 언덕 박스 계단 " + steps + "개");

            // ── 입구문(Ogongmun) 기둥 — 문 자체엔 콜라이더가 없어 기둥이 뚫림.
            //    통로(중앙 약 3.4m)는 개방 ──
            Box(root, "입구문_기둥_서", new Vector3(-16.1f, 2.5f, -73f), new Vector3(2.0f, 5f, 3.0f));
            Box(root, "입구문_기둥_동", new Vector3(-10.4f, 2.5f, -73f), new Vector3(2.0f, 5f, 3.0f));

            // ── 씬 경계 ±95 (외곽 언덕 최고 12m — 벽 상단 14m로 통과 차단) ──
            Box(root, "경계_동", new Vector3(Bound, 6f, 0f), new Vector3(1f, 16f, 2f * Bound + 2f));
            Box(root, "경계_서", new Vector3(-Bound, 6f, 0f), new Vector3(1f, 16f, 2f * Bound + 2f));
            Box(root, "경계_북", new Vector3(0f, 6f, Bound), new Vector3(2f * Bound + 2f, 16f, 1f));
            Box(root, "경계_남", new Vector3(0f, 6f, -Bound), new Vector3(2f * Bound + 2f, 16f, 1f));

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[마을보행] 콜라이더 구축 완료 — 관아 램프 1, 입구문 기둥 2, 경계 ±" + Bound);
        }

        static void Box(GameObject root, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.position = center;
            var b = go.AddComponent<BoxCollider>();
            b.size = size;
        }

        /// <summary>z축 방향 경사 램프 (rotX 음수 = +z로 갈수록 오름).</summary>
        static void RampX(GameObject root, string name, Vector3 center, Vector3 size, float rotX)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.position = center;
            go.transform.rotation = Quaternion.Euler(rotX, 0f, 0f);
            var b = go.AddComponent<BoxCollider>();
            b.size = size;
        }
    }
}
