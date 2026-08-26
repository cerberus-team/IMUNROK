using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 성하리 하늘 설정 창.
    /// - 위쪽: 프로젝트의 모든 스카이박스 머티리얼을 드롭다운/이전·다음 버튼으로 갈아끼우며 비교
    /// - 아래쪽: SkyPreset(낮_맑음 / 낮_흐림 / 밤) 버튼 한 번으로 조명·안개까지 일괄 적용
    /// </summary>
    public class SkySetupWindow : EditorWindow
    {
        const string PresetFolder = "Assets/_Project/Gyeonu/Art/Lighting";

        List<Material> _skyboxMats = new List<Material>();
        string[] _skyboxNames = new string[0];
        int _index = -1;
        List<SkyPreset> _presets = new List<SkyPreset>();
        Vector2 _scroll;

        [MenuItem("Tools/이문록/성하리 하늘 설정")]
        static void Open() => GetWindow<SkySetupWindow>("성하리 하늘");

        /// <summary>
        /// 씬 뷰의 안개 "표시"만 끄고 켠다 (에디터 전용).
        /// RenderSettings를 건드리지 않으므로 씬이 더러워지지 않고, Game 뷰/빌드에는 영향 없음.
        /// </summary>
        [MenuItem("Tools/이문록/씬 뷰 안개 토글")]
        static void ToggleSceneViewFog()
        {
            var sv = SceneView.lastActiveSceneView;
            SetSceneViewFog(sv == null || !sv.sceneViewState.showFog);
        }

        static void SetSceneViewFog(bool on)
        {
            foreach (SceneView v in SceneView.sceneViews)
            {
                v.sceneViewState.showFog = on;
                v.Repaint();
            }
        }

        void OnEnable()
        {
            RefreshSkyboxes();
            RefreshPresets();
        }

        void RefreshSkyboxes()
        {
            _skyboxMats = AssetDatabase.FindAssets("t:Material", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<Material>)
                .Where(m => m != null && m.shader != null && m.shader.name.StartsWith("Skybox"))
                .OrderBy(AssetDatabase.GetAssetPath)
                .ToList();

            _skyboxNames = _skyboxMats.Select(m =>
            {
                var parts = AssetDatabase.GetAssetPath(m).Split('/');
                var pack = parts.Length > 1 ? parts[1] : "?";
                return pack + "/" + m.name;   // "/"로 팩별 서브메뉴 그룹핑
            }).ToArray();

            _index = _skyboxMats.IndexOf(RenderSettings.skybox);
        }

        void RefreshPresets()
        {
            _presets = AssetDatabase.FindAssets("t:SkyPreset")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<SkyPreset>)
                .Where(p => p != null)
                .OrderBy(p => p.name)
                .ToList();
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            // ── 0. 씬 뷰 안개 토글 (에디터 표시 전용) ────────────
            var sv = SceneView.lastActiveSceneView;
            bool fogShown = sv == null || sv.sceneViewState.showFog;
            bool newFog = EditorGUILayout.ToggleLeft("씬 뷰 안개 표시 (에디터 전용 — 씬 데이터·Game 뷰에 영향 없음)", fogShown);
            if (newFog != fogShown)
                SetSceneViewFog(newFog);
            EditorGUILayout.Space(8);

            // ── 1. 스카이박스 비교 ──────────────────────────────
            EditorGUILayout.LabelField("스카이박스 비교", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("현재:", RenderSettings.skybox != null ? RenderSettings.skybox.name : "(없음)");

            int picked = EditorGUILayout.Popup("선택", _index, _skyboxNames);
            if (picked != _index && picked >= 0)
                ApplySkybox(picked);

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = _skyboxMats.Count > 0;
            if (GUILayout.Button("◀ 이전"))
                ApplySkybox((_index - 1 + _skyboxMats.Count) % _skyboxMats.Count);
            if (GUILayout.Button("다음 ▶"))
                ApplySkybox((_index + 1) % _skyboxMats.Count);
            GUI.enabled = true;
            if (GUILayout.Button("목록 새로고침", GUILayout.Width(100)))
                RefreshSkyboxes();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(12);

            // ── 2. 날씨 프리셋 ─────────────────────────────────
            EditorGUILayout.LabelField("날씨 프리셋 (조명·안개·스카이박스 일괄 적용)", EditorStyles.boldLabel);
            if (_presets.Count == 0)
                EditorGUILayout.HelpBox(PresetFolder + " 에 SkyPreset 에셋이 없습니다.", MessageType.Info);

            foreach (var preset in _presets)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(preset.name, GUILayout.Width(160)))
                    ApplyPreset(preset);
                GUI.enabled = false;
                EditorGUILayout.ObjectField(preset, typeof(SkyPreset), false);
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("프리셋 새로고침", GUILayout.Width(100)))
                RefreshPresets();

            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "프리셋 값을 고치려면 에셋을 선택해 인스펙터에서 수정한 뒤 다시 버튼을 누르세요.\n" +
                "적용 후 씬 저장(Ctrl+S)을 해야 값이 유지됩니다.", MessageType.None);

            EditorGUILayout.EndScrollView();
        }

        void ApplySkybox(int i)
        {
            _index = i;
            RenderSettings.skybox = _skyboxMats[i];
            DynamicGI.UpdateEnvironment();
            MarkDirty();
        }

        void ApplyPreset(SkyPreset preset)
        {
            var sun = FindSun();
            if (sun != null)
                Undo.RecordObjects(new Object[] { sun, sun.transform }, "하늘 프리셋 적용");
            preset.Apply(sun);
            _index = _skyboxMats.IndexOf(RenderSettings.skybox);
            MarkDirty();
        }

        static Light FindSun()
        {
            if (RenderSettings.sun != null)
                return RenderSettings.sun;
            return FindObjectsByType<Light>(FindObjectsSortMode.None)
                .FirstOrDefault(l => l.type == LightType.Directional);
        }

        static void MarkDirty()
        {
            if (!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
    }
}
