using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 혼천의 포커스 조작 (2026-08-14, 축 규약 확정판 / 2026-08-23 여덟 방위 스냅 추가) — FocusInteractable 구현.
    ///
    /// 실물 혼천의의 회전 규약을 따른다 (2026-08-14 사용자 확정):
    ///   지평환 = 고정(조작 대상 아님) / 자오환 = 수직축(자오환_축 피벗) /
    ///   적도환 = 극축(위도 37.5° 앙각) / 황도환 = 극축에서 23.5° 더 기운 축 / 소형환 3 = 내환부 자체 축.
    /// 각 대상의 **로컬 Y가 곧 회전축**이 되도록 빌더가 피벗을 세워 두므로, 여기서는
    /// 로컬 Y 회전만 한다 — 아무 방향으로나 돌아 물리적으로 불가능한 자세가 나오는 일이 없다.
    ///
    /// 회전각은 baseRot(빌드 자세) × Yaw(angle)로 적용하고 angles[]에 따로 적산한다 —
    /// 기울어진 피벗의 localEulerAngles를 직접 읽으면 합성 회전이라 각도가 깨진다.
    ///
    /// 휠로 고리 순환 선택(가는 고리 직접 클릭은 오조준이 잦고 VR 스틱 플릭으로 이식 쉬움),
    /// 선택 고리는 이미시브 런타임 사본으로 은은하게 표시(ringRenderers — 피벗은 렌더러가
    /// 없어서 강조 대상 렌더러를 따로 받는다).
    ///
    /// ⚠️ **정답 판정은 여기 없다 — 조작만 한다.** 어느 방위가 정답인지, 다 맞으면 무슨 일이
    ///    벌어지는지는 <see cref="HoncheonuiPuzzle"/>가 쥔다. 여기는 "어떻게 돌고 어디에 물리는가"만.
    ///
    /// ■ 방위(Bearing)를 어떻게 정하나 — 이 파일에서 가장 중요한 결정
    ///   고리마다 **여덟 방위 중 어디에 놓였는가**를 하나의 수로 재야 퍼즐이 성립한다.
    ///   가장 그럴듯한 방법은 고리 위 표점을 수평면에 투영해 방위각을 읽는 것인데,
    ///   **이 혼천의에서는 쓸 수 없다.** 소형환_3의 회전축이 정확히 수평(-0.951, 0, 0.309)이라
    ///   그 고리의 표점은 수직 원을 그리고, 수평 투영은 딱 두 방향(18°/198°)만 나온다 —
    ///   여덟 방위 중 여섯을 아예 가리킬 수 없다 (2026-08-23 실측).
    ///
    ///   그래서 **누적 회전각**으로 정의한다:
    ///     Bearing(i) = 받침 yaw + angles[i] + Σ angles[조상 고리]
    ///   0° = 월드 +Z = 北, 시계 방향으로 45°마다 한 방위. 여섯 고리 모두 축 기울기와 무관하게
    ///   같은 규칙으로 읽히고, **부모를 돌리면 자식의 방위가 그만큼 함께 밀린다** — 실제 계층이
    ///   그렇게 생겼기 때문이고, 그래서 바깥 고리부터 맞춰야 하는 구조가 저절로 성립한다.
    ///   (기준 표점 구슬을 두지 않기로 했으므로 눈으로 읽는 표시는 나침반 UI가 맡는다)
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

        [Tooltip("선행 조건·정답 판정을 쥔 퍼즐. 비우면 조건 없이 조작만 된다")]
        public HoncheonuiPuzzle puzzle;
        // 1.7이면 블룸이 정면 고리에서 화면 절반을 태운다 (실측) — 은은한 값
        [ColorUsage(false, true)]
        public Color highlight = new Color(0.45f, 0.28f, 0.12f);

        [Header("여덟 방위 스냅")]
        [Tooltip("방위 한 칸의 각도. 8방위이므로 45")]
        public float slotStep = 45f;
        [Tooltip("이만큼 가까워지면 칸에 물린다(도)")]
        public float detentEnter = 9f;
        [Tooltip("물린 뒤 이만큼 벗어나야 풀린다(도) — 물렸다 풀렸다 떨리는 것을 막는 여유")]
        public float detentExit = 14f;

        /// <summary>고리 하나가 칸에 물리거나 손을 뗀 순간 (인자 = 고리 번호). 판정·소리가 듣는다.</summary>
        public event System.Action<int> RingSettled;
        /// <summary>칸에 **새로** 물린 순간 — 「덜컥」 소리 자리.</summary>
        public event System.Action<int> RingDetent;
        /// <summary>포커스 진입(true)·이탈(false).</summary>
        public event System.Action<bool> FocusChanged;

        /// <summary>참이면 드래그를 받지 않는다 — 다 맞춘 뒤 고리를 굳힐 때.</summary>
        public bool Locked { get; set; }

        int sel;
        float[] angles;      // 실제로 먹인 각 (칸에 물리면 칸 값으로 당겨진다)
        float[] raw;         // 손이 끈 만큼 그대로 — 칸에서 빠져나갈 때 쓴다
        int[] slotOf;        // 지금 물린 칸 (-1 = 안 물림)
        int[][] ancestors;   // 각 고리의 조상 고리 번호들
        Quaternion[] baseRot;
        Renderer litRend;
        Material litOriginal, litClone;

        int dragFrame = -10;
        bool wasDragging;

        public override string Prompt => "살펴보기";

        /// <summary>
        /// 조사하면 포커스로 들어간다 — 단, 퍼즐이 아직 시작될 수 없으면 여기서 막는다.
        /// 조준 문구는 그대로 "살펴보기"다: 왜 안 되는지는 눌러 봐야 아는 정보이므로
        /// 조준 단계에서 미리 알려 주지 않는다 (암문 석축과 같은 규약).
        /// </summary>
        public override void Interact(GameObject actor)
        {
            if (puzzle != null && !puzzle.AllowFocus()) return;   // 안내는 퍼즐이 띄운다
            base.Interact(actor);
        }

        /// <summary>지금 고른 고리 번호.</summary>
        public int Selected => sel;
        public int RingCount => rings != null ? rings.Length : 0;
        public string RingName(int i) =>
            ringNames != null && i >= 0 && i < ringNames.Length ? ringNames[i] : "고리";

        void EnsureState()
        {
            if (rings == null || rings.Length == 0) return;
            if (angles != null && angles.Length == rings.Length) return;

            int n = rings.Length;
            angles = new float[n];
            raw = new float[n];
            slotOf = new int[n];
            baseRot = new Quaternion[n];
            ancestors = new int[n][];

            for (int i = 0; i < n; i++)
            {
                if (rings[i] != null) baseRot[i] = rings[i].localRotation;
                slotOf[i] = -1;

                // 조상 고리 찾기 — 계층을 타고 올라가며 rings에 든 것을 줍는다.
                // 손으로 적어 두면 빌더가 계층을 손볼 때 조용히 어긋난다.
                var list = new System.Collections.Generic.List<int>();
                for (var p = rings[i] != null ? rings[i].parent : null; p != null; p = p.parent)
                    for (int j = 0; j < n; j++)
                        if (rings[j] == p) list.Add(j);
                ancestors[i] = list.ToArray();
            }

            // 시작 자세를 여덟 방위에 맞춰 둔다 — 받침이 18° 돌아 앉아 있어 그대로 두면
            // 여섯 고리가 모두 칸 사이에 어중간하게 걸린다. 가장 가까운 칸으로만 당기므로
            // 원래 자세에서 22.5°를 넘게 벗어나지 않는다 (모델은 건드리지 않는다).
            for (int i = 0; i < n; i++) SettleRing(i, silent: true);
        }

        /// <summary>받침 자체가 돌아 앉은 각. 방위 0을 월드 +Z(北)에 맞추려고 더한다.</summary>
        float RootYaw => transform.eulerAngles.y;

        float Inherited(int i)
        {
            float sum = 0f;
            if (ancestors != null && ancestors[i] != null)
                foreach (var a in ancestors[i]) sum += angles[a];
            return sum;
        }

        /// <summary>i번 고리의 최종 방위 (0~360, 0 = 北, 시계 방향). 판정·UI가 이것만 본다.</summary>
        public float Bearing(int i)
        {
            EnsureState();
            if (angles == null || i < 0 || i >= angles.Length) return 0f;
            return Mathf.Repeat(RootYaw + angles[i] + Inherited(i), 360f);
        }

        /// <summary>i번 고리가 놓인 칸 (0=北, 1=北東 … 7=北西). 칸에 안 물렸으면 -1.</summary>
        public int Slot(int i)
        {
            EnsureState();
            if (slotOf == null || i < 0 || i >= slotOf.Length) return -1;
            return slotOf[i];
        }

        /// <summary>i번 고리의 축 회전각 (0~360, 도) — 진단용.</summary>
        public float RingAngle(int index)
        {
            EnsureState();
            return angles != null && index >= 0 && index < angles.Length
                ? Mathf.Repeat(angles[index], 360f) : 0f;
        }

        public override string FocusStatus
        {
            get
            {
                if (Locked) return "고리가 모두 제자리에 물렸다.";
                int s = Slot(sel);
                string dir = s >= 0 ? CompassNames.Ko[s] : "―";
                return $"조작 중: {RingName(sel)}   /   현재 방위: {dir}   (휠: 고리 전환)";
            }
        }

        public override string FocusHint =>
            Locked ? "Esc/우클릭: 물러나기" : "드래그: 고리 돌리기   휠: 고리 전환   Esc/우클릭: 물러나기";

        // ── 조작 ──────────────────────────────────────────

        public override void HandleDrag(Vector2 delta)
        {
            EnsureState();
            if (Locked || rings == null || sel >= rings.Length || rings[sel] == null) return;

            raw[sel] += -delta.x * degreesPerPixel;
            ApplyRing(sel);
            dragFrame = Time.frameCount;
            DragSpeed = Mathf.Abs(delta.x);
        }

        /// <summary>이번 프레임 드래그 세기 (픽셀) — 마찰음 크기에 쓴다. 읽고 나면 잦아든다.</summary>
        public float DragSpeed { get; private set; }

        public override void HandleScroll(float direction)
        {
            EnsureState();
            if (rings == null || rings.Length == 0 || Locked) return;
            int n = rings.Length;
            sel = ((sel + (direction > 0f ? -1 : 1)) % n + n) % n;
            ApplyHighlight();
        }

        void Update()
        {
            bool dragging = Time.frameCount - dragFrame <= 1;
            if (wasDragging && !dragging) SettleRing(sel);   // 손을 뗀 순간 반드시 칸에 앉힌다
            wasDragging = dragging;
            if (!dragging) DragSpeed = Mathf.Max(0f, DragSpeed - Time.deltaTime * 400f);
        }

        /// <summary>
        /// 손이 끈 값(raw)을 보고 칸에 물릴지 정한다.
        /// 들어갈 때는 좁게(detentEnter), 빠져나갈 때는 넓게(detentExit) 봐서 경계에서 떨지 않게 한다.
        /// </summary>
        void ApplyRing(int i)
        {
            float rawBearing = Mathf.Repeat(RootYaw + raw[i] + Inherited(i), 360f);
            int slot = Mathf.RoundToInt(rawBearing / slotStep) % Mathf.RoundToInt(360f / slotStep);
            float diff = Mathf.DeltaAngle(rawBearing, slot * slotStep);
            float window = (slotOf[i] == slot) ? detentExit : detentEnter;

            if (Mathf.Abs(diff) <= window)
            {
                angles[i] = raw[i] + diff;
                if (slotOf[i] != slot) { slotOf[i] = slot; RingDetent?.Invoke(i); }
            }
            else
            {
                angles[i] = raw[i];
                slotOf[i] = -1;
            }
            Push(i);
        }

        /// <summary>가장 가까운 칸에 정확히 앉힌다 (손을 뗐을 때·시작할 때).</summary>
        void SettleRing(int i, bool silent = false)
        {
            if (rings == null || i < 0 || i >= rings.Length || rings[i] == null) return;
            float bearing = Mathf.Repeat(RootYaw + raw[i] + Inherited(i), 360f);
            int steps = Mathf.RoundToInt(360f / slotStep);
            int slot = Mathf.RoundToInt(bearing / slotStep) % steps;
            float diff = Mathf.DeltaAngle(bearing, slot * slotStep);

            bool moved = slotOf[i] != slot;
            raw[i] += diff;
            angles[i] = raw[i];
            slotOf[i] = slot;
            Push(i);

            if (silent) return;
            if (moved) RingDetent?.Invoke(i);
            RingSettled?.Invoke(i);
        }

        void Push(int i)
        {
            rings[i].localRotation = baseRot[i] * Quaternion.Euler(0f, angles[i], 0f);
        }

        // ── 표시 ──────────────────────────────────────────

        public override void OnFocusChanged(bool focused)
        {
            EnsureState();
            if (focused) ApplyHighlight(); else ClearHighlight();
            FocusChanged?.Invoke(focused);
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

    /// <summary>여덟 방위 이름표. 나침반 UI와 상태 문구가 같은 것을 쓴다.</summary>
    public static class CompassNames
    {
        /// <summary>0=北 … 7=北西 (시계 방향)</summary>
        public static readonly string[] Han = { "北", "北東", "東", "南東", "南", "南西", "西", "北西" };
        public static readonly string[] Ko = { "북", "북동", "동", "남동", "남", "남서", "서", "북서" };
        /// <summary>네 주방위인가 — 한자를 크게 그릴 자리.</summary>
        public static bool Cardinal(int slot) => (slot & 1) == 0;
    }
}
