using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 혼천의 포커스 조작 (2026-08-14, 축 규약 확정판) — FocusInteractable 구현.
    ///
    /// 실물 혼천의의 회전 규약을 따른다 (2026-08-14 사용자 확정):
    ///   지평환 = 고정(조작 대상 아님) / 자오환 = 수직축(자오환_축 피벗) /
    ///   적도환 = 극축(위도 37.5° 앙각) / 황도환 = 극축에서 23.5° 더 기운 축 / 소형환 3 = 내환부 자체 축.
    /// 각 대상의 **로컬 Y가 곧 회전축**이 되도록 빌더가 피벗을 세워 두므로, 여기서는
    /// 로컬 Y 회전만 한다 — 아무 방향으로나 돌아 물리적으로 불가능한 자세가 나오는 일이 없다.
    ///
    /// 회전각은 baseRot(빌드 자세) × Yaw(angle)로 적용하고 angles[]에 따로 적산한다 —
    /// 기울어진 피벗의 localEulerAngles를 직접 읽으면 합성 회전이라 각도가 깨진다.
    /// 퍼즐 판정은 RingAngle(i)를 읽으면 된다.
    ///
    /// 휠로 고리 순환 선택(가는 고리 직접 클릭은 오조준이 잦고 VR 스틱 플릭으로 이식 쉬움),
    /// 선택 고리는 이미시브 런타임 사본으로 은은하게 표시(ringRenderers — 피벗은 렌더러가
    /// 없어서 강조 대상 렌더러를 따로 받는다). ⚠️ 퍼즐 정답 판정은 여기 없다 — 조작만.
    /// </summary>
    public class HoncheonuiFocusRings : FocusInteractable
    {
        [Tooltip("회전 대상 (로컬 Y = 회전축): 자오환_축·적도환·황도환·소형환1~3")]
        public Transform[] rings;
        [Tooltip("강조 표시용 렌더러 (rings와 병렬)")]
        public Renderer[] ringRenderers;
        public string[] ringNames = { "자오환", "적도환", "황도환", "소형환 일", "소형환 이", "소형환 삼" };
        [Tooltip("드래그 픽셀당 회전 각도")]
        public float degreesPerPixel = 0.25f;
        // 1.7이면 블룸이 정면 고리에서 화면 절반을 태운다 (실측) — 은은한 값
        [ColorUsage(false, true)]
        public Color highlight = new Color(0.45f, 0.28f, 0.12f);

        int sel;
        float[] angles;
        Quaternion[] baseRot;
        Renderer litRend;
        Material litOriginal, litClone;

        public override string Prompt => "살펴보기";

        void EnsureState()
        {
            if (rings == null || rings.Length == 0) return;
            if (angles == null || angles.Length != rings.Length)
            {
                angles = new float[rings.Length];
                baseRot = new Quaternion[rings.Length];
                for (int i = 0; i < rings.Length; i++)
                    if (rings[i] != null) baseRot[i] = rings[i].localRotation;
            }
        }

        /// <summary>i번 고리의 축 회전각 (0~360, 도) — 퍼즐 판정용 읽기.</summary>
        public float RingAngle(int index)
        {
            EnsureState();
            return angles != null && index >= 0 && index < angles.Length
                ? Mathf.Repeat(angles[index], 360f) : 0f;
        }

        public override string FocusStatus =>
            "조작 중: " + (ringNames != null && sel < ringNames.Length ? ringNames[sel] : "고리") + "   (휠: 고리 전환)";

        public override void HandleDrag(Vector2 delta)
        {
            EnsureState();
            if (rings == null || sel >= rings.Length || rings[sel] == null) return;
            angles[sel] += -delta.x * degreesPerPixel;
            rings[sel].localRotation = baseRot[sel] * Quaternion.Euler(0f, angles[sel], 0f);
        }

        public override void HandleScroll(float direction)
        {
            if (rings == null || rings.Length == 0) return;
            int n = rings.Length;
            sel = ((sel + (direction > 0f ? -1 : 1)) % n + n) % n;
            ApplyHighlight();
        }

        public override void OnFocusChanged(bool focused)
        {
            if (focused) ApplyHighlight();
            else ClearHighlight();
        }

        void ApplyHighlight()
        {
            ClearHighlight();
            var r = ringRenderers != null && sel < ringRenderers.Length ? ringRenderers[sel] : null;
            if (r == null) return;
            litRend = r;
            litOriginal = r.sharedMaterial;
            litClone = new Material(litOriginal);          // 런타임 사본 — 황동 에셋은 건드리지 않는다
            litClone.EnableKeyword("_EMISSION");
            litClone.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            litClone.SetColor("_EmissionColor", highlight);
            r.sharedMaterial = litClone;
        }

        void ClearHighlight()
        {
            if (litRend != null && litOriginal != null) litRend.sharedMaterial = litOriginal;
            if (litClone != null)
            {
                if (Application.isPlaying) Destroy(litClone);
                else DestroyImmediate(litClone);
            }
            litRend = null; litOriginal = null; litClone = null;
        }
    }
}
