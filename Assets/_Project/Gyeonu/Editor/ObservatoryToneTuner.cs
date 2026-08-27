using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 관측실 색감 프리셋 (2026-08-10) — 전체는 차갑고 불 주변만 따뜻한 대비가 목표.
    /// 앰비언트·안개·석재/바닥 머티리얼 Base Color만 만진다 (구조·배치·텍스처 불변).
    /// 등잔·사방등 라이트 색은 건드리지 않는다 — 불빛이 파랄 수는 없다.
    /// 목조 돔은 나무라 완전히 차갑게 하면 어색해서 절반 강도로만 식힌다.
    ///
    /// ⚠️ ObservatoryBuilder.Build()를 재실행하면 웜톤 원래 값으로 덮인다 —
    /// 프리셋 확정 시 Build()의 머티리얼 상수에 채택값을 반영할 것.
    /// </summary>
    public static class ObservatoryToneTuner
    {
        const string MatDir = "Assets/_Project/Gyeonu/Art/Materials/Observatory";

        struct Tone
        {
            public Color ambient, fog;
            public Color wall, floor, pattern, wood, frame;
        }

        static readonly Tone Original = new Tone
        {
            ambient = new Color(0.045f, 0.043f, 0.058f),
            fog = new Color(0.010f, 0.010f, 0.016f),
            wall = new Color(0.72f, 0.70f, 0.67f),
            floor = new Color(0.50f, 0.50f, 0.52f),
            pattern = new Color(0.26f, 0.26f, 0.31f),
            wood = new Color(0.60f, 0.54f, 0.48f),
            frame = new Color(0.40f, 0.35f, 0.30f),
        };

        static readonly Tone Weak = new Tone
        {
            ambient = new Color(0.045f, 0.058f, 0.100f),
            fog = new Color(0.012f, 0.016f, 0.032f),
            wall = new Color(0.62f, 0.66f, 0.70f),
            floor = new Color(0.44f, 0.47f, 0.55f),
            pattern = new Color(0.22f, 0.24f, 0.32f),
            wood = new Color(0.55f, 0.52f, 0.50f),
            frame = new Color(0.36f, 0.34f, 0.31f),
        };

        static readonly Tone Mid = new Tone
        {
            ambient = new Color(0.085f, 0.110f, 0.190f),
            fog = new Color(0.018f, 0.026f, 0.055f),
            wall = new Color(0.52f, 0.60f, 0.72f),
            floor = new Color(0.38f, 0.44f, 0.58f),
            pattern = new Color(0.18f, 0.22f, 0.33f),
            wood = new Color(0.50f, 0.49f, 0.50f),
            frame = new Color(0.33f, 0.32f, 0.32f),
        };

        static readonly Tone Strong = new Tone
        {
            ambient = new Color(0.140f, 0.180f, 0.310f),
            fog = new Color(0.026f, 0.038f, 0.085f),
            wall = new Color(0.42f, 0.54f, 0.74f),
            floor = new Color(0.30f, 0.40f, 0.60f),
            pattern = new Color(0.14f, 0.19f, 0.34f),
            wood = new Color(0.44f, 0.45f, 0.52f),
            frame = new Color(0.29f, 0.30f, 0.34f),
        };

        [MenuItem("Tools/이문록/관측실 톤/원래 (웜)")] public static void ApplyOriginal() => Apply(Original, "원래(웜)");
        [MenuItem("Tools/이문록/관측실 톤/쿨 약")] public static void ApplyWeak() => Apply(Weak, "쿨 약");
        [MenuItem("Tools/이문록/관측실 톤/쿨 중")] public static void ApplyMid() => Apply(Mid, "쿨 중");
        [MenuItem("Tools/이문록/관측실 톤/쿨 강")] public static void ApplyStrong() => Apply(Strong, "쿨 강");

        static void Apply(Tone t, string label)
        {
            SetColor("M_관측실_석벽", t.wall);
            SetColor("M_관측실_바닥", t.floor);
            SetColor("M_관측실_바닥무늬", t.pattern);
            SetColor("M_관측실_돔판재", t.wood);
            SetColor("M_관측실_돔골조", t.frame);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = t.ambient;
            RenderSettings.fogColor = t.fog;
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[관측실 톤] {label} 적용 — 앰비언트 {t.ambient}, 벽 {t.wall}. 프로브 재굽기 권장");
        }

        static void SetColor(string matName, Color c)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/{matName}.mat");
            if (m == null) { Debug.LogWarning($"[관측실 톤] 머티리얼 없음: {matName}"); return; }
            m.SetColor("_BaseColor", c);
            EditorUtility.SetDirty(m);
        }
    }
}
