using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static IMUNROK.Gyeonu.Editor.GwanaLayout;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 관아 담장 — **화성 행궁 에셋의 `SM_StraightStronewall_1` 한 조각을 반복 배치**해서 만든다. 멱등.
    ///
    /// ── 왜 바꾸나 (2026-08-21) ──
    /// 그 전에는 팀원 전달본(GwanaSet_FOR_TEAM)의 37.9m 짜리 긴 담장을 길이별로 잘라 썼다.
    /// 그 방식의 문제는 **자른 단면**이었다. 3D 스캔 원본이라 임의 위치 단면은 모양이 제각각이고,
    /// 모서리에서 그 단면이 밖으로 드러나 밝은 회색 판으로 보였다(모서리 처리를 두 번 뒤집었다).
    /// 화성 행궁 담장은 애초에 **타일링용 모듈**이라 끝면이 벽돌·기와 재질로 제대로 막혀 있다
    /// (시작면 134삼각형 / 1.14㎡ 실측). 이어 붙이면 데모처럼 이음새가 딱 맞고, 모서리에서
    /// 끝면이 드러나도 담장 바깥면과 같은 재질이라 그대로 모서리로 읽힌다.
    ///
    /// ── 조각 실측 (LOD0, 프리팹 로컬) ──
    ///   길이 **4.2077** (+Z 방향, 원점이 시작면) / 높이 **2.0900** / 두께 **0.8578** (X, 중앙 정렬)
    ///   삼각형 6,036 (LOD1 3,018 / LOD2 1,508 / LOD3 754), 서브메시 4
    ///   재질 MI_KoreanBrick_4 · MI_KoreanBrick_2 · MI_KoreanStone_1 · MI_R_Roof (전부 smoothness 0)
    ///   ※ `SM_StraightStronewall_2` 는 정확히 절반(2.1037)인 반 모듈이다. 지금은 안 쓴다.
    ///
    /// ── 데모 씬에서 확인한 이어 붙이는 법 ──
    /// `Assets/HwaseongHaenggung/Scenes/Demo.unity` 의 배치 453건을 뜯어 보면:
    ///   · 회전은 전부 **Y축 90° 배수**만 쓴다 (쿼터니언 (0,±0.7071,0,±0.7071) / (0,1,0,0) / 단위)
    ///   · 한 줄의 조각 간격이 4.18~4.20 으로 모듈 길이와 같다 — **끝면끼리 맞대는 방식**이다
    ///   · 길이가 안 맞는 줄은 **로컬 Z 스케일**로 늘리고 줄인다 (실측 0.677 ~ 1.341)
    /// → 우리도 같게 한다. 다만 데모는 손으로 놓아 간격이 몇 mm 씩 흔들리는데,
    ///   여기서는 한 변마다 `조각수 = round(변길이 / 4.2077)` 를 잡고 **그 변 전체에 같은 Z 스케일**을
    ///   먹여 이음새가 정확히 떨어지게 한다. 실제 배율은 0.980 ~ 1.035 로 데모 범위 안이다.
    ///
    /// ── 모서리 ──
    /// 서·동담이 모서리를 **관통**하고(z −34.43 ~ +27.43, 남·북담 바깥면까지), 남·북담이 그 안쪽으로
    /// 파고들어 끝난다. 그래서 바깥에서 보면 측담이 모서리를 돌아나오고, 남·북담의 끝면은 측담
    /// 몸통 속에 완전히 묻힌다. 드러나는 것은 측담의 **모듈 끝면**뿐인데 이건 제대로 막힌 면이다.
    ///   · 파고드는 깊이 <see cref="Bury"/> 0.15 — 0 이면 두 면이 동일 평면이라 z-파이팅이 난다.
    ///   · 남·북담은 <see cref="CrossSink"/> 만큼 더 낮춘다 — 겹치는 0.858×0.15 구간에서
    ///     기와 갓끼리 같은 높이로 만나 깜빡이는 것을 막는다. 8mm 라 눈에 안 띈다.
    ///
    /// ── 원본 무수정 ──
    /// `Assets/HwaseongHaenggung/` 는 gitignore 대상이고 절대 수정하지 않는다.
    ///   · 배치용 프리팹은 우리 폴더에 새로 만든다 (<see cref="PiecePrefabPath"/>)
    ///   · 색은 원본 .mat 을 복제해 <see cref="ToneDir"/> 에 두고 그 복제본만 만진다
    /// </summary>
    public static class GwanaWallSwap
    {
        const string SrcPrefabPath = "Assets/HwaseongHaenggung/Prefabs/Parts/SM_StraightStronewall_1.prefab";
        const string PrefabDir = "Assets/_Project/Gyeonu/Prefabs/Gwana";
        const string PiecePrefabPath = PrefabDir + "/관아_담장조각.prefab";
        const string ToneDir = "Assets/_Project/Gyeonu/Art/Materials/Gwana";

        // ── 조각 실측 (LOD0 bounds) ──────────────────────────
        /// <summary>모듈 길이. 이 값이 곧 이음새 간격이다.</summary>
        public const float PieceLen = 4.2077f;
        /// <summary>담장 높이(기와 갓 상단). 보행 콜라이더가 참조한다.</summary>
        public const float WallTopY = 2.0900f;
        /// <summary>담장 두께. 기와 갓 내밈까지 포함한 값이라 따로 여유를 더할 필요가 없다.</summary>
        public const float WallThick = 0.8578f;
        const float Half = WallThick * 0.5f;      // 0.4289

        // ── 배치 상수 ────────────────────────────────────────
        /// <summary>밑동을 지면 아래로 묻는 깊이. 담장 라인 주변 지형이 ±0.011 흔들려 그만큼 여유.</summary>
        const float Sink = 0.030f;
        /// <summary>남·북담을 서·동담 몸통 안으로 파고들게 하는 깊이 (동일 평면 z-파이팅 회피).</summary>
        const float Bury = 0.15f;
        /// <summary>겹치는 모서리 구간에서 남·북담을 추가로 낮추는 양 (갓끼리 깜빡임 방지).</summary>
        const float CrossSink = 0.008f;
        /// <summary>한 변의 Z 스케일이 이보다 더 벌어지면 경고 — 돌 크기가 눈에 띄게 달라진다.</summary>
        const float ScaleWarn = 0.12f;

        // ── 톤 (화면 픽셀 기준으로 맞춘 값) ──────────────────
        // 원본 4종은 전부 metallic 0 / smoothness 0 이라 하늘 반사로 뜨는 문제가 없다
        // (팀 전달본 담장이 하얗게 떴던 원인은 그쪽 재질의 smoothness 0.50 이었다).
        // 그래서 광택이 아니라 **알베도 배율** 하나만 쓴다.
        /// <summary>복제 재질에 먹이는 _BaseColor 배율. 1.0 = 원본 그대로.</summary>
        const float ToneMul = 1.00f;

        public static bool Available =>
            AssetDatabase.LoadAssetAtPath<GameObject>(SrcPrefabPath) != null;

        [MenuItem("Tools/이문록/관아 ▸ 담장 교체 (화성 행궁)", priority = 120)]
        public static void SwapInScene()
        {
            var host = GameObject.Find("관아_건물/관아_구조물");
            if (host == null) { Debug.LogError("[관아] 관아_건물/관아_구조물 이 없다 — 구조물부터 생성할 것"); return; }

            var old = host.transform.Find("담장");
            int sib = old != null ? old.GetSiblingIndex() : 0;
            if (old != null) Object.DestroyImmediate(old.gameObject);
            if (!BuildInto(host.transform)) return;
            host.transform.Find("담장").SetSiblingIndex(sib);

            GwanaWalkSetup.BuildColliders();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        /// <summary>담장 전체를 parent 아래에 만든다. 에셋이 없으면 false.</summary>
        public static bool BuildInto(Transform parent)
        {
            var piece = EnsurePiecePrefab();
            if (piece == null) { Debug.LogError("[관아] 담장 조각 프리팹을 만들지 못했다 — 화성 행궁 에셋 확인"); return false; }

            var group = new GameObject("담장");
            group.transform.SetParent(parent, false);

            // 서·동담이 모서리를 관통한다 — 남·북담 바깥면까지 나간다.
            float sideZ0 = WallZS - Half;          // −34.4289
            float sideZ1 = WallZN + Half;          //  27.4289
            // 남·북담은 측담 안쪽면보다 Bury 만큼 더 들어가 끝난다.
            float endX = WallHalfX - Half + Bury;  //  16.7211
            float gateX = GwanaGateSwap.SideFaceX - 0.20f;   // 3.66 — 협문칸 안쪽 벽을 0.2 파고든다

            float ySide = -Sink;
            float yCross = -(Sink + CrossSink);

            int n = 0;
            n += Run(group.transform, piece, "서담", new Vector3(-WallHalfX, ySide, sideZ0), Vector3.forward, sideZ1 - sideZ0);
            n += Run(group.transform, piece, "동담", new Vector3(WallHalfX, ySide, sideZ0), Vector3.forward, sideZ1 - sideZ0);
            n += Run(group.transform, piece, "남담_서", new Vector3(-endX, yCross, WallZS), Vector3.right, endX - gateX);
            n += Run(group.transform, piece, "남담_동", new Vector3(gateX, yCross, WallZS), Vector3.right, endX - gateX);
            n += Run(group.transform, piece, "북담", new Vector3(-endX, yCross, WallZN), Vector3.right, 2f * endX);

            long tri = 0;
            foreach (var mf in group.GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh != null && mf.gameObject.name.EndsWith("LOD0"))
                    tri += mf.sharedMesh.triangles.Length / 3;
            Debug.Log($"[관아] 담장 = 화성 행궁 모듈 {n}조각 (길이 {PieceLen:F4} / 높이 {WallTopY:F3} / 두께 {WallThick:F4})"
                    + $" — LOD0 합계 {tri:N0} 삼각형");
            return true;
        }

        /// <summary>
        /// start 에서 dir 방향으로 len 만큼을 조각으로 채운다. 조각 수는 반올림으로 정하고,
        /// 남는 오차는 **그 변 전체에 같은 Z 스케일**로 흡수한다 (데모가 쓰는 방식).
        /// </summary>
        static int Run(Transform parent, GameObject piece, string name, Vector3 start, Vector3 dir, float len)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(len / PieceLen));
            float scale = len / (count * PieceLen);
            if (Mathf.Abs(scale - 1f) > ScaleWarn)
                Debug.LogWarning($"[관아] {name}: 길이 보정 배율 {scale:F3} — 돌 크기가 다른 변과 눈에 띄게 달라진다");

            var run = new GameObject(name);
            run.transform.SetParent(parent, false);

            // ★로컬 +Z 가 진행 방향이 되게 LookRotation 을 쓴다. 그러면 로컬 X(두께)가
            //   자동으로 진행 방향과 직각이 된다 — 담장이 눕거나 뒤집힐 여지가 없다.
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            float step = PieceLen * scale;
            for (int i = 0; i < count; i++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(piece, run.transform);
                go.name = $"{name}_{i:00}";
                go.transform.SetPositionAndRotation(start + dir * (step * i), rot);
                go.transform.localScale = new Vector3(1f, 1f, scale);
            }
            Debug.Log($"[관아] {name}: {count}조각 × {step:F4}m (Z스케일 {scale:F4}) = {len:F3}m");
            return count;
        }

        // ── 배치용 프리팹 ────────────────────────────────────
        /// <summary>
        /// 화성 행궁 조각을 우리 폴더의 프리팹으로 복제한다 (원본 프리팹은 건드리지 않는다).
        /// LOD 그룹·메시는 원본 것을 그대로 참조하고, **재질만 복제본으로 갈고 콜라이더는 뗀다.**
        /// 이미 있으면 그 프리팹의 재질만 갱신한다 — 씬의 배치가 통째로 끊기지 않게.
        /// </summary>
        static GameObject EnsurePiecePrefab()
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(SrcPrefabPath);
            if (src == null) { Debug.LogError("[관아] " + SrcPrefabPath + " 없음 — 화성 행궁 에셋을 임포트할 것"); return null; }

            var tone = EnsureToneMaterials(src);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetFullPath(PrefabDir));

            var had = AssetDatabase.LoadAssetAtPath<GameObject>(PiecePrefabPath);
            if (had != null) { Retone(had, tone); return had; }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.name = "관아_담장조각";

            // ★콜라이더는 뗀다. 원본 프리팹의 MeshCollider 는 LOD3 메시(754삼각형)를 물고 있어
            //  담장 44조각이면 물리 씬이 무거워지고, 무엇보다 이 씬의 보행 차단은
            //  GwanaWalkSetup 이 만드는 **박스 콜라이더**가 맡는다 (씬 전체 MeshCollider 0개 방침).
            foreach (var c in inst.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            Retone(inst, tone);

            var pf = PrefabUtility.SaveAsPrefabAsset(inst, PiecePrefabPath);
            Object.DestroyImmediate(inst);
            Debug.Log("[관아] 담장 조각 프리팹 생성 — " + PiecePrefabPath);
            return pf;
        }

        static void Retone(GameObject root, Dictionary<Material, Material> tone)
        {
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var cur = r.sharedMaterials;
                var nu = new Material[cur.Length];
                bool changed = false;
                for (int i = 0; i < cur.Length; i++)
                {
                    Material t;
                    if (cur[i] != null && tone.TryGetValue(cur[i], out t)) { nu[i] = t; changed = true; }
                    else nu[i] = cur[i];
                }
                if (changed) r.sharedMaterials = nu;
            }
            if (AssetDatabase.Contains(root)) EditorUtility.SetDirty(root);
        }

        /// <summary>
        /// 원본 재질 → 복제 재질 대응표. 복제본은 우리 폴더에 두고 원본은 절대 수정하지 않는다.
        /// 매번 원본에서 다시 복사한 뒤 배율을 먹이므로 메뉴를 여러 번 눌러도 색이 겹쳐 곱해지지 않는다.
        /// </summary>
        static Dictionary<Material, Material> EnsureToneMaterials(GameObject src)
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetFullPath(ToneDir));
            var map = new Dictionary<Material, Material>();
            foreach (var r in src.GetComponentsInChildren<MeshRenderer>(true))
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null || map.ContainsKey(m)) continue;
                    string path = ToneDir + "/관아_담장_" + m.name + ".mat";
                    var clone = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (clone == null) { clone = new Material(m.shader); AssetDatabase.CreateAsset(clone, path); }
                    clone.shader = m.shader;
                    // ★원본에서 매번 다시 복사한다 — 복제본의 현재 값에 곱하면 누적된다
                    clone.CopyPropertiesFromMaterial(m);
                    var c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
                    var t = new Color(c.r * ToneMul, c.g * ToneMul, c.b * ToneMul, c.a);
                    if (clone.HasProperty("_BaseColor")) clone.SetColor("_BaseColor", t);
                    if (clone.HasProperty("_Color")) clone.SetColor("_Color", t);
                    EditorUtility.SetDirty(clone);
                    map[m] = clone;
                }
            return map;
        }
    }
}
