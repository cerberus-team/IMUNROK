using UnityEngine;
using UnityEngine.Rendering;

namespace IMUNROK.Common
{
    /// <summary>
    /// 대화(심문) 연출:
    ///  · 심문이 열리면 카메라 FOV를 좁혀 인물에 "당겨" 초점을 준다(배경 덜 보임).
    ///  · 밤 분위기 볼륨(비네트·피사계심도)의 weight를 올려 배경을 흐리고 인물을 띄운다.
    ///  · 대화가 끝나면 둘 다 부드럽게 원래대로 복귀.
    ///
    /// 붙이는 곳: 아무 상시 오브젝트(예: _연출_Volume). 볼륨을 _volume에 연결하면 됨.
    /// 설치는 [이문록 ▸ 연출: 대화 카메라·밤 분위기 설치]가 자동으로 해준다.
    /// ※ VR 빌드에서는 FOV를 바꾸면 멀미가 나므로 _zoomCamera를 꺼두기.
    /// </summary>
    [DisallowMultipleComponent]
    public class ConversationView : MonoBehaviour
    {
        [Header("카메라 초점")]
        [Tooltip("평상시 FOV(0이면 시작할 때 카메라의 현재 값을 사용)")]
        [SerializeField] private float _normalFov = 0f;
        [Tooltip("대화 시 좁힐 FOV(작을수록 인물 크게·배경 적게)")]
        [SerializeField] private float _talkFov = 42f;
        [SerializeField] private float _fovEase = 4f;
        [Tooltip("VR에서는 끄기(FOV 변경은 멀미 유발)")]
        [SerializeField] private bool _zoomCamera = true;

        [Header("분위기 볼륨(비네트·DOF)")]
        [SerializeField] private Volume _volume;
        [Range(0f, 1f)] [SerializeField] private float _idleWeight = 0.12f;
        [Range(0f, 1f)] [SerializeField] private float _talkWeight = 1f;
        [SerializeField] private float _weightEase = 4f;

        [Header("대화 조명(인물 밝히기)")]
        [Tooltip("말 걸 때 켜지는 은은한 빛(보통 카메라 자식 Point Light). 없으면 사용 안 함")]
        [SerializeField] private Light _talkLight;
        [Tooltip("대화 시 조명 밝기(어두우면 키우기)")]
        [SerializeField] private float _lightIntensity = 3f;
        [SerializeField] private float _lightEase = 5f;

        private Camera _cam;

        private void Start()
        {
            _cam = Camera.main;
            if (_normalFov <= 0f && _cam != null) _normalFov = _cam.fieldOfView;
            if (_volume != null) _volume.weight = _idleWeight;
            if (_talkLight != null) { _talkLight.enabled = true; _talkLight.intensity = 0f; }
        }

        private void LateUpdate()   // 카메라 이동 스크립트 뒤에 실행되도록 LateUpdate
        {
            if (_cam == null) { _cam = Camera.main; if (_cam == null) return; }
            bool talk = InterrogationController.AnyOpen;
            float dt = Time.deltaTime;

            if (_zoomCamera && _normalFov > 0f)
            {
                float k = 1f - Mathf.Exp(-_fovEase * dt);
                _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, talk ? _talkFov : _normalFov, k);
            }

            if (_volume != null)
            {
                float wk = 1f - Mathf.Exp(-_weightEase * dt);
                _volume.weight = Mathf.Lerp(_volume.weight, talk ? _talkWeight : _idleWeight, wk);
            }

            if (_talkLight != null)
            {
                float lk = 1f - Mathf.Exp(-_lightEase * dt);
                _talkLight.intensity = Mathf.Lerp(_talkLight.intensity, talk ? _lightIntensity : 0f, lk);
            }
        }
    }
}
