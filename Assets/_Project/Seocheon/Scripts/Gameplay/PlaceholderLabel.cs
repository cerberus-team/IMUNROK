using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace IMUNROK.Seocheon
{
    public sealed class PlaceholderLabel : MonoBehaviour
    {
        [Tooltip("Scene 뷰에 오브젝트 이름을 표시합니다.")]
        [SerializeField] private bool showLabel = true;

        [Tooltip("오브젝트 원점에서 이름표까지의 높이입니다.")]
        [SerializeField, Min(0f)] private float labelHeight = 2f;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!showLabel)
                return;

            Handles.Label(transform.position + Vector3.up * labelHeight, gameObject.name);
        }
#endif
    }
}
