using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IMUNROK.Common
{
    /// <summary>
    /// 개발용 카메라. 두 모드(비행/걷기)를 Tab 으로 전환.
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
        /// <b>바닥을 딛고 선다</b> — 발밑을 찾아 그 위 눈높이에 맞춘다.
        ///
        /// Tab 으로 걷기에 들어설 때 하는 일과 같다. 밖에서 카메라를 옮겨 놓은 뒤
        /// (들어서는 연출처럼) 부르면, 옮겨 놓은 높이가 아니라 <b>바닥에서 잰</b>
        /// 높이로 다시 선다. 연출이 끝나고 시야가 내려앉아 있던 것이 이것이었다 —
        /// 앉은 자리에서 0.5m 올라선 높이가 곧 선 키는 아니다.
        /// </summary>
        public void StandOnGround()
        {
            if (!Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down,
                                 out var floor, 200f, ~0, QueryTriggerInteraction.Ignore)) return;
            StandAtFloor(floor.point.y);
        }

        /// <summary>선 사람의 눈높이(바닥에서 눈까지, m). 앉히는 쪽이 같은 값을 써야 한다.</summary>
        public float EyeHeight => _eyeHeight;

        [Header("발소리")]
        [Tooltip("살살 걸을 때 나는 소리 크기(0~1)")]
        [Range(0f, 1f)] [SerializeField] private float _walkNoise = 0.18f;
        [Tooltip("뛸 때 나는 소리 크기(0~1). 남의 집 안방에서 뛰면 들려야 한다")]
        [Range(0f, 1f)] [SerializeField] private float _runNoise = 0.7f;
        [Tooltip("한 발짝 사이(초). 뛰면 이보다 촘촘해진다")]
        [SerializeField] private float _stepInterval = 0.55f;

        [Tooltip("<b>마루</b>를 딛는 소리. 널이 낮게 울린다. 여럿 넣으면 돌아가며 난다 — " +
                 "하나면 걸음이 기계가 된다")]
        [UnityEngine.Serialization.FormerlySerializedAs("_stepSounds")]
        [SerializeField] private AudioClip[] _woodSteps;

        [Tooltip("<b>흙</b>을 딛는 소리(마당·안뜰). 흙은 소리를 먹어 짧고 무디다. " +
                 "비우면 마루 소리를 낮게 눌러 쓴다")]
        [SerializeField] private AudioClip[] _dirtSteps;

        [Tooltip("<b>돌</b>을 딛는 소리(기단·댓돌). 딱딱하고 짧게 울린다. " +
                 "비우면 마루 소리를 쓴다")]
        [SerializeField] private AudioClip[] _stoneSteps;

        [Tooltip("<b>마루가 우는</b> 소리. 낡은 마루는 매 걸음 울지 않고 <b>가끔</b> 운다 — " +
                 "그 가끔이 잠행에서 사람을 배신하는 대목이다. 발소리 위에 얹힌다.\n" +
                 "<b>마루에서만</b> 난다 — 흙바닥과 댓돌에서는 아무리 걸어도 안 운다")]
        [SerializeField] private AudioClip[] _creakSounds;
        [Tooltip("한 발짝에 마루가 울 확률(0~1). 0.14 면 일곱 걸음에 한 번쯤")]
        [Range(0f, 1f)] [SerializeField] private float _creakChance = 0.14f;
        [Tooltip("마루가 울면 소리 크기가 이만큼 커진다. 삐걱은 발소리보다 멀리 간다")]
        [Range(0f, 0.6f)] [SerializeField] private float _creakAdds = 0.28f;

        [Tooltip("흙을 딛는 소리는 마루보다 이만큼 작다(배수). 흙은 <b>덜 들킨다</b> — " +
                 "마당을 가로지르는 것과 남의 방 널을 밟는 것이 같은 값일 수는 없다")]
        [Range(0.2f, 1f)] [SerializeField] private float _dirtQuieter = 0.55f;
        [Tooltip("돌을 딛는 소리는 마루보다 이만큼 작다(배수)")]
        [Range(0.2f, 1.4f)] [SerializeField] private float _stoneQuieter = 0.8f;

        private float _stepPhase;
        private int _stepTurn;

        /// <summary>
        /// 한 발짝마다 한 번씩. 매 프레임 알리면 소리가 끊기지 않고 눈금도 안 내려간다.
        ///
        /// <b>밟은 것에 따라 갈린다</b>: 여태는 발소리 한 벌과 삐걱 한 벌을 어디서나
        /// 틀었다. 그래서 마당 흙바닥을 걸어도 마루가 끼익 울었다 — 낡은 널이 우는
        /// 소리는 남의 방을 뒤질 때 <b>사람을 배신하는 대목</b>인데, 그것이 마당
        /// 한복판에서도 나면 긴장이 아니라 잡음이다. 어디서나 나는 소리는 뜻이 없다.
        ///
        /// 이제 <see cref="FloorKindProbe"/> 로 발밑을 물어본다.
        /// 흙은 무디게 울고 <b>안 운다</b>. 돌은 딱딱하게 울리고 역시 <b>안 운다</b>.
        /// <b>마루만 운다.</b>
        /// </summary>
        private void Footstep(bool running)
        {
            float period = _stepInterval * (running ? 0.6f : 1f);
            _stepPhase += Time.deltaTime;
            if (_stepPhase < period) return;
            _stepPhase = 0f;

            Vector3 feet = new Vector3(transform.position.x, transform.position.y - _eyeHeight, transform.position.z);
            FloorKind floor = FloorKindProbe.Under(feet);

            float lv = running ? _runNoise : _walkNoise;
            AudioClip[] set = _woodSteps;
            string what = running ? "뛰는 발소리" : "발소리";

            if (floor == FloorKind.흙)
            {
                if (_dirtSteps != null && _dirtSteps.Length > 0) set = _dirtSteps;
                lv *= _dirtQuieter;
                what = running ? "뛰는 발소리(흙)" : "발소리(흙)";
            }
            else if (floor == FloorKind.돌)
            {
                if (_stoneSteps != null && _stoneSteps.Length > 0) set = _stoneSteps;
                lv *= _stoneQuieter;
                what = running ? "뛰는 발소리(돌)" : "발소리(돌)";
            }

            NoiseMeter.Play(transform.position, Pick(set, ref _stepTurn), lv, what,
                            1f + Random.Range(-0.07f, 0.07f));

            // <b>삐걱은 마루의 일이다.</b> 흙과 돌은 아무리 밟아도 울지 않는다.
            if (floor != FloorKind.마루) return;

            // 뛰면 더 자주 운다 — 세게 디디니 그렇다
            float chance = _creakChance * (running ? 2.2f : 1f);
            if (_creakSounds == null || _creakSounds.Length == 0 || Random.value > chance) return;

            var creak = _creakSounds[Random.Range(0, _creakSounds.Length)];
            NoiseMeter.Play(transform.position, creak, Mathf.Clamp01(lv + _creakAdds),
                            "마루가 운다", 1f + Random.Range(-0.06f, 0.06f));
        }

        /// <summary>돌아가며 고른다. 무작위로만 뽑으면 같은 것이 연달아 나 눈에 띈다.</summary>
        private static AudioClip Pick(AudioClip[] set, ref int turn)
        {
            if (set == null || set.Length == 0) return null;
            turn = (turn + 1) % set.Length;
            return set[turn];
        }

        /// <summary>
        /// <b>바닥 높이를 밖에서 받아</b> 그 위에 선다.
        ///
        /// 발밑을 제가 찾으면 안 되는 자리가 있다 — 방석 위에서 일어설 때다. 아래로
        /// 쏘면 방석 윗면이 먼저 걸려, 방석 두께만큼 붕 뜬 키가 된다. 앉힌 쪽은 어느
        /// 것이 방석인지 알고 있으므로, 그쪽이 잰 <b>마루</b> 높이를 그대로 받는다.
        /// </summary>
        public void StandAtFloor(float floorY)
        {
            _walkMode = true;
            _walkY = floorY + _eyeHeight;
            Vector3 p = transform.position; p.y = _walkY; transform.position = p;
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
            // <b>글을 치는 동안에는 몸이 안 움직인다.</b> 두벌식으로 「ㅁ」이 <c>a</c>,
            // 「ㅅ」이 <c>t</c>, 「ㄴ」이 <c>s</c> 자리라, 한글을 치면 WASD가 줄줄이 눌린다 —
            // 물어보려고 한 줄 치는 사이에 마당 저쪽까지 걸어가 있다.
            if (Typing.Now) return;
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

                // <b>발소리</b> — 걸으면 조금, 뛰면 많이. 잠행 중에는 이것도 소리다.
                // 매 프레임 알리지 않고 걸음새에 맞춰 한 발짝마다 한 번씩 낸다.
                if (move.sqrMagnitude > 0.0001f) Footstep(kb.leftShiftKey.isPressed);
                else _stepPhase = 0f;

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

        /// <summary>
        /// <b>화면 왼쪽 아래 상태 줄.</b> 기본으로 꺼 둔다.
        ///
        /// 만드는 사람에게는 쓸모가 있었지만 <b>보는 사람에게는 없다</b> —
        /// 「카메라: 걷기 (Tab 전환 · G 바닥내려서기)」는 조선 후기 옹당촌에 있을 글이
        /// 아니고, 영상을 찍으면 그 줄이 그대로 남는다.
        ///
        /// 개발용이므로 빌드에서 보일 일은 없다.
        /// 그러니 이 줄이 보이는 자리는 <b>모니터로 찍는 화면</b>뿐이었다.
        /// </summary>
        [Tooltip("만드는 동안만 켠다. 켜면 화면 왼쪽 아래에 걷기/비행과 눈높이가 뜬다")]
        [SerializeField] private bool _showHud;

        private void OnGUI()
        {
            if (!_showHud) return;
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
