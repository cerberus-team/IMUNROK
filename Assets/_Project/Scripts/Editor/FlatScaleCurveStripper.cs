using UnityEditor;
using UnityEngine;

namespace IMUNROK.Common.Editor
{
    /// <summary>
    /// 애니메이션 클립에서 <b>변하지 않는 크기 곡선</b>을 들여올 때 걷어낸다.
    ///
    /// 왜 필요한가: 복동의 클립 다섯이 한 FBX 에 같이 들어 있는데,
    /// 뼈 'target_character' 의 크기를 양반걷기는 100 으로, 나머지 넷은 1 로 박아 두었다.
    /// 씬에 세워 둔 값이 100 이라, 양반걷기일 때만 키가 1.86m 이고
    /// 걷기·문열기·일어서기로 바뀌는 순간 100분의 1(2cm)로 쪼그라들었다.
    /// "양반걸음 하다가 갑자기 사라진다"던 것이 이것이다.
    ///
    /// 값이 처음부터 끝까지 같은 크기 곡선은 동작이 아니라 <b>내보낼 때 딸려온 단위</b>다.
    /// 애니메이션이 크기를 건드릴 이유가 없으니 지운다. 그러면 씬에 세워 둔 크기가
    /// 그대로 살아 있고, 어느 클립으로 바뀌든 키가 변하지 않는다.
    ///
    /// 진짜로 크기가 변하는 연출(부풀거나 줄어드는 동작)은 곡선 값이 일정하지 않으므로
    /// 여기서 건드리지 않는다.
    ///
    /// 원본 FBX 는 손대지 않는다 — 들여오는 결과만 바꾸므로, 이 파일을 지우고
    /// 다시 들여오면 원래대로 돌아온다.
    /// </summary>
    public class FlatScaleCurveStripper : AssetPostprocessor
    {
        private void OnPostprocessAnimation(GameObject root, AnimationClip clip)
        {
            int removed = 0;
            float value = 1f;

            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (!binding.propertyName.StartsWith("m_LocalScale")) continue;

                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null || curve.keys.Length == 0) continue;

                // 값이 한 번이라도 달라지면 진짜 연출이다 — 놔둔다
                float first = curve.keys[0].value;
                bool flat = true;
                for (int i = 1; i < curve.keys.Length; i++)
                {
                    if (Mathf.Abs(curve.keys[i].value - first) > 0.0001f) { flat = false; break; }
                }
                if (!flat) continue;

                AnimationUtility.SetEditorCurve(clip, binding, null);
                removed++;
                value = first;
            }

            if (removed > 0)
                Debug.Log($"[크기곡선정리] {System.IO.Path.GetFileName(assetPath)} ▸ {clip.name} " +
                          $": 늘 {value:F2} 이던 크기 곡선 {removed}개를 걷어냈습니다. " +
                          "씬에 세워 둔 크기가 그대로 살아 있게 됩니다.");
        }
    }
}
