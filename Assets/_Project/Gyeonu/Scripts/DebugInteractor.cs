using UnityEngine;
using UnityEngine.InputSystem;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 세상을 조준하는 입력 (2026-08-26 개편).
    /// 워커 카메라에 붙어 조준 광선을 쏘고, Interactable을 맞히면 조준점을 밝히며
    /// 이름/행동을 적는다. 누르면 Interact() 를 부른다.
    ///
    /// ■ 광선과 그림을 둘 다 밖으로 뺐다
    ///   광선 — <see cref="UiPointers"/> (PC = 화면 정중앙 시선, VR = 컨트롤러 광선)
    ///   그림 — <see cref="AimPanel"/> (월드 스페이스 캔버스. 예전엔 IMGUI라 HMD에 안 보였다)
    ///   그래서 이 클래스에는 <b>무엇을 맞혔는가</b> 판정만 남는다 — 그 부분은 안 건드렸다.
    /// </summary>
    public class DebugInteractor : MonoBehaviour
    {
        [Tooltip("상호작용 최대 거리(m)")]
        public float maxDistance = 3.5f;

        /// <summary>
        /// <b>몸통만</b> 막는 상자의 이름. 건물마다 하나씩 씌워 둔 진입 차단 박스다
        /// (<c>VillageBuildingBlocker</c> · <c>GyeonuVillageWalkSetup</c> 이 만든다).
        /// 보이는 물건이 아니라 걸어 들어가지 못하게 하는 <b>몸통</b> 판정이므로,
        /// 조준선은 여기서 멈추지 않는다 — 아래 <see cref="Update"/> 참고.
        /// </summary>
        public const string BodyBlockerName = "몸통차단";

        Interactable target;
        AimPanel aim;

        void OnDisable()
        {
            // 조준이 꺼지면 조준점도 함께 사라져야 한다 (소지품 판이 열릴 때 등)
            if (aim != null) aim.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (aim != null) Destroy(aim.gameObject);
        }

        void Update()
        {
            if (aim == null) aim = AimPanel.Create(transform);
            if (!aim.gameObject.activeSelf) aim.gameObject.SetActive(true);

            var ptr = UiPointers.Get(transform);
            target = null;
            var ray = ptr.GazeRay;
            // ⚠️ 급하게 내려보면 레이가 워커 자신의 캡슐에 먼저 맞는다 (낮은 기물 조준 시 실측, 2026-08-14).
            //    자기 몸통은 건너뛰고, 그 다음 가장 가까운 표면에서만 판정한다 (벽 뒤 투시 방지)
            var hits = Physics.RaycastAll(ray, maxDistance);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider.transform.root == transform.root) continue;

                // ⚠️ 건물 진입 차단 박스는 조준을 막지 않는다 (2026-08-27 실측으로 물린 것).
                //    이 상자는 건물 <b>둘레를 통째로</b> 감싸 마당 쪽으로도 한두 자 넘쳐 나온다.
                //    어머니는 그 넘친 자락 안에 앉아 있어서 — 집 밖에, 눈앞에 보이는데도 —
                //    어느 방향에서 겨눠도 광선이 상자에 먼저 막혀 **말을 걸 수가 없었다**
                //    (사방 12방위 × 몸통 다섯 높이, 전부 차단). 이 상자의 몫은 '몸통 진입 차단'
                //    하나뿐이다 — <b>보이지도 않는 부피가 조준을 먹는 것</b>이 잘못이다.
                //    대가: 이 상자를 씌운 마을 건물들은 팩 콜라이더를 꺼 두었으므로(성능),
                //    이제 그 벽 너머 3.5m 안쪽이 조준선에 열린다. 그 안에는 조준할 것이
                //    아무것도 없다(배경 건물이고 들어갈 수도 없다). 언젠가 마을 건물 안에
                //    만질 것을 두게 되면 그때는 벽에 진짜 콜라이더를 되살려야 한다.
                if (hit.collider.gameObject.name == BodyBlockerName) continue;

                // ⚠️ 사건을 알리는 트리거 부피도 조준을 먹는다 (2026-08-27 실측).
                //    아이01에게는 노래를 시작시키는 <b>반지름 7m 짜리 구</b>가 몸통과 같은
                //    오브젝트에 달려 있다. 그 구 밖에서 안쪽을 겨누면 — 아이02를 보고 있어도 —
                //    광선이 구 껍질에 먼저 닿아 <b>아이01이 잡힌다</b>.
                //    몸통이 따로 있는 트리거는 조준면이 아니다. 다만 <c>SceneExit</c>(출구_*)처럼
                //    트리거 하나가 곧 조준면인 것도 있으므로, <b>몸통이 따로 있을 때만</b> 건너뛴다.
                if (IsEventVolume(hit.collider)) continue;

                var it = hit.collider.GetComponentInParent<Interactable>();

                // 열린 가구는 한 겹 더 들여다본다 (2026-08-24).
                //   가구의 조준 판정은 몸통을 통째로 감싼 차단 박스가 받는다 — 그래서 궤 안에 든
                //   물건은 **늘 궤에 가려** 조준되지 않는다(반닫이 속 서책에서 실측).
                //   열려 있을 때만, 그리고 그 안쪽에 집을 물건이 있을 때만 통과시킨다.
                //   닫힌 가구는 그대로 막는다 — 안이 안 보이는데 집히면 안 된다.
                if (it is IOpenable openable && openable.IsOpen)
                {
                    var inner = InnerBehind(hits, hit.distance);
                    if (inner != null) { target = inner; break; }
                }

                if (it != null && it.CanInteract(gameObject)) target = it;
                break;
            }

            // ⚠️ 커서 잠금 검사는 PC 전용 규약이다 — 판이 열리면 커서가 풀리고, 그동안에는
            //    세상을 만지면 안 된다. VR에는 커서라는 것이 없으므로 그 조건을 건너뛴다.
            bool canPress = UiModes.IsVr || Cursor.lockState == CursorLockMode.Locked;
            if (target != null && canPress && ptr.PressDown)
                target.Interact(gameObject);

            aim.SetTarget(target == null ? null
                : (string.IsNullOrEmpty(target.displayName) ? target.Prompt
                                                            : target.displayName + " — " + target.Prompt));
        }

        /// <summary>조준면이 아니라 <b>사건을 알리는 부피</b>인가 — 같은 오브젝트에 몸통이 따로 있는 트리거.
        /// 매 프레임 도는 자리라 <see cref="_cols"/> 를 돌려 써서 할당을 만들지 않는다.</summary>
        static readonly System.Collections.Generic.List<Collider> _cols = new System.Collections.Generic.List<Collider>();

        static bool IsEventVolume(Collider c)
        {
            if (!c.isTrigger) return false;
            c.GetComponents(_cols);
            foreach (var o in _cols) if (!o.isTrigger) return true;
            return false;
        }

        /// <summary>주어진 거리보다 뒤에 있는 첫 **가구 속 대상** — 열린 가구를 들여다볼 때만 쓴다.
        /// <see cref="IInnerTarget"/> 표식이 달린 것만 한정한다: 아무 Interactable이나 통과시키면
        /// 열린 문 너머 엉뚱한 것이 조준되고, 궤를 다시 닫을 방법도 사라진다.
        ///
        /// ⚠️ 2026-08-26에 <c>ItemPickup</c> 을 이름으로 찾던 줄을 지웠다. 그 표식은 원래
        ///    "집는 것 말고 안에서 조작하는 것도 같은 사정"이라 뽑아 둔 것이라
        ///    (<see cref="IInnerTarget"/> 주석 참고), <c>ItemPickup</c> 에 표식을 달아 하나로 합쳤다.
        ///    조준 담당이 소지품 획득을 알 이유가 없다 — UI 뼈대를 다른 사건에 건네는 데 걸리던 매듭이다.</summary>
        Interactable InnerBehind(RaycastHit[] sorted, float from)
        {
            foreach (var h in sorted)
            {
                if (h.distance <= from) continue;
                var inner = h.collider.GetComponentInParent<Interactable>();
                if (inner is IInnerTarget && inner.CanInteract(gameObject)) return inner;
            }
            return null;
        }

    }
}
