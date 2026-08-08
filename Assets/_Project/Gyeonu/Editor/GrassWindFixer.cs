using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 잔디·들꽃 바람 밑동 고정 (2026-08-08, 멱등).
    ///
    /// 원인: Detail 프로토타입 12종이 공유하는 GrassCrossQuad 메시(우리 에셋)에 정점색이
    /// 없어서, 지형 잔디 셰이더(WavingGrass 계열 — 정점 알파 = 흔들림 가중치)가 알파를
    /// 1로 취급해 뿌리까지 통째로 흔들림.
    /// 해법: ① 메시에 정점색을 구움 — 알파 = 정규화 높이^1.3 (밑동 0 = 고정, 끝 1),
    ///       ② 지형 바람 값 감폭 (amount 0.5→0.25, speed 0.5→0.4, strength 0.5→0.3)
    ///          — 마을·은하담·스커트 8타일 TerrainData 전부.
    /// 12종 프리팹이 참조하는 메시를 전수 수집해 각각 굽는다 (원본 팩 폴더 무접촉 —
    /// 메시는 Art/Models/Grass/ 우리 에셋).
    /// </summary>
    public static class GrassWindFixer
    {
        const string VegPath = "Assets/_Project/Gyeonu/Prefabs/Vegetation/";
        static readonly string[] DetailPrefabs =
        {
            "Grass_잔디_짧은것_01", "Grass_잔디_짧은것_02",
            "Flower_들꽃_흰파랑_01", "Flower_들꽃_02", "Flower_들꽃_03", "Flower_들꽃_붉은_04",
            "Flower_들꽃_05", "Flower_들꽃_연분홍_06", "Flower_들꽃_07", "Flower_들꽃_노랑_08",
            "Flower_들꽃_09", "Flower_들꽃_노랑_10",
        };
        static readonly string[] TerrainDataPaths =
        {
            "Assets/_Project/Gyeonu/Art/Terrain/Gyeonu_TerrainData.asset",
            "Assets/_Project/Gyeonu/Art/Terrain/EunhaDam_TerrainData.asset",
            "Assets/_Project/Gyeonu/Art/Terrain/EunhaDam_Skirt_N.asset",
            "Assets/_Project/Gyeonu/Art/Terrain/EunhaDam_Skirt_S.asset",
            "Assets/_Project/Gyeonu/Art/Terrain/EunhaDam_Skirt_E.asset",
            "Assets/_Project/Gyeonu/Art/Terrain/EunhaDam_Skirt_W.asset",
            "Assets/_Project/Gyeonu/Art/Terrain/EunhaDam_Skirt_NE.asset",
            "Assets/_Project/Gyeonu/Art/Terrain/EunhaDam_Skirt_NW.asset",
            "Assets/_Project/Gyeonu/Art/Terrain/EunhaDam_Skirt_SE.asset",
            "Assets/_Project/Gyeonu/Art/Terrain/EunhaDam_Skirt_SW.asset",
        };

        [MenuItem("Tools/이문록/잔디 바람 밑동 고정")]
        public static void Fix()
        {
            // ── ① 메시 정점 알파 굽기 (12종 프리팹의 고유 메시 전수) ──
            var meshes = new HashSet<Mesh>();
            foreach (var name in DetailPrefabs)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VegPath + name + ".prefab");
                if (prefab == null) { Debug.LogWarning("[잔디바람] 프리팹 없음: " + name); continue; }
                foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.sharedMesh != null) meshes.Add(mf.sharedMesh);
            }

            int baked = 0;
            foreach (var mesh in meshes)
            {
                string path = AssetDatabase.GetAssetPath(mesh);
                if (!path.StartsWith("Assets/_Project/"))
                {
                    Debug.LogWarning("[잔디바람] 우리 에셋이 아니라 건너뜀 (원본 무수정 원칙): " + path);
                    continue;
                }
                var verts = mesh.vertices;
                float minY = mesh.bounds.min.y, range = Mathf.Max(0.001f, mesh.bounds.size.y);
                var cols = new Color[verts.Length];
                for (int i = 0; i < verts.Length; i++)
                {
                    float a = Mathf.Pow(Mathf.Clamp01((verts[i].y - minY) / range), 1.3f);
                    cols[i] = new Color(1f, 1f, 1f, a);          // RGB 흰색(틴트 무영향), 알파 = 흔들림 가중치
                }
                mesh.colors = cols;
                EditorUtility.SetDirty(mesh);
                baked++;
                Debug.Log("[잔디바람] 정점 알파 굽기: " + path + " (" + verts.Length + " 정점)");
            }

            // ── ② 바람 감폭 ──
            int tds = 0;
            foreach (var p in TerrainDataPaths)
            {
                var td = AssetDatabase.LoadAssetAtPath<TerrainData>(p);
                if (td == null) continue;
                td.wavingGrassAmount = 0.25f;
                td.wavingGrassSpeed = 0.4f;
                td.wavingGrassStrength = 0.3f;
                EditorUtility.SetDirty(td);
                tds++;
            }
            AssetDatabase.SaveAssets();

            // 프로토타입 인스턴싱 상태 진단 (인스턴싱 ON이면 지형 바람 경로를 안 탐)
            var vtd = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPaths[0]);
            int instancedCount = 0;
            foreach (var pr in vtd.detailPrototypes) if (pr.useInstancing) instancedCount++;
            Debug.Log($"[잔디바람] 완료 — 메시 {baked}개 굽기, TerrainData {tds}개 감폭, " +
                      $"인스턴싱 프로토타입 {instancedCount}/{vtd.detailPrototypes.Length} (0이어야 바람 경로 정상)");
        }
    }
}
