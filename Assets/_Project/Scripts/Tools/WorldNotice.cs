using UnityEngine;
using UnityEngine.UI;

namespace IMUNROK.Common
{
    /// <summary>
    /// 월드 공간의 간단한 알림판 — 글 한 줄(+선택적으로 그림 한 장)만 띄운다.
    /// 목표 안내·지도처럼 "보여주기만 하는" UI가 각자 Canvas를 만들지 않게 모아둔 것.
    ///
    /// 이름표(key)마다 하나씩 따로 만들어져 서로 겹치지 않는다:
    ///   WorldNotice.Show("목표", "밤이다. 몰래 집 안을 조사하라", 0.55f);
    ///   WorldNotice.ShowImage("지도", 지도텍스처, -0.05f);
    ///   WorldNotice.Hide("지도");
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class WorldNotice : MonoBehaviour
    {
        private static readonly System.Collections.Generic.Dictionary<string, WorldNotice> _all
            = new System.Collections.Generic.Dictionary<string, WorldNotice>();

        private CanvasGroup _group;
        private WorldHudAnchor _anchor;
        private Text _text;
        private RawImage _image;
        private Image _bg;

        /// <summary>
        /// <b>알림판의 세 결.</b>
        ///
        /// <c>Dark</c> — 어두운 판에 흰 글. 지도처럼 <b>들여다보는</b> 것에 쓴다.
        /// <c>Hanji</c> — 한지빛 종잇조각에 <b>금물 글씨</b>. 지나가는 한 마디에 쓴다.
        ///   어두운 네모는 아무리 흐려도 「창」으로 읽히는데, 이쪽은 종이 한 장이
        ///   떴다 지는 것이라 읽고 흘려보내게 된다. 조선의 방문(榜文)이 그러하다.
        /// <c>Gold</c> — <b>바탕이 아예 없다.</b> 금물 글씨만 허공에 뜬다.
        ///   한지 종잇조각도 결국 <b>네모</b>라, 「마을 어귀에 닿았다」 한 줄이 뜰 때마다
        ///   화면 한복판에 판때기가 하나 생겼다 놓인다. 세상을 가리지 않고 지나가는
        ///   말은 글씨만 있으면 된다 — 대신 밤하늘에도 읽히게 글씨 둘레에 먹빛
        ///   그림자를 두른다.
        /// </summary>
        public enum Look { Dark, Hanji, Gold }

        /// <summary>글을 띄운다. verticalOffset은 눈높이 기준 위아래(m).</summary>
        public static void Show(string key, string message, float verticalOffset,
                                Look look = Look.Dark)
        {
            var n = Get(key, verticalOffset);
            n.Wear(look);
            // 별표로 감싼 낱말은 도드라지게 — 자막은 이걸 하는데 알림판은 안 해서,
            // 상태창에 "*문서고*" 가 <b>별표째로</b> 찍히고 있었다.
            n._text.text = Emphasis.Rich(message ?? "",
                                         look == Look.Hanji ? Emphasis.OnPaper : Emphasis.OnDark);
            n._text.gameObject.SetActive(true);
            n._image.gameObject.SetActive(false);
            n._bg.enabled = true;
            n.FitPlate();
            n.SetVisible(true);
        }

        /// <summary>그림을 띄운다(지도 등). caption을 주면 그림 위에 제목이 붙는다.</summary>
        public static void ShowImage(string key, Texture texture, float verticalOffset, string caption = null)
        {
            var n = Get(key, verticalOffset);
            n.Wear(Look.Dark);
            n._image.texture = texture;
            n._image.gameObject.SetActive(texture != null);
            n._bg.enabled = true;

            bool hasCaption = !string.IsNullOrEmpty(caption);
            n._text.gameObject.SetActive(texture == null || hasCaption);
            if (texture == null) n._text.text = "(지도 그림이 없다)";
            else if (hasCaption) n._text.text = Emphasis.Rich(caption ?? "", Emphasis.OnDark);
            // 제목은 그림 위쪽으로 비켜 앉힌다
            ((RectTransform)n._text.transform).anchoredPosition =
                (texture != null && hasCaption) ? new Vector2(0f, 230f) : Vector2.zero;

            n.SetVisible(true);
        }

        public static void Hide(string key)
        {
            if (_all.TryGetValue(key, out var n) && n != null) n.SetVisible(false);
        }

        private static WorldNotice Get(string key, float verticalOffset)
        {
            if (_all.TryGetValue(key, out var found) && found != null)
            {
                found._anchor.SetDistance(1.6f, verticalOffset);
                return found;
            }
            var go = new GameObject($"알림_{key}", typeof(Canvas));
            var anchor = go.AddComponent<WorldHudAnchor>();
            anchor.Configure(WorldHudAnchor.Placement.Front);
            anchor.SetDistance(1.6f, verticalOffset);
            var n = go.AddComponent<WorldNotice>();
            _all[key] = n;
            return n;
        }

        private void Awake()
        {
            _anchor = GetComponent<WorldHudAnchor>();
            _group = gameObject.AddComponent<CanvasGroup>();

            // <b>판이 아니라 말이다.</b> 바탕을 0.72 로 깔아 두었더니 지나가는 한 마디가
            // <b>창</b>으로 보였다 — 테두리가 뚜렷한 네모는 「닫아야 하는 것」으로 읽힌다.
            // 글이 배경에 묻히지 않을 만큼만 남기고(0.28) 나머지는 글씨의 그림자가 받는다.
            var panel = NewRect("바탕", Vector2.zero, new Vector2(1100f, 420f), transform);
            _bg = panel.gameObject.AddComponent<Image>();
            _bg.color = UiLook.With(UiLook.Panel, 0.28f);

            var imgRt = NewRect("그림", Vector2.zero, new Vector2(1040f, 380f), panel);
            _image = imgRt.gameObject.AddComponent<RawImage>();
            _image.raycastTarget = false;
            imgRt.gameObject.SetActive(false);

            var txtRt = NewRect("글", Vector2.zero, new Vector2(1040f, 380f), panel);
            _text = txtRt.gameObject.AddComponent<Text>();
            _text.font = UiFont.Resolve(null);
            _text.fontSize = 40;
            _text.color = UiLook.Text;
            _text.alignment = TextAnchor.MiddleCenter;
            _text.horizontalOverflow = HorizontalWrapMode.Wrap;
            _text.verticalOverflow = VerticalWrapMode.Overflow;
            _text.raycastTarget = false;

            SetVisible(false);
        }

        private Look _look = Look.Dark;
        private Outline _glow;

        /// <summary>
        /// <b>결을 갈아입힌다.</b> 판 하나를 결마다 따로 만들 것 없다 — 알림판은
        /// 이름표마다 한 장이고, 같은 이름표가 다른 결로 뜰 일은 없다.
        /// </summary>
        private void Wear(Look look)
        {
            if (_bg == null || _text == null) return;
            _look = look;

            if (look == Look.Gold)
            {
                // 바탕은 <b>없다</b>. 끄지 않고 투명하게 두는 것은 판 크기를 재는 자리가
                // 이 사각형이기 때문이다(FitPlate) — 껐다 켰다 하면 글이 뜰 때마다
                // 한 프레임씩 자리를 못 잡는다.
                _bg.color = new Color(0f, 0f, 0f, 0f);
                // 금물 글씨. 한지 위가 아니라 <b>세상 위</b>에 얹히므로 밝은 금으로 둔다.
                _text.color = new Color(0.98f, 0.84f, 0.46f);
                // 둘레는 먹빛. 낮의 흰 담벽 위에서도 글자가 남게 하는 것은 이것뿐이다
                // (낡은 UI.Text 에는 그림자 말고 다른 길이 없다).
                if (_glow == null) _glow = _text.gameObject.AddComponent<Outline>();
                _glow.enabled = true;
                _glow.effectColor = new Color(0.06f, 0.04f, 0.02f, 0.92f);
                _glow.effectDistance = new Vector2(2.4f, -2.4f);
                return;
            }

            if (look == Look.Hanji)
            {
                // 한지 한 장. 어두운 판보다 <b>진하게</b> 깐다 — 종이는 비치는 것이
                // 아니라 앞을 가리는 것이고, 금물 글씨가 밤하늘 위에 그냥 뜨면
                // 별빛에 묻힌다.
                _bg.color = UiLook.With(UiLook.Paper, 0.86f);
                // <b>금박이지 금빛이 아니다.</b> 밝은 금을 한지 위에 얹으면 종이와
                // 같은 밝기라 글자가 안 읽힌다(<c>UiLook.Lit</c> 은 밝히기만 하므로
                // 음수를 줘도 어두워지지 않는다 — 여기서 값을 직접 적는 까닭이다).
                // 글자는 금물이 마른 <b>짙은 갈금</b>으로 두고,
                _text.color = new Color(0.34f, 0.22f, 0.06f);
                // 빛은 그 둘레에 두른 <b>밝은 금테</b>가 낸다. 낡은 UI.Text 에는
                // 이것 말고 빛을 흉내 낼 길이 없고, 실제로 금박 글씨가 그렇게 보인다.
                if (_glow == null) _glow = _text.gameObject.AddComponent<Outline>();
                _glow.enabled = true;
                _glow.effectColor = new Color(1f, 0.86f, 0.48f, 0.9f);
                _glow.effectDistance = new Vector2(2.2f, -2.2f);
            }
            else
            {
                _bg.color = UiLook.With(UiLook.Panel, 0.28f);
                _text.color = UiLook.Text;
                if (_glow != null) _glow.enabled = false;
            }
        }

        /// <summary>가고자 하는 밝기. 실제 밝기는 이쪽으로 <b>스르르</b> 따라간다.</summary>
        private float _want;

        /// <summary>
        /// <b>툭 켜고 툭 끄지 않는다.</b>
        ///
        /// 여태 알파를 0 과 1 로 곧장 바꿨다. 그러면 글이 <b>나타나는</b> 것이 아니라
        /// 화면에 <b>붙었다 떨어진다</b> — 창을 여닫는 꼴이라, 읽고 지나가는 한 마디로
        /// 안 읽히고 「무엇이 떴다」가 된다.
        /// 뜨는 것은 조금 느리게, 지는 것은 그보다 느리게 둔다 — 사라지는 것을
        /// 눈으로 좇을 수 있어야 「지나간 말」로 읽힌다.
        /// </summary>
        private void SetVisible(bool on)
        {
            if (_group == null) return;
            _want = on ? 1f : 0f;
            _group.blocksRaycasts = false;   // 알림판은 눌리지 않는다 — 뒤의 것을 가리면 안 된다
        }

        private void LateUpdate()
        {
            if (_group == null) return;
            if (Mathf.Approximately(_group.alpha, _want)) return;
            float speed = _want > _group.alpha ? 4.5f : 2.2f;      // 지는 것이 더 느리다
            _group.alpha = Mathf.MoveTowards(_group.alpha, _want, speed * Time.deltaTime);
        }

        /// <summary>
        /// <b>종이를 글에 맞춘다.</b> 판을 1100×420 으로 못 박아 두면 한 줄짜리
        /// 안내가 <b>벽보만 한 종잇장</b> 한복판에 앉는다. 글이 차지한 만큼에
        /// 여백만 두르면 방문 한 장으로 읽힌다.
        /// </summary>
        private void FitPlate()
        {
            if (_bg == null || _text == null) return;
            var plate = (RectTransform)_bg.transform;
            if (_look == Look.Dark) { plate.sizeDelta = new Vector2(1100f, 420f); return; }

            // 넓이를 먼저 <b>가둔 뒤</b> 높이를 묻는다. <c>preferredWidth</c> 는 줄바꿈을
            // 모르는 값이라, 그것만 보고 판을 잡으면 한 줄짜리 긴 글이 화면을 가로지른다.
            // 반대로 <c>preferredHeight</c> 는 지금 칸 너비를 보고 답하므로, 너비를
            // 정해 놓고 물어야 접힌 줄 수가 나온다.
            // 바탕이 없으면 여백이 곧 <b>허공</b>이라 넉넉히 둘 까닭이 없다.
            // <c>inset</c> 을 0 으로 두지 않으면, 글칸이 종이보다 72 좁은 채로 남아
            // 「마을 어귀에 닿았다」 한 줄이 <b>있지도 않은 종이 폭</b>에 걸려 두 줄로 접힌다.
            float pad = _look == Look.Gold ? 24f : 96f;
            float inset = _look == Look.Gold ? 0f : 72f;
            const float MaxW = 900f;
            var txt = (RectTransform)_text.transform;
            txt.sizeDelta = new Vector2(MaxW - inset, 400f);
            float w = Mathf.Clamp(_text.preferredWidth + pad, 340f, MaxW);
            txt.sizeDelta = new Vector2(w - inset, 400f);
            float h = Mathf.Clamp(_text.preferredHeight + (_look == Look.Gold ? 16f : 56f), 92f, 400f);
            txt.sizeDelta = new Vector2(w - inset, h - (_look == Look.Gold ? 0f : 28f));
            plate.sizeDelta = new Vector2(w, h);
        }

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
    }
}
