using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>수첩에 한 줄이 오르면 그 자리에서 알린다</b> — 「수첩에 적었다」.
    ///
    /// 여태 단서는 <b>소리 없이</b> 적혔다. 방에서 막 집은 종이 하나만 「새로 얻은 것」으로 펼쳐질 뿐
    /// (<see cref="NewFindNotice"/>), 들은 말·돋보기로 읽어 낸 줄·유척으로 잰 치수는 수첩을 펴 봐야
    /// 늘었는지 알았다. 그러면 무엇을 해야 사건이 나아가는지가 손에 안 잡힌다 — 2026-10-17
    /// 걷힌 팔을 재 보고 「증거가 모였다는 걸 어떻게 아나」고 물은 것이 그것이었다.
    ///
    /// 이제 물증이든 들은 말이든 <b>새로 적힐 때마다</b> 화면 위쪽에 한 줄이 잠깐 섰다 스러진다.
    /// 무엇이 적혔는지(제목)까지 함께 — 「적었다」만으로는 무엇을 얻었는지 모른다.
    /// 한꺼번에 여럿이 오르면 차례로 띄운다. 화면 판이라 어느 판이 떠 있어도 보인다.
    ///
    /// 씬에 놓을 것이 없다. 판이 올라오면 저 혼자 붙는다.
    /// </summary>
    public class ClueToast : MonoBehaviour
    {
        private static ClueToast _live;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_live != null) return;
            var go = new GameObject("_수첩에적었다", typeof(RectTransform));
            DontDestroyOnLoad(go);
            _live = go.AddComponent<ClueToast>();
        }

        private const float Hold = 2.6f;      // 다 떠 있는 동안
        private const float Fade = 0.35f;     // 배어 나오고 스러지는 데
        private const int MaxLen = 64;        // 한 줄에 담는 글자 — 넘치면 줄인다

        private readonly Queue<string> _queue = new Queue<string>();
        private CanvasGroup _group;
        private TextMeshProUGUI _head;
        private TextMeshProUGUI _line;
        private bool _running;
        private Journal _hooked;

        private void Awake()
        {
            ScreenPanel.Raise(gameObject, ScreenPanel.LayerBar + 5);
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            var root = (RectTransform)transform;
            // 화면 위쪽 가운데 — 안내판(StatusPanel) 아래, 사람 얼굴보다 위
            var plate = new GameObject("판", typeof(RectTransform), typeof(Image));
            var prt = (RectTransform)plate.transform;
            prt.SetParent(root, false);
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.78f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(1500f, 128f);
            var img = plate.GetComponent<Image>();
            img.color = UiLook.With(UiLook.Panel, 0.9f);
            img.raycastTarget = false;

            _head = UiText.New("머리", "수첩에 적었다", new Vector2(0f, 26f), new Vector2(1440f, 48f),
                               prt, ScreenPanel.LineSize - 18, TextAnchor.MiddleCenter, UiLook.Gold);
            _line = UiText.New("글", "", new Vector2(0f, -22f), new Vector2(1440f, 56f),
                               prt, ScreenPanel.LineSize - 8, TextAnchor.MiddleCenter, UiLook.Text);
            // 돋보기로 본 것처럼 긴 줄은 글씨를 줄여 한 줄에 담는다 — 두 줄로 꺾이면 판 밖으로 샌다
            _line.enableAutoSizing = true;
            _line.fontSizeMax = ScreenPanel.LineSize - 8;
            _line.fontSizeMin = 26;
        }

        /// <summary>
        /// 수첩에 오르는 것과 따로 <b>지금</b> 한 줄을 띄운다. 이미 적혀 있던 단서를 다시 얻으면
        /// 수첩은 두 번 적지 않아(같은 key) 알림도 안 뜬다 — 그래도 「잰 것이 맞다」는 말은 들어야 하는 자리에서 부른다.
        /// </summary>
        public static void Show(string line)
        {
            if (_live == null) Install();
            if (_live == null || string.IsNullOrEmpty(line)) return;
            _live.Push(line);
        }

        private void Update()
        {
            // 수첩은 씬마다 새로 설 수 있다 — 지금 수첩에 매달려 있는지 매번 본다
            var j = Journal.Instance;
            if (j == _hooked) return;
            if (_hooked != null) _hooked.OnClueAdded -= OnAdded;
            _hooked = j;
            if (j != null) j.OnClueAdded += OnAdded;
        }

        private void OnDestroy()
        {
            if (_hooked != null) _hooked.OnClueAdded -= OnAdded;
            if (_live == this) _live = null;
        }

        private void OnAdded(ClueEntry e)
        {
            if (e == null) return;
            string what = !string.IsNullOrEmpty(e.title) ? e.title : Emphasis.Plain(e.text);
            if (string.IsNullOrEmpty(what)) return;
            what = what.Replace('\n', ' ').Trim();
            if (what.Length > MaxLen) what = what.Substring(0, MaxLen - 1) + "…";
            if (e.kind == ClueKind.정황 && !string.IsNullOrEmpty(e.speaker)) what = e.speaker + " — " + what;
            Push(what);
        }

        private string _lastPushed;
        private float _lastPushedAt = -99f;

        private void Push(string what)
        {
            what = Emphasis.Plain(what).Replace('\n', ' ').Trim();
            if (what.Length > MaxLen) what = what.Substring(0, MaxLen - 1) + "…";
            // 같은 줄이 곧바로 두 번 오면(수첩 알림 + 직접 부름) 한 번만
            if (what == _lastPushed && Time.unscaledTime - _lastPushedAt < 1.5f) return;
            _lastPushed = what; _lastPushedAt = Time.unscaledTime;
            _queue.Enqueue(what);
            if (!_running) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            _running = true;
            while (_queue.Count > 0)
            {
                _line.text = _queue.Dequeue();
                for (float t = 0f; t < Fade; t += Time.unscaledDeltaTime) { _group.alpha = t / Fade; yield return null; }
                _group.alpha = 1f;
                for (float t = 0f; t < Hold; t += Time.unscaledDeltaTime) yield return null;
                for (float t = 0f; t < Fade; t += Time.unscaledDeltaTime) { _group.alpha = 1f - t / Fade; yield return null; }
                _group.alpha = 0f;
            }
            _running = false;
        }
    }
}
