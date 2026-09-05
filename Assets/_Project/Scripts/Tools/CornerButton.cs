using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>화면 귀퉁이에 배어 나오는 단추 하나.</b>
    ///
    /// 연출을 넘기는 길이 여태 <b>꾹 누르기</b>였다. 그것은 눌러도 한참 아무 일이
    /// 없다가 갑자기 되는 방식이라, 처음 온 사람에게는 <b>고장난 것과 구별이 안 된다</b>.
    /// 헤드셋에서는 더하다 — 얼마나 눌러야 하는지 알 데가 없고, 컨트롤러는 스치기만
    /// 해도 눌려서 되돌릴 수 없는 일에 그 방식을 쓰기도 어렵다.
    ///
    /// 넘길 수 있다는 것은 <b>눈에 보여야</b> 한다. 다만 처음부터 떠 있으면 안 된다 —
    /// 연출이 시작되자마자 "넘기기"가 떠 있으면 그것은 연출이 아니라 <b>기다리는 화면</b>이
    /// 된다. 그래서 <b>서서히 배어 나온다</b>. 볼 사람은 보고, 건너뛸 사람은 그때쯤
    /// 손이 움직인다.
    ///
    /// <b>자리는 월드가 아니라 화면으로 잡는다</b>(ViewportToWorldPoint). 표제에서는
    /// 고개가 이십도 넘게 돌고 헤드셋에서는 아예 사람이 돌린다 — 월드에 못 박아 두면
    /// 화면 밖으로 밀려나 없는 것이 된다.
    ///
    /// <code>
    ///   CornerButton.Show("튜토리얼 넘기기", () => 건너뛴다());
    ///   CornerButton.Hide();
    /// </code>
    ///
    /// 씬에 놓을 것이 없다. 부르면 서고, 감추라 하면 스러진다.
    /// </summary>
    public class CornerButton : MonoBehaviour
    {
        /// <summary>지금 서 있는 단추. 한 번에 하나만 둔다 — 귀퉁이는 하나다.</summary>
        private static CornerButton _live;

        /// <summary>화면을 0~1 로 본 자리. 여느 때는 오른쪽 <b>위</b>다.</summary>
        public static readonly Vector2 Corner = new Vector2(0.82f, 0.89f);

        /// <summary>
        /// 아래 <b>가운데</b>. 연출을 넘기는 단추가 아니라 <b>고르는 단추</b>가 설 자리다.
        ///
        /// 귀퉁이는 「지금 보고 있는 것에서 빠져나가는 길」의 자리라 눈이 잘 안 간다.
        /// 봉서 셋을 놓고 「어느 것을 맡을까」를 묻는 자리에서는, 고를 것 하나가
        /// 그 셋과 <b>같은 줄에</b> 있어야 함께 견줘진다.
        /// </summary>
        public static readonly Vector2 Below = new Vector2(0.5f, 0.12f);

        /// <summary>이 단추가 선 자리. <see cref="Show"/> 가 정한다.</summary>
        private Vector2 _at = Corner;

        /// <summary>
        /// 가만히 있을 때의 진하기.
        ///
        /// 한동안 0.62 로 흐리게 두었다. 「이 단추는 연출을 보는 사람에게 필요 없는
        /// 물건이니 있다는 것만 알면 된다」는 셈이었는데, 화면으로 보면 그 흐림이
        /// <b>다른 색</b>으로 보인다 — 판은 이름패와 똑같은 주칠인데, 0.62 로 깔리면
        /// 밝은 돌바닥이 비쳐 올라와 <b>연분홍</b>이 된다. 같은 색을 쓴 자리가 둘인데
        /// 한쪽만 물 빠진 꼴이라, 한 벌로 모은 보람이 화면에서 도로 흩어졌다.
        ///
        /// 그래서 판을 그대로 세운다. 눈이 그리로 가는 것은 <b>자리</b>(귀퉁이)와
        /// <b>배어 나오는 시간</b>(fadeIn)으로 눌러 두면 되지, 색을 물 타서 할 일이
        /// 아니었다.
        /// </summary>
        private const float Rest = 1f;

        /// <summary>단추에 적히는 글씨 크기(칸). 자막의 대사와 같아 보이도록 맞춘 값이다.</summary>
        private const int FontSize = 56;

        /// <summary>자막의 이름패가 쓰는 것과 같은 나뭇결. 판마다 새로 그릴 것이 없어 한 벌만 든다.</summary>
        private static readonly IMUNROK.Ui.InventorySkin Skin = new IMUNROK.Ui.InventorySkin();

        /// <summary>눈에서 이만큼 앞(m). 표제 글씨와 같은 거리라 앞뒤로 다투지 않는다.</summary>
        private const float Distance = 0.85f;

        /// <summary>
        /// 캔버스 한 칸이 몇 m 인가.
        ///
        /// <b>고르는 창의 단추와 같은 크기로 맞춘다.</b> 처음에는 이 단추만 크게
        /// 잡았는데(360×96 을 0.0017 로), 눈에서 0.85m 앞에 0.61m 짜리가 서니
        /// 가로로만 39도를 먹어 화면 한쪽이 통째로 단추가 됐다. 「처음부터」·
        /// 「물러나기」 는 280×84 를 0.001 로, 0.90m 앞에 세운다 — 겨눠 누르기에
        /// 모자란 적이 없었으니 여기서만 클 까닭이 없다.
        ///
        /// 거리가 0.85m 로 조금 가까우므로 그만큼 줄여야 <b>보이는 크기</b>가 같다.
        /// </summary>
        /// <summary>
        /// <b>화면 높이를 1732단위로 치는 자막판의 자를 그대로 쓴다.</b>
        /// 그래야 「대사와 같은 크기」가 곱셈 없이 같은 수로 적힌다.
        /// </summary>
        private const float RefH = 1732f;

        /// <summary>단추 한 장의 크기(칸). 고르는 창의 단추와 같다 — <b>가장 작을 때</b>다.</summary>
        private static readonly Vector2 Size = new Vector2(461f, 138f);

        /// <summary>
        /// 이 말을 담을 판의 크기. 한글 한 자를 글씨 크기만큼으로 치고 양옆에 한 자씩
        /// 여백을 둔다 — 재서 맞추는 것이 아니라 넉넉히 잡는 셈이다. 짧은 말은
        /// <see cref="Size"/> 그대로라 여태 서던 단추의 크기가 안 변한다.
        /// </summary>
        private static Vector2 SizeFor(string label)
        {
            int n = string.IsNullOrEmpty(label) ? 0 : label.Length;
            return new Vector2(Mathf.Max(Size.x, (n + 2) * FontSize), Size.y);
        }

        /// <summary>이제 안 쓴다 — 화면에 붙였으므로 거리가 없다. 셈의 내력으로 남긴다.</summary>

        private CanvasGroup _group;
        private Action _onPress;
        private bool _spent;

        /// <summary>
        /// 귀퉁이에 단추를 세운다. 이미 서 있으면 그것을 걷고 새로 세운다 —
        /// 두 개가 겹쳐 서면 어느 것을 눌렀는지 알 수 없다.
        /// </summary>
        public static CornerButton Show(string label, Action onPress, float fadeIn = 1.2f, Vector2? at = null)
        {
            Hide();
            var cam = Camera.main;
            if (cam == null) return null;

            var go = new GameObject("귀퉁이_단추", typeof(Canvas), typeof(CanvasScaler),
                                    typeof(CanvasGroup), typeof(GraphicRaycaster), typeof(CornerButton));
            var cb = go.GetComponent<CornerButton>();
            cb._onPress = onPress;
            cb._at = at ?? Corner;
            cb.Build(label);
            cb.StartCoroutine(cb.FadeIn(fadeIn));
            _live = cb;
            return cb;
        }

        /// <summary>서 있으면 걷는다.</summary>
        public static void Hide()
        {
            if (_live == null) return;
            Destroy(_live.gameObject);
            _live = null;
        }

        /// <summary>지금 서 있나.</summary>
        public static bool Up => _live != null;

        private void Build(string label)
        {
            // <b>화면에 붙인다.</b> 여태 눈앞 0.85m 허공에 세우고 매 칸 화면 귀퉁이로
            // 끌어다 놓았다 — 자리는 화면으로 잡으면서 판만 세상에 있던 셈이다.
            // 그러느라 판이 기둥에 가리고, 색도 세상을 거쳐 나오느라 자막의 이름패와
            // 어긋났다(민판 0.667 대 이름패 0.184). 화면에 붙이면 둘 다 없어진다.
            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;                // 자막과 같은 켜

            var scaler = GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefH * 16f / 9f, RefH);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            _group = GetComponent<CanvasGroup>();
            _group.alpha = 0f;

            // <b>긴 말은 판을 넓혀서 받는다.</b> 글씨는 넘쳐도 그려지도록 해 두었으므로
            // (horizontalOverflow), 판만 280 으로 못 박아 두면 글씨가 판 밖으로 비어져
            // 나와 허공에 뜬다. 「튜토리얼 넘기기」는 여덟 자라 280 에 들어맞지만,
            // 고르는 말은 그보다 길다.
            var size = SizeFor(label);

            var rt = canvas.GetComponent<RectTransform>();

            var bgGo = new GameObject("판", typeof(Image), typeof(Button));
            var brt = bgGo.GetComponent<RectTransform>();
            brt.SetParent(rt, false);
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = size;

            // <b>왕의 이름패와 똑같이 세운다 — 나뭇결 위의 주칠.</b>
            //
            // 여태 <c>UiLook.Seal</c> 을 민판에 그대로 발랐다. 그러면 값은 같은데 화면에서
            // 딴 색이 된다. 재 보면 이렇다 —
            //   민판에 주칠 : (0.667, 0.220, 0.165)  적어 둔 값 그대로, 훤하다
            //   이름패      : (0.184, 0.019, 0.006)  훨씬 깊다
            //
            // 한동안 이것을 그리기 탓으로 알고 어두운 값을 손으로 지어 맞췄는데, 뿌리는
            // 그게 아니었다. 이름패는 <c>Skin(im, _skin.Wood_, Seal)</c> 이다 — <b>주칠을
            // 나뭇결 무늬 위에 입힌 것</b>이고, 그 무늬가 어두워 색이 가라앉는다.
            // 민판에 같은 값을 발라 놓고 색이 다르다 한 것은 애초에 다른 물건이었다.
            //
            // 그래서 무늬까지 같이 쓴다. 지어낸 값이 하나도 없으니 저쪽 색이 바뀌면
            // 이쪽도 따라간다.
            var img = bgGo.GetComponent<Image>();
            img.sprite = Skin.Wood_;
            img.type = Image.Type.Simple;
            img.color = UiLook.Seal;

            var btn = bgGo.GetComponent<Button>();
            btn.targetGraphic = img;
            var c = btn.colors;
            c.normalColor = Color.white;
            c.highlightedColor = new Color(1.5f, 1.35f, 1.2f, 1f);   // 얹히면 놋빛으로 달아오른다
            c.pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
            c.fadeDuration = 0.08f;
            btn.colors = c;
            btn.onClick.AddListener(Press);

            // 테는 없앤다. 판이 낙관빛이 되었으니 그 위에 다시 붉은 줄을 그으면
            // 같은 색이 겹칠 뿐이고, 고르는 창의 단추에도 그런 줄은 없다.

            var txtGo = new GameObject("글", typeof(Text));
            var trt = txtGo.GetComponent<RectTransform>();
            trt.SetParent(brt, false);
            trt.anchoredPosition = Vector2.zero;
            trt.sizeDelta = size;
            var txt = txtGo.GetComponent<Text>();
            txt.font = UiFont.Resolve(null);
            // <b>자막의 대사와 같은 크기로 맞춘다.</b> 캔버스가 서로 달라 숫자만으로는
            // 견줄 수 없다 — 화면 높이에서 차지하는 몫으로 재야 한다.
            //   자막  : 대사 56단위 ÷ 화면 1732단위 = 3.23%
            //   여기  : 화면 반높이가 0.85m·화각 60°에서 520단위이므로 34 ÷ 1039 = 3.27%
            // 36 이던 것은 3.46% 라 대사보다 도리어 컸다. 넘기라고 조르는 말이
            // 왕의 말보다 큰 것은 앞뒤가 뒤집힌 것이다.
            // (값은 <see cref="FontSize"/> 에 있다 — 판 너비 셈도 그것을 쓴다)
            txt.fontSize = FontSize;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = UiLook.Text;
            txt.raycastTarget = false;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.text = label;

            Place();
        }

        /// <summary>눌렀다. <b>한 번만</b> 듣는다 — 넘기는 일을 두 번 하면 두 걸음 뛴다.</summary>
        public void Press()
        {
            if (_spent) return;
            _spent = true;
            var f = _onPress;
            _onPress = null;
            Hide();
            if (f != null) f();
        }



        /// <summary>
        /// 화면 귀퉁이에 붙여 세운다.
        ///
        /// <b>눈을 바라보게 세우면 안 된다.</b> 그렇게 두었더니 단추가 비스듬히
        /// 돌아가 걸렸다 — 어전은 시선이 오십일 도 숙어 있어서, 화면 <b>귀퉁이</b>로
        /// 가는 방향은 카메라가 보는 방향과 어긋난다. 그 어긋난 축을 바라보게
        /// 하면 판이 그만큼 기운다. 가운데 놓인 것에서는 안 드러나고 귀퉁이에서만
        /// 드러나는 종류의 어긋남이다.
        ///
        /// 화면에 붙은 것은 <b>화면과 나란해야</b> 한다. 카메라의 자세를 그대로
        /// 쓰면 어디에 놓든 반듯하다.
        /// </summary>
        private void Place()
        {
            // 화면을 0~1 로 본 자리를 <b>가운데 기준 단위</b>로 옮긴다.
            var rt = (RectTransform)transform.GetChild(0);
            rt.anchoredPosition = new Vector2((_at.x - 0.5f) * RefH * 16f / 9f,
                                              (_at.y - 0.5f) * RefH);
        }

        private IEnumerator FadeIn(float seconds)
        {
            float dur = Mathf.Max(0.01f, seconds);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / dur;
                if (_group != null) _group.alpha = Mathf.SmoothStep(0f, Rest, Mathf.Clamp01(t));
                yield return null;
            }
        }

        private void OnDestroy() { if (_live == this) _live = null; }
    }
}
