// 카드 화면을 월드 스페이스 판 위에 얹는 받침대. Screen Space - Overlay 는 HMD 에 아예 안 보입니다.
using UnityEngine;
using UnityEngine.UI;
using IMUNROK.Ui;

namespace IMUNROK.Seocheon
{
    /// <summary>
    /// 서천 카드(수첩·결합) 화면의 받침대 (2026-08-27).
    ///
    /// ■ 왜 필요한가
    ///   <c>SeocheonCardCanvas</c> 는 Screen Space - Overlay 였다. Overlay 는 카메라를 거치지 않아
    ///   <b>HMD 에 아예 그려지지 않는다</b> — 에디터 게임뷰에서만 멀쩡해 보인다.
    ///   대화창과 <b>같은 방식</b>으로 <see cref="VrPanel"/> 을 상속해 월드로 옮기고,
    ///   내용물(그리드·슬롯·툴팁)은 기존 프리팹 계층을 <b>그대로</b> 쓴다.
    ///
    /// ■ 판 크기를 어떻게 정했나
    ///
    ///   PC — <b>예전과 똑같이 보이게</b> 한다.
    ///     예전 캔버스는 <c>ScaleWithScreenSize 1920×1080 · match 0.5</c> 였다. 16:9 화면에서는
    ///     화면을 꽉 채운다. <see cref="UiTuning"/> 은 PC 에서 <b>1단위 = 1픽셀</b>로 맞추므로,
    ///     <c>min(W/1920, H/1080)</c> 을 곱하면 같은 결과가 된다.
    ///     ★Min 을 쓰는 이유: 16:9 가 아닌 화면에서 판이 넘치지 않게 하려는 것이다.
    ///
    ///   VR — <b>84° × 53.7°</b>. 꾸러미 소지품 판과 <b>같은 각</b>이다.
    ///     소지품 판 주석: "2.69 × 1.57 m 판을 1.50 m 앞에 = 시야각 84° × 55°".
    ///     카드 화면도 걸음을 멈추고 들여다보는 판이라 같은 급으로 잡았다.
    ///     우리 판은 1920×1080(16:9)이라 가로 84°를 맞추면 세로가 53.7°로 떨어진다.
    ///
    ///     ⚠️ <b>글자가 작다.</b> 이 각에서 1단위 = 0.0537° 다.
    ///        꾸러미 소지품 판의 <b>가장 작은 글자가 26단위(1.39°)</b> 인데,
    ///        카드 화면에는 20~24단위(1.07~1.29°)가 섞여 있어 VR 하한(약 1.3°) 아래로 떨어진다.
    ///        그래서 <see cref="VrMinFont"/> 미만은 VR 에서만 끌어올린다 — 팀이 대화 바에서
    ///        안내줄을 19→34 로 키운 것과 같은 처방이다. PC 로 돌아오면 원래 크기로 되돌린다.
    ///
    ///     ⚠️ 각도·하한 모두 <b>계산이지 헤드셋 실측이 아니다.</b>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SeocheonCardVrHost : VrPanel
    {
        /// <summary>판을 그린 기준 치수. 프리팹이 이 크기로 짜여 있다.</summary>
        public const float RefWidth = 1920f;
        public const float RefHeight = 1080f;

        /// <summary>VR 에서 읽히는 글자 크기의 하한(캔버스 단위). 꾸러미 소지품 판의 최소값과 같다.</summary>
        public const int VrMinFont = 26;

        [Tooltip("VR 에서 판이 덮을 ★가로 각(도). 꾸러미 소지품 판과 같은 84°")]
        [SerializeField, Range(40f, 110f)] private float vrTargetHDeg = 84f;

        [Tooltip("PC 에서 화면을 채우는 비율. 1 이면 예전 CanvasScaler 와 같다")]
        [SerializeField, Range(0.5f, 1f)] private float pcFillRatio = 1f;

        [Tooltip("판 내용물의 뿌리. 비워 두면 EventSystem 이 아닌 첫 자식을 쓴다")]
        [SerializeField] private RectTransform content;

        [Tooltip("이 화면이 열려 있을 때만 판을 그린다")]
        [SerializeField] private SeocheonCardScreen screen;

        // VR 에서 키운 글자를 PC 로 돌아올 때 되돌리기 위한 원래 크기표
        private TMPro.TMP_Text[] labels;
        private float[] baseSizes;
        private bool fontsRaised;

        protected override Vector2 PanelSize { get { return new Vector2(RefWidth, RefHeight); } }
        protected override Vector2 Pivot { get { return new Vector2(0.5f, 0.5f); } }

        /// <summary>★닫혀 있으면 판을 통째로 감춘다 — 받침대는 살아 있고 그림만 사라진다.</summary>
        protected override bool Visible { get { return screen == null || screen.IsOpen; } }

        /// <summary>화면 한가운데. 카드 화면은 구석에 밀어 둘 판이 아니다.</summary>
        protected override Vector2 Anchor { get { return new Vector2(0.5f, 0.5f); } }

        protected override void Awake()
        {
            base.Awake();
            if (screen == null) screen = GetComponent<SeocheonCardScreen>();

            // ★단추(맞춰보기·닫기)는 UGUI 이벤트를 탄다 — World Space 에서는 광선 판정기가 있어야 한다.
            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();

            // ⚠️ worldCamera 가 비면 UGUI 가 화면 좌표를 Overlay 로 해석해 <b>단추가 엉뚱한 자리에서</b>
            //    눌린다. 대화창에서 겪은 그 함정이다.
            if (canvas != null && canvas.worldCamera == null) canvas.worldCamera = Camera.main;
        }

        /// <summary>
        /// 내용물 뿌리를 찾는다. ★첫 자식을 그냥 쓰면 안 된다 —
        /// 프리팹에 <c>EventSystem</c> 이 앞 자식으로 들어 있어 그것을 물면
        /// 배율이 엉뚱한 오브젝트에 걸린다(대화창에서 실측한 함정).
        /// </summary>
        protected override void Build()
        {
            if (content != null || root == null) return;
            for (int i = 0; i < root.childCount; i++)
            {
                var rt = root.GetChild(i) as RectTransform;
                if (rt == null) continue;
                if (rt.GetComponent<UnityEngine.EventSystems.EventSystem>() != null) continue;
                content = rt;
                break;
            }
        }

        /// <summary>PC 배율 — 예전 <c>ScaleWithScreenSize(1920×1080, match 0.5)</c> 와 같은 결과.</summary>
        private float PcScale()
        {
            float w = Mathf.Max(1f, Screen.width) / RefWidth;
            float h = Mathf.Max(1f, Screen.height) / RefHeight;
            return Mathf.Min(w, h) * pcFillRatio;
        }

        /// <summary>
        /// VR 배율 — 판 <b>가로</b>가 <see cref="vrTargetHDeg"/> 만큼만 덮게 줄인다.
        /// <see cref="VrPanel"/> 은 1 m 앞에 세우고 1단위 = 2·tan(21°)/388 m 로 잡으므로,
        /// 1920단위를 그대로 두면 가로가 124° 가 되어 양 끝을 보려면 고개를 돌려야 한다.
        /// </summary>
        private float VrScale()
        {
            const float d = UiTuning.VrDistance;
            float unit = 2f * d * Mathf.Tan(UiTuning.VrContentVFov * 0.5f * Mathf.Deg2Rad) / UiTuning.VrRefHeight;
            float wantM = 2f * d * Mathf.Tan(vrTargetHDeg * 0.5f * Mathf.Deg2Rad);
            float haveM = RefWidth * unit;
            return haveM <= 0.0001f ? 1f : Mathf.Clamp(wantM / haveM, 0.05f, 1f);
        }

        /// <summary>1 캔버스 단위가 VR 에서 덮는 각(도). 글자 크기 검산에 쓴다.</summary>
        public float VrDegreesPerUnit
        {
            get
            {
                const float d = UiTuning.VrDistance;
                float unit = 2f * d * Mathf.Tan(UiTuning.VrContentVFov * 0.5f * Mathf.Deg2Rad) / UiTuning.VrRefHeight;
                float m = unit * VrScale();
                return 2f * Mathf.Atan2(m * 0.5f, d) * Mathf.Rad2Deg;
            }
        }

        protected override void Refresh()
        {
            if (content == null) Build();
            if (content == null) return;

            float s = UiModes.IsVr ? VrScale() : PcScale();

            if (content.sizeDelta != new Vector2(RefWidth, RefHeight))
                content.sizeDelta = new Vector2(RefWidth, RefHeight);
            Vector3 want = new Vector3(s, s, 1f);
            if (content.localScale != want) content.localScale = want;

            // 벽 회피 레이캐스트가 실제로 보이는 크기를 쓰도록 판 상자도 함께 줄인다.
            Vector2 box = new Vector2(RefWidth, RefHeight) * s;
            if (root != null && root.sizeDelta != box) root.sizeDelta = box;

            ApplyFontFloor(UiModes.IsVr);
        }

        /// <summary>
        /// ★VR 에서만 작은 글자를 하한까지 끌어올린다. PC 에서는 원래 크기로 되돌린다.
        ///
        /// 크기만 올리고 상자는 건드리지 않으므로, 넘치는 글은 <see cref="TMPro.TextOverflowModes.Overflow"/>
        /// 로 흘려 보낸다 — ⚠️ TMP 의 Truncate 는 상자에 <b>온전히 안 들어가는 줄을 통째로 버려서</b>
        /// 한 줄이 소리 없이 사라진다(꾸러미 주석에 실측 기록이 있다).
        /// </summary>
        private void ApplyFontFloor(bool vr)
        {
            if (vr == fontsRaised) return;

            if (labels == null)
            {
                labels = GetComponentsInChildren<TMPro.TMP_Text>(true);
                baseSizes = new float[labels.Length];
                for (int i = 0; i < labels.Length; i++) baseSizes[i] = labels[i].fontSize;
            }

            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] == null) continue;
                float want = vr ? Mathf.Max(baseSizes[i], VrMinFont) : baseSizes[i];
                if (!Mathf.Approximately(labels[i].fontSize, want)) labels[i].fontSize = want;
                if (vr) labels[i].overflowMode = TMPro.TextOverflowModes.Overflow;
            }
            fontsRaised = vr;
        }
    }
}
