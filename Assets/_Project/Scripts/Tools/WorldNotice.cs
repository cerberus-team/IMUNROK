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

        /// <summary>글을 띄운다. verticalOffset은 눈높이 기준 위아래(m).</summary>
        public static void Show(string key, string message, float verticalOffset)
        {
            var n = Get(key, verticalOffset);
            // 별표로 감싼 낱말은 도드라지게 — 자막은 이걸 하는데 알림판은 안 해서,
            // 상태창에 "*문서고*" 가 <b>별표째로</b> 찍히고 있었다.
            n._text.text = Emphasis.Rich(message ?? "", Emphasis.OnDark);
            n._text.gameObject.SetActive(true);
            n._image.gameObject.SetActive(false);
            n._bg.enabled = true;
            n.SetVisible(true);
        }

        /// <summary>그림을 띄운다(지도 등). caption을 주면 그림 위에 제목이 붙는다.</summary>
        public static void ShowImage(string key, Texture texture, float verticalOffset, string caption = null)
        {
            var n = Get(key, verticalOffset);
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
