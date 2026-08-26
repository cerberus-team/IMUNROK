using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>복제 재질 ↔ 원본 재질 대응표. 되돌리기·재적용이 항상 원본 기준이 되게 한다.</summary>
    public class GwanaToneMap : ScriptableObject
    {
        public List<Material> clones = new List<Material>();
        public List<Material> sources = new List<Material>();

        public Material SourceOf(Material clone)
        {
            int i = clones.IndexOf(clone);
            return i >= 0 && i < sources.Count ? sources[i] : null;
        }

        public void Record(Material clone, Material source)
        {
            int i = clones.IndexOf(clone);
            if (i >= 0) { sources[i] = source; return; }
            clones.Add(clone); sources.Add(source);
        }
    }

    /// <summary>
    /// 관아 인공 구조물(담장·외삼문·동헌·월대·어도·석물)의 톤을 낮춘다. 멱등, 되돌리기 가능.
    ///
    /// ── 왜 밝아 보였나 (2026-08-20 실측) ──
    /// 팀 전달본 재질은 손대지 않은 원본이고(BaseColor 1,1,1 / metallic 0), 색공간도 Linear 로 정상이다.
    /// 같은 담장을 중립 조명(유니티 기본 스카이박스·방향광 1.0) 씬에 놓고 같은 거리에서 찍어 비교하면
    /// 휘도 0.495 인데, 관아 씬에서는 0.753 — **52% 밝다.**
    ///
    /// 항목별로 하나씩 꺼서 재보면 범인이 분명하다:
    ///     반사(스카이 큐브맵) 끔 → 휘도 0.683 → 0.355   **기여 48%**
    ///     앰비언트(Trilight) 끔  → 0.683 → 0.653       기여 4%
    ///     태양 1.1→1.0          → 0.683 → 0.682       기여 0%
    ///     안개 끔                → 변화 없음             기여 0%
    ///
    /// 즉 조명 강도가 아니라 **반사**다. 팀 재질의 smoothness 가 0.50 이라 우리 하늘 큐브맵
    /// (EnvCube_SkyPreset_낮_맑음)을 그대로 비춘다 — 회벽·전돌에 하늘색 광택이 얹혀 하얗게 뜬다.
    /// 실제로 회벽(석회)과 전돌은 무광에 가까우므로 0.50 자체가 과하다.
    ///
    /// ── 왜 조명이 아니라 재질을 손대나 ──
    /// SkyPreset 은 마을·은하담·견우마을이 함께 쓰는 공용 에셋이라 거기서 앰비언트를 낮추면 다른 씬이
    /// 같이 어두워진다. RenderSettings.reflectionIntensity 는 씬 로컬이지만 지형·식생까지 싸잡아 바꾼다.
    /// → **재질 복제본**으로 구조물에만 적용한다. 원본 .mat 은 절대 수정하지 않는다.
    ///
    /// ── ★한 번 크게 망가뜨린 방식 (되풀이 금지) ──
    /// 처음엔 복제본의 임포터 userData 에 원본 GUID 만 적고, 복제하면서 그 자리에서
    /// SaveAndReimport() 를 불렀다. 두 가지가 겹쳐 터졌다:
    ///   ① 동헌 재질은 **FBX 내장 서브에셋**이라 GUID 만으로는 되찾을 수 없다
    ///      (LoadAssetAtPath&lt;Material&gt;(.fbx) 는 null). 복제본이 스스로를 원본으로 여겨
    ///      메뉴를 누를 때마다 색이 겹쳐 곱해졌다 (1.00 → 0.88 → … → 0.10).
    ///   ② 루프 도중 재임포트가 돌면서 슬롯 대응이 어긋나, 동헌 19개 렌더러의 서로 다른 재질이
    ///      **전부 하나(Changho_Cutout)로 뭉개졌다.** 프리팹 오버라이드 되돌리기로 복구했다.
    /// → 지금은 (1) 대응표를 에셋(GwanaToneMap)에 명시적으로 남기고
    ///          (2) 수집 → 복제 → 배정 **3패스로 분리**해 루프 도중 에셋을 건드리지 않는다.
    /// </summary>
    public static class GwanaToneTuner
    {
        const string ToneDir = "Assets/_Project/Gyeonu/Art/Materials/GwanaTone";
        const string MapPath = ToneDir + "/GwanaToneMap.asset";
        const string Suffix = "_관아톤";

        /// <summary>톤 단계. smoothCap = 광택 상한(하늘 반사량), colorMul = 알베도 배율.</summary>
        public struct Level
        {
            public string name; public float smoothCap; public float colorMul;
            public Level(string n, float s, float c) { name = n; smoothCap = s; colorMul = c; }
        }

        public static readonly Level[] Levels =
        {
            new Level("0_원본", -1f, 1f),          // 복제본 해제 — 전달본 그대로
            new Level("1_약",   0.20f, 1.00f),     // 광택만 무광으로 (하늘 반사 제거)
            new Level("2_중",   0.14f, 0.90f),
            new Level("3_강",   0.10f, 0.80f),
        };

        /// <summary>톤을 입힐 대상 루트. 사람(문지기)과 지형·식생은 제외한다.</summary>
        static readonly string[] Roots = { "관아_건물", "관아_소품" };

        [MenuItem("Tools/이문록/관아 ▸ 톤 — 0 원본", priority = 500)] static void L0() => Apply(0);
        [MenuItem("Tools/이문록/관아 ▸ 톤 — 1 약", priority = 501)] static void L1() => Apply(1);
        [MenuItem("Tools/이문록/관아 ▸ 톤 — 2 중", priority = 502)] static void L2() => Apply(2);
        [MenuItem("Tools/이문록/관아 ▸ 톤 — 3 강", priority = 503)] static void L3() => Apply(3);

        public static void Apply(int levelIndex)
        {
            var lv = Levels[Mathf.Clamp(levelIndex, 0, Levels.Length - 1)];
            var targets = Collect();
            if (targets.Count == 0) { Debug.LogError("[관아] 톤 대상 렌더러를 찾지 못했다"); return; }
            var map = LoadOrCreateMap();

            // ── 1패스: 슬롯마다 원본을 확정한다 (에셋은 건드리지 않는다) ──
            var plan = new List<Material[]>(targets.Count);
            var distinct = new List<Material>();
            foreach (var r in targets)
            {
                var cur = r.sharedMaterials;
                var src = new Material[cur.Length];
                for (int i = 0; i < cur.Length; i++)
                {
                    src[i] = Resolve(cur[i], map);
                    if (src[i] != null && !distinct.Contains(src[i])) distinct.Add(src[i]);
                }
                plan.Add(src);
            }

            // ── 2패스: 복제본을 만들고 값을 굽는다 ──
            var clone = new Dictionary<Material, Material>();
            if (lv.smoothCap >= 0f)
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetFullPath(ToneDir));
                foreach (var s in distinct) clone[s] = MakeClone(s, lv, map);
                EditorUtility.SetDirty(map);
                AssetDatabase.SaveAssets();
            }

            // ── 3패스: 배정 ──
            int slots = 0;
            for (int t = 0; t < targets.Count; t++)
            {
                var r = targets[t];
                var src = plan[t];
                var mats = new Material[src.Length];
                bool changed = false;
                var cur = r.sharedMaterials;
                for (int i = 0; i < src.Length; i++)
                {
                    mats[i] = src[i] == null ? cur[i]
                            : (lv.smoothCap < 0f ? src[i] : clone[src[i]]);
                    if (mats[i] != cur[i]) changed = true;
                    slots++;
                }
                if (changed) { r.sharedMaterials = mats; EditorUtility.SetDirty(r); }
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[관아] 톤 {lv.name} — 렌더러 {targets.Count} / 슬롯 {slots} / 원본 재질 {distinct.Count}"
                    + (lv.smoothCap < 0f ? " (원본 복귀)" : $" / 복제 {clone.Count} (광택≤{lv.smoothCap:F2}, 색×{lv.colorMul:F2})"));
        }

        static List<Renderer> Collect()
        {
            var list = new List<Renderer>();
            foreach (var rootName in Roots)
            {
                var root = GameObject.Find(rootName);
                if (root == null) continue;
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    // 문지기(나졸)는 사람이고 URP/Unlit 이라 반사와 무관 — 건드리지 않는다
                    bool isGuard = false;
                    for (var t = r.transform; t != null; t = t.parent) if (t.name == "문지기") isGuard = true;
                    if (isGuard) continue;
                    list.Add(r);
                }
            }
            return list;
        }

        /// <summary>복제본이면 대응표에서 원본으로, 아니면 그대로. 못 찾으면 null(그 슬롯은 건드리지 않는다).</summary>
        static Material Resolve(Material m, GwanaToneMap map)
        {
            if (m == null) return null;
            string p = AssetDatabase.GetAssetPath(m);
            if (string.IsNullOrEmpty(p) || !p.StartsWith(ToneDir)) return m;
            var src = map.SourceOf(m);
            if (src == null) Debug.LogWarning("[관아] 대응표에 없는 복제 재질 — 그대로 둔다: " + p);
            return src;
        }

        static GwanaToneMap LoadOrCreateMap()
        {
            var map = AssetDatabase.LoadAssetAtPath<GwanaToneMap>(MapPath);
            if (map == null)
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetFullPath(ToneDir));
                map = ScriptableObject.CreateInstance<GwanaToneMap>();
                AssetDatabase.CreateAsset(map, MapPath);
            }
            return map;
        }

        static Material MakeClone(Material src, Level lv, GwanaToneMap map)
        {
            string path = ToneDir + "/" + src.name + Suffix + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(src); AssetDatabase.CreateAsset(m, path); }
            else { m.shader = src.shader; m.CopyPropertiesFromMaterial(src); }

            // ① 광택 — 하늘 큐브맵 반사가 밝기의 절반을 만든다. 상한을 씌워 무광으로.
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", Mathf.Min(src.GetFloat("_Smoothness"), lv.smoothCap));
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", Mathf.Min(src.GetFloat("_Glossiness"), lv.smoothCap));
            // ② 알베도 — 남은 밝기를 차분하게. 알파는 건드리지 않는다(컷아웃 재질 보호)
            if (m.HasProperty("_BaseColor"))
            {
                var c = src.GetColor("_BaseColor");
                m.SetColor("_BaseColor", new Color(c.r * lv.colorMul, c.g * lv.colorMul, c.b * lv.colorMul, c.a));
            }
            if (m.HasProperty("_Color"))
            {
                var c = src.GetColor("_Color");
                m.SetColor("_Color", new Color(c.r * lv.colorMul, c.g * lv.colorMul, c.b * lv.colorMul, c.a));
            }

            map.Record(m, src);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
