using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>지도를 펴서 옮겨 간다.</b> 관아처럼 넓기만 하고 걸을 일이 없는 데를 건너뛴다.
    ///
    /// <b>왜 필요한가.</b> 관아 담 안이 37 × 27m 다. 대문에서 동헌까지 스물세 걸음,
    /// 문서고에서 동헌까지 스무 걸음인데 그 사이에 <b>아무것도 없다</b>. 한 번 걸으면
    /// 넓은 관아지만, 대장을 읽고 동헌으로 갔다가 다시 대조하러 서고로 돌아오는 일을
    /// 네 번 하면 그건 그냥 빈 마당을 여덟 번 가로지르는 것이다.
    ///
    /// 마당을 좁히는 길도 있었다. 그런데 담을 안으로 밀면 132 조각을 다 옮겨야 하고,
    /// 무엇보다 <b>관아는 원래 넓다</b> — 좁히면 관아처럼 안 보인다.
    /// 넓이는 두고 <b>오가는 수고만</b> 덜어 낸다.
    ///
    /// <b>처음 한 번은 걸어야 한다.</b> 잠겨 있다가 서리가 길을 다 일러 준 뒤에 풀린다
    /// (<see cref="Unlock"/>). 어디가 어디인지 모르는 채로 이름만 눌러 건너뛰면,
    /// 관아가 <b>세 칸짜리 메뉴</b>가 되고 만다. 한 번 걸어 본 데라야 지도가 지도다.
    ///
    /// <b>앉아 있을 때는 안 열린다.</b> 앉은 채로 옮겨 가면 허공에 앉은 꼴이 된다 —
    /// 일어서라고 이른다.
    ///
    /// 붙이는 곳: 씬에 빈 오브젝트 하나(관아의 _오갈데).
    /// </summary>
    public class TravelBoard : MonoBehaviour
    {
        [System.Serializable]
        public class Stop
        {
            [Tooltip("단추에 뜨는 이름")]
            public string 이름 = "";

            [Tooltip("가서 설 자리. 방향까지 이 표식을 따른다")]
            public Transform 자리;

            [Tooltip("이 거리(m) 안에 있으면 '여기'로 보고 흐리게 그린다")]
            public float 여기 = 6f;
        }

        public static TravelBoard Instance { get; private set; }

        [SerializeField] private Stop[] _stops;

        [Tooltip("이 키로 폈다 접는다")]
        [SerializeField] private Key _key = Key.M;

        [Tooltip("켜면 처음부터 쓸 수 있다. 끄면 Unlock() 을 부를 때까지 잠겨 있다")]
        [SerializeField] private bool _openAtStart = false;

        [Tooltip("옮겨 갈 때 눈을 감았다 뜨는 시간(초)")]
        [SerializeField] private float _blink = 0.45f;

        [SerializeField] private Font _font;
        [SerializeField] private string _title = "어디로 가시겠소";

        private bool _unlocked;
        private bool _open;
        private Canvas _canvas;
        private CanvasGroup _group;
        private WorldHudAnchor _anchor;
        private RectTransform _row;
        private readonly List<GameObject> _chips = new List<GameObject>();

        /// <summary>서리가 길을 다 일러 준 뒤에 부른다.</summary>
        public void Unlock()
        {
            if (_unlocked) return;
            _unlocked = true;
            SubtitleView.Show("", "이제 *지도*(" + _key + ")로 관아 안을 오갈 수 있다.", "", true);
        }

        /// <summary>다시 잠근다.</summary>
        public void Lock() { _unlocked = false; Close(); }

        public void Toggle() { if (_open) Close(); else Open(); }

        public void Open()
        {
            if (!_unlocked) return;
            if (PlayerSeat.Instance != null && PlayerSeat.Instance.Seated)
            {
                SubtitleView.Show("", "앉은 채로는 못 간다. *일어서야* 한다.", "", false);
                return;
            }
            _open = true;
            Build();
            SetVisible(true);
            if (_anchor != null) _anchor.Recenter();
        }

        public void Close() { _open = false; SetVisible(false); }

        private void OnEnable() { Instance = this; _unlocked |= _openAtStart; }
        private void OnDisable() { if (Instance == this) Instance = null; }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (Typing.Now) return;   // 치는 동안에는 지도가 안 펴진다
            var kb = Keyboard.current;
            if (kb != null && _key != Key.None && kb[_key].wasPressedThisFrame) Toggle();
#endif
            // 다른 창이 열리면 접는다 — 겹쳐 뜨면 어느 것을 누르는지 알 수 없다
            if (_open && (JournalView.AnyOpen || InterrogationController.AnyOpen || DocumentView.IsOpen)) Close();
        }

        // ── 옮겨 가기 ──

        private void Go(Stop s)
        {
            if (s == null || s.자리 == null) return;
            Close();

            var cam = Camera.main;
            if (cam == null) return;
            var rig = cam.transform.root;

            // 눈을 감았다 뜬다. 그 사이에 옮긴다 — 화면이 훅 끊기면 옮긴 것이 아니라
            // 튕긴 것으로 보인다.
            ScreenFade.Blink(_blink, _blink * 1.6f, () =>
            {
                // 발이 땅에 닿게 내려놓는다. 표식은 바닥 자리를 가리키므로 눈높이를 얹는다.
                float eye = 1.6f;
                var fly = rig.GetComponentInChildren<DebugFlyCamera>();
                if (fly != null) eye = fly.EyeHeight;

                rig.position = s.자리.position + Vector3.up * eye;
                rig.rotation = Quaternion.Euler(0f, s.자리.eulerAngles.y, 0f);
                Physics.SyncTransforms();
            });
        }

        private bool Here(Stop s)
        {
            var cam = Camera.main;
            if (cam == null || s == null || s.자리 == null) return false;
            return Vector3.Distance(cam.transform.position, s.자리.position) <= s.여기;
        }

        // ── 판 ──

        private void SetVisible(bool on)
        {
            if (_group == null) return;
            _group.alpha = on ? 1f : 0f;
            _group.blocksRaycasts = on;
            _group.interactable = on;
        }

        private void Build()
        {
            if (_canvas == null)
            {
                var go = new GameObject("VR_오갈데", typeof(Canvas));
                _anchor = go.AddComponent<WorldHudAnchor>();
                _anchor.Configure(WorldHudAnchor.Placement.Front);
                _anchor.SetDistance(1.3f, -0.10f);
                _canvas = go.GetComponent<Canvas>();
                _group = go.AddComponent<CanvasGroup>();
                _font = UiFont.Resolve(_font);
                _row = NewRect("줄", new Vector2(0f, -40f), new Vector2(1200f, 110f), go.transform);
                var t = NewText("제목", _title, new Vector2(0f, 70f), new Vector2(1200f, 70f), go.transform, 34);
                t.color = new Color(0.98f, 0.96f, 0.92f);
            }

            foreach (var c in _chips) if (c != null) Destroy(c);
            _chips.Clear();
            if (_stops == null || _stops.Length == 0) return;

            int n = _stops.Length;
            const float gap = 24f;
            float w = Mathf.Min(340f, (1200f - gap * (n - 1)) / n);
            float x0 = -(n * w + gap * (n - 1)) * 0.5f + w * 0.5f;

            for (int i = 0; i < n; i++)
            {
                var s = _stops[i];
                if (s == null) continue;
                bool here = Here(s);

                var rt = NewRect("갈곳" + i, new Vector2(x0 + i * (w + gap), 0f), new Vector2(w, 100f), _row);
                var bg = rt.gameObject.AddComponent<Image>();
                bg.color = here ? new Color(0.09f, 0.09f, 0.09f, 0.5f) : new Color(0.16f, 0.13f, 0.10f, 0.93f);
                var btn = rt.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                var captured = s;
                if (!here) btn.onClick.AddListener(() => Go(captured));
                NewText("라벨", here ? s.이름 + "  (여기)" : s.이름, Vector2.zero, new Vector2(w - 20f, 100f), rt, 30);
                _chips.Add(rt.gameObject);
            }
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

        private Text NewText(string name, string content, Vector2 pos, Vector2 size, Transform parent, int fs)
        {
            var rt = NewRect(name, pos, size, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = _font;
            t.fontSize = fs;
            t.color = new Color(0.98f, 0.96f, 0.92f);
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = content;
            return t;
        }
    }
}
