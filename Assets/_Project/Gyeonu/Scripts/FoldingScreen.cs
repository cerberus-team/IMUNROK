using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 병풍 — 클릭하면 접혀서 한쪽으로 치워지고, 다시 클릭하면 펼쳐진다.
    ///
    /// 폭(panel)마다 "펼친 포즈"와 "접은 포즈"를 미리 계산해 두고 그 사이를 보간한다.
    /// 경첩을 물리적으로 시뮬레이션하지 않는 이유: 폭이 10장이라 연쇄 회전을 런타임에
    /// 풀면 오차가 누적되는데, 두 포즈는 빌더(GwanaOfficeBuilder)가 경첩 사슬을 따라
    /// **정확히** 계산할 수 있다. 보간 중간 프레임의 경첩이 미세하게 벌어지지만
    /// 1.4초 동안 지나가는 연출이라 눈에 띄지 않는다.
    ///
    /// 콜라이더는 이 GO(루트)에 하나만 둔다 — 폭마다 달면 접히는 동안 캡슐을 문다.
    /// 대신 접힘/펼침 상태에 맞춰 루트 콜라이더의 크기·중심을 함께 보간한다.
    /// </summary>
    public class FoldingScreen : Interactable
    {
        [System.Serializable]
        public class Panel
        {
            public Transform target;
            public Vector3 spreadPos, foldedPos;        // 부모 로컬
            public Vector3 spreadEuler, foldedEuler;    // 부모 로컬
        }

        public Panel[] panels;
        [Tooltip("여닫는 시간(초)")]
        public float duration = 1.4f;
        [Tooltip("씬에 저장된 상태 — 펼쳐 둔다")]
        public bool folded;

        [Tooltip("비우지 않으면 접힘 상태를 GyeonuWorld에 기억한다 — 씬을 나갔다 들어와도 유지된다. " +
                 "(씬 파일에 저장된 folded 값은 '처음 들어왔을 때'의 상태로만 쓰인다)")]
        public string persistKey = "";

        [Header("차단 박스 (로컬)")]
        public BoxCollider blocker;
        public Vector3 spreadBoxCenter, spreadBoxSize;
        public Vector3 foldedBoxCenter, foldedBoxSize;

        float k;   // 0 = 펼침, 1 = 접힘

        public override string Prompt => folded ? "펼치기" : "치우기";

        void Awake()
        {
            // 세션에 기억된 상태가 있으면 그것으로 시작한다 (서고에 다녀와도 치워진 채로).
            if (!string.IsNullOrEmpty(persistKey)) folded = GyeonuWorld.Has(persistKey);

            k = folded ? 1f : 0f;
            Apply(k);
        }

        public override void Interact(GameObject actor)
        {
            folded = !folded;
            if (!string.IsNullOrEmpty(persistKey)) GyeonuWorld.Set(persistKey, folded);
        }

        void Update()
        {
            float target = folded ? 1f : 0f;
            if (Mathf.Approximately(k, target)) return;
            k = Mathf.MoveTowards(k, target, Time.deltaTime / Mathf.Max(0.05f, duration));
            Apply(k * k * (3f - 2f * k));   // 스무스스텝
        }

        void Apply(float s)
        {
            if (panels != null)
                foreach (var p in panels)
                {
                    if (p.target == null) continue;
                    p.target.localPosition = Vector3.Lerp(p.spreadPos, p.foldedPos, s);
                    p.target.localRotation = Quaternion.Slerp(
                        Quaternion.Euler(p.spreadEuler), Quaternion.Euler(p.foldedEuler), s);
                }

            if (blocker != null)
            {
                blocker.center = Vector3.Lerp(spreadBoxCenter, foldedBoxCenter, s);
                blocker.size = Vector3.Lerp(spreadBoxSize, foldedBoxSize, s);
            }
        }
    }
}
