using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IMUNROK.Common
{
    /// <summary>
    /// 수첩(手帖)의 화면 — 지금 사건에서 <b>주운 물증과 들은 말</b>을 카드로 깔고,
    /// 심문 중이면 들이밀 수 있는 것에 "들이밀기" 버튼을 붙인다.
    ///
    /// 상태(열림/닫힘)는 <see cref="JournalView"/>가 들고 있고, 여기는 그리기만 한다.
    /// 씬에 미리 둘 필요 없다 — 수첩을 처음 펼칠 때 스스로 만들어진다.
    ///
    /// <b>칸은 하나다.</b> 예전에는 왼쪽에 물증, 오른쪽에 정황을 나란히 깔았는데 그러면
    /// 같은 사건이 두 장으로 쪼개져 어느 쪽을 보고 있었는지 잊는다. 지금은 한 판에
    /// <b>주운 것이 앞, 들은 말이 뒤</b>로 눕고, 들은 말은 낯이 묽고 그림 자리에 言 이
    /// 찍혀 갈린다 — 그리고 받아낸 실토에는 들이밀기 단추가 안 붙는다(presentable=false).
    /// 한동안 들은 말을 아예 안 적어 보았는데, 그러니 출도에 필요한 J02 가 甲의 입에서만
    /// 나오면서 수첩에는 영영 안 들어와 필수 넷 가운데 하나를 채울 길이 없었다.
    ///
    /// 카드가 한 장 넓이를 다 쓴다 — 한 줄에 넉 장씩. 넘치면 쪽을 넘긴다.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class JournalPanel : MonoBehaviour
    {
        // 낡은 판 시절의 글꼴 칸을 걷었다 — 글씨는 꾸러미의 TMP 에셋 하나뿐이다
        // (UiText ▸ UiSkin.Font).
        //
        // <b>크기도 저쪽 소지품 판의 것을 쓴다.</b> 좌표계를 통째로 받아 온 판이므로
        // (PageW·PageH) 저쪽이 적어 둔 56 이 여기서도 같은 뜻이다. 칸 이름 30 은
        // 우리 <c>_clueFontSize - 4</c> 와 이미 같은 수라 그대로 둔다.
        private const int _titleFontSize = 56;
        private const int _clueFontSize = 34;
        private Color _paperColor = UiLook.With(UiLook.Paper, 0.96f);
        private Color _inkColor = UiLook.Ink;
        private Color _cardColor = UiLook.With(UiLook.PaperDim, 0.45f);
        private Color _presentColor = UiLook.With(UiLook.Seal, 0.92f);
        [Tooltip("물증을 다시 펼쳐 보는 단추. 들이밀기(붉은색)와 헷갈리지 않게 먹빛으로")]
        private Color _readColor = UiLook.With(UiLook.InkSoft, 0.90f);

        /// <summary>꾸러미의 결. 정적으로 들면 판을 넘겨 살아남되 내용이 죽는다 — 판마다 제 것.</summary>
        private readonly IMUNROK.Ui.InventorySkin _skin = new IMUNROK.Ui.InventorySkin();

        private static JournalPanel _instance;

        private JournalView _owner;
        private CanvasGroup _group;
        private RectTransform _col;
        private RectTransform _page;                       // 판 전체 — 쪽·안내가 여기 붙는다
        private TextMeshProUGUI _title, _brief, _foot;

        // ── 상세 ────────────────────────────────────────────────
        //
        // <b>칸을 누르면 그 자리에서 자세히 본다.</b>
        //
        // 여태 칸을 누르면 <b>판이 통째로 닫히고</b> 물건이 손에 들렸다. 그것은 그것대로
        // 좋은 손짓이지만(끌어 돌려 보고 돋보기를 댄다), 그 앞에 <b>한 뼘이 비어</b> 있었다 —
        // 「이게 뭐였더라」를 알려면 판을 닫고 종이를 펴고 다시 판을 여는 수밖에 없었다.
        // 저쪽 소지품 판은 목록 ▸ 상세 두 켜로 되어 있고, 상세에서 크게 보고 읽은 뒤
        // 거기서 「쓰기」로 넘어간다. 같은 판이라면 같은 켜를 가져야 한다.
        //
        // 그래서 한 켜를 넣는다: 칸을 누르면 <b>판 안에서</b> 크게 뜨고, 거기서
        // 「손에 들고 본다」로 넘어가거나 「들이밀기」로 내민다.
        private RectTransform _detail;          // 상세 켜 — 목록과 번갈아 뜬다
        private RectTransform _detailButtons;   // 그 아래 단추 줄(볼 때마다 다시 짓는다)
        private RawImage _detailShot;
        private RectTransform _detailShotRt;
        private DetailTurner _turner;
        private TextMeshProUGUI _detailName, _detailBody;
        private DetailScroll _detailScroll;
        private readonly List<GameObject> _detailChips = new List<GameObject>();

        /// <summary>지금 상세를 보고 있나. 그동안에는 휠도 쪽을 안 넘긴다.</summary>
        private bool InDetail { get { return _detail != null && _detail.gameObject.activeSelf; } }

        private readonly List<GameObject> _cards = new List<GameObject>();

        /// <summary>
        /// <b>판의 좌표계를 견우팀 꾸러미에서 통째로 받아 왔다.</b>
        ///
        /// 화면에 보이는 크기는 여태도 거의 같았다 — 재 보면 우리 82.8°×55.8°,
        /// 저쪽 84°×55° 였고 본문 글자도 1.75° 대 1.82° 였다. 다른 것은 <b>단위</b>였다:
        /// 저쪽은 1920×1120 을 0.0014m 로 세워 1.50m 앞에 두고, 우리는 1500×900 을
        /// 0.001m 로 세워 0.85m 앞에 두었다. 보이기는 같은데 <b>적히는 수가 달라</b>,
        /// 저쪽 치수를 한 줄도 그대로 옮겨 올 수 없었다.
        ///
        /// 그래서 좌표계째로 받는다. 그러면 칸 320×272 · 사이 28 · 본문 34 같은 수가
        /// <b>뜻을 지닌 채</b> 건너온다 — 자막 바에서 <c>BarStyle</c> 을 그렇게 받아 온 것과
        /// 같은 셈이다. 한 줄에 다섯 장이 들어가는 것도 이 폭에서 저절로 나온다.
        /// </summary>
        private static float PageW { get { return IMUNROK.Ui.InventoryUI.PanelW; } }
        private static float PageH { get { return IMUNROK.Ui.InventoryUI.PanelH; } }

        /// <summary>
        /// <b>판이 화면 세로에서 차지하는 몫.</b>
        ///
        /// 월드에 세울 때는 「1.79m 앞」이 그 몫을 정했다. 화면에 붙인 뒤로는 거리라는
        /// 것이 없으므로 그 몫을 <b>곧바로</b> 적는다 — 저쪽 판이 저쪽 화면에서 덮던
        /// 79%, 우리가 재서 맞춰 둔 76% 가 이 수다.
        /// </summary>
        private const float PageScreenShare = 0.76f;

        /// <summary>
        /// <b>저쪽 판이 저쪽 화면에서 차지하던 몫을 우리 화면에서도 지킨다.</b>
        ///
        /// 저쪽 <c>PanelDistance</c> 는 1.50m 인데, 그 값은 <b>세로 화각 70°</b> 짜리
        /// 게임 뷰를 놓고 잡은 것이다 — 거기서 55° 짜리 판은 화면의 79% 를 덮어
        /// 「거의 채운」 것으로 읽힌다. 우리 카메라는 60° 라, 같은 1.50m 에 세우면
        /// 92% 가 되어 <b>아래가 잘려 나간다</b>(재 보니 뷰포트 −0.23 까지 내려갔다).
        ///
        /// 그래서 거리로 되받는다. 1.568m 짜리 판이 47.4°(=60°의 79%)로 보이는 자리는
        /// 1.79m 다. 재 보면 가로 74% · 세로 76% — 저쪽이 적어 둔 「70%대」다.
        /// 숫자를 옮겨 오는 것과 <b>보이는 몫을 옮겨 오는 것</b>은 다른 일이고,
        /// 여기서 지킬 것은 뒤엣것이다.
        /// </summary>
        /// (더 쓰지 않는다 — 화면에 붙였으므로 거리가 없다. 위 셈의 내력이라 남긴다)

        public static void Open(JournalView owner)
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<JournalPanel>();
                if (_instance == null)
                {
                    var go = new GameObject("수첩", typeof(Canvas),
                                            typeof(UnityEngine.UI.CanvasScaler),
                                            typeof(UnityEngine.UI.GraphicRaycaster));
                    _instance = go.AddComponent<JournalPanel>();
                }
            }
            // 읽는 자리는 하나뿐이다. 손에 든 문서가 있으면 그것이 내려간다.
            ReadingFocus.Claim(ReadingFocus.Panel.Journal, Close);
            _instance.OpenInternal(owner);
        }

        /// <summary>Esc 로도 덮는다 — 단추가 안 먹는 날에도 빠져나올 길은 있어야 한다.</summary>
        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                // 상세를 보는 중이면 <b>한 켜만</b> 물러난다. 자세히 보다 Esc 를 눌렀는데
                // 판이 통째로 덮이면, 목록으로 돌아가려던 손이 세상으로 튕겨 나간다.
                if (InDetail) BackToList();
                else Close();
                return;
            }

            // 휠로도 쪽을 넘긴다. 저쪽 꾸러미가 휠에 쪽 넘기기를 걸어 두었으니
            // 같은 판을 보는 사람이 같은 손짓을 쓰는 것이 맞다.
            var m = UnityEngine.InputSystem.Mouse.current;
            if (m != null && _pageCount > 1 && !InDetail)
            {
                float dy = m.scroll.ReadValue().y;
                if (dy > 0.01f) Turn(-1);
                else if (dy < -0.01f) Turn(1);
            }
