using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 은하담 물 설정 창. SkySetupWindow와 같은 패턴 —
    /// WaterPreset(물_맑음/물_흐림) 버튼 한 번으로 수면 상태를 전환한다.
    /// 에디트 모드에선 공유 머티리얼(에셋)에 적용되고, 플레이 중엔 런타임 인스턴스에 적용된다.
    /// </summary>
    public class WaterSetupWindow : EditorWindow
    {
        List<WaterPreset> _presets = new List<WaterPreset>();

        [MenuItem("Tools/이문록/은하담 물 설정")]
        static void Open() => GetWindow<WaterSetupWindow>("은하담 물");

        void OnEnable() => Refresh();

        void Refresh()
        {
            _presets = AssetDatabase.FindAssets("t:WaterPreset")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<WaterPreset>)
                .Where(p => p != null)
                .OrderBy(p => p.name)
                .ToList();
        }

        void OnGUI()
        {
            var water = FindObjectsByType<WaterSurface>(FindObjectsSortMode.None).FirstOrDefault();
            if (water == null)
            {
                EditorGUILayout.HelpBox(
                    "씬에 WaterSurface가 없습니다.\nTools ▸ 이문록 ▸ 은하담 조립을 먼저 실행하세요.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("현재 프리셋:", water.preset != null ? water.preset.name : "(없음)");
            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField("물 상태 전환", EditorStyles.boldLabel);
            if (_presets.Count == 0)
                EditorGUILayout.HelpBox("WaterPreset 에셋이 없습니다. 은하담 조립을 실행하면 생성됩니다.", MessageType.Info);

            foreach (var p in _presets)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(p.name, GUILayout.Width(160)))
                    Apply(water, p);
                GUI.enabled = false;
                EditorGUILayout.ObjectField(p, typeof(WaterPreset), false);
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("프리셋 새로고침", GUILayout.Width(100)))
                Refresh();

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "런타임 전환은 게임 코드에서 waterSurface.ApplyPreset(preset) 호출.\n" +
                "프리셋 값을 고치려면 에셋을 인스펙터에서 수정한 뒤 다시 버튼을 누르세요.",
                MessageType.None);
        }

        void Apply(WaterSurface water, WaterPreset preset)
        {
            if (Application.isPlaying)
            {
                water.ApplyPreset(preset);
                return;
            }

            var mat = water.GetComponent<Renderer>().sharedMaterial;
            if (mat != null)
            {
                Undo.RecordObject(mat, "물 프리셋 적용");
                preset.ApplyTo(mat);
                EditorUtility.SetDirty(mat);
            }
            Undo.RecordObject(water, "물 프리셋 적용");
            water.preset = preset;
            EditorUtility.SetDirty(water);
        }
    }
}
