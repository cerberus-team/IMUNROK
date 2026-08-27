using UnityEngine;
using IMUNROK.Ui;

namespace IMUNROK.Seocheon
{
    /// <summary>
    /// 서천 대화창을 월드 스페이스 판 위에 얹는 받침대 (2026-08-26).
    ///
    /// ■ 왜 필요한가
    ///   서천 대화창은 Screen Space - Overlay 캔버스였다. Overlay 는 카메라를 거치지 않아
    ///   <b>HMD 에 아예 보이지 않는다</b>. 에디터 게임뷰에서만 멀쩡해 보인다.
    ///   <see cref="VrPanel"/> 을 상속해 판을 월드로 옮기고, 내용물(선택지·워드픽·지난 말)은
    ///   기존 프리팹 계층을 <b>그대로</b> 쓴다. 견우 꾸러미의 DialogueUI 로 갈아끼우지 않는 이유는
    ///   서천에만 있는 워드픽·선택지·대화 로그가 그쪽에는 없기 때문이다.
    ///
    /// ■ 무엇을 물려받는가 (VrPanel 이 해 주는 것)
    ///   · 배율 — <see cref="UiTuning"/> 이 PC 에서 <b>캔버스 1단위 = 화면 1픽셀</b>을 맞춘다.
    ///     그래서 PC 화면은 교체 전과 같아 보인다.
    ///   · 추종 — PC 는 화면 붙박이, VR 은 죽은 구간 7° + 지연 0.16초 (멀미 방지).
    ///   · 벽 회피 — 막히면 앞으로 당기되 배율을 함께 줄여 보이는 각을 지킨다.
    ///
    /// ■ VR 축소 배율을 왜 두는가  ★이 파일에서 가장 중요한 부분
    ///   VR 기준 화면은 <see cref="UiTuning.VrRefHeight"/> = 388 단위(세로 42°)다.
    ///   서천 판은 1920x1080 화면에 맞춰 그린 <b>1500 x 430</b> 이라, 그대로 얹으면
    ///   세로 430 x 0.108°/단위 = <b>46°</b> 로 시야를 통째로 덮는다.
    ///   VrScale() 이 <b>세로 20.3°</b> 가 되게 줄인다 — 팀 확정안과 같은 각이다.
    ///
    ///   ⚠️ 줄이면 글자도 같이 줄어든다. VR 에서 읽히는 하한은 약 1.3° 다:
    ///        상태글 28px x 0.45 = 12.6 단위 x 0.108 = <b>1.36°</b>  (하한 바로 위)
    ///        선택지 32px x 0.45 = 14.4 단위 x 0.108 = <b>1.55°</b>
    ///      이 값들은 <b>계산이지 헤드셋 실측이 아니다.</b> 실물로 확인하고
    ///      안 읽히면 vrTargetVDeg 만 키울 것 — 글자·여백이 함께 따라온다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SeocheonDialogueVrHost : VrPanel
    {
        /// <summary>PC 기준 판 치수 — ★치수표가 정한다(팀 확정 2900×461).</summary>
        public static Vector2 PcSize { get { return new Vector2(SeocheonBarMetrics.Of(false).panelW,
                                                                SeocheonBarMetrics.Of(false).panelH); } }

        [Tooltip("VR 에서 판이 덮을 세로 각(도). 팀 확정안은 20°.")]
        [SerializeField, Range(10f, 40f)] private float vrTargetVDeg = 20.3f;

        [Tooltip("PC 에서 판이 화면 폭의 몇 할을 차지할지. 팀 문서는 90~97%.")]
        [SerializeField, Range(0.5f, 1f)] private float pcWidthRatio = 0.97f;

        [Tooltip("PC 에서 화면 밑변으로부터 띄우는 픽셀. 팀 확정안 46.")]
        [SerializeField] private float pcBottomPx = 46f;

        [Tooltip("판 내용물의 뿌리. 비워 두면 첫 자식을 쓴다.")]
        [SerializeField] private RectTransform content;

        private static SeocheonBarMetrics M { get { return SeocheonBarMetrics.Of(UiModes.IsVr); } }

        protected override Vector2 PanelSize { get { return new Vector2(M.panelW, M.panelH); } }

        protected override Vector2 Pivot { get { return new Vector2(0.5f, 0.5f); } }

        /// <summary>
        /// 화면에서의 자리. PC 는 <b>아래 끝에서 46px</b> 띄운 자리(팀 확정),
        /// VR 은 눈에서 아래로 16° — 둘 다 판 높이가 바뀌면 함께 따라온다.
        /// </summary>
        protected override Vector2 Anchor
        {
            get
            {
                if (UiModes.IsVr) return new Vector2(0.5f, 0.5f - 16f / IMUNROK.Ui.UiTuning.VrContentVFov);
                float h = M.panelH * PcScale();
                float sh = Mathf.Max(1f, Screen.height);
                return new Vector2(0.5f, (pcBottomPx + h * 0.5f) / sh);
            }
        }

        protected override void Awake()
        {
            base.Awake();
            if (GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
                gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }

        /// <summary>
        /// 내용물의 뿌리를 찾는다. ★첫 자식을 그냥 쓰면 안 된다 —
        /// 프리팹에는 <c>EventSystem</c> 이 판보다 앞 자식으로 들어 있어 그것을 물면
        /// 배율이 엉뚱한 오브젝트에 걸려 <b>판이 화면 밖으로 넘친다</b>(실측).
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

        /// <summary>
        /// PC 배율 — 판은 팀 치수 2900 폭으로 짓는데 <see cref="IMUNROK.Ui.UiTuning"/> 은
        /// PC 에서 <b>1단위 = 1픽셀</b>로 맞춘다. 2560px 화면에 2900을 그대로 놓으면 넘치므로
        /// 화면 폭에 맞춰 통째로 줄인다 — ★비율은 팀 값 그대로라 생김새가 안 바뀐다.
        /// </summary>
        private float PcScale()
        {
            float w = Mathf.Max(1f, Screen.width) * pcWidthRatio;
            return Mathf.Min(1f, w / SeocheonBarMetrics.Of(false).panelW);
        }

        /// <summary>
        /// VR 배율 — 판이 <see cref="vrTargetVDeg"/> 만큼만 덮게 줄인다.
        /// ★VrPanel 은 1 m 앞에 세우고 1단위 = (2·tan21°)/388 m 로 잡으므로,
        ///   535단위를 그대로 두면 세로 56° 가 되어 시야를 통째로 덮는다.
        /// </summary>
        private float VrScale()
        {
            const float d = IMUNROK.Ui.UiTuning.VrDistance;
            float unit = 2f * d * Mathf.Tan(IMUNROK.Ui.UiTuning.VrContentVFov * 0.5f * Mathf.Deg2Rad)
                         / IMUNROK.Ui.UiTuning.VrRefHeight;
            float wantM = 2f * d * Mathf.Tan(vrTargetVDeg * 0.5f * Mathf.Deg2Rad);
            float haveM = SeocheonBarMetrics.Of(true).panelH * unit;
            return haveM <= 0.0001f ? 1f : Mathf.Clamp(wantM / haveM, 0.05f, 1f);
        }

        /// <summary>모드에 따라 판 상자와 내용물 배율을 맞춘다. 매 프레임 자리 잡기 전에 불린다.</summary>
        protected override void Refresh()
        {
            if (content == null) Build();      // ★Awake 순서에 기대지 않는다 — 없으면 여기서 찾는다
            if (content == null) return;
            var m = M;
            float s = UiModes.IsVr ? VrScale() : PcScale();

            if (content.sizeDelta != new Vector2(m.panelW, m.panelH))
                content.sizeDelta = new Vector2(m.panelW, m.panelH);
            Vector3 want = new Vector3(s, s, 1f);
            if (content.localScale != want) content.localScale = want;

            // 벽 회피 레이캐스트가 실제로 보이는 크기를 쓰도록 판 상자도 함께 줄인다.
            Vector2 box = new Vector2(m.panelW, m.panelH) * s;
            if (root != null && root.sizeDelta != box) root.sizeDelta = box;
        }
    }
}