#endif
        }

        public static void Close()
        {
            ReadingFocus.Release(ReadingFocus.Panel.Journal);
            if (_instance != null) _instance.SetVisible(false);
        }

        /// <summary>단서가 새로 기록되면 다시 그린다(펼쳐둔 채로 단서를 얻는 경우).</summary>
        public static void Refresh()
        {
            if (_instance != null && _instance._group.alpha > 0.5f) _instance.Rebuild();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            SitOnScreen();

            _group = gameObject.GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();

            BuildFrame();
            SetVisible(false);
        }

        private void OnDestroy() { if (_instance == this) _instance = null; }

        private void OpenInternal(JournalView owner)
        {
            _owner = owner;
            _pageNo = 0;              // 펼 때마다 첫 쪽부터
            BackToList();             // 상세를 보다 덮었어도 다시 펴면 목록부터
            SetVisible(true);
        }

        /// <summary>
        /// <b>수첩을 월드에서 떼어 화면에 붙인다.</b>
        ///
        /// 여태 이 판은 눈앞 허공에 세워 두고 고개를 따라오게 했다. 세상에 세워 두던
        /// 시절의 방식이다 — 거기서는 화면이라는 것이 없어서 <b>세울 데가 세상밖에</b>
        /// 없었다. 화면으로 보는 지금 그 방식이 남기는 것은 두 가지 탈뿐이다:
        ///
        ///   · <b>기둥에 뚫린다.</b> 월드에 놓인 판이라 깊이 검사를 받는다. 조사청에서
        ///     재 보니 대들보가 수첩을 가로질러 지나갔다. 자막판이 재질을 갈아 끼워
        ///     피하던 바로 그 탈이고, 이쪽은 그 손질조차 없었다.
        ///   · <b>떠다닌다.</b> 아무리 굼뜨게 잡아도 고개를 돌리면 판이 뒤따라 흔들린다.
        ///     화면 게임에서 수첩은 <b>펴면 거기 있는</b> 것이지 쫓아오는 것이 아니다.
        ///
        /// 화면에 붙이면 둘 다 한꺼번에 없어진다. 덧붙여 누르기도 제대로 산다 —
        /// 월드 캔버스에는 <c>GraphicRaycaster</c> 조차 안 붙어 있었다.
        ///
        /// 치수는 그대로 쓴다. 견우팀에서 받아 온 1920×1120 좌표계를 <c>CanvasScaler</c>
        /// 의 기준 해상도로 넘기면 칸 320×272 도 글씨 34 도 뜻을 지킨다. 기준 세로를
        /// <c>PageH ÷ PageScreenShare</c> 로 잡는 것이 요점이다 — 그래야 판이 화면
        /// 세로의 그 몫만큼만 덮고, 화면이 넓든 좁든 그 몫이 변하지 않는다.
        /// </summary>
        private void SitOnScreen()
        {
            // 수첩만 제 좌표계를 쓴다. 판이 화면 세로의 몇 할을 덮을지가 이 판의
            // 셈이라, 기준 세로를 그 몫에서 낸다(다른 판은 공통 1732 를 쓴다).
            ScreenPanel.Raise(gameObject, ScreenPanel.LayerBook, PageH / PageScreenShare);
        }

        /// <summary>테두리 넉 줄. 상자 하나에 외곽선을 그릴 길이 없어 얇은 띠 넷을 두른다.</summary>
        private void Edge(RectTransform page, float w, float h, Color c)
        {
            const float t = 5f;
            Strip(page, "테_위", new Vector2(0f, h * 0.5f - t * 0.5f), new Vector2(w, t), c);
            Strip(page, "테_아래", new Vector2(0f, -h * 0.5f + t * 0.5f), new Vector2(w, t), c);
            Strip(page, "테_좌", new Vector2(-w * 0.5f + t * 0.5f, 0f), new Vector2(t, h), c);
            Strip(page, "테_우", new Vector2(w * 0.5f - t * 0.5f, 0f), new Vector2(t, h), c);
        }

        private void Strip(RectTransform parent, string name, Vector2 at, Vector2 size, Color c)
        {
            var rt = NewRect(name, at, size, parent);
            Skin(rt.gameObject.AddComponent<Image>(), _skin.Wood_, c);
        }

        /// <summary>아래 안내 글씨 — 저쪽 판의 26 이다.</summary>
        private const int HintSizeOf = 26;

        /// <summary>빈 판에 뜨는 한 줄 — 저쪽 판의 36.</summary>
        private const int EmptySizeOf = 36;

        /// <summary>
        /// 칸에 얹는 그림 자리.
        ///
        /// 저쪽 칸은 196×196 을 +40 에 얹는데, 그 그림은 <b>3D 물건을 찍은 것</b>이라
        /// 둘레에 빈 여백이 넉넉하다. 우리 그림은 <b>종이를 통째로 찍은 것</b>이라
        /// 네 변이 꽉 차 있어, 같은 수를 쓰면 칸 위로 비어져 나온다
        /// (196 을 +40 에 두면 윗변이 138 인데 칸 윗변이 136 이고, 속지는 128 이다).
        /// 속지 안(±128)에 들어가고 아래 이름줄과도 안 겹치게 줄여 잡는다.
        /// </summary>
        private const float Shot = 150f;

        /// <summary>그림이 앉는 높이. 속지 안(±128)에 들어가고 이름줄과도 안 겹친다.</summary>
        private const float ShotY = 50f;

        /// <summary>
        /// 이름줄이 앉는 자리와 높이.
        ///
        /// <b>석 줄이 필요해졌다.</b> 증언 카드는 「[마을 사람의 증언] …」처럼 말한 이가
        /// 앞에 서는데, 그 이름만으로 한 줄이 찬다. 두 줄짜리 칸에 두면 <b>이름만 뜨고
        /// 정작 무슨 말인지는 말줄임으로 잘렸다</b>. 그림을 한 뼘 줄여 그 자리를 준다.
        /// </summary>
        private const float NameY = -78f;
        private const float NameH = 100f;

        /// <summary>겹 하나. 무늬를 깐 네모를 어버이 위에 얹는다.</summary>
        private RectTransform Plate(string name, Sprite sp, Color c, Transform parent)
        {
            var rt = NewRect(name, Vector2.zero, Vector2.zero, parent);
            Skin(rt.gameObject.AddComponent<Image>(), sp, c);
            return rt;
        }

        /// <summary>어버이를 꽉 채운다. inset 이 음수면 그만큼 안으로 물린다(테가 드러난다).</summary>
        private static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-inset, -inset);
            rt.offsetMax = new Vector2(inset, inset);
        }

        /// <summary>무늬를 깔고 그 위에 색을 얹는다 — 꾸러미가 판을 그리는 방식이다.</summary>
        private static void Skin(Image im, Sprite sp, Color c)
        {
            im.sprite = sp;
            im.type = Image.Type.Simple;
            im.color = c;
        }

        private void SetVisible(bool on)
        {
            if (_group == null) return;
            _group.alpha = on ? 1f : 0f;
            _group.blocksRaycasts = on;
            _group.interactable = on;
        }

        // ── 뼈대(한 번만) ──

        private void BuildFrame()
        {
            // <b>여기서 색을 덮어쓰던 자리를 걷었다.</b> 인스펙터에 손으로 적어 둔 색이
            // 씬에 구워져 있어서, 그것을 실행 중에 꾸러미 색으로 도로 갈아 끼우고
            // 있었다. 이제 칸에 적힌 것이 곧 꾸러미 색이고 인스펙터에 새지도 않으므로
            // 갈아 끼울 것이 없다 — 적힌 값과 보이는 값이 같아졌다.

            // <b>틀을 저쪽 소지품 판과 같은 네 겹으로 짓는다.</b>
            //
            // 여태 한지 한 장에 테두리 띠 넷을 둘렀다(<c>Edge</c>). 저쪽은
            //   목재 틀 ▸ 한지 ▸ 안쪽 테선 ▸ 속지
            // 네 겹을 겹쳐, 바깥에 두꺼운 나무테가 서고 그 안에 가는 먹선이 한 번 더 돈다.
            // 겹을 맞추지 않으면 <b>같은 색인데 다른 판</b>으로 보인다 — 색만 빌려 오고
            // 짜임을 안 빌려 온 것이 이 판의 이질감이었다.
            var page = NewRect("판", Vector2.zero, new Vector2(PageW, PageH), transform);
            _page = page;
            Stretch(Plate("틀", _skin.Wood_, Color.white, page), 40f);
            var paper = Plate("바탕", _skin.Hanji_, _paperColor, page);
            Stretch(paper, 0f);
            Stretch(Plate("테선", _skin.Wood_, UiLook.Wood, page), -20f);
            Stretch(Plate("속지", _skin.Hanji_, _paperColor, page), -26f);

            // 제목 — 저쪽과 같은 자리·크기. 그 아래 판을 가로지르는 구분선 한 줄.
            // <b>표제가 두 줄이다.</b> 저쪽 판은 제목 한 줄이면 끝인데(물건 이름은 칸에
            // 적히므로) 우리 수첩에는 <b>사건 개요</b>가 한 줄 더 붙는다. 그래서 제목을
            // 조금 올리고 그 밑에 개요를 눕힌 뒤, 구분선을 그만큼 내린다.
            // 안 그러면 개요가 첫 줄 칸 위에 겹쳐 앉는다(칸 윗변이 382 다).
            _title = NewText("제목", "수첩", new Vector2(0f, 494f), new Vector2(1400f, 64f),
                             page, _titleFontSize, _inkColor);
            var rule = NewRect("구분선", new Vector2(0f, 416f), new Vector2(1760f, 4f), page);
            Skin(rule.gameObject.AddComponent<Image>(), _skin.Wood_, UiLook.Wood);

            // <b>닫기는 왼쪽 위 네모 하나다.</b> 저쪽이 그 자리에 92×92 짜리 「×」를 두었다.
            // 우리는 오른쪽 위에 「✕ 덮기」라 적힌 긴 단추를 두었는데, 두 판을 나란히 놓으면
            // 닫는 자리가 서로 반대쪽이라 손이 매번 헤맨다.
            var close = NewRect("닫기", new Vector2(-836f, 470f), new Vector2(92f, 92f), page);
            var closeBg = close.gameObject.AddComponent<Image>();
            Skin(closeBg, _skin.Wood_, UiLook.Wood);
            var closeBtn = close.gameObject.AddComponent<Button>();
            closeBtn.targetGraphic = closeBg;
            closeBtn.onClick.AddListener(() => _owner?.Close());
            NewText("라벨", "×", Vector2.zero, new Vector2(92f, 92f), close, 44, UiLook.SealText);

            // 사건 개요는 구분선 바로 밑에 한 줄로 눕힌다 — 조사종이를 안 읽고 지나갔어도
            // 첫 장에는 남아 있어야 한다.
            _brief = NewText("개요", "", new Vector2(0f, 446f), new Vector2(1760f, 34f),
                             page, HintSizeOf, new Color(_inkColor.r, _inkColor.g, _inkColor.b, 0.72f));

            // <b>칸이 깔리는 자리.</b> 저쪽 셈으로 다섯 칸 × 세 줄이 꼭 들어가는 크기다:
            // 가로 5×320 + 4×28 = 1712, 세로 3×272 + 2×28 = 872, 가운데는 −54.
            _col = NewRect("단서", new Vector2(0f, -54f), new Vector2(1712f, 872f), page);

            // 아래 안내 한 줄 — 저쪽 <c>hintText</c> 자리(−516).
            _foot = NewText("안내", "", new Vector2(0f, -516f), new Vector2(1840f, 40f),
                            page, HintSizeOf, UiLook.With(UiLook.InkSoft, 0.9f));

            BuildDetail(page);
        }


        /// <summary>
        /// 상세 켜를 짓는다(한 번만). 자리는 저쪽 소지품 판의 상세와 같다 —
        /// 왼쪽에 큰 그림 한 장, 오른쪽에 이름과 읽을 것.
        /// </summary>
        private void BuildDetail(RectTransform page)
        {
            _detail = NewRect("상세", Vector2.zero, new Vector2(PageW, PageH), page);

            // 왼쪽 — 그림틀. 칸의 그림과 달리 여기서는 종이가 <b>읽히는 크기</b>로 뜬다.
            var frame = NewRect("그림틀", new Vector2(-470f, 14f), new Vector2(700f, 700f), _detail);
            Skin(frame.gameObject.AddComponent<Image>(), _skin.Wood_, UiLook.Wood);
            var inner = Plate("그림바탕", _skin.Slot_, _cardColor, frame);
            Stretch(inner, -10f);
            // <b>돌아간 종이는 틀 안에서 잘린다.</b> 안 자르면 비스듬히 돈 모서리가
            // 나뭇결 틀 밖으로 삐져나와, 틀이 <b>테두리가 아니라 그림 한 장</b>이 된다.
            inner.gameObject.AddComponent<RectMask2D>();
            // 종이는 틀보다 한 뼘 작게 — 조금 돌려도 네 귀가 틀 안에 남는다.
            _detailShotRt = NewRect("그림", Vector2.zero, new Vector2(600f, 600f), inner);
            _detailShot = _detailShotRt.gameObject.AddComponent<RawImage>();

            // <b>끌어서 돌려 본다.</b> 저쪽 상세는 왼쪽 그림자리를 끌어 물건을 굴리는
            // 자리인데, 우리 판에는 그림 한 장이 못 박혀 있었다 — 크게 보이기는 하되
            // 만질 수가 없어서, 자세히 보려면 결국 판을 닫고 종이를 따로 펴야 했다.
            // 우리 물건은 대개 종이이므로 굴리는 대신 <b>돌려 넘긴다</b>(DetailTurner).
            //
            // 끄는 손을 받으려면 광선에 걸려야 한다 — 그림 자체가 손잡이다.
            _detailShot.raycastTarget = true;
            _turner = _detailShotRt.gameObject.AddComponent<DetailTurner>();
            _turner.Page = _detailShotRt;
            _turner.Image = _detailShot;

            // 오른쪽 — 이름, 그 밑에 줄 하나, 그 밑에 읽을 것.
            const float TextX = 400f, TextW = 940f;
            _detailName = NewText("이름", "", new Vector2(TextX, 296f), new Vector2(TextW, 72f),
                                  _detail, 46, _inkColor);
            _detailName.alignment = TextAlignmentOptions.Left;
            _detailName.fontStyle = FontStyles.Bold;
            UiText.Wrap(_detailName, true);

            var nameRule = NewRect("이름줄", new Vector2(TextX, 252f), new Vector2(TextW, 4f), _detail);
            Skin(nameRule.gameObject.AddComponent<Image>(), _skin.Wood_, UiLook.Wood);

            // 설명이 길어졌다(견우 판처럼 무엇·생김새·유리로 읽은 것·이상한 점). 칸을 단추줄
            // 바로 위까지 내리고, 그래도 넘치면 휠로 굴린다 — 글씨를 줄여 우겨넣지 않는다.
            var box = NewRect("글틀", new Vector2(TextX, -50f), new Vector2(TextW, 560f), _detail);
            box.gameObject.AddComponent<RectMask2D>();
            var wheel = box.gameObject.AddComponent<Image>();   // 휠을 받을 자리 — 보이지 않는다
            wheel.color = new Color(0f, 0f, 0f, 0f);
            _detailBody = NewText("글", "", Vector2.zero, new Vector2(TextW - 20f, 560f), box,
                                  _clueFontSize - 2, new Color(_inkColor.r, _inkColor.g, _inkColor.b, 0.92f));
            _detailBody.alignment = TextAlignmentOptions.TopLeft;
            _detailBody.raycastTarget = false;
            UiText.Wrap(_detailBody, true);
            _detailScroll = box.gameObject.AddComponent<DetailScroll>();
            _detailScroll.Text = _detailBody;
            var more = NewText("더읽기", "▼ 휠로 더 읽기", new Vector2(TextX + TextW * 0.5f - 130f, -345f),
                               new Vector2(260f, 30f), _detail, _clueFontSize - 12,
                               new Color(_inkColor.r, _inkColor.g, _inkColor.b, 0.55f));
            more.alignment = TextAlignmentOptions.Right;
            more.raycastTarget = false;
            more.gameObject.SetActive(false);
            _detailScroll.More = more;

            _detailButtons = NewRect("단추줄", new Vector2(0f, -420f), new Vector2(1712f, 80f), _detail);

            _detail.gameObject.SetActive(false);
        }

        /// <summary>
        /// 상세를 띄운다. <paramref name="onHold"/> 가 「손에 들고 본다」에 걸린다 —
        /// 비어 있으면 그 단추가 안 뜬다(들 것이 없는 것이다).
        /// </summary>
        private void ShowDetail(Texture2D shot, string name, string body,
                                System.Action onHold, ClueEntry present, Texture2D back = null)
        {
            if (_detail == null) return;

            _detailShot.texture = shot;
            _detailShot.enabled = shot != null;
            if (_turner != null)
            {
                _turner.Front = shot;
                _turner.Back = back;
                _turner.ResetView();   // 물건이 바뀌면 자세도 처음으로
            }
            if (shot != null)
            {
                // 틀 안에 <b>온전히</b> 들어가게 맞춘다 — 종이는 세로가 길어, 폭에 맞추면
                // 위아래가 잘린다.
                float ar = shot.height > 0 ? (float)shot.width / shot.height : 1f;
                float hh = 600f, ww = hh * ar;
                if (ww > 600f) { ww = 600f; hh = ww / Mathf.Max(0.01f, ar); }
                _detailShotRt.sizeDelta = new Vector2(ww, hh);
            }

            _detailName.text = string.IsNullOrEmpty(name) ? "(이름 없는 것)" : name;
            _detailBody.text = Emphasis.Rich(body ?? "", Emphasis.OnPaper);

            foreach (var c in _detailChips) if (c != null) Destroy(c);
            _detailChips.Clear();

            bool talking = InterrogationController.AnyOpen && InterrogationController.Active != null;

            // <b>못 내미는 까닭을 적는다.</b> 단추가 그냥 없으면 「왜 없지」로 남는다.
            bool canShow = CanPresent(present, talking);
            if (talking && present != null && !canShow)
            {
                string why = !present.presentable
                    ? "*이것은 내밀 것이 못 된다* — 처음부터 지니고 있던 것이거나, 받아낸 말이다. "
                      + "들이밀 수 있는 것은 <b>뒤져서 찾아낸 종이</b>다."
                    : "*이 말은 이 사람에게서 들은 것이다* — 제 말을 제 앞에 도로 꺼내 보여야 "
                      + "「그건 소인이 한 말입지요」로 끝난다. <b>남에게 들은 말</b>이라야 짚을 것이 된다.";
                _detailBody.text = Emphasis.Rich(
                    (string.IsNullOrEmpty(body) ? "" : body + "\n\n") + why, Emphasis.OnPaper);
            }

            if (_detailScroll != null) _detailScroll.ResetTop();

            var row = new List<KeyValuePair<string, System.Action>>();
            if (onHold != null) row.Add(new KeyValuePair<string, System.Action>("손에 들고 본다", onHold));
            if (canShow)
            {
                var captured = present;
                row.Add(new KeyValuePair<string, System.Action>("들이밀기", () =>
                {
                    InterrogationController.Active?.PresentFromJournal(captured);
                    _owner?.Close();
                }));
            }
            row.Add(new KeyValuePair<string, System.Action>("◀ 목록으로", BackToList));

            const float bw = 320f, bgap = 24f;
            float x0 = -(row.Count * bw + (row.Count - 1) * bgap) * 0.5f + bw * 0.5f;
            for (int i = 0; i < row.Count; i++)
            {
                bool last = i == row.Count - 1;
                var rt = NewRect(row[i].Key, new Vector2(x0 + i * (bw + bgap), 0f),
                                 new Vector2(bw, 72f), _detailButtons);
                var bg = rt.gameObject.AddComponent<Image>();
                // 물러나는 단추는 <b>묽게</b> — 시키는 것과 되돌아가는 것은 무게가 다르다.
                Skin(bg, _skin.Wood_, last ? UiLook.With(UiLook.InkSoft, 0.75f) : _presentColor);
                var b = rt.gameObject.AddComponent<Button>();
                b.targetGraphic = bg;
                var act = row[i].Value;
                b.onClick.AddListener(() => act());
                NewText("라벨", row[i].Key, Vector2.zero, new Vector2(bw - 24f, 72f), rt,
                        _clueFontSize - 4, UiLook.SealText);
                _detailChips.Add(rt.gameObject);
            }

            _col.gameObject.SetActive(false);
            _detail.gameObject.SetActive(true);
            _foot.text = shot != null
                       ? "끌어서 돌려 본다   ·   눌러서 뒤집는다   ·   ◀ 목록으로   ·   Esc — 덮기"
                       : "◀ 목록으로 — 돌아간다   ·   Esc — 덮기";
        }

        /// <summary>상세에서 목록으로 되돌아간다.</summary>
        private void BackToList()
        {
            if (_detail != null) _detail.gameObject.SetActive(false);
            if (_col != null) _col.gameObject.SetActive(true);
            Rebuild();
        }

        // ── 내용(펼칠 때마다) ──

        private void Rebuild()
        {
            foreach (var c in _cards) if (c != null) Destroy(c);
            _cards.Clear();

            var gs = GameState.Instance;
            if (!gs.InCase) { FillCaseSheets(); return; }

            CaseId caseId = gs.CurrentCase.Value;
            var clues = Journal.Instance.GetClues(caseId);

            // 물증이 앞, 들은 말이 뒤. 섞어 깔면 어느 것이 손에 쥔 것인지 흐려진다.
            var laid = new List<ClueEntry>();
            foreach (var c in clues) if (c.kind == ClueKind.물증) laid.Add(c);
            int nMul = laid.Count;
            foreach (var c in clues) if (c.kind != ClueKind.물증) laid.Add(c);
            int nJeong = laid.Count - nMul;

            // <b>판 이름은 「소지품」 하나다.</b> 여태 제목 줄에 사건 이름·물증 수를
            // 함께 밀어 넣어 「수첩 — 물증 9 · 들은 말 2」가 되어 있었다. 저쪽 판의
            // 제목은 <b>이 판이 무엇인가</b> 한마디뿐이고, 그것이 맞다 — 무엇이 들었는지는
            // 칸이 이미 보여 준다. 세는 수는 개요 줄로 내린다.
            _title.text = "소지품";

            string tally = nJeong > 0 ? $"물증 {nMul} · 들은 말 {nJeong}" : $"물증 {nMul}";
            string brief = Journal.Instance.GetBrief(caseId);
            if (string.IsNullOrWhiteSpace(brief)) brief = _owner != null ? _owner.CaseBrief : null;
            _brief.text = string.IsNullOrWhiteSpace(brief) ? tally : brief + "      (" + tally + ")";

            // 심문 중일 때만 들이밀 수 있다
            bool talking = InterrogationController.AnyOpen && InterrogationController.Active != null;

            // <b>조작은 판 아래 한 줄로.</b> 저쪽 소지품 판도 아래에 그 줄을 둔다.
            // 여태 우리 판에는 없어서, 카드를 눌러 보기 전에는 눌러도 되는 것인 줄 몰랐다.
            _foot.text = talking
                       ? "묽은 칸은 내밀 수 없다   ·   칸을 누르면 자세히 본다   ·   Esc — 덮기"
                       : "칸을 누르면 자세히 본다   ·   휠 — 쪽 넘기기   ·   Esc — 덮기";

            FillEvidence(_col, laid, caseId, talking);
        }

        /// <summary>
        /// 물증은 <b>카드</b>로 깐다 — 생김새 한 장과 이름.
        ///
        /// 물증은 글이 아니라 물건이다. 줄글로 늘어놓으면 "[J06] 스무 해치 기록 뒤…" 같은
        /// 것이 열 줄 쌓이고, 그 중 어느 것이 그 낡은 장부였는지 알 수 없게 된다.
        /// 생김새가 먼저 보여야 손이 기억한 것과 이어진다.
        ///
        /// 카드를 누르면 그 물건을 <b>손에 든다</b> — 끌어 돌려 앞뒤를 보고, 아래에서
        /// 요약을 읽고, 돋보기를 대면 잔글씨까지 읽힌다.
        /// </summary>
        private void FillEvidence(RectTransform col, List<ClueEntry> list, CaseId caseId, bool talking)
        {
            _shown = list;            // 손에 들었을 때 이웃으로 넘어갈 수 있게 들고 있는다
            _shownCase = caseId;
            float w = col.sizeDelta.x;

            // <b>판 안의 머리글을 걷었다.</b> 「── 주운 것과 들은 말 ──」 한 줄이 칸 자리를
            // 104화소 먹어, 세 줄이 들어갈 자리에 두 줄밖에 안 들어갔다. 무엇이 깔려 있는지는
            // 판 제목이 이미 말하고 있다 — 같은 말을 두 군데 적으면 둘 다 안 읽힌다.
            // (저쪽 소지품 판도 제목 아래는 곧바로 칸이다)
            float y = col.sizeDelta.y * 0.5f;

            if (list.Count == 0)
            {
                var e = NewText("없음", "아직 주운 것도 들은 말도 없다.",
                                Vector2.zero, new Vector2(1200f, 80f),
                                col, EmptySizeOf, UiLook.With(UiLook.InkSoft, 0.85f));
                _cards.Add(e.gameObject);
                return;
            }

            // <b>칸 치수를 저쪽에 물어 온다.</b> 여태 320·272·28 을 손으로 적어 두었는데,
            // 적어 둔 수는 저쪽이 고쳐도 안 따라온다. 꾸러미의 그 칸들이 닫혀 있어
            // 베낄 수밖에 없던 것이라, 열고 나서 갈아 끼웠다.
            float cw = IMUNROK.Ui.InventoryUI.CellW;
            float ch = IMUNROK.Ui.InventoryUI.CellH;
            float gap = IMUNROK.Ui.InventoryUI.Gap;
            int perRow = Mathf.Max(1, Mathf.FloorToInt((w + gap) / (cw + gap)));
            float x0 = -(perRow * cw + (perRow - 1) * gap) * 0.5f + cw * 0.5f;

            // <b>넘치면 쪽을 넘긴다.</b> 여태 한 판에 다 깔았다 — 사건당 물증이 열 남짓이라
            // 한 화면에 담긴다고 보았기 때문인데, 제2막의 관아 문서까지 더하면 그 수를
            // 넘는다. 넘친 것은 <b>없는 것과 같다</b>: 판 밖으로 나간 카드는 보이지도
            // 눌리지도 않으면서 있는 줄 알게 만든다.
            //
            // 몇 장이 들어가는지는 <b>남은 자리가 정한다</b>. 칸 크기를 저쪽에서 받아 왔으니
            // 쪽당 장수도 저쪽 것을 베낄 것이 아니라 이 판에서 나와야 맞는다.
            // 쪽 넘기는 줄은 이제 판 아래(−452)에 따로 서므로, 칸 자리를 덜어 줄 것이 없다.
            float roomY = col.sizeDelta.y;
            int rows = Mathf.Max(1, Mathf.FloorToInt((roomY + gap) / (ch + gap)));
            _perPage = Mathf.Max(1, perRow * rows);
            int pages = Mathf.Max(1, Mathf.CeilToInt(list.Count / (float)_perPage));
            _pageNo = Mathf.Clamp(_pageNo, 0, pages - 1);
            _pageCount = pages;

            int from = _pageNo * _perPage;
            int to = Mathf.Min(list.Count, from + _perPage);

            for (int i = from; i < to; i++)
            {
                var c = list[i];
                int r = (i - from) / perRow, k = (i - from) % perRow;
                var card = NewRect("증거카드",
                                   new Vector2(x0 + k * (cw + gap), y - ch * 0.5f - r * (ch + gap)),
                                   new Vector2(cw, ch), col);
                // <b>칸도 저쪽처럼 두 겹이다</b> — 바깥이 나뭇결 테, 안이 한지 속살.
                // 한 겹으로 깔면 칸이 판 위에 <b>얼룩</b>처럼 앉고, 눌리는 것으로 안 읽힌다.
                var bg = card.gameObject.AddComponent<Image>();
                bool heard = c.kind != ClueKind.물증;   // 들은 말은 종이가 아니다

                // <b>내밀 수 없는 것은 미리 갈라 둔다.</b>
                //
                // 여태 심문 중에도 칸이 다 똑같이 보였다. 눌러서 상세까지 들어가야
                // 「들이밀기」가 없는 것을 알았고, 없는 까닭은 아무 데도 안 적혀 있었다 —
                // 눌러 봐야 아는 것은 <b>막은 것</b>이 아니라 <b>고장난 것</b>으로 읽힌다.
                // 내밀 자리(심문)에서는 못 내미는 칸을 <b>한 켜 묽게</b> 깔아,
                // 손이 그리로 가기 전에 눈이 먼저 가린다.
                bool blocked = talking && !CanPresent(c, true);
                Skin(bg, _skin.Wood_, blocked ? UiLook.With(UiLook.Wood, 0.45f) : UiLook.Wood);
                var faceColor = heard
                              ? new Color(_cardColor.r, _cardColor.g, _cardColor.b, _cardColor.a * 0.7f)
                              : _cardColor;
                if (blocked) faceColor.a *= 0.45f;
                Stretch(Plate("속", _skin.Slot_, faceColor, card), -8f);
                _cards.Add(card.gameObject);

                var doc = Journal.Instance.GetDocument(caseId, c.key);
                // 모형만 걸린 물증(자물쇠·팔)은 종이가 없다 — 그때는 단서 그림으로 물러선다. 여태 page 가 비어 「?」가 떴다.
                Texture2D shot = doc != null && doc.page != null ? doc.page : Journal.Instance.GetClueImage(caseId, c.key);

                // 생김새 — 속지 안에 들어가는 크기로.
                var shotRt = NewRect("모양", new Vector2(0f, ShotY), new Vector2(Shot, Shot), card);
                if (shot != null)
                {
                    var raw = shotRt.gameObject.AddComponent<RawImage>();
                    raw.texture = shot;
                    raw.raycastTarget = false;
                    float ar = (float)shot.width / Mathf.Max(1, shot.height);
                    float hh = Shot, ww = hh * ar;
                    if (ww > Shot) { ww = Shot; hh = ww / Mathf.Max(0.01f, ar); }
                    shotRt.sizeDelta = new Vector2(ww, hh);
                }
                else
                {
                    // 들은 말에는 言, 아직 못 본 물건에는 ?
                    var mark = NewText("표", heard ? "言" : "?", Vector2.zero, new Vector2(Shot, Shot), shotRt,
                                       _clueFontSize + 26, new Color(_inkColor.r, _inkColor.g, _inkColor.b, 0.35f));
                    mark.raycastTarget = false;
                }

                // 이름 — 단서 문구의 첫 토막만. 나머지는 손에 들면 아래에 나온다.
                // 자리·크기·맞춤 전부 저쪽 칸의 이름줄 그대로다(−96, 292×80, 30, 위쪽맞춤).
                var name = NewText("이름", ShortName(doc, c), new Vector2(0f, NameY),
                                   new Vector2(cw - 28f, NameH), card, _clueFontSize - 8,
                                   blocked ? UiLook.With(_inkColor, 0.45f) : _inkColor);
                name.alignment = TextAlignmentOptions.Top;
                UiText.Wrap(name, true);
                // <b>칸 밖으로 흐르지 않게 맺는다.</b> Overflow 로 두면 넘친 줄이 칸 아래로
                // 새어 나와, 다음 줄 칸 위에 글자가 겹쳐 앉는다(들이밀기 단추 위에도 앉았다).
                // Ellipsis 는 <c>Truncate</c> 와 달리 줄을 통째로 버리지 않고 말줄임으로 맺는다.
                name.overflowMode = TextOverflowModes.Ellipsis;

                // 카드 전체가 단추다 — 누르면 손에 든다
                var btn = card.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                int at = i;
                var captured2 = c;
                var doc2 = doc;
                bool heard2 = heard;
                btn.onClick.AddListener(() =>
                {
                    // <b>판 안에서 먼저 크게 본다.</b> 손에 드는 것은 그다음이다.
                    // <b>손에 드는 것은 물증뿐이다.</b> 들은 말은 종이가 아니라 기억이라
                    // 집어 들 것이 없는데도 단추가 떠서, 눌러 보면 아무 일도 안 났다.
                    System.Action hold = heard2 ? null : (System.Action)(() => { OpenAt(at); _owner?.Close(); });
                    ShowDetail(shot,
                               ShortName(doc2, captured2),
                               DetailBody(doc2, captured2),
                               hold,   // 수첩을 덮어야 두 손이 빈다
                               captured2,
                               doc2 != null ? doc2.back : null);
                });

                // 심문 중이면 들이밀 수 있다
                // 들이밀 수 있는가는 <b>presentable</b> 하나가 정한다. 여태 key 가
                // "_revealed" 로 끝나는지도 보았는데, 그런 key 를 만드는 곳이 없어진 지 오래다.
                bool canPresent = CanPresent(c, talking);
                if (!canPresent) continue;
                var b = NewRect("들이밀기", new Vector2(0f, -ch * 0.5f + 22f), new Vector2(cw - 44f, 36f), card);
                var pbg = b.gameObject.AddComponent<Image>();
                pbg.color = _presentColor;
                var pbtn = b.gameObject.AddComponent<Button>();
                pbtn.targetGraphic = pbg;
                var captured = c;
                pbtn.onClick.AddListener(() =>
                {
                    InterrogationController.Active?.PresentFromJournal(captured);
                    _owner?.Close();
                });
                NewText("라벨", "들이밀기", Vector2.zero, new Vector2(cw - 44f, 36f), b, _clueFontSize - 10,
                        UiLook.SealText);
            }

            PageRow(pages);
        }

        /// <summary>카드에 적을 짧은 이름. 문서가 있으면 그 제목, 없으면 단서 문구의 앞 토막.</summary>
        /// <summary>
        /// <b>조사청에서 펴면 사건 종이만 보인다.</b>
        ///
        /// 여태 「사건 밖에서는 물증이 보이지 않는다」 한 줄만 띄우고 판을 비웠다.
        /// 물증이 안 보이는 것은 맞다 — 그것은 <b>사건 안에서 손에 잡은 것</b>이고,
        /// 방을 나오면 그 방에 두고 온다. 그러나 <b>무엇을 맡았는지</b>는 방을 나와서도
        /// 알아야 한다. 어전에서 받아 온 봉서는 물증이 아니라 <b>출발점</b>이기 때문이다.
        ///
        /// 그래서 조사청 수첩에는 받아 온 사건 종이만 사건 차례로 눕는다.
        /// 끝난 사건의 물증을 여기서 다시 뒤지게 하지 않는다 — 조사청은 정리하는
        /// 방이 아니라 <b>다음 사건을 고르는</b> 방이다.
        /// </summary>
        private void FillCaseSheets()
        {
            var sheets = Journal.Instance.CaseSheets();
            _title.text = "소지품";
            _brief.text = sheets.Count == 0 ? "아직 맡은 사건이 없다"
                                            : "받아 온 봉서 " + sheets.Count;
            _pageCount = 1;
            _pageNo = 0;
            _shown = null;                  // 여기서는 이웃으로 넘길 물증 목록이 없다
            _foot.text = sheets.Count == 0 ? "" : "봉서를 누르면 펴 본다   ·   Esc — 덮기";

            float y = _col.sizeDelta.y * 0.5f;

            if (sheets.Count == 0)
            {
                var e = NewText("없음", "어전에서 봉서를 받으면 여기 남는다.",
                                Vector2.zero, new Vector2(1200f, 80f),
                                _col, EmptySizeOf, UiLook.With(UiLook.InkSoft, 0.85f));
                _cards.Add(e.gameObject);
                return;
            }

            float cw = IMUNROK.Ui.InventoryUI.CellW, ch = IMUNROK.Ui.InventoryUI.CellH,
                  gap = IMUNROK.Ui.InventoryUI.Gap;
            float x0 = -(sheets.Count * cw + (sheets.Count - 1) * gap) * 0.5f + cw * 0.5f;

            for (int i = 0; i < sheets.Count; i++)
            {
                var d = sheets[i];
                var card = NewRect("사건카드", new Vector2(x0 + i * (cw + gap), y - ch * 0.5f),
                                   new Vector2(cw, ch), _col);
                var bg = card.gameObject.AddComponent<Image>();
                Skin(bg, _skin.Wood_, UiLook.Wood);
                Stretch(Plate("속", _skin.Slot_, _cardColor, card), -8f);
                _cards.Add(card.gameObject);

                var shotRt = NewRect("모양", new Vector2(0f, ShotY), new Vector2(Shot, Shot), card);
                if (d.page != null)
                {
                    var img = shotRt.gameObject.AddComponent<RawImage>();
                    img.texture = d.page;
                    img.raycastTarget = false;
                    float ar = d.page.height > 0 ? (float)d.page.width / d.page.height : 1f;
                    float hh = Shot, ww = hh * ar;
                    if (ww > Shot) { ww = Shot; hh = ww / Mathf.Max(0.01f, ar); }
                    shotRt.sizeDelta = new Vector2(ww, hh);
                }

                var name = NewText("이름", string.IsNullOrEmpty(d.title) ? d.caseId.ToString() : d.title,
                                   new Vector2(0f, NameY), new Vector2(cw - 28f, NameH),
                                   card, _clueFontSize - 8, _inkColor);
                name.alignment = TextAlignmentOptions.Top;
                UiText.Wrap(name, true);
                name.overflowMode = TextOverflowModes.Ellipsis;

                var btn = card.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                var dd = d;
                btn.onClick.AddListener(() =>
                {
                    ShowDetail(dd.page, dd.title, dd.body, () =>
                    {
                        DocumentView.Show(dd.page, dd.title, dd.body, dd.fine, null, true,
                                          null, null, null, null, dd.back);
                        DocumentView.SetStageModel(dd.model, dd.modelEuler, dd.modelFixed);
                        _owner?.Close();
                    }, null, dd.back);
                });
            }
        }

        /// <summary>지금 판에 깔린 물증들. 손에 든 종이에서 이웃으로 넘어갈 때 쓴다.</summary>
        private List<ClueEntry> _shown;
        private CaseId _shownCase;

        /// <summary>
        /// <b>이 자리의 물증을 손에 든다</b> — 그리고 앞뒤 이웃을 일러 준다.
        ///
        /// 여태 카드를 누르면 종이 한 장을 펴 주고 끝이었다. 그러면 다음 것을 보려고
        /// 매번 수첩을 폈다 덮었다 해야 하는데, 물증은 <b>견주어 보는</b> 물건이다 —
        /// 필적이 같은지 다른지는 두 장을 잇달아 봐야 안다.
        /// </summary>
        private void OpenAt(int i)
        {
            if (_shown == null || i < 0 || i >= _shown.Count) return;
            var c = _shown[i];
            var doc = Journal.Instance.GetDocument(_shownCase, c.key);
            Texture2D shot = doc != null && doc.page != null ? doc.page : Journal.Instance.GetClueImage(_shownCase, c.key);

            // 수첩에서 꺼내 든 것은 어둠 위에 놓는다(dim) — 둘레가 비어야 그 하나만 보인다
            // 방에서 집었던 그 종이가 아직 씬에 있으면 <b>그 종이로</b> 편다 — 한글 벌·등불·
            // 「다 읽으면 수첩에 덧붙이기」까지 방에서와 똑같이(InspectableNote.OpenFromJournal).
            if (doc != null && doc.model == null && InspectableNote.OpenFromJournal(_shownCase, c.key))
            {
                DocumentView.SetStageModel(null, Vector3.zero);
            }
            else if (doc != null)
            {
                // 방의 종이가 없어도 한글 벌·등불 겹은 수첩이 들고 있다 — 돋보기·등불이 그대로 먹는다
                DocumentView.Show(doc.page, doc.title, doc.body, doc.fine, null, true,
                                  doc.lit, doc.litText, null, doc.litGlyphs, doc.back, doc.ko);
                DocumentView.SetStageModel(doc.model, doc.modelEuler, doc.modelFixed);   // 종이가 아니면 무대로
            }
            else
                DocumentView.Show(shot, ShortName(null, c), c.text, null, null, true);

            // <b>이웃은 물증끼리만 잇는다.</b>
            //
            // 저쪽 꾸러미(견우)의 좌우 짝은 <b>소지품</b> 사이를 넘긴다 — 거기 목록에 있는
            // 것이 다 물건이라 그 말이 곧 「손에 쥘 수 있는 것」이다. 우리 수첩에는 그 뒤에
            // <b>들은 말</b>이 함께 눕는데, 증언은 쥘 종이가 없다. 그래서 넘기다 증언에
            // 닿으면 종이도 없는 것이 종이 자리에 펴졌다 — 카드로 누르면 안 펴지는 것이
            // 옆으로 넘기면 펴지니, 같은 물건이 길에 따라 달라 보인다.
            //
            // 증언은 <b>건너뛴다</b>. 넘기는 손은 견주어 보려는 손이고, 견줄 수 있는 것은
            // 종이뿐이다.
            DocumentView.SetNeighbors(StepTo(i, -1), StepTo(i, +1));
        }

        /// <summary>
        /// <paramref name="from"/> 에서 <paramref name="dir"/> 쪽으로 <b>가장 가까운 물증</b>으로
        /// 가는 손짓. 그쪽에 물증이 없으면 null — 그러면 그 짝은 안 뜬다.
        /// </summary>
        private System.Action StepTo(int from, int dir)
        {
            if (_shown == null) return null;
            for (int k = from + dir; k >= 0 && k < _shown.Count; k += dir)
            {
                var c = _shown[k];
                if (c == null || c.kind != ClueKind.물증) continue;   // 들은 말은 건너뛴다
                int at = k;
                return () => OpenAt(at);
            }
            return null;
        }

        /// <summary>
        /// <b>쪽 넘기는 줄</b> — 판 아래에 「◀   2 / 3   ▶」.
        ///
        /// 휠로도 넘어간다. 저쪽 꾸러미가 휠에 쪽 넘기기를 걸어 두었고(<c>UiWords.Wheel</c>),
        /// 같은 판을 보는 사람이 같은 손짓을 쓰는 것이 맞다.
        /// </summary>
        private void PageRow(int pages)
        {
            if (pages <= 1 || _page == null) return;

            // 저쪽 판의 쪽 표시 자리(−452)다. 여태 칸 자리 안에 두어 마지막 줄을 밀어냈다.
            var row = NewRect("쪽", new Vector2(0f, -452f), new Vector2(600f, 56f), _page);
            _cards.Add(row.gameObject);

            MakePageStep(row, -150f, "◀", -1);
            var lab = NewText("쪽수", (_pageNo + 1) + " / " + pages + " 쪽", Vector2.zero,
                              new Vector2(240f, 56f), row, HintSizeOf,
                              new Color(_inkColor.r, _inkColor.g, _inkColor.b, 0.72f));
            lab.raycastTarget = false;
            MakePageStep(row, 150f, "▶", 1);
        }

        private void MakePageStep(RectTransform row, float x, string glyph, int step)
        {
            var rt = NewRect("쪽넘김", new Vector2(x, 0f), new Vector2(72f, 56f), row);
            var bg = rt.gameObject.AddComponent<Image>();
            Skin(bg, _skin.Wood_, new Color(UiLook.Wood.r, UiLook.Wood.g, UiLook.Wood.b, 0.85f));
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            b.onClick.AddListener(() => Turn(step));
            NewText("글", glyph, Vector2.zero, new Vector2(72f, 56f), rt, HintSizeOf + 4, UiLook.SealText);
        }

        /// <summary>쪽을 넘긴다. 끝에서는 더 안 간다 — 도로 첫 쪽으로 돌면 어디까지 봤는지 잃는다.</summary>
        private void Turn(int step)
        {
            int want = Mathf.Clamp(_pageNo + step, 0, Mathf.Max(0, _pageCount - 1));
            if (want == _pageNo) return;
            _pageNo = want;
            Rebuild();
        }

        /// <summary>지금 보고 있는 쪽. 덮었다 펴면 첫 쪽부터다.</summary>
        private int _pageNo;
        private int _pageCount = 1;
        private int _perPage = 15;

        /// <summary>
        /// <b>[J01] 같은 것은 사람에게 안 보인다.</b>
        ///
        /// 그 대괄호는 <b>우리끼리 쓰는 이름표</b>다 — 어느 단서인지 코드가 짚으려고 붙인
        /// 것이지, 어사가 수첩에 「제이공일」이라 적어 두지는 않는다. 그것이 카드에 그대로
        /// 찍히니 수첩이 <b>자료표</b>처럼 보였다.
        ///
        /// 그리고 <b>들은 말에는 말한 이를 앞세운다</b>. 증언은 누가 했는지가 곧 그 무게라,
        /// 「[마을 사람의 증언] 미친 사내」처럼 임자를 앞에 두면 열 장이 깔려도 갈린다.
        /// </summary>
        /// <summary>
        /// <b>이것을 지금 이 사람에게 내밀 수 있나.</b>
        ///
        /// 여태 <c>presentable</c> 하나만 보았다. 그런데 증언은 <b>누구에게서 들은
        /// 말인지</b>가 함께 적혀 있고(<see cref="ClueEntry.speaker"/>), 그 말을
        /// <b>그 사람 앞에 도로 꺼내 보이는 것</b>은 증거를 대는 것이 아니다 —
        /// 마을 사람이 한 말을 마을 사람에게 들이밀면 "그건 제가 한 말입지요" 로 끝난다.
        /// 남의 말을 옮겨 짚을 때라야 증언이 증거가 된다.
        /// </summary>
        private static bool CanPresent(ClueEntry c, bool talking)
        {
            if (!talking || c == null) return false;
            // 마주한 사람에게 <b>이것을 기다리는 게이트</b>가 있으면 누구의 말이든 내민다(2026-09-15)
            if (InterrogationController.ActiveWants(c.key)) return true;
            if (!c.presentable) return false;
            if (string.IsNullOrEmpty(c.speaker)) return true;          // 주워 온 것은 누구에게든
            string now = InterrogationController.ActiveName;
            if (string.IsNullOrEmpty(now)) return true;
            return c.speaker.Trim() != now.Trim();
        }

        /// <summary>
        /// 칸에 뜨는 이름.
        ///
        /// <b>적어 둔 제목이 있으면 그것이 먼저다</b>(<see cref="ClueEntry.title"/>). 없을
        /// 때에만 본문의 첫 토막을 잘라 쓴다 — 그 잘라 쓰기가 증언 카드를 죄 망가뜨렸다.
        /// 「온 마을이 甲을 주인으로 알고, 담 밖의 사…」는 제목이 아니라 <b>중간에서 끊긴
        /// 문장</b>이고, 그것을 그럴듯하게 보이려고 본문까지 짧게 적히게 만들었다.
        ///
        /// <b>말한 이는 여기서 붙인다.</b> 「[마을 사람의 증언] 문 앞 사내의 정체」 —
        /// 앞은 수첩이 알고 있는 것(<see cref="ClueEntry.speaker"/>)이고 뒤가 적어 둔 제목이다.
        /// 그래서 제목에 말한 이를 또 적으면 두 번 적힌다.
        /// </summary>
        private static string ShortName(Journal.ClueDocument doc, ClueEntry c)
        {
            if (doc != null && !string.IsNullOrEmpty(doc.title)) return Emphasis.Plain(doc.title);

            string t;
            if (c != null && !string.IsNullOrWhiteSpace(c.title))
            {
                t = Emphasis.Plain(c.title.Trim());
            }
            else
            {
                t = Body(c);
                // 칸은 두 줄이다. 열대여섯 자를 넘기면 셋째 줄이 나서 칸 밖으로 흐른다.
                int room = string.IsNullOrEmpty(c.speaker) ? 26 : 18;
                if (t.Length > room) t = t.Substring(0, room).TrimEnd() + "…";
            }
            return string.IsNullOrEmpty(c.speaker) ? t : "[" + c.speaker + "의 증언] " + t;
        }

        /// <summary>
        /// 상세 칸의 본문 — <b>수첩에 적힌 줄이 먼저다.</b>
        ///
        /// 여태 종이가 걸려 있으면 그 종이의 <c>body</c>(방에서 집을 때 뜨는 한 줄 —
        /// 「받은 물건을 적은 물목기다」)를 보였다. 그러면 돋보기로 읽어 덧붙인 것, 무엇이
        /// 이상한지 적어 둔 것이 상세에서 <b>통째로 빠진다</b> — 증거 설명이 한 줄뿐인
        /// 까닭이 이것이었다. 적힌 줄이 없을 때만 종이의 한 줄로 물러선다.
        /// 강조 서식은 살린다(이름표 [J07] 만 걷는다).
        /// </summary>
        private static string DetailBody(Journal.ClueDocument doc, ClueEntry c)
        {
            string t = c != null ? (c.text ?? "") : "";
            if (t.StartsWith("["))
            {
                int close = t.IndexOf(']');
                if (close > 0 && close <= 6 && close + 1 < t.Length) t = t.Substring(close + 1).Trim();
            }
            if (!string.IsNullOrWhiteSpace(t)) return t;
            return doc != null && !string.IsNullOrEmpty(doc.body) ? doc.body : Body(c);
        }

        /// <summary>
        /// 사람이 읽을 본문 — <b>우리끼리 쓰는 이름표를 걷은</b> 것.
        /// 강조 별표도 함께 걷는다(카드 이름은 토막이라 별표가 글자로 남는다).
        /// </summary>
        private static string Body(ClueEntry c)
        {
            string t = Emphasis.Plain(c != null ? (c.text ?? "") : "");
            if (t.StartsWith("["))
            {
                int close = t.IndexOf(']');
                // 「[J01]」처럼 <b>짧은</b> 대괄호만 걷는다 — 「[마을 사람의 증언]」처럼
                // 사람에게 보여 줄 셈으로 적어 둔 것까지 걷으면 안 된다.
                if (close > 0 && close <= 6 && close + 1 < t.Length) t = t.Substring(close + 1).Trim();
            }
            return t;
        }

        // ── UI 헬퍼 ──

        private RectTransform NewRect(string name, Vector2 pos, Vector2 size, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>
        /// 글 한 줄. <b>낡은 판에서 TMP 로 옮겼다</b>(<see cref="UiText"/>).
        /// 낡은 판은 안 접는 것이 기본이라 여기서도 안 접는다 — 접을 자리에서 다시 켠다.
        /// </summary>
        private TextMeshProUGUI NewText(string name, string content, Vector2 pos, Vector2 size,
                                        Transform parent, float fontSize, Color color)
        {
            var t = UiText.New(name, content, pos, size, parent, fontSize, TextAnchor.MiddleCenter, color);
            UiText.Wrap(t, false);
            return t;
        }
    }
}
