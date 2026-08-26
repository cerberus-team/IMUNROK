using UnityEngine;
using UnityEngine.XR;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>손에서 뻗은 광선으로 짚는다</b> — 마우스가 하던 그 일을 컨트롤러가 한다.
    ///
    /// 짚는 <b>방식</b>만 갈아 끼우고 짚히는 <b>쪽</b>은 그대로다. 방아쇠를 당기면
    /// <see cref="ISelectable.OnSelect"/>, 당기고 있으면 <see cref="IHoldable.OnHoldTick"/> —
    /// 마우스 왼쪽 단추가 하던 것과 한 글자도 다르지 않다. 그래서 재를 헤집는 일도,
    /// 서랍을 빼는 일도, 문 한 짝을 미는 일도 VR 에서 저절로 된다.
    ///
    /// <b>짚은 점을 적어 둔다</b>(<see cref="Pointing"/>). 두 짝 문처럼 한 물건 안에서도
    /// 짚은 자리에 따라 하는 일이 다른 것이 있기 때문이다.
    ///
    /// 광선은 <b>가리키는 동안만</b> 보인다. 늘 두 줄기가 뻗어 있으면 방을 보는 것이
    /// 아니라 광선을 보게 된다.
    /// </summary>
    public class VRRaySelector : MonoBehaviour
    {
        [Tooltip("어느 손인가")]
        [SerializeField] private XRNode _hand = XRNode.RightHand;

        [Tooltip("이 거리까지 짚는다(m)")]
        [SerializeField] private float _maxDistance = 20f;

        [Tooltip("짚을 수 있는 레이어")]
        [SerializeField] private LayerMask _mask = ~0;

        [Tooltip("광선을 그릴 줄. 비우면 안 그린다")]
        [SerializeField] private LineRenderer _line;

        [Tooltip("아무것도 안 짚을 때 광선 길이(m)")]
        [SerializeField] private float _idleLength = 0.6f;

        private ISelectable _current;
        private IHoldable _holding;
        private bool _wasPressed;
        private VRUiRay _ui;

        private void Awake() { _ui = GetComponent<VRUiRay>(); }

        private void Update()
        {
            Ray ray = new Ray(transform.position, transform.forward);
            bool pressed = Trigger();

            // ── 화면의 단추와 방의 물건 가운데 <b>가까운 쪽</b>을 짚는다 ──
            //
            // 심문 판은 사람 앞에 떠 있고 그 너머에 심문받는 사람이 서 있다. 둘이
            // 한 광선 위에 놓이므로 어느 쪽을 짚은 것인지 가려야 한다. 눈에 가까운
            // 것을 짚는 것이 사람의 셈이다 — 판 너머의 사람을 짚으려면 판을 비켜서
            // 겨눈다.
            float uiAt = _ui != null ? _ui.Pick(ray) : float.PositiveInfinity;

            ISelectable hit = null;
            Vector3 end = ray.origin + ray.direction * _idleLength;
            float worldAt = float.PositiveInfinity;

            if (Physics.Raycast(ray, out RaycastHit info, _maxDistance, _mask))
            {
                worldAt = info.distance;
                hit = info.collider.GetComponentInParent<ISelectable>();
                end = info.point;
            }

            if (uiAt < worldAt)
            {
                Clear();
                _ui.Drive(pressed);
                _wasPressed = pressed;
                Draw(true, _ui.Point);
                return;
            }
            if (_ui != null) _ui.Clear();

            // 종이를 쥐고 있는 동안에는 방을 짚지 않는다 — 마우스 쪽과 같은 규칙이다.
            // <b>단추는 위에서 이미 짚었다.</b> 종이의 내려놓기도 단추이므로, 이 문을
            // 단추보다 앞에 두면 종이를 편 채로는 내려놓을 수가 없어진다.
            if (DocumentView.IsOpen) { Clear(); Draw(false, Vector3.zero); _wasPressed = pressed; return; }

            if (hit != null || worldAt < float.PositiveInfinity)
                Pointing.Set(end, info.collider != null ? info.collider.transform : null);

            // 도구를 익히는 동안에는 손이 방으로 가지 않는다(자막의 닫기 표만 예외)
            if (ToolTutorial.Learning && !(hit is NoticeCloseTab)) hit = null;

            if (!ReferenceEquals(hit, _current))
            {
                ReleaseHold();
                _current?.OnHoverExit();
                _current = hit;
                _current?.OnHoverEnter();
            }

            var holdable = _current as IHoldable;
            if (holdable != null && !holdable.HoldReady) holdable = null;

            if (holdable != null && pressed)
            {
                if (!_wasPressed && TouchRefusal.Blocks(_current as Component)) { _wasPressed = true; Draw(hit != null, end); return; }
                _holding = holdable;
                holdable.OnHoldTick(Time.deltaTime);
                _wasPressed = pressed;
                Draw(true, end);
                return;
            }
            ReleaseHold();

            if (pressed && !_wasPressed && holdable == null)
            {
                if (!TouchRefusal.Blocks(_current as Component))
                {
                    FaceThePlayer.Notify(_current as Component);
                    _current?.OnSelect();
                }
            }
            _wasPressed = pressed;
            Draw(hit != null, end);
        }

        /// <summary>
        /// <b>마지막으로 방아쇠를 당긴 손.</b> 손을 울릴 때(<see cref="Haptics"/>) 쓴다 —
        /// 양손을 다 울리면 어느 손으로 쳤는지 몸이 헷갈린다.
        /// </summary>
        public static XRNode LastHand { get; private set; } = XRNode.RightHand;

        /// <summary>방아쇠를 당기고 있나. 검지 방아쇠와 손아귀 중 어느 쪽이든 친다.</summary>
        private bool Trigger()
        {
            var dev = InputDevices.GetDeviceAtXRNode(_hand);
            if (!dev.isValid) return false;
            if (dev.TryGetFeatureValue(CommonUsages.triggerButton, out bool t) && t) { LastHand = _hand; return true; }
            if (dev.TryGetFeatureValue(CommonUsages.gripButton, out bool g) && g) { LastHand = _hand; return true; }
            return false;
        }

        private void Draw(bool onSomething, Vector3 end)
        {
            if (_line == null) return;
            _line.enabled = true;
            _line.positionCount = 2;
            _line.SetPosition(0, transform.position);
            _line.SetPosition(1, onSomething ? end : transform.position + transform.forward * _idleLength);
            var c = onSomething ? new Color(1f, 0.86f, 0.55f, 0.85f) : new Color(1f, 1f, 1f, 0.18f);
            _line.startColor = c; _line.endColor = new Color(c.r, c.g, c.b, c.a * 0.15f);
        }

        private void Clear()
        {
            ReleaseHold();
            _current?.OnHoverExit();
            _current = null;
        }

        private void ReleaseHold()
        {
            if (_holding == null) return;
            _holding.OnHoldRelease();
            _holding = null;
        }

        /// <summary>밖에서 만들 때 줄을 물려 준다.</summary>
        public void Bind(XRNode hand, LineRenderer line) { _hand = hand; _line = line; }
    }
}
