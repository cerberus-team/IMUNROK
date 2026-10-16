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
            // 모델까지 넘긴다 — 크기도 공통이다. 조사청의 등불이 옹고집전 것의
            // 절반이었고(8 대 21), 같은 방 문갑에 놓인 등불보다도 작았다.
            if (_useCommonPose) HeldRig.Apply(transform, _toolId, _model != null ? _model.transform : null);
            if (_hangFromHook) HangFromHook();
        }

        [Tooltip("<b>걸린 자리에서 돌게 한다.</b> 모델의 중심이 몸통 한가운데 있으면 " +
                 "손이 돌 때 등불이 <b>제 배꼽을 축으로</b> 빙 돈다 — 매달린 물건은 " +
                 "걸린 데를 축으로 돌아야 한다. 켜면 모델을 재어 <b>윗면 한가운데</b>가 " +
                 "이 오브젝트의 원점에 오도록 밀어 준다. 조사청 등불이 어긋나 보이면 켜 볼 것")]
        [SerializeField] private bool _hangFromHook = false;

        /// <summary>
        /// <b>매달린 자리를 축으로 삼는다.</b>
        ///
        /// 등불은 고리에 걸려 있으므로 도는 축이 <b>고리</b>다. 그런데 모델의 원점은
        /// 대개 몸통 한가운데(또는 발치)에 찍혀 오므로, 그것을 그대로 손에 물리면
        /// 손이 돌 때 등불이 제 몸 한가운데를 축으로 돈다 — 들고 도는 것이 아니라
        /// <b>제자리에서 구르는</b> 것으로 보인다.
        ///
        /// 그려지는 몸을 재어 <b>윗면 한가운데</b>가 이 오브젝트의 원점에 오도록 민다.
        /// 이미 고리가 원점에 있는 모델이면 미는 양이 0 에 가까우므로 아무 일도 없다.
        /// </summary>
        private void HangFromHook()
        {
            if (_model == null) return;

            // <b>월드 상자(Renderer.bounds)로 재면 안 된다.</b> 그것은 축에 나란한 상자라,
            // 고개를 숙이거나 돌린 채로 재면 몸보다 큰 상자가 잡히고 「윗면 한가운데」도
            // 몸의 고리가 아닌 데를 가리킨다 — 조사청에서 2.7cm 어긋났고, 그 값이
            // <b>Awake 때 카메라가 어디를 보고 있었나</b>에 따라 매번 달라졌다.
            // 이 오브젝트 자리에서 메시를 직접 재면 어느 쪽을 보든 같은 값이 나온다.
            Bounds b = default(Bounds);
            bool any = false;
            foreach (var mf in _model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                Bounds mb = mf.sharedMesh.bounds;
                Matrix4x4 toHere = transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                for (int c = 0; c < 8; c++)
                {
                    Vector3 corner = mb.center + Vector3.Scale(mb.extents,
                        new Vector3((c & 1) == 0 ? -1f : 1f, (c & 2) == 0 ? -1f : 1f, (c & 4) == 0 ? -1f : 1f));
                    Vector3 pt = toHere.MultiplyPoint3x4(corner);
                    if (!any) { b = new Bounds(pt, Vector3.zero); any = true; }
                    else b.Encapsulate(pt);
                }
            }
            if (!any) return;

            // 윗면 한가운데 — 이미 이 오브젝트 자리로 잰 값이다
            Vector3 local = new Vector3(b.center.x, b.max.y, b.center.z);

            // ① 몸을 내려 고리가 이 오브젝트의 원점에 오게 한다 — 이제 축이 고리다.
            _model.transform.localPosition -= local;

            // ② <b>내린 만큼 매단 자리를 올린다.</b> 이것을 안 하면 축만 바꾼 것이 아니라
            //    등불이 통째로 내려앉는다 — 조사청에서 재어 보니 몸이 26cm 인데 10.6cm 를
            //    내려 화면 아래로 빠졌다. 바꾸려는 것은 <b>도는 자리</b>이지 보이는 자리가
            //    아니다. 올려 주면 눈에 보이는 등불은 한 치도 안 움직이고, 손이 돌 때
            //    도는 중심만 배꼽에서 고리로 옮겨 간다.
            Vector3 deltaWorld = transform.TransformVector(local);
            transform.localPosition += transform.parent != null
                ? transform.parent.InverseTransformVector(deltaWorld)
                : deltaWorld;

            // ③ 종이 뒤로 들어 올리는 자리도 같이 내린다 — 몸의 기준이 내려갔으므로
            //    그대로 두면 올리는 거리가 내린 만큼 더 길어진다.
            _upPose -= local;

            DevLog.Note("[등불] 걸린 자리를 축으로 맞췄다 — 몸을 " + (-local).ToString("F3") +
                        " 만큼 내리고 매단 자리를 그만큼 올렸다(보이는 자리는 그대로).", this);
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

            // 종이 뒤로 넘어가는 움직임.
            //
            if (_model != null)
            {
                if (!_restTaken) { _restPose = _model.transform.localPosition; _restTaken = true; }
                _raise = Mathf.MoveTowards(_raise, want ? _wantRaise : 0f, _raiseSpeed * Time.deltaTime);
                _model.transform.localPosition = Vector3.Lerp(_restPose, _upPose, Mathf.SmoothStep(0f, 1f, _raise));
            }
        }
    }
}
