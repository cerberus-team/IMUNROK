using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace IMUNROK.Ui
{
    /// <summary>
    /// 소지품 판 (2026-08-23, 2026-08-24 확대 개편) — 목록 ↔ 상세의 두 화면.
    ///
    /// ■ 왜 월드 스페이스 캔버스인가
    ///   이 프로젝트의 임시 UI(DebugToast·DebugInteractor)는 IMGUI라 **VR HMD에는 아예 안 보인다**.
    ///   소지품은 실제로 플레이어가 쓰는 화면이므로 처음부터 월드에 세운다 — 지금은 모니터로,
    ///   나중엔 HMD로 같은 판을 본다.
    ///
    /// ■ 카메라에 붙이지 않고 **느슨하게 따라오게** 한다
    ///   머리에 딱 붙이면 시야가 판에 묶여 멀미가 나고, 그렇다고 연 자리에 못 박아 두면
    ///   고개를 숙인 채 꺼낸 물건의 창이 머리 위에 남는다(2026-08-24 실측).
    ///   그래서 죽은 구간 7° + 지연 0.16초로 **시야 한가운데를 천천히 따라간다**.
    ///   가리키기는 마우스(→VR 컨트롤러) 광선이 맡으므로 판이 움직여도 조준은 흔들리지 않는다.
    ///
    /// ■ 입력과의 분리
    ///   이 클래스는 "무엇이 어떻게 보이는가"만 안다. 키·마우스 해석은 InventoryInput 담당이고,
    ///   VR 컨트롤러로 갈아끼울 때도 여기 API(PointAt / Activate / Drag / Scroll / Back)를
    ///   그대로 부르면 된다.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        // ── 판 치수 ─────────────────────────────────────────
        //   캔버스는 1920×1120 단위, 실제 크기는 CanvasScale이 정한다.
        //   **2.69 × 1.57 m 판을 1.50 m 앞에** = 시야각 84° × 55°. Game 뷰(세로 70° FOV)에서
        //   가로·세로 70%대를 덮어 "화면을 거의 채운" 것으로 읽히고, Quest 3(가로 약 110°)에서는
        //   시선을 좌우 ±42°까지만 옮기면 되므로 고개를 크게 돌릴 일이 없다.
        //   ⚠️ 여기서 더 키우면 모서리 칸을 보려고 목을 돌려야 하고, 그 순간 판이 시야
        //      밖으로 밀려난다 — 1.12m 판 시절에 정반대 이유로 겪은 것과 같은 실패다.
        //   글자는 **각도**로 잡았다: 본문 34단위 × 0.0014 = 48mm = 1.8°.
        //   판 치수를 바꿀 일이 생기면 **CanvasScale 하나만** 만지면 된다 — 배치·글자가 함께 큰다.
        const float PanelW = 1920f, PanelH = 1120f;
        const float CanvasScale = 0.0014f;
        const float PanelDistance = 1.50f;
        /// <summary>판이 따라가는 시선 기울기의 한도.
        /// ⚠️ 35°로 조였더니 **반닫이에서 서책을 꺼낼 때 판이 화면 위로 잘려 나갔다**(실측) —
        /// 낮은 가구를 뒤지려면 60° 가까이 숙이게 된다. 이제는 시야를 계속 따라가므로 75°까지
        /// 열어 두되, 완전히 90°는 두지 않는다 — 정수리·발밑에서 판이 수평으로 누워 버린다.</summary>
        const float MaxPitch = 75f;

        /// <summary>이 각도 안에서 고개가 움직이면 판은 가만히 있는다 (VR 멀미 방지).</summary>
        const float FollowDeadZone = 7f;
        /// <summary>시야를 따라잡는 데 걸리는 시간(초). 0.1 미만은 머리에 붙은 듯 답답하고, 0.3 이상은 끌린다.</summary>
        const float FollowTime = 0.16f;

        const int Cols = 5, Rows = 3;
        const int PerPage = Cols * Rows;
        const float CellW = 320f, CellH = 272f, Gap = 28f;

        public static InventoryUI Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        public enum Mode { 목록, 상세, 조사 }
        public Mode CurrentMode { get; private set; } = Mode.목록;
        public bool IsOpen { get; private set; }
        public InventoryHotspot Hovered { get; private set; }
        /// <summary>지금 상세로 보고 있는 물건 (목록 화면이면 null).</summary>
        public IUiItem Viewing { get; private set; }

        /// <summary>획득 직후 자동으로 뜬 상세 화면인가 — 이때는 물러나기가 곧 닫기다.</summary>
        public bool PickupMode { get; private set; }

        /// <summary>
        /// 지금 드래그로 물건을 돌릴 수 있는 화면인가 (상세·조사).
        ///
        /// ⚠️ 입력 측이 이 판정을 따로 들고 있으면 안 된다 — 실제로 조사 모드를 넣을 때
        ///    InventoryUI.Drag만 고치고 InventoryInput의 `CurrentMode == 상세` 조건을 빠뜨려,
        ///    **조사 화면에서만 드래그가 먹지 않았다**(2026-08-26 실측: 판정식 False,
        ///    ui.Drag를 직접 부르면 0°→339°로 잘 돌았다). 화면이 늘어도 여기만 고치면 되게 둔다.
        /// </summary>
        public bool CanRotate => (CurrentMode == Mode.상세 || CurrentMode == Mode.조사) && preview.HasModel;

        /// <summary>
        /// 조사 화면에 물건과 **함께 띄울 특징 줄** (2026-08-24, 서고 장부).
        /// 비워 두면 지금까지처럼 생김새만 보인다. <see cref="Open"/> 직전에 채운다.
        ///
        /// 왜 필요한가: 장부 기물은 홈 개수·눌린 자국·녹 위치 같은 **미세한 차이로만** 갈린다.
        /// 화면에서 픽셀을 뒤지게 하면 추리가 아니라 눈싸움이 된다 — 본 것을 글로 함께 적어 주어
        /// 세 후보를 **논리로 견주게** 한다(기획 요구).
        /// </summary>
        public string[] ExternalTraits { get; set; }

        /// <summary>판이 스스로 닫히길 원할 때(✕ 버튼 등). 입력 측이 걷기·조준 잠금까지 풀어야 한다.</summary>
        public event System.Action CloseRequested;

        // ── 제시 모드 (2026-08-25, 대화 시스템) ─────────────────
        /// <summary>
        /// 지금 판이 <b>대화 중 증거를 고르는 화면</b>인가.
        /// 켜지면 두 가지가 달라진다: 목록의 알맹이가 <see cref="ItemSource"/> 로 바뀌고,
        /// 상세의 쓰임 버튼이 「제시하기」가 된다.
        /// </summary>
        public bool PresentMode { get; private set; }

        /// <summary>
        /// 목록에 무엇을 담을지. 비우면 지금까지처럼 <see cref="Inventory.Items"/> 그대로다.
        ///
        /// 왜 목록을 갈아 끼울 수 있게 했나: 제3사건의 단서는 <b>물건과 정보가 섞여 있다</b>
        /// (A1 지도는 손에 쥐는 것, B3 증언은 들은 것). 플레이어에게는 둘 다 "내가 아는 것"이라
        /// 한 판에서 골라야 하는데, 정보 단서는 소지품 목록에 담기지 않는다.
        /// 그래서 판이 목록의 <b>출처</b>만 남에게 물어보게 했다 — 판은 여전히 물건만 그린다.
        /// </summary>
        public System.Func<IReadOnlyList<IUiItem>> ItemSource;

        /// <summary>제시 모드에서 「제시하기」를 눌렀다.</summary>
        public event System.Action<IUiItem> PresentRequested;

        /// <summary>지금 목록이 보여 줄 것들.</summary>
        IReadOnlyList<IUiItem> Source => ItemSource != null ? ItemSource() : UiItems.Source.Items;
        int SourceCount => Source != null ? Source.Count : 0;

        readonly InventorySkin skin = new InventorySkin();
        readonly InventoryPreview preview = new InventoryPreview();
        readonly List<InventoryHotspot> slots = new List<InventoryHotspot>();

        TMP_FontAsset font;
        Canvas canvas;
        CanvasScaler scaler;
        BoxCollider board;                 // 판 전체 — 커서 위치를 얻는 데 쓴다
        RectTransform listView, detailView;
        TextMeshProUGUI titleText, hintText, emptyText, pageText;
        RectTransform cursor;
        // 상세
        RawImage previewImage;
        TextMeshProUGUI detailName, detailDesc, useLabel, backLabel;
        RectTransform descViewport, descRect;
        InventoryHotspot useSpot, backSpot, prevSpot, nextSpot, zoomSpot;
        Image useFrame;
        float descScroll;
        float wheelReadyAt;           // 이 시각 전의 휠 입력은 무시 (화면 전환 직후 여운 차단)
        int page;
        // 시야 추종
        Transform eye;
        RectTransform panelRoot;      // 판 전체 — 조사 화면에서 통째로 감춘다
        float followDist = PanelDistance;
        bool following;
        // 전체 화면 조사
        InventoryInspect inspect;
        Mode inspectReturn = Mode.목록;   // 조사에서 물러나면 갈 곳 (획득 직후면 판을 닫는다)
        bool inspectClosesAll;

        // ─────────────────────────────────────────────────────────
        public static InventoryUI Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("소지품_판");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<InventoryUI>();
            Instance.Build();
            go.SetActive(false);
            return Instance;
        }

        /// <summary>눈 위치 기준으로 판을 세우고 연다.
        /// <paramref name="showItem"/>을 주면 목록을 건너뛰고 그 물건의 상세로 바로 들어간다.</summary>
        public void Open(Transform eyeTf, IUiItem showItem = null, bool pickup = false)
        {
            if (eyeTf == null) return;
            eye = eyeTf;

            gameObject.SetActive(true);
            ApplyPose(1f);                 // 열 때는 보간 없이 바로 눈앞에
            IsOpen = true;
            PickupMode = pickup && showItem != null;
            page = 0;

            // 처음 손에 넣은 물건은 **글 없이 물건만** — 전체 화면 조사로 연다.
            // 이름도 설명도 소지품에서 상세까지 들어가야 나온다.
            if (PickupMode) ShowInspect(showItem, Mode.목록);
            else if (showItem != null) ShowDetail(showItem);
            else ShowList();
            UiItems.Source.Changed += OnInventoryChanged;
        }

        /// <summary>
        /// 대화 중 <b>증거를 고르는 화면</b>으로 연다 (2026-08-25).
        /// 판·칸·상세·3D 미리보기는 소지품과 완전히 같은 것을 쓴다 — 플레이어에게 새 화면을
        /// 익히게 하지 않는다. 달라지는 건 목록의 출처와 버튼 문구뿐이다.
        /// </summary>
        public void OpenForPresent(Transform eyeTf, System.Func<IReadOnlyList<IUiItem>> source,
                                   System.Action<IUiItem> onPresent)
        {
            PresentMode = true;
            ItemSource = source;
            PresentRequested = null;
            if (onPresent != null) PresentRequested += onPresent;
            Open(eyeTf);
        }

        /// <summary>
        /// 판이 있어야 할 자리·기울기·크기를 계산한다. 시야 정면을 따라가되 기울기는 MaxPitch까지.
        ///
        /// ⚠️ 실내는 좁다 — 벽·장롱이 1m 앞에 있으면 판이 그 속에 박힌다(선아집 실측).
        ///    막힌 만큼 판을 앞으로 당기되 **크기도 같은 비율로 줄여** 보이는 각(84°)은 그대로 둔다.
        ///    네 귀퉁이 방향까지 재야 비스듬한 벽에 모서리만 박히는 경우를 잡는다.
        /// ⚠️ **잰 거리보다 멀리 세우면 안 된다.** 하한을 0.35로 올려 뒀더니 문서장 옆(자유 깊이
        ///    0.18m)에서 판 오른쪽이 장 속에 들어가 잘려 보였다(실측).
        /// </summary>
        void TargetPose(out Vector3 pos, out Quaternion rot, out float dist)
        {
            Vector3 fwd = eye.forward;
            Vector3 flat = new Vector3(fwd.x, 0f, fwd.z);
            if (flat.sqrMagnitude < 0.0001f) flat = eye.up * Mathf.Sign(-fwd.y);   // 정수리·발밑을 볼 때
            flat.Normalize();
            float pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(fwd.y, -1f, 1f)) * Mathf.Rad2Deg, -MaxPitch, MaxPitch);
            rot = Quaternion.LookRotation(flat, Vector3.up) * Quaternion.Euler(-pitch, 0f, 0f);

            dist = PanelDistance;
            Vector3 ahead = rot * Vector3.forward;
            foreach (var corner in PanelCornerDirs(rot))
                if (Physics.Raycast(eye.position, corner, out var hit, PanelDistance * 1.2f, ~0, QueryTriggerInteraction.Ignore))
                    dist = Mathf.Min(dist, hit.distance * Vector3.Dot(corner, ahead) - 0.08f);   // 판 면까지의 수직 거리로 환산
            dist = Mathf.Max(0.18f, dist);
            pos = eye.position + ahead * dist;
        }

        /// <summary>목표 자세로 t만큼 다가간다 (t=1이면 즉시).</summary>
        void ApplyPose(float t)
        {
            TargetPose(out var pos, out var rot, out float dist);
            if (t >= 1f)
            {
                transform.SetPositionAndRotation(pos, rot);
                followDist = dist;
            }
            else
            {
                transform.SetPositionAndRotation(Vector3.Lerp(transform.position, pos, t),
                                                 Quaternion.Slerp(transform.rotation, rot, t));
                followDist = Mathf.Lerp(followDist, dist, t);
            }
            transform.localScale = Vector3.one * CanvasScale * (followDist / PanelDistance);
        }

        /// <summary>
        /// 시야 추종 (2026-08-25). 판이 열려 있는 동안 **늘 시야 한가운데**에 있게 한다 —
        /// 예전에는 연 자리에 못 박혀 있어서, 아래를 보고 꺼낸 물건의 창이 머리 위에 남았다.
        ///
        /// ■ VR 편안함을 위해 두 가지를 넣었다
        ///   ① 죽은 구간 <see cref="FollowDeadZone"/>° — 고개의 미세한 떨림까지 따라가면 판이
        ///      끊임없이 흔들려 멀미가 난다. 살짝 움직이는 동안에는 그 자리에 가만히 둔다.
        ///   ② 지연 <see cref="FollowTime"/>초 — 벗어나면 부드럽게 따라온다. 0.1초 아래는 머리에
        ///      붙은 듯해 답답하고, 0.3초 위는 판이 질질 끌린다. 0.16초가 둘 사이의 자리다.
        ///   카메라가 Update에서 움직이므로 **LateUpdate**에서 따라간다 (한 프레임 밀리지 않게).
        /// </summary>
        void LateUpdate()
        {
            if (!IsOpen || eye == null) return;

            // 모드에 따라 글리프 굽는 밀도만 갈아 끼운다.
            // ⚠️ 판의 **각 크기**(84°×55°)와 거리(1.5 m)는 모드에 상관없이 그대로 둔다.
            //    이 판은 처음부터 VR 기준으로 잡힌 것이다 — 본문 34단위 × 0.0014 ÷ 1.5 m = 1.8°로,
            //    이미 VR에서 편히 읽히는 하한(약 1.3°) 언저리다. 여기서 각을 줄이면 글자가
            //    같이 작아져 못 읽게 되고, 늘리면 모서리 칸을 보려고 목을 돌려야 한다.
            //    바꾸려면 배치를 다시 짜야 하는 일이라, 헤드셋으로 재 보기 전에는 건드리지 않는다.
            if (scaler != null)
            {
                float dpu = UiModes.IsVr ? UiTuning.VrPixelsPerUnit : UiTuning.PcPixelsPerUnit;
                if (!Mathf.Approximately(scaler.dynamicPixelsPerUnit, dpu)) scaler.dynamicPixelsPerUnit = dpu;
            }
            // 조작 이름(좌클릭/트리거 …)이 모드 따라 갈린다 — 바뀌었으면 곧바로 고쳐 적는다
            if (hintText != null && CurrentMode != Mode.조사)
            {
                string h = HintFor();
                if (hintText.text != h) hintText.text = h;
            }

            TargetPose(out _, out var rot, out _);
            float off = Quaternion.Angle(transform.rotation, rot);
            if (off > FollowDeadZone) following = true;
            else if (off < FollowDeadZone * 0.35f) following = false;
            if (!following) return;
            ApplyPose(1f - Mathf.Exp(-Time.unscaledDeltaTime / FollowTime));
        }

        /// <summary>판 중심 + 네 귀퉁이를 향하는 방향들 — 자리 확보용 레이캐스트에 쓴다.</summary>
        static IEnumerable<Vector3> PanelCornerDirs(Quaternion rot)
        {
            float hx = PanelW * CanvasScale * 0.5f, hy = PanelH * CanvasScale * 0.5f;
            yield return rot * Vector3.forward;
            yield return rot * new Vector3(-hx, -hy, PanelDistance).normalized;
            yield return rot * new Vector3(hx, -hy, PanelDistance).normalized;
            yield return rot * new Vector3(-hx, hy, PanelDistance).normalized;
            yield return rot * new Vector3(hx, hy, PanelDistance).normalized;
        }

        public void Close()
        {
            if (!IsOpen) return;
            UiItems.Source.Changed -= OnInventoryChanged;
            IsOpen = false;
            PickupMode = false;
            PresentMode = false;
            ItemSource = null;
            PresentRequested = null;
            Hovered = null;
            Viewing = null;
            if (inspect != null) inspect.Hide();
            ExternalTraits = null;   // 다음에 여는 물건이 남의 특징 줄을 물려받지 않게
            if (panelRoot != null) panelRoot.gameObject.SetActive(true);   // 다음에 열 때를 위해
            CurrentMode = Mode.목록;
            preview.Clear();
            preview.SetRendering(false);
            gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            UiItems.Source.Changed -= OnInventoryChanged;
            preview.Dispose();
            skin.Dispose();
            if (Instance == this) Instance = null;
        }

        void OnInventoryChanged() { if (CurrentMode == Mode.목록) RefreshList(); }

        // ── 화면 전환 ─────────────────────────────────────────
        public void ShowList()
        {
            CurrentMode = Mode.목록;
            PickupMode = false;
            Viewing = null;
            Hovered = null;
            preview.Clear();
            preview.SetRendering(false);
            listView.gameObject.SetActive(true);
            detailView.gameObject.SetActive(false);
            wheelReadyAt = Time.unscaledTime + 0.35f;
            titleText.text = PresentMode ? "무 엇 을  내 밀 까" : "소  지  품";
            hintText.text = HintFor();
            RefreshList();
        }

        public void ShowDetail(IUiItem item)
        {
            if (item == null) return;
            CurrentMode = Mode.상세;
            Viewing = item;
            Hovered = null;
            listView.gameObject.SetActive(false);
            detailView.gameObject.SetActive(true);
            titleText.text = PresentMode ? "내밀 것  ▶  살펴보기" : (PickupMode ? "새로  얻은  것" : "소 지 품  ▶  자세히");

            detailName.text = item.DisplayName;
            detailDesc.text = string.IsNullOrEmpty(item.Description) ? "(적힌 것이 없다)" : item.Description;
            descScroll = 0f;
            wheelReadyAt = Time.unscaledTime + 0.35f;
            ApplyDescScroll();

            preview.Show(item);
            preview.SetRendering(true);
            previewImage.texture = preview.Texture;
            previewImage.enabled = preview.HasModel;

            // 쓰임 버튼은 **물건에 따라 있고 없다.** 서책·문서처럼 읽으면 끝인 것에는 아예 안 뜬다.
            // 다만 제시 모드에서는 무엇을 고르든 내밀 수 있어야 하므로 늘 띄운다.
            bool showUse = PresentMode || item.ShowUseButton;
            useSpot.gameObject.SetActive(showUse);
            if (showUse)
            {
                useSpot.interactable = true;
                // 문구도 ItemUse에 물어본다 — 하는 일이 상황에 따라 달라지는 물건이 있다
                useLabel.text = PresentMode ? "내 밀 기" : UiItems.Use.LabelFor(item);
                useLabel.color = InventorySkin.Hanji;
                useFrame.color = InventorySkin.Vermilion;
                useSpot.idleColor = useFrame.color;
                useSpot.hoverColor = new Color(0.82f, 0.33f, 0.24f);
            }

            // 획득 직후에는 목록이 아니라 게임으로 돌아간다 — 버튼 문구도 그렇게 읽히게
            backLabel.text = PickupMode ? "×   닫기" : "◀   목록으로";
            // 버튼이 하나뿐이면 가운데로 — 오른쪽에 홀로 치우쳐 있으면 빈 자리가 눈에 걸린다
            ((RectTransform)backSpot.transform).anchoredPosition = new Vector2(showUse ? 570f : 390f, -300f);
            bool many = !PickupMode && SourceCount > 1;
            prevSpot.gameObject.SetActive(many);
            nextSpot.gameObject.SetActive(many);

            zoomSpot.gameObject.SetActive(preview.HasModel);

            hintText.text = HintFor();
        }

        /// <summary>
        /// 지금 화면의 조작 안내. 조작 <b>이름</b>만 모드에 따라 갈린다 (<see cref="UiWords"/>) —
        /// 문장을 두 벌로 두지 않는다. 매 프레임 다시 만들어 F8로 모드를 바꿔도 곧바로 따라온다.
        /// </summary>
        string HintFor()
        {
            if (CurrentMode == Mode.상세)
                return preview.HasModel
                    ? "드래그 — 돌리기        돋보기 — 크게 보기        " + UiWords.Wheel + "(글 위) — 굴려 읽기        " + UiWords.BackShort + " — 목록"
                    : UiWords.Wheel + "(글 위) — 굴려 읽기        " + UiWords.Back + " — 물러나기";
            return PresentMode
                ? UiWords.Aim + " " + UiWords.Press + " — 고르기        " + UiWords.Wheel + " — 쪽 넘기기        " + UiWords.Back + " — 대화로"
                : UiWords.Aim + " " + UiWords.Press + " — 자세히 보기        " + UiWords.Wheel + " — 쪽 넘기기        " + UiWords.Menu + " — 닫기";
        }

        // ── 전체 화면 조사 ────────────────────────────────────
        /// <summary>물건 하나만 어두운 막 위에 크게 띄운다. 글은 없다 — 생김새만 본다.</summary>
        public void ShowInspect(IUiItem item, Mode back)
        {
            if (item == null) return;
            inspect = InventoryInspect.Ensure(eye, skin, font);
            inspectReturn = back;
            inspectClosesAll = PickupMode;
            CurrentMode = Mode.조사;
            Hovered = null;
            Viewing = item;

            // 상세에서 들어온 경우엔 이미 무대에 올라 있다 — 그 각도·배율을 그대로 이어 본다
            if (!preview.HasModel || preview.ShowingItem != item) preview.Show(item);
            preview.SetRendering(true);

            panelRoot.gameObject.SetActive(false);   // 한지 판은 통째로 물러난다
            inspect.Show(preview.Texture, ExternalTraits);
            wheelReadyAt = Time.unscaledTime + 0.35f;
        }

        /// <summary>조사에서 물러난다 — 들어온 곳으로 되돌아가거나, 획득 직후였으면 판을 닫는다.</summary>
        void CloseInspect()
        {
            if (inspect != null) inspect.Hide();
            panelRoot.gameObject.SetActive(true);
            if (inspectClosesAll) return;   // 획득 직후였다 — 부르는 쪽이 판까지 닫는다
            if (inspectReturn == Mode.상세 && Viewing != null) ShowDetail(Viewing);
            else ShowList();
        }

        /// <summary>뒤로 한 단계. false를 주면 "더 물러날 곳이 없으니 판을 닫아라"는 뜻이다.</summary>
        public bool Back()
        {
            if (CurrentMode == Mode.조사) { CloseInspect(); return !inspectClosesAll; }
            if (CurrentMode == Mode.상세 && !PickupMode) { ShowList(); return true; }
            return false;
        }

        // ── 포인터 (마우스 / 컨트롤러 광선) ───────────────────
        /// <summary>
        /// 광선을 받아 조준점을 옮기고 호버를 갱신한다. 판 안을 가리키고 있으면 true.
        ///
        /// 조준점은 콜라이더가 아니라 **판 평면과의 교점**으로 잡는다 — 광선이 판 밖을 향해도
        /// 커서가 사라지지 않고 가장자리로 따라간다(가장자리에서 놓치면 어디를 가리키는지 모른다).
        /// 자리(칸·버튼) 판정만 콜라이더가 맡는다.
        /// </summary>
        public bool PointAt(Ray ray)
        {
            Hovered = null;
            var hits = Physics.RaycastAll(ray, 8f, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                var spot = h.collider.GetComponent<InventoryHotspot>();
                if (spot != null && spot.interactable && spot.gameObject.activeInHierarchy)
                {
                    Hovered = spot;
                    break;
                }
                if (h.collider == board) break;   // 판 뒤의 씬 물체까지 볼 이유가 없다
            }

            // 조사 중에는 조준점이 조사 판 위에 있다 (한지 판은 물러나 있다)
            bool onBoard;
            if (CurrentMode == Mode.조사)
            {
                onBoard = inspect != null && inspect.PointAt(ray);
            }
            else
            {
                // 판 평면(법선 = 판의 −forward, 보는 쪽) 과의 교점
                onBoard = false;
                var plane = new Plane(-transform.forward, transform.position);
                if (plane.Raycast(ray, out float dist))
                {
                    // InverseTransformPoint가 캔버스 스케일까지 풀어 주므로 결과가 이미 캔버스 단위다.
                    // (여기서 CanvasScale로 한 번 더 나누면 좌표가 1000배로 튄다 — 2026-08-23 실측)
                    var local = transform.InverseTransformPoint(ray.GetPoint(dist));
                    float hw = PanelW * 0.5f, hh = PanelH * 0.5f;
                    onBoard = Mathf.Abs(local.x) <= hw && Mathf.Abs(local.y) <= hh;
                    cursor.anchoredPosition = new Vector2(Mathf.Clamp(local.x, -hw, hw), Mathf.Clamp(local.y, -hh, hh));
                    cursor.gameObject.SetActive(true);
                }
                else cursor.gameObject.SetActive(false);
            }

            foreach (var s in slots) s.SetHovered(s == Hovered);
            zoomSpot.SetHovered(zoomSpot == Hovered);
            useSpot.SetHovered(useSpot == Hovered);
            backSpot.SetHovered(backSpot == Hovered);
            prevSpot.SetHovered(prevSpot == Hovered);
            nextSpot.SetHovered(nextSpot == Hovered);
            return onBoard;
        }

        /// <summary>가리킨 자리를 누른다.</summary>
        public void Activate(InventoryHotspot spot)
        {
            if (spot == null || !spot.interactable) return;
            switch (spot.kind)
            {
                case InventoryHotspot.Kind.칸:
                    int i = page * PerPage + spot.index;
                    if (i >= 0 && i < SourceCount) ShowDetail(Source[i]);
                    break;
                case InventoryHotspot.Kind.뒤로:
                    if (!Back()) CloseRequested?.Invoke();
                    break;
                case InventoryHotspot.Kind.닫기:
                    CloseRequested?.Invoke();
                    break;
                case InventoryHotspot.Kind.쪽넘김:
                    SetPage(page + spot.index);
                    break;
                case InventoryHotspot.Kind.이웃:
                    ShowNeighbour(spot.index);
                    break;
                case InventoryHotspot.Kind.돋보기:
                    ShowInspect(Viewing, Mode.상세);
                    break;
                case InventoryHotspot.Kind.조사닫기:
                    if (!Back()) CloseRequested?.Invoke();
                    break;
                case InventoryHotspot.Kind.사용:
                    if (Viewing == null) break;
                    // 제시 모드에서는 이 버튼이 「제시하기」다 — 물건의 쓰임과 섞이지 않게 먼저 가른다.
                    if (PresentMode) { PresentRequested?.Invoke(Viewing); break; }
                    // 무엇을 할지는 판이 알 바가 아니다 — ItemUse가 물건 id로 갈라 보낸다.
                    // 맡은 데가 없으면(아직 동작이 안 붙은 물건) 그 물건의 안내 문구만 띄운다.
                    if (UiItems.Use.Try(Viewing, () => CloseRequested?.Invoke())) break;
                    DebugToast.Show(string.IsNullOrEmpty(Viewing.UseNotReadyHint)
                        ? "아직 여기서 쓸 수 없다." : Viewing.UseNotReadyHint, 2.5f);
                    break;
            }
        }

        /// <summary>상세에서 앞뒤 물건으로 넘어간다 (양 끝은 돌아 감는다).</summary>
        public void ShowNeighbour(int dir)
        {
            int n = SourceCount;
            if (n <= 1 || Viewing == null) return;
            int cur = 0;
            for (int i = 0; i < n; i++) if (Source[i] == Viewing) { cur = i; break; }
            ShowDetail(Source[((cur + dir) % n + n) % n]);
        }

        /// <summary>드래그 — 상세에서 모델을 돌린다 (버튼 위가 아닐 때만 입력 측이 부른다).</summary>
        public void Drag(Vector2 pixelDelta)
        {
            if (CanRotate) preview.Rotate(pixelDelta);
        }

        /// <summary>휠 — 가리키는 곳에 따라 확대·축소 / 글 굴리기 / 쪽 넘기기.</summary>
        public void Scroll(float direction)
        {
            // 화면이 막 바뀐 직후의 휠은 흘려 보낸다 — 목록에서 휠로 쪽을 넘기다 칸을 고르면
            // 그 여운이 상세 설명을 첫 줄부터 밀어 버린다(실측: 설명이 끝까지 굴러간 채로 떴다).
            if (Time.unscaledTime < wheelReadyAt) return;
            if (CurrentMode == Mode.조사) { preview.Zoom(direction); return; }   // 조사 중엔 오직 확대·축소
            if (CurrentMode == Mode.목록) { SetPage(page + (direction > 0f ? -1 : 1)); return; }
            if (Hovered != null && Hovered.kind == InventoryHotspot.Kind.설명)
            {
                descScroll = Mathf.Max(0f, descScroll + (direction > 0f ? -50f : 50f));
                ApplyDescScroll();
            }
            else preview.Zoom(direction);
        }

        void SetPage(int p)
        {
            int pages = Mathf.Max(1, Mathf.CeilToInt(SourceCount / (float)PerPage));
            page = Mathf.Clamp(p, 0, pages - 1);
            RefreshList();
        }

        void ApplyDescScroll()
        {
            // 글이 뷰포트보다 짧으면 굴릴 것이 없다
            Canvas.ForceUpdateCanvases();
            float overflow = Mathf.Max(0f, detailDesc.preferredHeight - descViewport.rect.height);
            descScroll = Mathf.Min(descScroll, overflow);
            descRect.anchoredPosition = new Vector2(descRect.anchoredPosition.x, descScroll);
        }

        // ── 목록 갱신 ─────────────────────────────────────────
        void RefreshList()
        {
            int total = SourceCount;
            int pages = Mathf.Max(1, Mathf.CeilToInt(total / (float)PerPage));
            page = Mathf.Clamp(page, 0, pages - 1);

            for (int i = 0; i < slots.Count; i++)
            {
                int idx = page * PerPage + i;
                var spot = slots[i];
                bool on = idx < total;
                spot.gameObject.SetActive(on);
                if (!on) continue;
                var item = Source[idx];
                spot.label.text = item.DisplayName;
                var tex = preview.Thumbnail(item);
                spot.icon.texture = tex;
                spot.icon.enabled = tex != null;
                spot.interactable = true;
                spot.SetHovered(false);
            }

            emptyText.gameObject.SetActive(total == 0);
            pageText.gameObject.SetActive(pages > 1);
            pageText.text = (page + 1) + " / " + pages + " 쪽";
        }

        // ── 판 짓기 ───────────────────────────────────────────
        void Build()
        {
            font = MakeFont();

            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            // ⚠️ dynamicPixelsPerUnit을 높이면 오히려 흐려진다 — 1캔버스단위 ≒ 0.6화면픽셀이라
            //    3으로 구우면 5배 축소되며 뭉갠다(2026-08-23 실측). 실제 화면 밀도에 맞춘 1이 가장 또렷하다.
            //    ⚠️ 단 그 "0.6"은 **모니터** 밀도다. HMD는 눈당 렌더 해상도가 더 촘촘하고 Link가
            //       슈퍼샘플링까지 하므로 VR에서는 더 높은 값이 맞다 — UiTuning이 모드별로 준다.
            //       (VR 쪽 값은 계산상 타당할 뿐 실측이 아니다. 헤드셋에서 견주어 볼 것)
            scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = UiTuning.PcPixelsPerUnit;
            var canvasRt = (RectTransform)transform;
            canvasRt.sizeDelta = new Vector2(PanelW, PanelH);
            canvasRt.localScale = Vector3.one * CanvasScale;

            // 판의 모든 것은 이 뿌리 밑에 — 전체 화면 조사로 들어가면 통째로 물러난다
            panelRoot = MakeRect(canvasRt, "판");
            Stretch(panelRoot, 0f);
            var rt = panelRoot;

            // 목재 틀 ▸ 한지 ▸ 안쪽 테선 ▸ 한지 — 네 겹으로 겹쳐 틀과 테선을 만든다
            Stretch(MakeImage(rt, "틀", skin.Wood_, Color.white), 40f);
            var paper = MakeImage(rt, "바탕", skin.Hanji_, Color.white);
            Stretch(paper, 0f);
            board = paper.gameObject.AddComponent<BoxCollider>();
            board.isTrigger = true;
            board.size = new Vector3(PanelW, PanelH, 2f);
            Stretch(MakeImage(rt, "테선", skin.Wood_, InventorySkin.Wood), -20f);
            Stretch(MakeImage(rt, "속지", skin.Hanji_, Color.white), -26f);

            titleText = MakeText(rt, "제목", 56, TextAnchor.MiddleCenter, InventorySkin.Ink);
            Place(titleText.rectTransform, new Vector2(0f, 476f), new Vector2(1400f, 84f));

            var rule = MakeImage(rt, "구분선", skin.Wood_, InventorySkin.Wood);
            Place(rule, new Vector2(0f, 424f), new Vector2(1760f, 4f));

            hintText = MakeText(rt, "안내", 26, TextAnchor.MiddleCenter, InventorySkin.InkSoft);
            Place(hintText.rectTransform, new Vector2(0f, -516f), new Vector2(1840f, 40f));

            // 왼쪽 위 닫기 — 참고 이미지의 ✕ 자리
            TextMeshProUGUI xLabel; Image xFrame;
            MakeButton(rt, "닫기", new Vector2(-836f, 470f), new Vector2(92f, 92f),
                                       InventoryHotspot.Kind.닫기, InventorySkin.Wood, 44, out xFrame, out xLabel);
            xLabel.text = "×";

            BuildList(rt);
            BuildDetail(rt);

            // 조준점 — 판 위에 찍히는 커서 (VR에서도 그대로 보인다)
            var dot = MakeImage(rt, "조준", skin.Dot_, new Color(1f, 0.95f, 0.8f, 0.95f));
            Place(dot, Vector2.zero, new Vector2(40f, 40f));
            cursor = dot;
            cursor.SetAsLastSibling();
        }

        void BuildList(RectTransform parent)
        {
            listView = MakeRect(parent, "목록");
            Stretch(listView, 0f);

            float startX = -(Cols * CellW + (Cols - 1) * Gap) * 0.5f + CellW * 0.5f;
            float startY = 246f;
            for (int i = 0; i < PerPage; i++)
            {
                int col = i % Cols, row = i / Cols;
                var cell = MakeImage(listView, "칸_" + i, skin.Wood_, InventorySkin.Wood);
                Place(cell, new Vector2(startX + col * (CellW + Gap), startY - row * (CellH + Gap)),
                      new Vector2(CellW, CellH));

                // 칸 속 — 테두리(목재)가 바깥, 한지 속살이 안쪽
                Stretch(MakeImage(cell, "속", skin.Slot_, Color.white), -8f);

                // 위 = 물건 그림 (미리보기 무대에서 뽑은 3D 한 장), 아래 = 이름
                var iconGo = new GameObject("그림", typeof(RectTransform), typeof(RawImage));
                iconGo.transform.SetParent(cell, false);
                var icon = iconGo.GetComponent<RawImage>();
                icon.raycastTarget = false;
                Place((RectTransform)iconGo.transform, new Vector2(0f, 40f), new Vector2(196f, 196f));

                var label = MakeText(cell, "이름", 30, TextAnchor.UpperCenter, InventorySkin.Ink);
                Place(label.rectTransform, new Vector2(0f, -96f), new Vector2(292f, 80f));

                var box = cell.gameObject.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(CellW, CellH, 2f);
                box.center = new Vector3(0f, 0f, -6f);   // 바탕보다 앞 — 광선이 먼저 맞는다
                var spot = cell.gameObject.AddComponent<InventoryHotspot>();
                spot.kind = InventoryHotspot.Kind.칸;
                spot.index = i;
                spot.frame = cell.GetComponent<Image>();
                spot.icon = icon;
                spot.label = label;
                spot.idleColor = InventorySkin.Wood;
                spot.hoverColor = InventorySkin.Vermilion;
                slots.Add(spot);
            }

            emptyText = MakeText(listView, "빈안내", 36, TextAnchor.MiddleCenter, InventorySkin.InkSoft);
            Place(emptyText.rectTransform, new Vector2(0f, 0f), new Vector2(1200f, 80f));
            emptyText.text = "아직 지닌 것이 없다.";

            pageText = MakeText(listView, "쪽", 26, TextAnchor.MiddleCenter, InventorySkin.InkSoft);
            Place(pageText.rectTransform, new Vector2(0f, -452f), new Vector2(400f, 36f));
        }

        void BuildDetail(RectTransform parent)
        {
            detailView = MakeRect(parent, "상세");
            Stretch(detailView, 0f);

            // 왼쪽 — 3D 그림자리
            var stageFrame = MakeImage(detailView, "그림틀", skin.Wood_, InventorySkin.Wood);
            Place(stageFrame, new Vector2(-470f, 34f), new Vector2(740f, 740f));
            Stretch(MakeImage(stageFrame, "그림바탕", skin.Slot_, Color.white), -10f);
            var img = new GameObject("그림", typeof(RectTransform), typeof(RawImage));
            img.transform.SetParent(stageFrame, false);
            previewImage = img.GetComponent<RawImage>();
            previewImage.raycastTarget = false;
            Stretch((RectTransform)img.transform, -10f);

            // 그림자리 오른쪽 아래 — 돋보기 (쇼핑몰 상품 확대 버튼 자리)
            TextMeshProUGUI zl; Image zf;
            zoomSpot = MakeButton(stageFrame, "돋보기", new Vector2(280f, -280f), new Vector2(148f, 148f),
                                  InventoryHotspot.Kind.돋보기, InventorySkin.Wood, 52, out zf, out zl);
            zl.text = "";   // 글꼴에 돋보기 글리프가 없다 — 직접 그린 것을 얹는다
            Stretch(MakeImage(zoomSpot.transform, "쇠", skin.Glass_, Color.white), -24f);

            // 오른쪽 — 이름 · 설명
            const float TextX = 410f, TextW = 940f;
            detailName = MakeText(detailView, "이름", 46, TextAnchor.MiddleLeft, InventorySkin.Ink);
            Place(detailName.rectTransform, new Vector2(TextX, 340f), new Vector2(TextW, 72f));
            detailName.fontStyle = FontStyles.Bold;

            var nameRule = MakeImage(detailView, "이름줄", skin.Wood_, InventorySkin.Wood);
            Place(nameRule, new Vector2(TextX, 296f), new Vector2(TextW, 4f));

            descViewport = MakeRect(detailView, "설명틀");
            Place(descViewport, new Vector2(TextX, 50f), new Vector2(TextW, 440f));
            descViewport.gameObject.AddComponent<RectMask2D>();

            detailDesc = MakeText(descViewport, "설명", 34, TextAnchor.UpperLeft, InventorySkin.Ink);
            descRect = detailDesc.rectTransform;
            descRect.anchorMin = new Vector2(0f, 1f);
            descRect.anchorMax = new Vector2(1f, 1f);
            descRect.pivot = new Vector2(0.5f, 1f);
            descRect.anchoredPosition = Vector2.zero;
            descRect.sizeDelta = new Vector2(0f, 440f);   // 가로는 앵커로 늘어난다 — 여기 값을 주면 폭이 두 배가 된다
            detailDesc.overflowMode = TextOverflowModes.Overflow;
            detailDesc.lineSpacing = UiSkin.LineSpacing(1.3f);

            // 설명 영역도 자리 — 휠을 여기서 굴리면 글이 움직인다
            var descBox = descViewport.gameObject.AddComponent<BoxCollider>();
            descBox.isTrigger = true;
            descBox.size = new Vector3(TextW, 440f, 2f);
            descBox.center = new Vector3(0f, 0f, -6f);
            descViewport.gameObject.AddComponent<InventoryHotspot>().kind = InventoryHotspot.Kind.설명;

            // 버튼 두 개
            useSpot = MakeButton(detailView, "사용하기", new Vector2(210f, -300f), new Vector2(300f, 84f),
                                 InventoryHotspot.Kind.사용, InventorySkin.Vermilion, 32, out useFrame, out useLabel);
            Image backFrame;
            backSpot = MakeButton(detailView, "목록으로", new Vector2(570f, -300f), new Vector2(300f, 84f),
                                  InventoryHotspot.Kind.뒤로, InventorySkin.Wood, 32, out backFrame, out backLabel);
            backLabel.text = "◀   목록으로";

            // 좌우 화살표 — 소지품 사이를 넘긴다
            TextMeshProUGUI pl, nl; Image pf, nf;
            prevSpot = MakeButton(detailView, "이전", new Vector2(-898f, 34f), new Vector2(64f, 150f),
                                  InventoryHotspot.Kind.이웃, InventorySkin.Wood, 40, out pf, out pl);
            prevSpot.index = -1; pl.text = "◀";
            nextSpot = MakeButton(detailView, "다음", new Vector2(898f, 34f), new Vector2(64f, 150f),
                                  InventoryHotspot.Kind.이웃, InventorySkin.Wood, 40, out nf, out nl);
            nextSpot.index = 1; nl.text = "▶";
        }

        InventoryHotspot MakeButton(RectTransform parent, string name, Vector2 pos, Vector2 size,
                                    InventoryHotspot.Kind kind, Color baseColor, int fontSize,
                                    out Image frame, out TextMeshProUGUI label)
        {
            var btn = MakeImage(parent, name, skin.Wood_, baseColor);
            Place(btn, pos, size);
            frame = btn.GetComponent<Image>();
            label = MakeText(btn, "글", fontSize, TextAnchor.MiddleCenter, InventorySkin.Hanji);
            Stretch(label.rectTransform, -6f);
            label.text = name;

            var box = btn.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(size.x, size.y, 2f);
            box.center = new Vector3(0f, 0f, -6f);
            var spot = btn.gameObject.AddComponent<InventoryHotspot>();
            spot.kind = kind;
            spot.frame = frame;
            spot.idleColor = baseColor;
            spot.hoverColor = Color.Lerp(baseColor, InventorySkin.Gold, 0.55f);
            return spot;
        }

        // ── 작은 도구들 ───────────────────────────────────────
        /// <summary>본문 글꼴 — 판마다 따로 만들지 않고 <see cref="UiSkin.Font"/> 하나를 함께 쓴다.
        /// 2026-08-26 에 OS 글꼴(맑은 고딕)에서 TMP 폰트 에셋(조선 궁서체)으로 옮겼다.</summary>
        static TMPro.TMP_FontAsset MakeFont() { return UiSkin.Font; }

        static RectTransform MakeRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static RectTransform MakeImage(Transform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;   // UGUI 이벤트는 안 쓴다 — 판정은 콜라이더 광선이 한다
            return (RectTransform)go.transform;
        }

        TextMeshProUGUI MakeText(Transform parent, string name, int size, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = UiSkin.Dress(go.GetComponent<TextMeshProUGUI>(), size, anchor, color);
            t.textWrappingMode = TextWrappingModes.Normal;
            // ⚠️ 레거시의 Truncate 를 TMP 의 Truncate 로 그대로 옮기면 안 된다 (2026-08-26 실측).
            //    TMP 는 상자 높이에 <b>온전히 들어가지 않는 줄을 통째로 버린다</b>. 조선 궁서체는
            //    줄 높이가 글자 크기의 1.25배라 맑은 고딕(약 1.18배)보다 높은데, IMGUI 시절 숫자로
            //    잡아 둔 상자들이 그만큼의 여유가 없다 — 대화창 아래 조작 안내(22px 글, 26px 상자)가
            //    <b>한 줄 통째로 사라졌다</b>. 1.5px 모자란 것이 원인이라 화면에서는 원인이 안 보인다.
            //    레거시가 실제로 그리던 모습은 Overflow 쪽이다 — 여러 줄 글은 어차피 판(RectMask2D)이
            //    잘라 주므로 여기서 버릴 이유가 없다.
            t.overflowMode = TextOverflowModes.Overflow;
            t.richText = true;
            return t;
        }

        /// <summary>부모를 꽉 채운다. inset이 음수면 그만큼 안으로 물린다(테두리가 드러난다).</summary>
        static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-inset, -inset);
            rt.offsetMax = new Vector2(inset, inset);
        }

        static void Place(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
    }
}
