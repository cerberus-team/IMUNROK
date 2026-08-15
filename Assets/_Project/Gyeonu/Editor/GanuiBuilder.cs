using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 간의(簡儀) 절차 생성 (2026-08-13, 멱등) — 관측실 배경 소품. 퍼즐 비대상이라 고리는 고정.
    ///
    /// 형태 근거 (웹 확인 불가 — 아는 범위): 세종대 간의 원본은 소실됐고, 현재 알려진 형태는
    /// 여주 영릉·대전 등지의 **현대 복원품** 계열이다. 그 구성을 따르되 배경 소품 수준으로 단순화:
    ///   - 직사각 받침틀 + 십자보, 네 다리 밑에 백색 석재 초석
    ///   - 극축(북극 방향, 조선 위도 37.5° 앙각)을 남쪽 반원 아치와 북쪽 기둥 한 쌍이 받친다
    ///     (복원품의 용주(龍柱)는 용 조각이라 생략하고 곧은 기둥으로 대체)
    ///   - 적도환(주천도수 눈금) + 그 아래 나란한 백각환(100각 눈금) — 극축 남단 쪽
    ///   - 사유환(극축을 품는 면에서 도는 환) + 규형(窺衡, 지름 방향 조준 막대 + 양끝 조준판)
    ///   - 정극환(북단의 작은 눈금 고리 + 십자 실)
    /// 단순화로 뺀 것: 용 조각, 입운환, 규표 연동부, 물받이 홈.
    ///
    /// 원환·눈금 메시는 HoncheonuiBuilder.BuildRing(띠 단면 + 소/대 눈금 합성)을 재활용하고,
    /// 재질도 혼천의의 황동·청동 에셋을 그대로 쓴다 — 두 기구가 같은 공방에서 나온 것처럼 읽힌다.
    /// 크기: 받침틀 2.0×1.25m, 총높이 약 1.9m — 혼천의(1.77m)보다 조금 크다 (실물 간의는 대형 기구).
    ///
    /// 배치: 작업실 서편, 붕괴 개구부 난간 곁 (18.35, 바닥, 33.25) — 혼천의(남동)와 구역을 나눈다.
    /// 극축은 +Z(북, 돔 방향)를 향한다. 씬에 이미 있으면 위치·회전을 보존하고 재생성한다.
    /// </summary>
    public static class GanuiBuilder
    {
        const string PrefabDir = "Assets/_Project/Gyeonu/Prefabs/Observatory";
        const string MatDir = "Assets/_Project/Gyeonu/Art/Materials/Observatory";
        const string RootName = "간의";

        const float Lat = 37.5f;                  // 조선 위도 — 극축 앙각
        static readonly Vector3 AxisS = new Vector3(0f, 0.88f, -0.62f);   // 극축 남단 (아치 정상)
        static Vector3 AxisDir => new Vector3(0f, Mathf.Sin(Lat * Mathf.Deg2Rad), Mathf.Cos(Lat * Mathf.Deg2Rad));
        const float AxisLen = 1.35f;

        [MenuItem("Tools/이문록/간의 생성")]
        public static void Build()
        {
            // 기존 인스턴스 포즈 보존
            Vector3 pos = new Vector3(18.35f, -3.06f, 33.25f);
            Quaternion rot = Quaternion.identity;
            for (GameObject g; (g = HoncheonuiBuilder.FindRootIncludingInactive(RootName)) != null;)
            {
                pos = g.transform.position; rot = g.transform.rotation;
                Object.DestroyImmediate(g);
            }
            HoncheonuiBuilder.EnsureFolder(PrefabDir);

            var brass = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/M_혼천의_황동.mat");
            var bronze = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/M_혼천의_청동.mat");
            if (brass == null || bronze == null)
            { Debug.LogError("[간의] 혼천의 재질이 없습니다 — '혼천의 생성'을 먼저 실행하세요"); return; }
            var stone = HoncheonuiBuilder.Mat("M_간의_초석", new Color(0.62f, 0.63f, 0.66f), 0f, 0.28f);

            Vector3 N = AxisS + AxisDir * AxisLen;

            // ── 청동 틀: 받침틀 + 다리 + 십자보 + 남쪽 아치 + 북쪽 기둥 쌍 + 극축 ──
            var frame = new List<CombineInstance>();
            Mesh cube = HoncheonuiBuilder.PrimitiveMesh(PrimitiveType.Cube);
            void Bar(Vector3 c2, Vector3 s2) => HoncheonuiBuilder.Add(frame, cube, c2, Quaternion.identity, s2);
            Bar(new Vector3(0.55f, 0.30f, 0f), new Vector3(0.10f, 0.10f, 2.0f));    // 세로 보 (동)
            Bar(new Vector3(-0.55f, 0.30f, 0f), new Vector3(0.10f, 0.10f, 2.0f));   // 세로 보 (서)
            Bar(new Vector3(0f, 0.30f, 0.95f), new Vector3(1.20f, 0.10f, 0.10f));   // 가로 보 (북)
            Bar(new Vector3(0f, 0.30f, -0.95f), new Vector3(1.20f, 0.10f, 0.10f));  // 가로 보 (남)
            Bar(new Vector3(0f, 0.30f, 0.30f), new Vector3(1.10f, 0.08f, 0.08f));   // 십자보
            Bar(new Vector3(0f, 0.30f, -0.30f), new Vector3(1.10f, 0.08f, 0.08f));
            foreach (var sx in new[] { -0.55f, 0.55f })
                foreach (var sz in new[] { -0.95f, 0.95f })
                    Bar(new Vector3(sx, 0.20f, sz), new Vector3(0.10f, 0.16f, 0.10f)); // 다리
            // 남쪽 반원 아치 — 극축 남단을 정상에서 받는다
            var arch = HoncheonuiBuilder.BuildTorus(0.62f, 0.035f, 48, 10, 180f);
            // Rx(+90)은 호가 -Y로 처져 바닥을 뚫는다 (2026-08-13 실측) — -90이 정상 위 방향
            frame.Add(new CombineInstance { mesh = arch, transform = Matrix4x4.TRS(new Vector3(0f, 0.32f, -0.62f), Quaternion.Euler(-90f, 0f, 0f), Vector3.one) });
            HoncheonuiBuilder.Add(frame, cube, new Vector3(0f, 0.90f, -0.62f), Quaternion.identity, new Vector3(0.10f, 0.12f, 0.10f)); // 아치 정상 멍에
            // 북쪽 기둥 쌍 (용주 자리) + 정상 멍에
            HoncheonuiBuilder.AddDiag(frame, cube, new Vector3(0.52f, 0.30f, 0.80f), N + new Vector3(0.05f, -0.10f, -0.04f), 0.075f);
            HoncheonuiBuilder.AddDiag(frame, cube, new Vector3(-0.52f, 0.30f, 0.80f), N + new Vector3(-0.05f, -0.10f, -0.04f), 0.075f);
            HoncheonuiBuilder.Add(frame, cube, N - AxisDir * 0.03f - Vector3.up * 0.02f, Quaternion.FromToRotation(Vector3.up, AxisDir), new Vector3(0.16f, 0.10f, 0.09f));
            // 극축
            HoncheonuiBuilder.AddDiag(frame, cube, AxisS - AxisDir * 0.05f, N + AxisDir * 0.05f, 0.045f);
            var frameMesh = HoncheonuiBuilder.SaveMesh(HoncheonuiBuilder.Combine(frame), "간의_틀");

            // ── 황동 환부: 적도환·백각환(남단) + 사유환·규형(중앙) + 정극환(북단) ──
            var rings = new List<CombineInstance>();
            void Ring(Mesh m, Vector3 c2, Quaternion r2) =>
                rings.Add(new CombineInstance { mesh = m, transform = Matrix4x4.TRS(c2, r2, Vector3.one) });
            var alongAxis = Quaternion.FromToRotation(Vector3.up, AxisDir);         // 고리 면 ⊥ 극축
            var inAxisPlane = Quaternion.FromToRotation(Vector3.up, Vector3.right); // 고리 면이 극축을 품는다 (YZ면)

            var jeokdo = HoncheonuiBuilder.BuildRing(0.50f, 0.050f, 0.018f, 96, 36, 12, 0, false); // 적도환 (주천도수 눈금)
            var baekgak = HoncheonuiBuilder.BuildRing(0.56f, 0.050f, 0.018f, 96, 50, 12, 0, false); // 백각환 (100각 눈금)
            var sayu = HoncheonuiBuilder.BuildRing(0.52f, 0.048f, 0.018f, 96, 36, 12, 0, false);   // 사유환
            var jeonggeuk = HoncheonuiBuilder.BuildRing(0.085f, 0.020f, 0.008f, 40, 0, 4, 0, false); // 정극환
            Ring(jeokdo, AxisS + AxisDir * 0.30f, alongAxis);
            Ring(baekgak, AxisS + AxisDir * 0.21f, alongAxis);
            Vector3 M = AxisS + AxisDir * 0.78f;
            Ring(sayu, M, inAxisPlane);
            Ring(jeonggeuk, N - AxisDir * 0.02f, alongAxis);
            // 정극환 십자 실
            Mesh cube2 = HoncheonuiBuilder.PrimitiveMesh(PrimitiveType.Cube);
            HoncheonuiBuilder.Add(rings, cube2, N - AxisDir * 0.02f, alongAxis, new Vector3(0.17f, 0.006f, 0.006f));
            HoncheonuiBuilder.Add(rings, cube2, N - AxisDir * 0.02f, alongAxis * Quaternion.Euler(0, 90, 0), new Vector3(0.17f, 0.006f, 0.006f));
            // 사유환 허브 (극축 관통부)
            Mesh cyl = HoncheonuiBuilder.PrimitiveMesh(PrimitiveType.Cylinder);
            HoncheonuiBuilder.Add(rings, cyl, M, Quaternion.FromToRotation(Vector3.up, AxisDir), new Vector3(0.07f, 0.05f, 0.07f));
            // 규형 — 사유환 지름 방향 조준 막대 + 양끝 조준판. 극축에서 28° 젖힌 각도로 "겨눠 둔" 채 고정
            var aimDir = Quaternion.AngleAxis(-28f, Vector3.right) * AxisDir;
            HoncheonuiBuilder.AddDiag(rings, cube2, M - aimDir * 0.46f, M + aimDir * 0.46f, 0.032f);
            HoncheonuiBuilder.Add(rings, cube2, M + aimDir * 0.44f, Quaternion.LookRotation(aimDir), new Vector3(0.065f, 0.10f, 0.012f));
            HoncheonuiBuilder.Add(rings, cube2, M - aimDir * 0.44f, Quaternion.LookRotation(aimDir), new Vector3(0.065f, 0.10f, 0.012f));
            var ringsMesh = HoncheonuiBuilder.SaveMesh(HoncheonuiBuilder.Combine(rings), "간의_환부");
            Object.DestroyImmediate(jeokdo); Object.DestroyImmediate(baekgak);
            Object.DestroyImmediate(sayu); Object.DestroyImmediate(jeonggeuk);
            Object.DestroyImmediate(arch);

            // ── 백색 석재 초석 (네 다리 밑, 2단) ──
            var ped = new List<CombineInstance>();
            foreach (var sx in new[] { -0.55f, 0.55f })
                foreach (var sz in new[] { -0.95f, 0.95f })
                {
                    HoncheonuiBuilder.Add(ped, cube, new Vector3(sx, 0.035f, sz), Quaternion.identity, new Vector3(0.30f, 0.07f, 0.30f));
                    HoncheonuiBuilder.Add(ped, cube, new Vector3(sx, 0.10f, sz), Quaternion.identity, new Vector3(0.23f, 0.06f, 0.23f));
                }
            var pedMesh = HoncheonuiBuilder.SaveMesh(HoncheonuiBuilder.Combine(ped), "간의_초석");

            // ── 조립 · 프리팹 · 씬 배치 ──
            var root = new GameObject(RootName);
            HoncheonuiBuilder.MeshGO("틀", frameMesh, bronze, root.transform, Vector3.zero, Quaternion.identity);
            HoncheonuiBuilder.MeshGO("환부", ringsMesh, brass, root.transform, Vector3.zero, Quaternion.identity);
            HoncheonuiBuilder.MeshGO("초석", pedMesh, stone, root.transform, Vector3.zero, Quaternion.identity);
            var col = new GameObject("차단");
            col.transform.SetParent(root.transform, false);
            col.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            col.AddComponent<BoxCollider>().size = new Vector3(1.35f, 1.2f, 2.1f);
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true)) mr.gameObject.isStatic = true;
            col.isStatic = true;

            PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabDir + "/간의.prefab", InteractionMode.AutomatedAction);
            root.transform.SetPositionAndRotation(pos, rot);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[간의] 생성 완료 — {HoncheonuiBuilder.CountTris(root):n0}tri, 위치 {pos}, 프리팹 저장 (배경 소품 — 고리 고정)");
        }
    }
}
