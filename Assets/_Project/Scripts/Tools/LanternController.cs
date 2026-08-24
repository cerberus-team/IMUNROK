using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 등불(燈): "등불"을 손에 든 동안(도구벨트 SelectedToolId == "lantern")만 켜진다.
    ///  · 켤 때는 부드럽게 밝아지고, 다른 도구로 바꾸면 즉시 꺼진다(잔상 없음).
    ///  · 빛은 Unity Light, 모델은 발광 재질로 스스로 빛나 보임.
    /// 한 손에 하나 원칙: 별도 토글 키 없음 — 손 도구 전환(Q/휠)으로만 켜고 끈다.
    /// </summary>
    public class LanternController : MonoBehaviour
    {
        [SerializeField] private GameObject _model;   // 등불 모델(들었을 때만 보임)
        [SerializeField] private Light _light;
        [Tooltip("켰을 때 빛 밝기")]
        [SerializeField] private float _onIntensity = 3f;
        [Tooltip("켤 때 밝아지는 부드러움")]
        [SerializeField] private float _ease = 8f;
        [SerializeField] private string _toolId = "lantern";

        [Header("쥐는 순간")]
        [Tooltip("쥘 때 한 번 틀 동작 이름. Animator 가 없거나 그 상태가 없으면 그냥 넘어간다")]
        [SerializeField] private string _takeState = "Tassel_Lift";
        [Tooltip("비우면 모델에서 Animator 를 찾아 쓴다")]
        [SerializeField] private Animator _animator;
        [Tooltip("끄면 쥐어도 흔들리지 않는다")]
        [SerializeField] private bool _swingOnTake = true;

        [Header("종이 뒤로 들어 올리기")]
        [Tooltip("들어 올렸을 때 소품이 갈 자리(카메라 기준). 종이는 0.6m 앞에 있으므로 " +
                 "그보다 멀고 조금 위라야 <b>종이 너머</b>로 보인다")]
        [SerializeField] private Vector3 _upPose = new Vector3(0f, 0.26f, 0.88f);
        [Tooltip("올리고 내리는 빠르기")]
        [SerializeField] private float _raiseSpeed = 6f;

        private bool _shown;
        private float _raise;            // 0 = 평소 자리, 1 = 종이 뒤
        private float _wantRaise;
        private Vector3 _restPose;
        private bool _restTaken;

        /// <summary>
        /// 등불을 종이 뒤로 들어 올린다(0~1). <see cref="LanternReveal"/> 가 부른다.
        ///
        /// 소품을 <b>옮기는</b> 까닭: 종이가 밝아지는 것만으로는 무엇이 밝힌 것인지
        /// 알 수 없다. 등불이 종이 너머로 넘어가 거기서 빛이 오는 것이 보여야,
        /// 배접 속이 비치는 일이 <b>빛을 뒤에 넣어서</b>임이 눈으로 읽힌다.
        /// </summary>
        public void SetRaise(float t) { _wantRaise = Mathf.Clamp01(t); }

        [Tooltip("끄면 씬에 맞춰 둔 자리를 그대로 쓴다. 켜져 있으면 공통 자세(HeldRig)를 받아 앉는다")]
        [SerializeField] private bool _useCommonPose = true;

        /// <summary>
        /// 손에 드는 자리를 <b>공통</b>에서 받아 앉는다(HeldRig).
        ///
        /// Start 가 아니라 Awake 인 까닭: 돋보기 렌즈 장치(<see cref="MagnifierLens"/>)가
        /// 씬이 올라온 뒤 이 매단 자리를 <b>재어 두고</b> 그것을 드는 자세로 삼는다.
        /// Start 에서 옮기면 이미 잰 뒤라 옛 자리가 그대로 굳는다.
        /// </summary>
        private void Awake()
        {
            if (_useCommonPose) HeldRig.Apply(transform, _toolId);
        }

        private void Start()
        {
            if (_light != null) _light.intensity = 0f;
            if (_model != null) _model.SetActive(false);
            _shown = false;
        }

        /// <summary>
        /// 쥐는 순간 한 번 흔든다 — 매달린 것이 있는 물건은 손에 들면 흔들린다.
        /// 없으면 물건이 쥐어지는 것이 아니라 <b>켜진다</b>.
        /// <see cref="HeldToolModel"/> 와 같은 일이다. 등불은 빛을 지녀 그쪽을 못 쓰므로
        /// 여기에도 같은 손짓을 둔다.
        /// </summary>
        private void Swing()
        {
            if (!_swingOnTake || string.IsNullOrEmpty(_takeState) || _model == null) return;
            if (_animator == null) _animator = _model.GetComponentInChildren<Animator>(true);
            if (_animator == null || _animator.runtimeAnimatorController == null) return;
            if (!_animator.HasState(0, Animator.StringToHash(_takeState))) return;
            _animator.Play(_takeState, 0, 0f);
        }

        private void Update()
        {
            bool want = ToolbeltHud.SelectedToolId == _toolId;

            if (_light != null)
            {
                if (want)
                {
                    float k = 1f - Mathf.Exp(-_ease * Time.deltaTime);
                    _light.intensity = Mathf.Lerp(_light.intensity, _onIntensity, k);
                }
                else
                {
                    _light.intensity = 0f;   // 즉시 꺼서 잔상 없음
                }
            }

            if (_model != null && _model.activeSelf != want) _model.SetActive(want);

            // 안 보이다가 보이게 된 <b>그 한 순간</b>에만 흔든다.
            if (want && !_shown) Swing();
            _shown = want;

            // 종이 뒤로 넘어가는 움직임
            if (_model != null)
            {
                if (!_restTaken) { _restPose = _model.transform.localPosition; _restTaken = true; }
                _raise = Mathf.MoveTowards(_raise, want ? _wantRaise : 0f, _raiseSpeed * Time.deltaTime);
                _model.transform.localPosition = Vector3.Lerp(_restPose, _upPose, Mathf.SmoothStep(0f, 1f, _raise));
            }
        }
    }
}
