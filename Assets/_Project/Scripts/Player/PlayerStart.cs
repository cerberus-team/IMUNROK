using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 이 오브젝트 위치를 "플레이어 시작 지점"으로 삼는다.
    /// 씬 시작 시 메인 카메라를 이 지점의 위치·방향으로 옮긴다(그래서 여기서 시작).
    ///  · 마커(캡슐 등)에 붙이면, 그 마커가 곧 시작 위치가 된다.
    ///  · 시작할 때 이 오브젝트의 보이는 메시는 자동으로 꺼서 시야를 안 가린다.
    ///
    /// 카메라를 이 지점에 세운다.
    /// </summary>
    public class PlayerStart : MonoBehaviour
    {
        [Tooltip("눈높이 보정(캡슐 중심 → 눈높이). 카메라 y를 이만큼 올림")]
        [SerializeField] private float _eyeHeightOffset = 0.7f;

        // DebugFlyCamera가 Start에서 각도를 읽기 전에 옮기도록 Awake에서 처리
        private void Awake()
        {
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 pos = transform.position + Vector3.up * _eyeHeightOffset;
                cam.transform.SetPositionAndRotation(pos, transform.rotation);
            }

            // 시작 지점 마커가 화면을 가리지 않게 렌더러 끔
            var r = GetComponent<Renderer>();
            if (r != null) r.enabled = false;
        }
    }
}
