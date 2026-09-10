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
    public class WorldNotice : MonoBehaviour
    {
        private static readonly System.Collections.Generic.Dictionary<string, WorldNotice> _all
            = new System.Collections.Generic.Dictionary<string, WorldNotice>();

        /// <summary>
        /// <b>눈앞 1.6m 의 한 뼘이 화면에서 몇 단위인가.</b>
        ///
        /// 이 판은 여태 <b>세상에 서 있었다</b>(<see cref="WorldHudAnchor"/>) — 자막·수첩·
        /// 귀퉁이 단추가 차례로 화면으로 옮겨 가는 동안 혼자 남아, 고개를 돌리면 뒤늦게
        /// 따라오고 벽에 가까이 서면 벽을 뚫고 나왔다. 「마을 어귀에 닿았다」는 세상 속
        /// 물건이 아니라 <b>이야기가 건네는 한 마디</b>이므로 화면에 박혀 있어야 한다.
        ///
        /// 옮기면서 <b>보이는 크기는 그대로</b> 두려고, 옛 좌표를 이 값으로 환산한다:
        /// 세로 화각 60°에서 1.6m 앞의 반높이는 0.924m 이고 그것이 866단위였으므로,
        /// 1m 는 937단위다. 예전 verticalOffset(미터)에 이것을 곱하면 같은 자리에 앉는다.
        /// </summary>
        private const float UnitPerMetre = 937f;

        private CanvasGroup _group;
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
            n._bloom = 1f;          // 적힌 그 순간 한 번 번진다
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
                found.PlaceAt(verticalOffset);
                return found;
            }
            var go = new GameObject($"알림_{key}", typeof(RectTransform));
            var n = go.AddComponent<WorldNotice>();
            // 자막 바(100)보다 한 켜 아래 — 말이 오가는 자리를 안내가 덮으면 안 된다.
            ScreenPanel.Raise(go, ScreenPanel.LayerBar - 5);
            n.PlaceAt(verticalOffset);
            _all[key] = n;
            return n;
        }

        /// <summary>눈높이 기준 위아래(m) 를 화면 자리로 옮겨 앉힌다.</summary>
        private void PlaceAt(float verticalOffset)
        {
            if (_bg == null) return;
            ((RectTransform)_bg.transform).anchoredPosition =
                new Vector2(0f, verticalOffset * UnitPerMetre);
        }

        private void Awake()
        {
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
            // <b>40 은 컸다.</b> 한 줄짜리 안내는 멀쩡했는데 문장이 길어지면 판 밖으로
            // 넘어갔다 — 넓이 상한(MaxW)에 먼저 걸려 접히고, 접힌 줄이 높이 상한에
            // 또 걸려 잘렸다. 글씨를 줄이면 같은 넓이에 더 담기므로 접히는 자리부터
            // 뒤로 밀린다.
            _text.fontSize = 33;
            _text.color = UiLook.Text;
            _text.alignment = TextAnchor.MiddleCenter;
            _text.horizontalOverflow = HorizontalWrapMode.Wrap;
            _text.verticalOverflow = VerticalWrapMode.Overflow;
            _text.raycastTarget = false;

            SetVisible(false);
        }

        private Look _look = Look.Dark;
        private Outline _glow;      // 먹빛 그림자 — 어디에 얹혀도 글자가 남게
        private Outline _halo;      // 금빛 무리 — 뜰 때 한 번 번졌다 가라앉는다

        /// <summary>
        /// <b>갓 떴는가.</b> 1 이면 방금, 0 이면 다 가라앉았다.
        ///
        /// 툭 켜지고 툭 꺼지는 것은 이미 알파로 다뤘는데(<see cref="SetVisible"/>),
        /// 그것만으로는 <b>글자가 켜진 것</b>이지 <b>먹이 마르며 금박이 앉는 것</b>이
        /// 아니다. 금물 글씨는 처음 한순간 환하게 번졌다가 제 빛으로 잦아든다 —
        /// 그 잦아듦이 있어야 「방금 무엇이 적혔다」가 눈에 걸린다.
        /// </summary>
        private float _bloom;

        /// <summary>번진 것이 가라앉는 데 걸리는 시간(초).</summary>
        private const float BloomSeconds = 1.3f;

        // 가라앉은 금물과 갓 번진 금물. 뜰 때는 흰빛에 가깝게 밝았다가 제 색을 찾는다.
        private static readonly Color GoldRest = new Color(0.98f, 0.84f, 0.46f);
        private static readonly Color GoldLit  = new Color(1f, 0.97f, 0.82f);
        // 무리는 <b>옅어야 한다</b>. 처음에 0.85·일곱 화소로 두었더니 글자 사이가
        // 통째로 메워져 금덩이 한 장이 떴다 — 빛나는 것이 아니라 <b>뭉개진 것</b>이다.
        private static readonly Color HaloLit  = new Color(1f, 0.90f, 0.58f, 0.42f);
        private static readonly Color HaloRest = new Color(1f, 0.88f, 0.50f, 0f);

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
                _text.color = GoldRest;
                // 둘레는 먹빛. 낮의 흰 담벽 위에서도 글자가 남게 하는 것은 이것뿐이다
                // (낡은 UI.Text 에는 그림자 말고 다른 길이 없다).
                if (_glow == null) _glow = _text.gameObject.AddComponent<Outline>();
                _glow.enabled = true;
                _glow.effectColor = new Color(0.06f, 0.04f, 0.02f, 0.92f);
                _glow.effectDistance = new Vector2(2.2f, -2.2f);
                // 그 바깥에 <b>한 겹 더</b> — 금빛 무리. 이것만 시간에 따라 옅어진다.
                // 낡은 UI.Text 로 빛을 흉내 낼 길은 테두리를 겹치는 것뿐이다.
                if (_halo == null) _halo = _text.gameObject.AddComponent<Outline>();
                _halo.enabled = true;
                _halo.effectColor = HaloRest;
                _halo.effectDistance = new Vector2(2.6f, -2.6f);
                return;
            }
            if (_halo != null) _halo.enabled = false;

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
            Settle();
            if (_group == null) return;
            if (Mathf.Approximately(_group.alpha, _want)) return;
            float speed = _want > _group.alpha ? 4.5f : 2.2f;      // 지는 것이 더 느리다
            _group.alpha = Mathf.MoveTowards(_group.alpha, _want, speed * Time.deltaTime);
        }

        /// <summary>번진 금물이 제 빛으로 잦아든다.</summary>
        private void Settle()
        {
            if (_look != Look.Gold || _text == null || _halo == null) return;
            if (_bloom <= 0f) return;
            _bloom = Mathf.MoveTowards(_bloom, 0f, Time.deltaTime / BloomSeconds);
            // 끝으로 갈수록 천천히 — 확 밝았다가 오래 잦아드는 것이 불빛의 결이다.
            float k = _bloom * _bloom;
            _text.color = Color.Lerp(GoldRest, GoldLit, k);
            _halo.effectColor = Color.Lerp(HaloRest, HaloLit, k);
            _halo.effectDistance = Vector2.Lerp(new Vector2(2.6f, -2.6f), new Vector2(4.2f, -4.2f), k);
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
            // 바탕이 없는 결은 넓게 써도 판때기가 안 생긴다 — 접히는 자리를 늦춘다.
            float MaxW = _look == Look.Gold ? 1240f : 900f;
            var txt = (RectTransform)_text.transform;
            txt.sizeDelta = new Vector2(MaxW - inset, 400f);
            float w = Mathf.Clamp(_text.preferredWidth + pad, 340f, MaxW);
            txt.sizeDelta = new Vector2(w - inset, 400f);
            float h = Mathf.Clamp(_text.preferredHeight + (_look == Look.Gold ? 16f : 56f), 76f, 520f);
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
