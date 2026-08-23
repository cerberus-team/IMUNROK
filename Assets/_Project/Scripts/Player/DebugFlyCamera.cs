using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IMUNROK.Common
{
    /// <summary>
    /// 에디터/비-VR 테스트용 카메라. 두 모드(비행/걷기)를 Tab으로 전환.
    ///
    ///  ● 비행(Fly) — 자유롭게 날며 둘러보기(배치 확인용)
    ///  ● 걷기(Walk) — 사람처럼 눈높이 고정, 수평으로만 이동(잠행 동선 테스트용)
    ///
    /// [조작] — 마우스 오른쪽 버튼(RMB)을 "누르고 있는 동안"만 이동/회전
    ///   Tab            : 비행 ↔ 걷기 전환
    ///   G              : "바로 아래 바닥으로 내려서기" → 그 바닥을 걷는 높이로 잡음(걷기모드 자동 전환)
    ///   RMB + 마우스   : 시점 회전
    ///   RMB + W/A/S/D  : 이동
    ///   RMB + E / Q    : (비행 모드만) 위 / 아래
    ///   RMB + Shift    : 빠르게
    ///
    /// 화면 좌하단에 현재 모드/높이가 표시된다. RMB를 떼면 멈춘다(좌클릭 선택과 안 겹침).
    /// ※ G(바닥 내려서기)는 바닥에 Collider가 있어야 작동. 집 부품엔 대부분 Mesh Collider가 있음.
    /// </summary>
    public class DebugFlyCamera : MonoBehaviour
    {
        [SerializeField] private float _moveSpeed = 3f;
        [Tooltip("픽셀당 회전 각도")]
        [SerializeField] private float _lookSpeed = 0.12f;
        [Tooltip("Shift로 빨라지는 배수")]
        [SerializeField] private float _sprintMultiplier = 3f;
        [Tooltip("걷기 모드로 시작할지")]
        [SerializeField] private bool _walkMode = false;
        [Tooltip("걷기/바닥내려서기 시 눈높이(바닥으로부터). 선 사람의 눈높이다 — " +
                 "몸으로 막는 캡슐도 이 값에서 나오므로, 이것만 맞으면 눈과 몸이 어긋나지 않는다")]
        [SerializeField] private float _eyeHeight = 1.7f;

        [Header("몸 — 통과하지 않게")]
        [Tooltip("끄면 예전처럼 벽이고 문이고 다 통과한다(배치 확인용)")]
        [SerializeField] private bool _solid = true;
        [Tooltip("몸의 굵기(반지름 m). 이보다 좁은 틈은 못 지나간다")]
        [SerializeField] private float _bodyRadius = 0.26f;

        [Header("턱 오르내림")]
        [Tooltip("한 번에 올라설 수 있는 턱 높이(m).\n" +
                 "이 값은 <b>몸이 무엇을 뚫고 가는지</b>도 함께 정한다 — 윗면이 이 안에 드는 것은 " +
                 "걸음이 넘어설 수 있는 턱으로 보아 막지 않는다. 0.62 로 두었더니 경상이며 " +
                 "문갑 위로 걸어 올라갔다. 댓돌을 잘게 나누고 이 값을 낮춰, 밟고 오를 것과 " +
                 "부딪힐 것을 갈랐다")]
        [SerializeField] private float _stepUp = 0.40f;
        [Tooltip("발밑을 얼마나 아래까지 훑을지(m). 계단을 내려갈 때 쓴다")]
        [SerializeField] private float _stepDown = 2.0f;
        [Tooltip("턱을 오르내리는 속도(m/s). 즉시 붙으면 화면이 튄다")]
        [SerializeField] private float _stepSpeed = 4f;

        /// <summary>
        /// 참이면 걸음만 막힌다 — 둘러보기는 그대로다.
        ///
        /// 앉아 있는 동안 쓴다(<see cref="PlayerSeat"/>). 부품을 통째로 끄지 않는 까닭:
        /// 그러면 시점 회전까지 죽어 방 안을 볼 수 없고, 다시 켤 때 각도를 잃는다.
        /// 앉은 사람은 못 걸을 뿐 고개는 돌린다.
        /// </summary>
        [System.NonSerialized] public bool MoveLocked;

        private float _yaw;
        private float _pitch;
        private float _walkY;
        private GUIStyle _hud;
        private Quaternion _applied;      // 우리가 마지막으로 쓴 회전

        private void Start()
        {
            SyncAngles();
            _walkY = transform.position.y;
        }

        /// <summary>
        /// 지금 보고 있는 방향을 각도로 다시 읽는다.
        ///
        /// 왜 필요한가: 이 부품은 yaw·pitch 를 <b>제가 들고</b> 그것으로 회전을 만든다.
        /// 그래서 바깥에서 시점을 돌려 놓아도(순간이동으로 사랑방에 들어서며 복동 쪽을
        /// 보게 한다든지) 들고 있는 각도는 옛것 그대로다. 도착한 순간에는 제대로 보고 있다가,
        /// 둘러보려고 마우스를 누르는 순간 옛 각도로 홱 돌아가 버린다 — 방에 들어서면
        /// 엉뚱한 쪽을 보고 있다는 것이 이것이었다.
        /// </summary>
        public void SyncAngles()
        {
            Vector3 e = transform.eulerAngles;
            _yaw = e.y;
            _pitch = e.x > 180f ? e.x - 360f : e.x;    // 350도는 -10도다
            _applied = transform.rotation;
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null) return;

            // 남이 시점을 돌려 놨으면 그것을 받아들인다. 매 프레임 견주므로
            // 부르는 쪽이 따로 알려 줄 필요가 없다.
            if (Quaternion.Angle(transform.rotation, _applied) > 0.05f) SyncAngles();

            // Tab: 비행 ↔ 걷기 (현재 높이를 눈높이로 고정)
            if (!MoveLocked && kb.tabKey.wasPressedThisFrame)
            {
                _walkMode = !_walkMode;
                // 걷기로 들어설 때는 <b>바닥에 내려선다</b>.
                //
                // 여태 그때의 높이를 그대로 눈높이로 삼았다. 날아다니던 높이가 곧
                // 걷는 높이가 되니, Tab 을 누른 자리에 따라 사람 키가 매번 달랐다 —
                // 서 있는 높이와 걷는 높이가 어긋난다던 것이 이것이다.
                // 걷는다는 것은 바닥을 딛는 일이므로 바닥에서 눈높이만큼 위가 맞다.
                if (_walkMode && Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down,
                                                 out var floor, 200f, ~0, QueryTriggerInteraction.Ignore))
                {
                    _walkY = floor.point.y + _eyeHeight;
                    Vector3 q = transform.position; q.y = _walkY; transform.position = q;
                }
                else _walkY = transform.position.y;
            }

            // G: 바로 아래 바닥으로 내려서서 그 높이를 걷는 눈높이로
            if (!MoveLocked && kb.gKey.wasPressedThisFrame)
            {
                if (Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, out var hit, 200f))
                {
                    _walkMode = true;
                    _walkY = hit.point.y + _eyeHeight;
                    Vector3 p0 = transform.position; p0.y = _walkY; transform.position = p0;
                }
            }

            if (!mouse.rightButton.isPressed) return;

            // 시점 회전
            Vector2 delta = mouse.delta.ReadValue();
            _yaw += delta.x * _lookSpeed;
            _pitch -= delta.y * _lookSpeed;
            _pitch = Mathf.Clamp(_pitch, -89f, 89f);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            _applied = transform.rotation;

            if (MoveLocked) return;      // 앉아 있다 — 여기까지만. 고개는 이미 돌렸다

            float speed = _moveSpeed * (kb.leftShiftKey.isPressed ? _sprintMultiplier : 1f);

            if (_walkMode)
            {
                // 남이 나를 옮겼으면(순간이동) 그 높이를 받아들인다.
                // 이걸 안 하면 다음 줄에서 _walkY 로 되돌려 놓아, 마루로 올라간 순간
                // 다시 마당 높이로 끌어내려 머리가 바닥 밑에 처박힌다 — 화면이 캄캄해지는 원인이었다.
                if (Mathf.Abs(transform.position.y - _walkY) > 0.02f) _walkY = transform.position.y;

                // 수평(yaw 기준)으로만 이동, 높이 고정
                Vector3 fwd = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
                Vector3 right = Quaternion.Euler(0f, _yaw, 0f) * Vector3.right;
                Vector3 move = Vector3.zero;
                if (kb.wKey.isPressed) move += fwd;
                if (kb.sKey.isPressed) move -= fwd;
                if (kb.dKey.isPressed) move += right;
                if (kb.aKey.isPressed) move -= right;

                transform.position += Slide(move.normalized * speed * Time.deltaTime);

                // 발밑에서 "짧게" 아래로 쏴서 바닥을 따라감(지붕·처마로 튀지 않게).
                // 광선을 발보다 _stepUp 만큼만 위에서 시작한다 — 그보다 높은 턱은 아예 안 보이므로
                // 디딤돌을 밟고 담장 위로 기어오르는 일이 생기지 않는다.
                float feetY = transform.position.y - _eyeHeight;
                Vector3 origin = new Vector3(transform.position.x, feetY + _stepUp, transform.position.z);
                if (Physics.Raycast(origin, Vector3.down, out var gh, _stepUp + _stepDown, ~0, QueryTriggerInteraction.Ignore))
                {
                    float targetY = gh.point.y + _eyeHeight;
                    if (targetY - _walkY <= _stepUp + 0.01f)                    // 오를 수 있는 턱만
                        _walkY = Mathf.MoveTowards(_walkY, targetY, _stepSpeed * Time.deltaTime);
                }

                Vector3 p = transform.position;
                p.y = _walkY;
                transform.position = p;
            }
            else
            {
                Vector3 dir = Vector3.zero;
                if (kb.wKey.isPressed) dir += Vector3.forward;
                if (kb.sKey.isPressed) dir += Vector3.back;
                if (kb.aKey.isPressed) dir += Vector3.left;
                if (kb.dKey.isPressed) dir += Vector3.right;
                if (kb.eKey.isPressed) dir += Vector3.up;
                if (kb.qKey.isPressed) dir += Vector3.down;
                transform.Translate(dir.normalized * speed * Time.deltaTime, Space.Self);
            }
