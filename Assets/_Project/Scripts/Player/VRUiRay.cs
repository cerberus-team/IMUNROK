using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>광선으로 화면의 단추를 누른다.</b>
    ///
    /// 헤드셋을 꽂고 나서 드러난 두 번째 구멍이다. 첫째는 단추가 <b>키보드</b>에 매여
    /// 있던 것이었고(<see cref="VRButtons"/>), 둘째가 이것이다 — <b>화면에 그려진 단추</b>가
    /// 통째로 죽어 있었다. 심문의 질문 고르기도, 증거 내밀기도, 수첩의 단서 카드도,
    /// 판결의 낙관 찍기도, 지도의 가는 곳도, 도구벨트의 칸도 전부 유니티의 UI 단추다.
    /// 열다섯 자리가 있고 <b>한 자리도 안 눌렸다</b>.
    ///
    /// <b>왜 안 눌렸나</b>: 유니티는 UI 단추를 <b>마우스가 있는 화면 좌표</b>로 짚는다.
    /// 헤드셋을 쓰면 마우스는 책상 위 어딘가에 그대로 있고, 그 자리는 판이 있는 데가
    /// 아니다. 그래서 오류 한 줄 없이 조용히 아무 일도 안 난다 — 이 게임에서 가장
    /// 자주 밟은 함정이고(EventSystem 이 없던 조사청도 같은 꼴이었다), 이번에는
    /// 판이 있는데 <b>짚는 손</b>이 없는 쪽이었다.
    ///
    /// <b>어떻게 짚나</b>: 마우스 흉내를 내지 않는다. 카메라를 거쳐 화면 좌표로 옮기면
    /// 판이 <b>눈 밖에 있을 때</b> 도로 안 짚히고, 그 함정은 이미 한 번 밟았다.
    /// 그냥 <b>광선과 판을 직접 만난다</b>:
    ///
    ///   ㉠ 월드 공간 판(Canvas)에 붙은 그림 가운데 <b>짚을 수 있다고 표시된 것</b>만 본다
    ///   ㉡ 광선이 그 그림의 면과 만나는 점을 구하고, 그 점이 네모 안에 드는지 본다
    ///   ㉢ 가장 가까운 것을 고른다. 겹쳐 있으면 <b>나중에 그린 것</b>이 위다
    ///
    /// 그러고 나서 유니티가 마우스에게 보내던 것과 <b>똑같은 기별</b>을 보낸다
    /// (들어옴·나감·눌림·놓임·눌렀다놓음). 그래야 단추가 물드는 것도, onClick 에
    /// 걸어 둔 일도 고친 데 없이 그대로 돈다 — 단추 열다섯 자리를 하나씩 손볼 일이 없다.
    ///
    /// <b>손이 안다.</b> 단추에 얹히면 살짝, 누르면 조금 세게 울린다. 판은 허공에
    /// 떠 있어서 눌린 줄을 손이 모르는데, 울림이 그 자리를 대신한다.
    ///
    /// <b>짚는 것은 하나다.</b> 방에 있는 물건(<see cref="ISelectable"/>)과 화면의 단추가
    /// 한 광선 위에 같이 놓일 수 있으므로, 어느 쪽이 <b>더 가까운가</b>로 가린다.
    /// 그 판가름은 <see cref="VRRaySelector"/> 가 한다 — 여기서는 얼마나 먼지만 답한다.
    ///
    /// <see cref="VRRig"/> 가 손을 지을 때 같이 붙인다.
    /// </summary>
    public class VRUiRay : MonoBehaviour
    {
        /// <summary>지금 광선이 단추 위에 얹혀 있나.</summary>
        public bool OnUi { get { return _target != null; } }

        /// <summary>
        /// <b>어느 손이든</b> 단추를 겨누고 있나. 방아쇠에 다른 일을 걸어 둔 데서
        /// 본다 — 단추를 누르려고 당긴 방아쇠가 엉뚱한 일까지 하면 안 된다.
        /// 손이 둘이라 한 손이 지운 것을 다른 손이 다시 세우지 않도록 칸으로 센다.
        /// </summary>
        public static bool AnyOnUi
        {
            get { return _uiFrame >= Time.frameCount - 1; }
        }
        private static int _uiFrame = -9;

        /// <summary>광선이 판에 닿은 자리. 광선을 거기서 끊어 그린다.</summary>
        public Vector3 Point { get; private set; }

        private XRNode _hand = XRNode.RightHand;
        private Graphic _target;
        private GameObject _hover;
        private GameObject _press;
        private PointerEventData _data;
        private bool _wasPressed;

        /// <summary>세우는 쪽이 어느 손인지 일러 준다 — 얹힌 손이 울려야 한다.</summary>
        public void Bind(XRNode hand) { _hand = hand; }

        private void Awake()
        {
            _data = new PointerEventData(EventSystem.current);

            // 마우스가 이 판들을 같이 짚고 있으면 얹힘이 서로 지워져 단추가 깜빡인다.
            // 헤드셋을 쓰고 있을 때 책상 마우스는 어차피 손이 닿지 않는 데 있으므로,
            // 짚는 일은 광선 한 쪽에 맡긴다(EventSystem 자체는 그대로 둔다 — 누가
            // 골라져 있는지는 그쪽이 들고 있다).
            var es = EventSystem.current;
            if (es != null)
            {
                var module = es.GetComponent<BaseInputModule>();
                if (module != null && module.enabled) module.enabled = false;
            }
        }

        // ─────────────────────────────────────────────
        //  ㉠ 짚을 수 있는 그림들 — 자주 세지 않는다
        // ─────────────────────────────────────────────

        private static readonly List<Graphic> _all = new List<Graphic>();
        private static float _countedAt = -99f;

        /// <summary>
        /// 판은 열렸다 닫혔다 한다. 매 칸 세면 비싸고, 한 번만 세면 새로 열린 판을
        /// 못 본다. 눈 깜짝할 만큼(0.2초)마다 다시 센다 — 판을 여는 것은 사람의 짓이라
        /// 그 사이에 못 짚어도 사람은 모른다.
        /// </summary>
        private static void Recount()
        {
            if (Time.unscaledTime - _countedAt < 0.2f) return;
            _countedAt = Time.unscaledTime;
            _all.Clear();
            _all.AddRange(Object.FindObjectsByType<Graphic>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        }

        // ─────────────────────────────────────────────
        //  ㉡ 얼마나 먼가 — 중재는 VRRaySelector 가 한다
        // ─────────────────────────────────────────────

        /// <summary>
        /// 이 광선이 닿는 가장 가까운 단추를 골라 두고 그 거리를 답한다.
        /// 아무 데도 안 닿으면 <see cref="float.PositiveInfinity"/>.
        /// </summary>
        public float Pick(Ray ray)
        {
            Recount();

            _target = null;
            float best = float.PositiveInfinity;
            int bestDepth = int.MinValue;
            Vector3 bestPoint = Vector3.zero;

            for (int i = 0; i < _all.Count; i++)
            {
                var g = _all[i];
                if (g == null || !g.raycastTarget || !g.isActiveAndEnabled) continue;

                var canvas = g.canvas;
                if (canvas == null || !canvas.isActiveAndEnabled) continue;
                if (canvas.renderMode != RenderMode.WorldSpace) continue;   // 헤드셋에는 월드 판만 보인다
                if (Muted(g.transform)) continue;

                var rt = g.transform as RectTransform;
                if (rt == null) continue;

                // 광선과 판의 면이 만나는 점
                Vector3 n = rt.forward;
                float denom = Vector3.Dot(n, ray.direction);
                if (Mathf.Abs(denom) < 1e-5f) continue;                     // 면과 나란하다
                float t = Vector3.Dot(n, rt.position - ray.origin) / denom;
                if (t <= 0.02f) continue;                                   // 등 뒤

                // 겹친 것 가리기: 더 가까운 쪽, 같은 자리면 <b>나중에 그린</b> 쪽
                bool nearer = t < best - 0.001f;
                bool sameDepthCloser = t < best + 0.001f && g.depth > bestDepth;
                if (!nearer && !sameDepthCloser) continue;

                Vector3 p = ray.origin + ray.direction * t;
                Vector3 local = rt.InverseTransformPoint(p);
                if (!rt.rect.Contains(new Vector2(local.x, local.y))) continue;

                best = t; bestDepth = g.depth; _target = g; bestPoint = p;
            }

            Point = bestPoint;
            if (_target == null) return float.PositiveInfinity;
            _uiFrame = Time.frameCount;
            return best;
        }

        /// <summary>
        /// 이 그림이 <b>지금은 못 짚는</b> 것인가. 도구벨트처럼 스러졌다 떠오르는 판은
        /// CanvasGroup 의 투명도만 내려 두고 오브젝트는 켜 둔 채다 — 안 보이는 판이
        /// 광선을 가로채면, 허공을 짚었는데 어디선가 단추가 눌린다.
        /// </summary>
        private static bool Muted(Transform t)
        {
            while (t != null)
            {
                var grp = t.GetComponent<CanvasGroup>();
                if (grp != null)
                {
                    if (!grp.blocksRaycasts || grp.alpha < 0.1f) return true;
                    if (grp.ignoreParentGroups) return false;
                }
                t = t.parent;
            }
            return false;
        }

        // ─────────────────────────────────────────────
        //  ㉢ 마우스가 보내던 것과 똑같은 기별을 보낸다
        // ─────────────────────────────────────────────

        /// <summary>골라 둔 단추에 얹힘·눌림을 전한다.</summary>
        public void Drive(bool pressed)
        {
            var go = _target != null ? _target.gameObject : null;
            _data.position = ScreenGuess();
            _data.button = PointerEventData.InputButton.Left;

            Hover(go);

            if (pressed && !_wasPressed) Down(go);
            else if (!pressed && _wasPressed) Up(go);
            _wasPressed = pressed;
        }

        /// <summary>광선이 판에서 떠났다. 눌러 잡고 있던 것도 <b>누른 것으로 치지 않고</b> 놓는다.</summary>
        public void Clear()
        {
            _target = null;
            Hover(null);
            if (_press != null)
            {
                ExecuteEvents.Execute(_press, _data, ExecuteEvents.pointerUpHandler);
                _press = null;
            }
            _wasPressed = false;
        }

        private void Hover(GameObject go)
        {
            var enter = go != null ? ExecuteEvents.GetEventHandler<IPointerEnterHandler>(go) : null;
            if (enter == _hover) return;

            if (_hover != null) ExecuteEvents.Execute(_hover, _data, ExecuteEvents.pointerExitHandler);
            _hover = enter;
            if (_hover != null)
            {
                ExecuteEvents.Execute(_hover, _data, ExecuteEvents.pointerEnterHandler);
                Haptics.TapOn(_hand, 0.18f, 0.02f);      // 얹혔다 — 살짝
            }
        }

        private void Down(GameObject go)
        {
            if (go == null) return;
            _data.pressPosition = _data.position;
            _press = ExecuteEvents.ExecuteHierarchy(go, _data, ExecuteEvents.pointerDownHandler);
            if (_press == null) _press = ExecuteEvents.GetEventHandler<IPointerClickHandler>(go);
            if (_press != null) Haptics.TapOn(_hand, 0.55f, 0.04f);   // 눌렸다 — 조금 세게
        }

        private void Up(GameObject go)
        {
            if (_press == null) return;
            ExecuteEvents.Execute(_press, _data, ExecuteEvents.pointerUpHandler);

            // 누른 자리에서 <b>떼야</b> 누른 것이다. 눌러 놓고 손이 미끄러져 다른 단추에서
            // 떼면 아무 일도 안 난다 — 마우스와 같은 규칙이고, 허공에 뜬 판에서는
            // 손이 흔들리기 마련이라 이 규칙이 더 요긴하다.
            var click = go != null ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(go) : null;
            if (click == _press) ExecuteEvents.Execute(_press, _data, ExecuteEvents.pointerClickHandler);
            _press = null;
        }

        /// <summary>
        /// 화면 좌표는 <b>짚는 데 쓰지 않는다</b>. 다만 몇몇 UI 부품이 이 값을 들여다보므로
        /// 그럴듯한 자리를 하나 넣어 준다 — 눈으로 본 그 자리다.
        /// </summary>
        private Vector2 ScreenGuess()
        {
            var cam = Camera.main;
            if (cam == null) return Vector2.zero;
            Vector3 s = cam.WorldToScreenPoint(Point);
            return new Vector2(s.x, s.y);
        }
    }
}
