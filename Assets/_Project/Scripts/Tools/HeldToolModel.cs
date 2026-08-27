using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 손에 든 도구 모델 — 도구벨트에서 해당 id를 들었을 때만 모델이 보인다(빛 없음).
    ///  · 등불처럼 빛이 필요하면 LanternController를 쓰고, 돋보기처럼 모델만이면 이걸 쓴다.
    /// 붙이는 곳: 카메라(또는 손) 자식으로 둔 도구 모델 오브젝트.
    ///
    /// <b>쥐는 순간 한 번 흔들린다</b>: 물건을 손에 쥐면 매달린 것들이 흔들린다 —
    /// 술이 그렇고 고리가 그렇다. 여태는 눈에 <b>댈 때만</b> 흔들렸는데, 그러면
    /// 도구를 바꿔 든 순간에는 아무 일도 없어서 물건이 <b>나타난다</b>. 쥐는 것이
    /// 아니라 켜지는 것이다. 여기서 한 번 흔들어 주면 그 순간이 손짓이 된다.
    ///
    /// 이 일은 <b>도구를 가리지 않는다</b>. 모델 밑에 Animator 와 그 이름의 상태가
    /// 있으면 흔들고, 없으면 그냥 넘어간다. 팀원이 도구를 하나 더 만들어 붙여도
    /// 여기를 고칠 일이 없다.
    /// </summary>
    public class HeldToolModel : MonoBehaviour
    {
        [Tooltip("이 id를 손에 들었을 때만 모델이 보임 (예: magnify)")]
        [SerializeField] private string _toolId = "magnify";
        [SerializeField] private GameObject _model;

        [Header("쥐는 순간")]
        [Tooltip("쥘 때 한 번 틀 동작 이름. 모델에 Animator 가 없거나 이 상태가 없으면 그냥 넘어간다")]
        [SerializeField] private string _takeState = "Tassel_Lift";
        [Tooltip("비우면 모델에서 Animator 를 찾아 쓴다")]
        [SerializeField] private Animator _animator;
        [Tooltip("끄면 쥐어도 흔들리지 않는다")]
        [SerializeField] private bool _swingOnTake = true;

        /// <summary>이 소품이 대신하는 도구 id. 진짜 렌즈(<see cref="MagnifierLens"/>)가 겹치는지 볼 때 쓴다.</summary>
        public string ToolId => _toolId;

        private bool _shown;

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
            // 모델까지 넘긴다 — 크기도 공통이다. 돋보기가 40cm 나 되어 눈앞
            // 32cm 에 들면 화면의 반을 가렸다.
            if (_useCommonPose) HeldRig.Apply(transform, _toolId, _model != null ? _model.transform : null);
        }

        private void Start()
        {
            if (_model != null) _model.SetActive(false);
            _shown = false;
        }

        private void Update()
        {
            bool want = ToolbeltHud.SelectedToolId == _toolId;
            if (_model == null) return;

            if (_model.activeSelf != want) _model.SetActive(want);

            // 안 보이다가 보이게 된 <b>그 한 순간</b>에만 흔든다. 매 프레임 켜져 있는
            // 동안 트는 것이 아니다 — 그러면 든 내내 술이 파닥거린다.
            if (want && !_shown) Swing();
            _shown = want;
        }

        private void Swing()
        {
            if (!_swingOnTake || string.IsNullOrEmpty(_takeState)) return;
            if (_animator == null) _animator = _model.GetComponentInChildren<Animator>(true);
            if (_animator == null || _animator.runtimeAnimatorController == null) return;
            if (!_animator.HasState(0, Animator.StringToHash(_takeState))) return;
            _animator.Play(_takeState, 0, 0f);
        }
    }
}
