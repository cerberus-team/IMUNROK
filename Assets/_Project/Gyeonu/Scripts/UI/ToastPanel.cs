using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 안내 문구 판 (2026-08-26) — <see cref="DebugToast"/> 가 IMGUI로 그리던 것을 월드로 옮긴 것.
    ///
    /// ■ PC에서 같아 보이게 한 방법
    ///   IMGUI가 쓰던 <c>Rect</c>·<c>fontSize</c>·색을 <b>숫자 그대로</b> 옮겼다.
    ///   <see cref="VrPanel"/> 이 캔버스 1단위 = 화면 1픽셀로 맞춰 주므로 자리도 크기도 같다.
    ///     위 안내 : 상자 520×40 을 화면 높이 22% 자리에, 글 18px 굵게
    ///     아래 고정: 상자 520×36 을 화면 바닥에서 34px 자리에, 글 16px 굵게
    ///
    /// ■ 부르는 쪽은 한 줄도 안 고쳤다
    ///   <c>DebugToast.Show/ShowPinned</c> 는 정적 API 그대로다 — 21개 파일 43곳이 무수정이다.
    /// </summary>
    [AddComponentMenu("")]
    public class ToastPanel : VrPanel
    {
        /// <summary>아래 고정 줄인가 (아니면 위에 잠깐 뜨는 줄).</summary>
        public bool pinned;

        string message;
        Image box;
        TextMeshProUGUI label;

        public static ToastPanel Create(Transform parent, bool pinned)
        {
            var go = new GameObject(pinned ? "안내_고정판" : "안내_판", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<ToastPanel>();
            p.pinned = pinned;
            return p;
        }

        /// <summary>비우거나 null이면 판이 통째로 사라진다.</summary>
        public void SetMessage(string msg) { message = msg; }

        // 2026-09-10 — 두세 줄짜리 안내(관아 앞 "다른 길" 문구 등)가 36px 판 아래로 흘러넘쳐 잘렸다.
        //   줄 수만큼 판을 키우고, 고정판은 바닥 여백을 지키도록 그만큼 위로 올린다.
        const float LineStep = 20f;
        float Extra { get { return Mathf.Max(0, Lines(message) - 1) * LineStep; } }

        static int Lines(string s)
        {
            if (string.IsNullOrEmpty(s)) return 1;
            int n = 1;
            foreach (var c in s) if (c == '\n') n++;
            return n;
        }

        protected override Vector2 PanelSize
        {
            get { return pinned ? new Vector2(520f, 36f + Extra) : new Vector2(520f, 40f + Extra); }
        }

        // IMGUI 자리 그대로: 위 = 화면 높이 22% + 글 절반, 아래 = 바닥에서 34px
        protected override Vector2 Anchor
        {
            get
            {
                return pinned
                    ? FromTopLeft(RefW * 0.5f, RefH - 34f - Extra * 0.5f)
                    : FromTopLeft(RefW * 0.5f, RefH * 0.22f + 14f + Extra * 0.5f);
            }
        }

        protected override bool Visible { get { return !string.IsNullOrEmpty(message); } }

        protected override void Build()
        {
            box = MakeBox(root, "바탕", UiSkin.ToastBack);
            Stretch(box.rectTransform, 0f);

            label = MakeText(root, "글", pinned ? 16 : 18, TextAnchor.MiddleCenter, UiSkin.ToastText);
            label.fontStyle = FontStyles.Bold;
            Stretch(label.rectTransform, 0f);
        }

        protected override void Refresh()
        {
            if (label != null && label.text != message)
            {
                label.text = message ?? "";
                root.sizeDelta = PanelSize;   // 줄 수에 맞춰 판을 다시 잰다
            }
        }
    }
}
