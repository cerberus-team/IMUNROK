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
                if (it != null && it.CanInteract(gameObject)) target = it;
                break;
            }

            if (target != null && Cursor.lockState == CursorLockMode.Locked
                && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                target.Interact(gameObject);
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
                GUI.Label(new Rect(cx - 120f, cy + 16f, 240f, 22f), target.displayName + " — " + target.Prompt);
            }
        }
    }
}
