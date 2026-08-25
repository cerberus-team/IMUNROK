using UnityEngine;
using UnityEngine.InputSystem;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 임시 상호작용 입력 (마우스 — VR 컨트롤러/공통 시스템으로 교체 예정).
    /// 워커 카메라에 붙어 화면 중앙으로 레이캐스트 → Interactable 조준 시 조준점 강조 +
    /// 이름/행동 표시, 좌클릭으로 Interact() 호출. 교체 시 이 컴포넌트만 갈아끼우면 된다.
    /// </summary>
    public class DebugInteractor : MonoBehaviour
    {
        [Tooltip("상호작용 최대 거리(m)")]
        public float maxDistance = 3.5f;

        Interactable target;

        void Update()
        {
            target = null;
            var ray = new Ray(transform.position, transform.forward);
            // ⚠️ 급하게 내려보면 레이가 워커 자신의 캡슐에 먼저 맞는다 (낮은 기물 조준 시 실측, 2026-08-14).
            //    자기 몸통은 건너뛰고, 그 다음 가장 가까운 표면에서만 판정한다 (벽 뒤 투시 방지)
            var hits = Physics.RaycastAll(ray, maxDistance);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider.transform.root == transform.root) continue;
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

            if (target != null && Cursor.lockState == CursorLockMode.Locked
                && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                target.Interact(gameObject);
        }

        /// <summary>주어진 거리보다 뒤에 있는 첫 **가구 속 대상** — 열린 가구를 들여다볼 때만 쓴다.
        /// 집을 수 있는 물건(<see cref="ItemPickup"/>)과 안에서 조작하는 것(<see cref="IInnerTarget"/>)만
        /// 한정한다: 아무 Interactable이나 통과시키면 열린 문 너머 엉뚱한 것이 조준되고,
        /// 궤를 다시 닫을 방법도 사라진다.</summary>
        Interactable InnerBehind(RaycastHit[] sorted, float from)
        {
            foreach (var h in sorted)
            {
                if (h.distance <= from) continue;
                var pick = h.collider.GetComponentInParent<ItemPickup>();
                if (pick != null && pick.CanInteract(gameObject)) return pick;
                var inner = h.collider.GetComponentInParent<Interactable>();
                if (inner is IInnerTarget && inner.CanInteract(gameObject)) return inner;
            }
            return null;
        }

        void OnGUI()
        {
            float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
            var dot = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            dot.normal.textColor = target != null ? Color.yellow : new Color(1f, 1f, 1f, 0.45f);
            GUI.Label(new Rect(cx - 20f, cy - 20f, 40f, 40f), target != null ? "◆" : "·", dot);

            if (target != null)
            {
                var label = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
                label.normal.textColor = Color.yellow;
                // displayName을 비워 두면(예: 관아 개구멍) 접두사 없이 행동 문구만 뜬다.
                string text = string.IsNullOrEmpty(target.displayName) ? target.Prompt : target.displayName + " — " + target.Prompt;
                GUI.Label(new Rect(cx - 120f, cy + 16f, 240f, 22f), text, label);
            }
        }
    }
}
