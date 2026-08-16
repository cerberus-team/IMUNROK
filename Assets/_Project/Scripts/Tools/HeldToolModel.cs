using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 손에 든 도구 모델 — 도구벨트에서 해당 id를 들었을 때만 모델이 보인다(빛 없음).
    ///  · 등불처럼 빛이 필요하면 LanternController를 쓰고, 돋보기처럼 모델만이면 이걸 쓴다.
    /// 붙이는 곳: 카메라(또는 손) 자식으로 둔 도구 모델 오브젝트.
    /// </summary>
    public class HeldToolModel : MonoBehaviour
    {
        [Tooltip("이 id를 손에 들었을 때만 모델이 보임 (예: magnify)")]
        [SerializeField] private string _toolId = "magnify";
        [SerializeField] private GameObject _model;

        private void Start()
        {
            if (_model != null) _model.SetActive(false);
        }

        private void Update()
        {
            bool want = ToolbeltHud.SelectedToolId == _toolId;
            if (_model != null && _model.activeSelf != want) _model.SetActive(want);
        }
    }
}
