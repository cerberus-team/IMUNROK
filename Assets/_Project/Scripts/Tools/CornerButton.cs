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

        /// <summary>화면을 0~1 로 본 자리. 오른쪽 위.</summary>
        private static readonly Vector2 At = new Vector2(0.80f, 0.82f);

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
        private const float Scale = 0.001f * (Distance / 0.90f);

        /// <summary>단추 한 장의 크기(칸). 고르는 창의 단추와 같다.</summary>
        private static readonly Vector2 Size = new Vector2(280f, 84f);

        private Transform _eye;
        private Camera _cam;
        private CanvasGroup _group;
        private Action _onPress;
        private bool _spent;

        /// <summary>
        /// 귀퉁이에 단추를 세운다. 이미 서 있으면 그것을 걷고 새로 세운다 —
        /// 두 개가 겹쳐 서면 어느 것을 눌렀는지 알 수 없다.
        /// </summary>
        public static CornerButton Show(string label, Action onPress, float fadeIn = 1.2f)
        {
            Hide();
            var cam = Camera.main;
            if (cam == null) return null;

            var go = new GameObject("귀퉁이_단추", typeof(Canvas), typeof(CanvasGroup),
                                    typeof(GraphicRaycaster), typeof(CornerButton));
            var cb = go.GetComponent<CornerButton>();
            cb._eye = cam.transform;
            cb._cam = cam;
            cb._onPress = onPress;
            cb.Build(label, cam);
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

        private void Build(string label, Camera cam)
        {
            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;                 // 없으면 마우스가 못 짚는다
            _group = GetComponent<CanvasGroup>();
            _group.alpha = 0f;

            var rt = canvas.GetComponent<RectTransform>();
            rt.sizeDelta = Size;
            rt.localScale = Vector3.one * Scale;

            var bgGo = new GameObject("판", typeof(Image), typeof(Button));
            var brt = bgGo.GetComponent<RectTransform>();
            brt.SetParent(rt, false);
            brt.anchoredPosition = Vector2.zero;
            brt.sizeDelta = Size;

            // 고르는 창의 「처음부터」와 같은 낙관빛. 어전은 어두워서 검은 판을
            // 두면 있는지조차 잘 안 보인다.
            var img = bgGo.GetComponent<Image>();
            img.color = new Color(0.58f, 0.10f, 0.09f);

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
            trt.sizeDelta = Size;
            var txt = txtGo.GetComponent<Text>();
            txt.font = UiFont.Resolve(null);
            txt.fontSize = 36;                       // 고르는 창의 단추 글씨와 같다
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = new Color(0.98f, 0.94f, 0.86f);
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

        private void LateUpdate()
        {
            if (_cam == null || _eye == null) { Destroy(gameObject); return; }
            Place();
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
            transform.position = _cam.ViewportToWorldPoint(new Vector3(At.x, At.y, Distance));
            transform.rotation = _eye.rotation;
        }

        private IEnumerator FadeIn(float seconds)
        {
            float dur = Mathf.Max(0.01f, seconds);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / dur;
                if (_group != null) _group.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                yield return null;
            }
        }

        private void OnDestroy() { if (_live == this) _live = null; }
    }
}