#endif
        }

        /// <summary>
        /// 한 걸음을 <b>몸으로 막고 벽을 따라 미끄러뜨린다</b>.
        ///
        /// <b>여태 몸이 없었다.</b> 걸음은 그냥 transform.position 에 더해지고 있었고,
        /// 아래로 쏘는 광선은 <b>발밑 높이</b>를 따라갈 뿐 앞을 막지 않았다. 벽이며
        /// 문이며 세간이며 다 통과하던 것이 이것이다 — 콜라이더를 아무리 붙여도
        /// 그것을 물어보는 데가 없었으니 아무 일도 일어나지 않았다.
        ///
        /// 사람 굵기의 캡슐을 걸음 방향으로 쓸어 본다. 무언가에 닿으면 그 벽면을 따라
        /// 걸음을 눕혀(투영) 미끄러진다 — 벽에 비스듬히 부딪혔을 때 멈춰 서지 않고
        /// 벽을 훑으며 나아가는 것이 걷는 느낌이다. 세 번까지 되풀이하는 까닭은
        /// 구석에서 두 벽에 동시에 닿기 때문이다.
        ///
        /// <b>낮은 턱은 몸으로 치지 않는다</b>. 윗면이 발에서 <see cref="_stepUp"/> 안에
        /// 드는 것은 걸음이 넘어설 수 있는 것이므로 그냥 지나가게 두고, 높이 따라가기가
        /// 알아서 올려 준다. 이 값이 너무 크면 경상이며 문갑 위로 걸어 올라가게 되고,
        /// 너무 작으면 댓돌에 걸려 못 오른다.
        /// </summary>
        private Vector3 Slide(Vector3 step)
        {
            if (!_solid || step.sqrMagnitude < 1e-8f) return step;

            float feetY = transform.position.y - _eyeHeight;
            const float Skin = 0.02f;

            for (int pass = 0; pass < 3 && step.sqrMagnitude > 1e-8f; pass++)
            {
                // 몸통 캡슐 — 넘어설 수 있는 턱보다 위부터 눈 바로 아래까지
                // 캡슐은 <b>눈높이에서 나온다</b>. 발밑에서 넘어설 수 있는 턱만큼 띄운
                // 자리가 밑이고, 눈이 곧 정수리 언저리이므로 그 바로 아래가 위다.
                // 두 값이 다른 데서 오면 눈은 벽 너머를 보는데 몸은 안 지나가는 일이 난다.
                Vector3 low = new Vector3(transform.position.x, feetY + _stepUp + _bodyRadius, transform.position.z);
                Vector3 high = new Vector3(transform.position.x, feetY + _eyeHeight - _bodyRadius, transform.position.z);
                if (high.y < low.y) high = low;

                float dist = step.magnitude;
                var hits = Physics.CapsuleCastAll(low, high, _bodyRadius, step.normalized,
                                                  dist + Skin, ~0, QueryTriggerInteraction.Ignore);
                if (hits.Length == 0) break;

                RaycastHit best = default;
                float bestD = float.MaxValue;
                foreach (var h in hits)
                {
                    if (h.collider == null) continue;
                    if (h.collider.transform.IsChildOf(transform)) continue;   // 손에 든 것
                    // 바닥·천장은 걸음을 막지 않는다. 막는 것은 <b>서 있는 면</b>이다.
                    Vector3 n = h.normal; n.y = 0f;
                    if (n.sqrMagnitude < 0.04f) continue;
                    if (h.distance < bestD) { bestD = h.distance; best = h; }
                }
                if (bestD == float.MaxValue) break;

                Vector3 wall = best.normal; wall.y = 0f;
                if (wall.sqrMagnitude < 1e-6f) return Vector3.zero;
                step = Vector3.ProjectOnPlane(step, wall.normalized);
            }
            return step;
        }

        private void OnGUI()
        {
            if (_hud == null)
                _hud = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };
            string mode = MoveLocked ? "<color=#fc8>앉음</color>"
                        : _walkMode ? "<color=#8f8>걷기</color>" : "<color=#8cf>비행</color>";
            string keys = MoveLocked ? "(앉아 있는 동안은 둘러보기만)" : "(Tab 전환 · G 바닥내려서기)";
            GUI.Label(new Rect(12, Screen.height - 46, 520, 22),
                $"카메라: {mode}  {keys}  y={transform.position.y:0.0}", _hud);
        }
    }
}
