using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>자는 척한다</b> — 요에 뛰어들어 눕는 자리.
    ///
    /// 잠행에 <b>도망칠 데</b>가 없으면 소리는 그저 벌점이다. 눈금이 차면 사람이 오고,
    /// 오면 끝 — 그 사이에 사람이 할 수 있는 일이 없다. 그러면 조심하는 것이 아니라
    /// <b>가만히 있는 것</b>이 최선이 되고, 그건 조사가 아니다.
    ///
    /// 그래서 자리를 하나 둔다. 밖에서 발소리가 멎으면 <b>요로 뛰어가 눕는다</b>.
    /// 하인이 문 앞까지 와서 들여다보면 손님은 자고 있다 — 아무 일도 없다.
    /// 못 눕고 서 있다가 마주치면 그때 들킨다.
    ///
    /// 이 한 가지로 잠행이 <b>쫓고 쫓기는 일</b>이 된다. 서랍을 여는 것도, 재를 헤집는
    /// 것도, 이제는 "얼마나 빨리 요까지 갈 수 있는가"를 재면서 하는 일이 된다.
    ///
    /// 붙이는 곳: 손님에게 내준 요(콜라이더 있는 것).
    /// </summary>
    public class HidePlace : MonoBehaviour, IInspectable, ISelectable
    {
        [Header("글")]
        [SerializeField] private string _title = "잘자리";
        [TextArea(2, 3)]
        [SerializeField] private string _body = "손님에게 내준 자리다. 요와 베개가 펴져 있다.";
        [SerializeField] private string _hintLie = "(누워 자는 척한다)";
        [SerializeField] private string _hintUp = "(일어난다)";

        [Header("일어나기")]
#if ENABLE_INPUT_SYSTEM
        [Tooltip("누운 채로 이 키를 누르면 일어난다. " +
                 "<b>왜 키가 따로 있어야 하나</b>: 누우면 눈이 <b>천장</b>을 본다. " +
                 "그 자리에서 요를 다시 누르려면 고개를 숙여 제 몸을 짚어야 하는데, " +
                 "그런 것이 될 리가 없다 — 누운 채로 갇혀 아무것도 못 하게 된다")]
        [SerializeField] private UnityEngine.InputSystem.Key _riseKey = UnityEngine.InputSystem.Key.Space;
#endif

        [Header("눕고 일어나기")]
        [Tooltip("이 거리(m) 안에서만 누울 수 있다")]
        [SerializeField] private float _maxTouchDistance = 3f;
        [Tooltip("누웠을 때 눈높이(요 위에서 m). 베개를 벤 만큼")]
        [SerializeField] private float _lieEyeHeight = 0.22f;
        [Tooltip("누우면서 천장 쪽으로 이만큼 젖힌다(도)")]
        [SerializeField] private float _lookUp = 52f;
        [Tooltip("눕는 데 걸리는 시간(초). <b>짧아야 한다</b> — 쫓겨 뛰어드는 것이다")]
        [SerializeField] private float _lieSeconds = 0.55f;
        [Tooltip("일어나는 데 걸리는 시간(초)")]
        [SerializeField] private float _riseSeconds = 0.5f;

        [Header("누운 동안")]
        [TextArea(2, 3)]
        [Tooltip("누울 때 한 마디. 비우면 아무 말도 없다")]
        [SerializeField] private string _lieLine = "요에 몸을 뉜다. *숨을 죽인다*.";

        [Header("여기서 밤을 끝낸다")]
        [Tooltip("누워 있는 동안 「출도」 단추를 띄운다. 누르면 한 번 더 묻고, 두 번째에 마패를 내보인다(RoyalWarrant)")]
        [SerializeField] private bool _offerReveal = true;
        [Tooltip("누워 있는 동안 소리 달아오름을 이만큼 빨리 식힌다(배수). " +
                 "가만히 누워 있으면 집도 곧 조용해진다")]
        [Range(1f, 8f)] [SerializeField] private float _coolFaster = 3f;

        [Header("이대로 밤을 보내기")]
        [Tooltip("누운 채로 이만큼(초) 있으면 <b>밤을 보낸 것</b>으로 친다. 0이면 안 쓴다 — " +
                 "잠행을 끝내고 다음 마디로 넘어가는 문이 필요하면 여기에 잇는다")]
        [SerializeField] private float _sleepAfterSeconds = 0f;
        [SerializeField] private UnityEvent _onSleptThrough;

        /// <summary>지금 누워 자는 척하고 있나. 하인이 이것을 본다.</summary>
        public static bool Hiding { get; private set; }

        /// <summary>지금 눕거나 일어나는 중. 그 사이에는 다시 못 누른다.</summary>
        private bool _moving;

        private Vector3 _stoodAt;
        private Quaternion _stoodRot;
        private float _lyingSince;

        public string GetInspectTitle() => _title;
        public string GetInspectBody() => _body + "\n" + (Hiding ? _hintUp : _hintLie);
        public void OnInspected() { }
        public void OnHoverEnter() { }
        public void OnHoverExit() { }

        public void OnSelect()
        {
            if (_moving) return;
            var cam = Camera.main;
            if (cam == null) return;

            if (Hiding) { StartCoroutine(Rise(cam.transform)); return; }

            if (_maxTouchDistance > 0f &&
                ModelBounds.DistanceTo(transform, cam.transform.position) > _maxTouchDistance) return;
            StartCoroutine(Lie(cam.transform));
        }

        private IEnumerator Lie(Transform eye)
        {
            _moving = true;
            _stoodAt = eye.position;
            _stoodRot = eye.rotation;

            // 누운 채로 걸어 다닐 수는 없다
            var fly = eye.GetComponent<DebugFlyCamera>();
            if (fly != null) fly.enabled = false;

            Vector3 lie = ModelBounds.TryGet(transform, out var b)
                        ? new Vector3(b.center.x, b.max.y + _lieEyeHeight, b.center.z)
                        : transform.position + Vector3.up * _lieEyeHeight;
            Quaternion lieRot = Quaternion.Euler(-_lookUp, _stoodRot.eulerAngles.y, 0f);

            yield return Ease(_lieSeconds, t =>
            {
                eye.position = Vector3.Lerp(_stoodAt, lie, t);
                eye.rotation = Quaternion.Slerp(_stoodRot, lieRot, t);
            });

            Hiding = true;
            _lyingSince = Time.time;
            _moving = false;
            if (!string.IsNullOrEmpty(_lieLine)) SubtitleView.Show("", _lieLine, "");
        }

        /// <summary>
        /// 밖에서 일어서라 이를 때.
        /// </summary>
        public void RiseNow()
        {
            if (_moving) return;
            var cam = Camera.main;
            if (cam != null) StartCoroutine(Rise(cam.transform));
        }

        private IEnumerator Rise(Transform eye)
        {
            _moving = true;
            Hiding = false;
            _told = false;
            StatusPanel.Clear("눕기");
            Vector3 from = eye.position;
            Quaternion fromRot = eye.rotation;
            Quaternion upRot = Quaternion.Euler(0f, _stoodRot.eulerAngles.y, 0f);

            yield return Ease(_riseSeconds, t =>
            {
                eye.position = Vector3.Lerp(from, _stoodAt, t);
                eye.rotation = Quaternion.Slerp(fromRot, upRot, t);
            });

            var fly = eye.GetComponent<DebugFlyCamera>();
            if (fly != null) { fly.enabled = true; fly.SyncAngles(); fly.StandOnGround(); }
            _moving = false;
        }

        private void Update()
        {
            ShowRevealButton(Hiding && !_moving && _offerReveal && NoiseMeter.Armed
                             && RoyalWarrant.Instance != null && RoyalWarrant.Instance.CanRevealNow);
            if (!Hiding)
            {
                if (_told) { _told = false; StatusPanel.Clear("눕기"); }
                return;
            }

            // <b>일어나는 법을 알려 준다.</b> 여태 아무 데도 안 적혀 있어서, 한 번 누우면
            // 어떻게 일어나는지 알 길이 없었다. 누운 사람에게 가장 급한 글이 이것이다.
            if (!_told)
            {
                _told = true;
                // <b>같은 말을 두 판에 적지 않는다.</b>
                //
                // 여기 「요에 누워 숨을 죽인다」라고 적어 두었더니, 자막의
                // <see cref="_lieLine"/>(「요에 몸을 뉜다. 숨을 죽인다.」)와 <b>거의 같은
                // 문장</b>이 위아래로 겹쳐 떴다. 그래서 자막의 「×」를 눌러 하나를 닫아도
                // 남은 줄이 같은 말을 하고 있어, <b>눌러도 안 닫히는 것처럼</b> 보였다.
                // 실제로 그렇게 보고되었다 — 「엑스표시 안 눌림」.
                //
                // 이 줄이 할 일은 <b>일어나는 법</b> 하나다. 그것은 자막이 못 하는 말이다.
                StatusPanel.Set("눕기", 3, "*" + RiseKeyName() + "* — 일어난다");
            }

#if ENABLE_INPUT_SYSTEM
            var kb = Typing.Now ? null : UnityEngine.InputSystem.Keyboard.current;   // 치는 동안엔 안 일어난다
            var rk = kb != null ? kb[_riseKey] : null;
            if (!_moving && rk != null && rk.wasPressedThisFrame)
            {
                var cam = Camera.main;
                if (cam != null) { StartCoroutine(Rise(cam.transform)); return; }
            }
#endif


            // 가만히 누워 있으면 집이 도로 조용해진다
            if (_coolFaster > 1f) NoiseMeter.Cool((_coolFaster - 1f) * Time.deltaTime * 0.2f);

            if (_sleepAfterSeconds > 0f && Time.time - _lyingSince >= _sleepAfterSeconds)
            {
                _sleepAfterSeconds = 0f;      // 한 번만
                _onSleptThrough?.Invoke();
            }
        }

        private bool _told;

        // ── 「출도」 단추 ─────────────────────────
        //
        // 필수 단서를 다 모아야 마패를 꺼낼 수 있던 길을 걷고, <b>요에 누웠을 때</b> 여기서
        // 밤을 끝낼지 고르게 했다. 누운 자리는 이미 몸을 숨기고 한숨 돌리는 자리라, 조사를
        // 여기서 멈추겠다는 결정을 내리기에 알맞다. 되돌릴 수 없는 일이라 두 번 누르게 한다.

        private GameObject _revealRoot;
        private TMPro.TextMeshProUGUI _revealLabel;
        private float _armedUntil;
        private const float ConfirmWindow = 4f;
        private const string AskLabel = "출도(出道)하기";
        private const string SureLabel = "정말 출도한다 — 되돌릴 수 없다";
        private readonly IMUNROK.Ui.InventorySkin _skin = new IMUNROK.Ui.InventorySkin();

        private void ShowRevealButton(bool on)
        {
            if (!on)
            {
                if (_revealRoot != null && _revealRoot.activeSelf) _revealRoot.SetActive(false);
                _armedUntil = 0f;
                return;
            }
            if (_revealRoot == null) BuildRevealButton();
            if (!_revealRoot.activeSelf) { _revealRoot.SetActive(true); _armedUntil = 0f; }
            bool asking = Time.unscaledTime < _armedUntil;
            string want = asking ? SureLabel : AskLabel;
            if (_revealLabel.text != want) _revealLabel.text = want;
        }

        private void BuildRevealButton()
        {
            _revealRoot = new GameObject("출도_단추판", typeof(RectTransform));
            ScreenPanel.Raise(_revealRoot, ScreenPanel.LayerBar + 5);
            var btnRt = new GameObject("출도", typeof(RectTransform)).GetComponent<RectTransform>();
            btnRt.SetParent(_revealRoot.transform, false);
            btnRt.anchorMin = btnRt.anchorMax = new Vector2(0.5f, 0.5f);
            btnRt.pivot = new Vector2(0.5f, 0.5f);
            btnRt.anchoredPosition = new Vector2(0f, -120f);
            btnRt.sizeDelta = new Vector2(560f, 92f);
            var bg = btnRt.gameObject.AddComponent<UnityEngine.UI.Image>();
            bg.sprite = _skin.Wood_;
            bg.color = UiLook.With(UiLook.Seal, 0.94f);
            var btn = btnRt.gameObject.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(OnRevealPressed);
            _revealLabel = UiText.New("글", AskLabel, Vector2.zero, btnRt.sizeDelta, btnRt, 40f,
                                      TextAnchor.MiddleCenter, UiLook.SealText);
            _revealLabel.raycastTarget = false;
            _revealRoot.SetActive(false);
        }

        private void OnRevealPressed()
        {
            var warrant = RoyalWarrant.Instance;
            if (warrant == null || !warrant.CanRevealNow) return;
            if (Time.unscaledTime >= _armedUntil)
            {
                _armedUntil = Time.unscaledTime + ConfirmWindow;
                string miss = RoyalWarrant.MissingNote != null ? RoyalWarrant.MissingNote() : null;
                SubtitleView.Show("", "여기서 밤을 끝내고 *출도*하면 옹씨댁은 더 뒤질 수 없다. 한 번 더 누르면 마패를 내보인다." +
                                      (string.IsNullOrEmpty(miss) ? "" : System.Environment.NewLine + miss), "");
                return;
            }
            _armedUntil = 0f;
            ShowRevealButton(false);
            warrant.RevealNow();
        }

        private void OnDestroy() { if (_revealRoot != null) Destroy(_revealRoot); }

        private string RiseKeyName()
        {
#if ENABLE_INPUT_SYSTEM
            return _riseKey == UnityEngine.InputSystem.Key.Space ? "Space" : _riseKey.ToString();
#else
            return "Space";
#endif
        }

        private static IEnumerator Ease(float seconds, System.Action<float> step)
        {
            if (seconds <= 0f) { step(1f); yield break; }
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / seconds;
                step(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
                yield return null;
            }
            step(1f);
        }
    }
}
