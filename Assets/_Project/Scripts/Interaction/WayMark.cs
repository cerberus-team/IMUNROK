using UnityEngine;
using UnityEngine.UI;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>저쪽이다</b> — 목표가 어느 쪽인지 가리키는 표 하나.
    ///
    /// 처음 들어선 방에서 무엇을 해야 하는지 아는 사람은 없다. 조사청에 떨어뜨려
    /// 놓고 "사건 문서를 고르시오" 라고만 하면, 그 문서가 <b>등 뒤에</b> 있을 때는
    /// 아무 말도 안 한 것과 같다. 방향은 말로 할 것이 아니라 <b>가리킬</b> 것이다.
    ///
    /// 두 가지 모습으로 선다:
    ///   · 목표가 <b>화면 안</b>에 있으면 — 그 위에 이름표가 붙는다. 저것이 그것이다.
    ///   · 목표가 <b>화면 밖</b>에 있으면 — 가장자리로 밀려나 그쪽을 가리킨다.
    ///     고개를 돌리면 표가 따라 돌아 안으로 들어오고, 그러면 첫 모습이 된다.
    ///
    /// <b>화면과 나란히 세운다.</b> 눈을 바라보게 하면 귀퉁이에서 비스듬히 돈다 —
    /// 시선이 숙어 있을수록 심해진다. 넘기기 단추에서 한 번 겪은 일이다.
    ///
    /// <code>
    ///   WayMark.Show(문서큐브.transform, "사건 문서");
    ///   WayMark.Hide();
    /// </code>
    /// </summary>
    public class WayMark : MonoBehaviour
    {
        private static WayMark _live;

        /// <summary>눈에서 이만큼 앞(m).</summary>
        private const float Distance = 1.10f;

        /// <summary>캔버스 한 칸이 몇 m 인가. 고르는 창의 글씨와 같은 눈금이다.</summary>
        private const float Scale = 0.001f * (Distance / 0.90f);

        /// <summary>화면 가장자리에서 이만큼 안쪽까지만 나간다(0~1). 잘리지 않게.</summary>
        private const float Edge = 0.12f;

        private Transform _target;
        private Camera _cam;
        private Text _text;
        private CanvasGroup _group;

        /// <summary>표를 세운다. 이미 서 있으면 그것을 걷고 새로 세운다.</summary>
        public static WayMark Show(Transform target, string label)
        {
            Hide();
            if (target == null) return null;
            var cam = Camera.main;
            if (cam == null) return null;

            var go = new GameObject("길표", typeof(Canvas), typeof(CanvasGroup));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;

            var mk = go.AddComponent<WayMark>();
            mk._target = target;
            mk._cam = cam;
            mk._group = go.GetComponent<CanvasGroup>();
            mk._group.alpha = 0f;
            mk.Build(canvas, label);
            _live = mk;
            return mk;
        }

        public static void Hide()
        {
            if (_live == null) return;
            Destroy(_live.gameObject);
            _live = null;
        }

        public static bool Up { get { return _live != null; } }

        private void Build(Canvas canvas, string label)
        {
            var rt = canvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(320f, 76f);
            rt.localScale = Vector3.one * Scale;

            var plate = new GameObject("판", typeof(Image));
            var prt = plate.GetComponent<RectTransform>();
            prt.SetParent(rt, false);
            prt.sizeDelta = rt.sizeDelta;
            // 고르는 창의 단추와 같은 낙관빛. 안내가 저마다 다른 색이면 안내로 안 읽힌다.
            plate.GetComponent<Image>().color = new Color(0.58f, 0.10f, 0.09f, 0.92f);
            plate.GetComponent<Image>().raycastTarget = false;   // 가리키는 것이지 누르는 것이 아니다

            var txtGo = new GameObject("글", typeof(Text));
            var trt = txtGo.GetComponent<RectTransform>();
            trt.SetParent(prt, false);
            trt.sizeDelta = rt.sizeDelta;
            _text = txtGo.GetComponent<Text>();
            _text.font = UiFont.Resolve(null);
            _text.fontSize = 32;
            _text.alignment = TextAnchor.MiddleCenter;
            _text.color = new Color(0.98f, 0.94f, 0.86f);
            _text.raycastTarget = false;
            _text.horizontalOverflow = HorizontalWrapMode.Overflow;
            _text.verticalOverflow = VerticalWrapMode.Overflow;
            _text.text = label;
            _label = label;

            Place();
        }

        private string _label;

        private void LateUpdate()
        {
            if (_cam == null || _target == null) { Destroy(gameObject); return; }
            if (_group.alpha < 1f) _group.alpha = Mathf.Min(1f, _group.alpha + Time.deltaTime * 1.2f);
            Place();
        }

        private void Place()
        {
            Vector3 vp = _cam.WorldToViewportPoint(_target.position);

            // <b>등 뒤에 있는 것의 화면 좌표는 쓰면 안 된다.</b> z 가 음수면 투영이
            // 가운데를 지나 뒤집히고 값도 마구 커진다. 가운데를 기준으로 되뒤집어
            // 써 봤는데, 왼쪽 뒤에 있는 것을 <b>아래쪽</b>이라 가리켰다 — 방향이
            // 세로에 먹혀 버린 것이다.
            //
            // 등 뒤라면 사람이 할 일은 <b>고개를 좌우로 돌리는 것</b>이지 위아래로
            // 드는 것이 아니다. 그러니 화면 좌표가 아니라 <b>카메라가 본 자리</b>에서
            // 왼쪽인지 오른쪽인지만 뽑는다.
            Vector3 local = _cam.transform.InverseTransformPoint(_target.position);
            bool behind = local.z <= 0.05f;

            bool onScreen = !behind
                            && vp.x > Edge && vp.x < 1f - Edge
                            && vp.y > Edge && vp.y < 1f - Edge;

            if (onScreen)
            {
                // 화면 안 — 그 위에 이름표를 붙인다
                _text.text = _label;
                var above = new Vector3(vp.x, Mathf.Min(1f - Edge, vp.y + 0.10f), Distance);
                transform.position = _cam.ViewportToWorldPoint(above);
            }
            else
            {
                // 화면 밖 — 가장자리로 밀어 그쪽을 가리킨다
                Vector2 dir;
                if (behind)
                {
                    // 딱 등 뒤라 좌우가 반반이면 오른쪽으로 보낸다. 어느 쪽으로 돌든
                    // 닿는 자리이므로, 망설이는 표보다 한쪽을 정해 주는 편이 낫다.
                    float x = Mathf.Abs(local.x) < 0.01f ? 1f : local.x;
                    dir = new Vector2(Mathf.Sign(x), 0f);
                }
                else
                {
                    dir = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
                    if (dir.sqrMagnitude < 1e-6f) dir = new Vector2(0f, -1f);
                    dir.Normalize();
                }
                float half = 0.5f - Edge;
                float k = half / Mathf.Max(Mathf.Abs(dir.x), Mathf.Abs(dir.y));
                var at = new Vector3(0.5f + dir.x * k, 0.5f + dir.y * k, Distance);
                transform.position = _cam.ViewportToWorldPoint(at);
                _text.text = Arrow(dir) + " " + _label;
            }

            transform.rotation = _cam.transform.rotation;   // 화면과 나란히
        }

        /// <summary>어느 쪽을 가리키는 화살인가. 글자로 그린다 — 그림 한 장을 아낀다.</summary>
        private static string Arrow(Vector2 dir)
        {
            if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y)) return dir.x > 0f ? "▶" : "◀";
            return dir.y > 0f ? "▲" : "▼";
        }

        private void OnDestroy() { if (_live == this) _live = null; }
    }
}
