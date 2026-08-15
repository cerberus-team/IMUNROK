using System.Collections.Generic;
using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 여닫이 가구 (2026-08-15) — 문짝·서랍이 본(Transform)으로 분리된 가구를 클릭 한 번으로
    /// 전부 부드럽게 열고 닫는다. 마을 담장 문(HingeDoor)과 같은 원리:
    /// 부품 GameObject를 리페어런트하지 않고, **부모 좌표계의 자기 원점(경첩)을 축으로 자체 회전**한다.
    /// 스킨드 메시(kcisa)는 본을 돌리면 메시가 따라오고, 문갑(Table04)은 Dummy 본이 문·서랍을 끈다.
    /// 서랍은 회전 대신 로컬 오프셋으로 앞으로 빠진다.
    /// 클릭 판정은 가구의 기존 차단 콜라이더가 받는다 (DebugInteractor가 부모에서 이 컴포넌트를 찾는다).
    /// </summary>
    public class FurnitureParts : Interactable
    {
        [System.Serializable]
        public class Part
        {
            [Tooltip("움직일 본/노드")]
            public Transform node;
            [Tooltip("회전 축 (본 자기 로컬 — 실험 계측과 동일 기준. 0이면 회전 안 함)")]
            public Vector3 axisInSelf;
            [Tooltip("부모 좌표계 힌지 축 — 지정하면 axisInSelf 대신 이 축·pivotInParent를 쓴다 (0이면 미사용). 함 뚜껑·모서리 경첩처럼 본 원점이 경첩이 아닐 때")]
            public Vector3 axisInParent;
            [Tooltip("부모 좌표계 경첩점 (axisInParent 사용 시)")]
            public Vector3 pivotInParent;
            [Tooltip("열림 각도")]
            public float openAngle;
            [Tooltip("서랍용 — 열렸을 때의 로컬 이동량 (0이면 이동 없음)")]
            public Vector3 slideLocal;
            [System.NonSerialized] public Vector3 closedPos;
            [System.NonSerialized] public Quaternion closedRot;
        }

        public List<Part> parts = new List<Part>();
        [Tooltip("여닫는 시간(초)")]
        public float duration = 0.9f;

        public bool IsOpen => open;

        float t;          // 0=닫힘, 1=열림
        bool open;

        public override string Prompt => open ? "닫기" : "열기";

        void Awake()
        {
            foreach (var p in parts)
            {
                if (p.node == null) continue;
                p.closedPos = p.node.localPosition;   // 씬은 닫힌 상태로 저장됨
                p.closedRot = p.node.localRotation;
            }
        }

        public override void Interact(GameObject actor)
        {
            open = !open;
        }

        void Update()
        {
            float target = open ? 1f : 0f;
            if (Mathf.Approximately(t, target)) return;
            t = Mathf.MoveTowards(t, target, Time.deltaTime / Mathf.Max(0.05f, duration));
            float s = t * t * (3f - 2f * t);            // 스무스스텝
            foreach (var p in parts)
            {
                if (p.node == null) continue;
                if (p.axisInParent.sqrMagnitude > 1e-4f && Mathf.Abs(p.openAngle) > 0.01f)
                {
                    // 부모 좌표계 경첩 — HingeDoor와 같은 원리: pivot을 축으로 위치·회전을 함께 돌린다
                    var q = Quaternion.AngleAxis(p.openAngle * s, p.axisInParent.normalized);
                    p.node.localPosition = p.pivotInParent + q * (p.closedPos - p.pivotInParent);
                    p.node.localRotation = q * p.closedRot;
                }
                else if (p.axisInSelf.sqrMagnitude > 1e-4f && Mathf.Abs(p.openAngle) > 0.01f)
                {
                    // 경첩 = 본의 자기 원점 — 자기 로컬 축으로 제자리 회전 (위치 불변)
                    var q = Quaternion.AngleAxis(p.openAngle * s, p.axisInSelf.normalized);
                    p.node.localRotation = p.closedRot * q;
                }
                if (p.slideLocal.sqrMagnitude > 1e-6f)
                    p.node.localPosition = p.closedPos + p.slideLocal * s;
            }
        }
    }
}
