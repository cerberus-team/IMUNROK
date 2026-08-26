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

        /// <summary>캔버스 한 칸이 몇 m 인가. 겨눠서 누르는 것이라 읽기만 하는 글씨보다 크다.</summary>
        private const float Scale = 0.0017f;

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
            rt.sizeDelta = new Vector2(360f, 96f);
            rt.localScale = Vector3.one * Scale;

            var bgGo = new GameObject("판", typeof(Image), typeof(Button));
            var brt = bgGo.GetComponent<RectTransform>();
            brt.SetParent(rt, false);
            brt.anchoredPosition = Vector2.zero;
            brt.sizeDelta = rt.sizeDelta;

            var img = bgGo.GetComponent<Image>();
            img.color = new Color(0.09f, 0.09f, 0.10f, 0.80f);

            var btn = bgGo.GetComponent<Button>();
            btn.targetGraphic = img;
            var c = btn.colors;
            c.normalColor = Color.white;
            c.highlightedColor = new Color(1.5f, 1.35f, 1.2f, 1f);   // 얹히면 놋빛으로 달아오른다
            c.pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
            c.fadeDuration = 0.08f;
            btn.colors = c;
            btn.onClick.AddListener(Press);

            // 낙관빛 테 한 줄. 어둠 위에 얹히는 것이라 흰 판을 놓으면 그것만 떠오른다.
            var line = new GameObject("테", typeof(Image));
            var lrt = line.GetComponent<RectTransform>();
            lrt.SetParent(brt, false);
            lrt.anchoredPosition = new Vector2(0f, -46f);
            lrt.sizeDelta = new Vector2(360f, 3f);
            line.GetComponent<Image>().color = new Color(0.78f, 0.24f, 0.19f, 0.9f);
            line.GetComponent<Image>().raycastTarget = false;

            var txtGo = new GameObject("글", typeof(Text));
            var trt = txtGo.GetComponent<RectTransform>();
            trt.SetParent(brt, false);
            trt.anchoredPosition = Vector2.zero;
            trt.sizeDelta = rt.sizeDelta;
            var txt = txtGo.GetComponent<Text>();
            txt.font = UiFont.Resolve(null);
            txt.fontSize = 40;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = new Color(0.97f, 0.93f, 0.82f);
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

        private void Place()
        {
            Vector3 pos = _cam.ViewportToWorldPoint(new Vector3(At.x, At.y, Distance));
            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(pos - _eye.position, _eye.up);
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
