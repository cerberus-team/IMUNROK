using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 김명관 담장 세트(KMWall_*)로 집 마당을 두르는 빌더.
    ///
    /// 규칙 (2026-08-04 지시, 같은 날 개정):
    ///   - 직선은 전부 KMWall_직선_2.5m_민무늬 하나로 통일 (대문옆 마감도 민무늬로 대체)
    ///   - 온전한 사각 담. 코너 = KMWall_코너(모서리점 피벗, 날개 1.2m)
    ///   - 정면에 KMWall_일각문_높임_통로2.2m
    ///   - 담장 끝은 문 바운즈 안쪽으로 0.3m 밀어넣어 처마 밑 기둥에 닿게
    ///   - 길이 보정은 Z스케일 0.8~1.2 (데모 방식)
    ///   - 발바닥 = Terrain −0.1m (문은 −0.02m)
    ///
    /// 선아집: 마당 22×17m, 남향 대문. 어머니집: 12.8×20.6m, 북향 대문
    /// (동쪽 대숲·서쪽 방문들 때문에 북쪽이 유일하게 트인 면).
    /// </summary>
    public static class KMWallBuilder
    {
        const string Dir = "Assets/_Project/Gyeonu/Prefabs/KimMyeonggwan/Walls/";
        const string ParentName = "성하리_건물";
        const float Bury = 0.10f;
        const float GateBury = 0.02f;
        const float CornerWing = 1.2f;
        const float GateHalf = 1.28f;      // 일각문 폭 2.56의 절반
        const float GateTuck = 0.30f;      // 담장이 문 바운즈로 파고드는 깊이

        // 직선은 민무늬 단일 조각
        const string FillName = "KMWall_직선_2.5m_민무늬";
        const float FillLen = 2.509f;

        [MenuItem("Tools/이문록/선아집·어머니집 김명관 담장 생성")]
        public static void Build()
        {
            var terrain = Object.FindFirstObjectByType<Terrain>();
            var parent = GameObject.Find(ParentName);
            if (terrain == null || parent == null) { Debug.LogError("[KMWallBuilder] Terrain/부모 없음"); return; }

            BuildEnclosure(parent.transform, terrain, "선아집_담장",
                xW: -54f, xE: -32f, zS: 51.5f, zN: 68.5f, gateOnSouth: true, gateX: -43f);
            // 대문 x=-62: 통로 앞 벚나무(-59.6, -36.2)를 피해 서쪽으로. 왼쪽 마감은 코너 날개에 바로 붙는다
            BuildEnclosure(parent.transform, terrain, "어머니집_담장",
                xW: -66.6f, xE: -53.8f, zS: -57.4f, zN: -36.8f, gateOnSouth: false, gateX: -62f);

            EditorSceneManager.MarkSceneDirty(parent.scene);
        }

        static void BuildEnclosure(Transform parent, Terrain terrain, string groupName,
            float xW, float xE, float zS, float zN, bool gateOnSouth, float gateX)
        {
            var old = parent.Find(groupName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var group = new GameObject(groupName).transform;
            group.SetParent(parent, false);

            // ── 코너 4개 (모서리점 피벗, 날개가 안쪽 변을 따라가게 회전) ──
            PlaceCorner(group, terrain, new Vector3(xW, 0, zS), 0f);     // SW: +X, +Z
            PlaceCorner(group, terrain, new Vector3(xE, 0, zS), 270f);   // SE: -X, +Z
            PlaceCorner(group, terrain, new Vector3(xE, 0, zN), 180f);   // NE: -X, -Z
            PlaceCorner(group, terrain, new Vector3(xW, 0, zN), 90f);    // NW: +X, -Z

            // ── 동·서 변 (Z 방향, 전체 채움) — 돌면(로컬 -X)이 바깥으로: 동쪽은 반전 ──
            FillRun(group, terrain, new Vector3(xW + 0.125f, 0, zS + CornerWing), 0f, (zN - CornerWing) - (zS + CornerWing), false);
            FillRun(group, terrain, new Vector3(xE - 0.125f, 0, zS + CornerWing), 0f, (zN - CornerWing) - (zS + CornerWing), true);

            // ── 남·북 변 (X 방향) — 북쪽 변은 반전해야 돌면이 밖(+Z)을 본다 ──
            float zGateLine = gateOnSouth ? zS + 0.125f : zN - 0.125f;
            float zPlainLine = gateOnSouth ? zN - 0.125f : zS + 0.125f;
            bool flipGateLine = !gateOnSouth;
            bool flipPlainLine = gateOnSouth;

            // 문 없는 변: 전체 채움
            FillRun(group, terrain, new Vector3(xW + CornerWing, 0, zPlainLine), 90f, (xE - CornerWing) - (xW + CornerWing), flipPlainLine);

            // 문 있는 변: [채움 → 문 안 0.3][문][문 안 0.3 → 채움]
            float gateL = gateX - GateHalf, gateR = gateX + GateHalf;
            FillRun(group, terrain, new Vector3(xW + CornerWing, 0, zGateLine), 90f, (gateL + GateTuck) - (xW + CornerWing), flipGateLine);
            PlacePiece(group, terrain, "KMWall_일각문_높임_통로2.2m", new Vector3(gateL, 0, zGateLine), 90f, 1f, GateBury);
            FillRun(group, terrain, new Vector3(gateR - GateTuck, 0, zGateLine), 90f, (xE - CornerWing) - (gateR - GateTuck), flipGateLine);

            Debug.Log("[KMWallBuilder] " + groupName + " 완료: " + group.childCount + "개 조각");
        }

        // 시작점에서 방향(rotY 0=+Z, 90=+X)으로 span을 직선 조각으로 채움 (Z스케일 0.8~1.2 보정).
        // flipFace=true면 조각을 180° 돌려 돌면을 반대쪽으로 — 피벗을 조각 끝점으로 옮겨 같은 구간을 덮는다.
        static void FillRun(Transform group, Terrain terrain, Vector3 start, float rotY, float span, bool flipFace)
        {
            if (span < 0.6f) { if (span > 0.05f) Debug.LogWarning("[KMWallBuilder] 채움 불가 짧은 구간 " + span.ToString("F2") + "m"); return; }

            int n = Mathf.Max(1, Mathf.RoundToInt(span / FillLen));
            float k = span / (n * FillLen);
            if (k > 1.2f) { n++; k = span / (n * FillLen); }
            else if (k < 0.8f && n > 1) { n--; k = span / (n * FillLen); }
            if (k < 0.79f || k > 1.21f)
                Debug.LogWarning("[KMWallBuilder] Z스케일 범위 초과 " + k.ToString("F2") + " (구간 " + span.ToString("F2") + "m) — 이음새 우선으로 그대로 적용");

            Vector3 dir = rotY == 0f ? Vector3.forward : Vector3.right;
            float yaw = flipFace ? rotY + 180f : rotY;
            Vector3 cursor = start;
            for (int i = 0; i < n; i++)
            {
                Vector3 pivot = flipFace ? cursor + dir * (FillLen * k) : cursor;
                PlacePiece(group, terrain, FillName, pivot, yaw, k, Bury);
                cursor += dir * (FillLen * k);
            }
        }

        static void PlaceCorner(Transform group, Terrain terrain, Vector3 xz, float rotY)
        {
            var go = Instantiate(group, "KMWall_코너", xz, rotY, 1f, Bury, terrain);
            go.name = "KMWall_코너";
        }

        static GameObject PlacePiece(Transform group, Terrain terrain, string prefabName, Vector3 xz, float rotY, float zScale, float bury)
        {
            return Instantiate(group, prefabName, xz, rotY, zScale, bury, terrain);
        }

        static GameObject Instantiate(Transform group, string prefabName, Vector3 xz, float rotY, float zScale, float bury, Terrain terrain)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + prefabName + ".prefab");
            if (prefab == null) { Debug.LogError("[KMWallBuilder] 프리팹 없음: " + prefabName); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(group, false);
            float h = terrain.SampleHeight(xz) + terrain.transform.position.y;
            go.transform.position = new Vector3(xz.x, h - bury, xz.z);
            go.transform.rotation = Quaternion.Euler(0, rotY, 0);
            if (!Mathf.Approximately(zScale, 1f)) go.transform.localScale = new Vector3(1, 1, zScale);
            return go;
        }
    }
}
