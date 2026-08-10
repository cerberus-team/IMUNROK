using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 혼상 발광 제어 (2026-08-10).
    /// - SetLit(bool): 내부 광원 + 내피 이미시브만 켜고 끈다 (구멍으로 빛이 새어나오는 상태).
    /// - StarNight(bool): 연출 일괄 처리 — 혼상 점등 + 방 등잔 소등 + 천장 별 점등.
    ///   방 조명·별 그룹은 관측실이 재생성돼도 깨지지 않게 이름으로 찾는다.
    ///   아래층 등불(선아, "아래층_등불")은 서사상 항상 켜 두므로 소등에서 제외.
    /// 에디터(비플레이)에서도 동작한다 — 코루틴 없음, MaterialPropertyBlock 사용.
    /// </summary>
    public class HonsangController : MonoBehaviour
    {
        [Header("혼상 내부")]
        public Light innerLight;
        public Renderer interiorRenderer;                      // 구_내피
        [ColorUsage(false, true)]
        public Color emissionLit = new Color(3.2f, 1.9f, 0.9f); // 켜졌을 때 내피 발광색 (웜)

        [Header("관측실 연동 (이름으로 탐색)")]
        public string roomRootName = "관측실";
        public string lightGroupName = "조명";
        public string starGroupName = "혼상별_별빛";
        public string keepLitName = "아래층_등불";              // 소등 제외 (선아의 등불)

        public bool IsLit { get; private set; }

        MaterialPropertyBlock mpb;

        /// <summary>혼상 자체 점등/소등 — 내부 광원 + 내피 이미시브.</summary>
        public void SetLit(bool lit)
        {
            IsLit = lit;
            if (innerLight != null) innerLight.enabled = lit;
            if (interiorRenderer != null)
            {
                if (mpb == null) mpb = new MaterialPropertyBlock();
                interiorRenderer.GetPropertyBlock(mpb);
                mpb.SetColor("_EmissionColor", lit ? emissionLit : Color.black);
                interiorRenderer.SetPropertyBlock(mpb);
            }
        }

        /// <summary>연출 일괄: 혼상 점등 → 방 등잔 소등 → 천장 별 점등 (off면 역순 복구).</summary>
        public void StarNight(bool on)
        {
            SetLit(on);
            var root = GameObject.Find(roomRootName);
            if (root == null) return;
            var lights = root.transform.Find(lightGroupName);
            if (lights != null)
                foreach (var l in lights.GetComponentsInChildren<Light>(true))
                {
                    if (l == innerLight) continue;
                    var t = l.transform;
                    bool keep = false;
                    for (; t != null && t != lights; t = t.parent)
                        if (t.name == keepLitName) { keep = true; break; }
                    if (!keep) l.enabled = !on;
                }
            var stars = root.transform.Find(starGroupName);
            if (stars != null) stars.gameObject.SetActive(on);
        }
    }
}
