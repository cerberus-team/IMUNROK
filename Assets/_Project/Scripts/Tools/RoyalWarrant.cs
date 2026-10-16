using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>마패(馬牌)</b> — 1막과 2막을 잇는 <b>단 하나의 물건</b>.
    ///
    /// 다른 도구는 무엇을 <b>알아내는</b> 데 쓴다. 등불은 비추고 돋보기는 키우고 유척은
    /// 잰다. 마패만은 아무것도 알아내지 못한다 — 마패가 하는 일은 <b>내가 누구인지를
    /// 밝히는 것</b> 하나뿐이고, 그 한 번으로 잠행이 끝난다.
    ///
    /// <b>그래서 도구가 아니라 문(門)이다.</b>
    ///
    /// ⚠️ <b>이것이 막는 것은 출도뿐이다.</b> 등불도 돋보기도 유척도 처음부터 언제든 쓴다 —
    /// 조사를 막는 자물쇠가 아니다. 그런데 「아직 이르다, 품에 든 것을 꺼내기에는」이라고만
    /// 적어 두었더니 <b>도구가 잠긴 줄로 읽혔다</b>. 말이 무엇을 막는지 밝히지 않으면,
    /// 사람은 자기가 지금 못 하는 일 가운데 <b>가장 답답한 것</b>을 그 말에 갖다 붙인다.
    ///
    /// <b>왜 도구벨트에서 뺐나</b>: 한동안 마패가 벨트 네 번째 칸에 걸려 있었다.
    /// 그러면 두 가지가 한꺼번에 망가진다.
    ///
    /// 하나, 어사가 제 표식을 <b>허리에 차고</b> 남의 집 마당을 걸어 들어가는 꼴이 된다.
    /// 조선의 어사는 마패와 유척을 봉서와 함께 <b>품에 넣고</b> 다녔고 출도 전에는
    /// 아무에게도 보이지 않았다. 그것이 잠행이다.
    ///
    /// 둘, 마패가 등불·돋보기와 <b>같은 급의 물건</b>이 된다. Q 를 몇 번 돌리면 나오는
    /// 것은 문이 아니라 도구다.
    ///
    /// 그래서 이것은 끝까지 <b>도구 목록에 없다</b>. 있다는 것은 아는데 꺼낼 수가 없다 —
    /// 그 답답함이 1막 내내 깔려 있다가 출도에서 한 번에 풀린다.
    ///
    /// <b>맨손일 때만 나온다.</b> F 는 「들어 올린다」는 하나의 손짓이라 돋보기·등불과
    /// 나눠 쓰는데, 손에 무언가 들려 있으면 그 F 는 <b>그 도구의 것</b>이다.
    /// 마패는 품에서 나오는 것이므로 손이 비어 있어야 한다.
    ///
    /// <b>꺼내는 느낌</b>: 톡 눌러 되돌릴 수 없는 일이 벌어지면 안 되므로 <b>꾹</b>
    /// 눌러야 한다. 누르고 있는 동안 패가 품에서 <b>천천히 올라온다</b>. 손을 떼면
    /// 도로 들어간다 — 그 되돌아가는 동작이 있어야 "아직 안 늦었다"가 몸으로 읽힌다.
    ///
    /// <b>낼 것이 없으면 안 나간다</b>: 증거 없이 출도한 어사는 무고한 이를 잡아들인
    /// 것이 된다. 아직 못 챙긴 것이 있으면 패는 <b>품에서 나오지도 않고</b>, 화면에도
    /// 아무 말이 없다 — 때가 되면 품이 무거워지는 것으로 알린다(아래 긴 주석 참고).
    ///
    /// 붙이는 곳: 카메라 밑의 마패 자리(<see cref="_model"/> 을 자식으로 둔 빈 오브젝트).
    /// </summary>
    public class RoyalWarrant : MonoBehaviour
    {
        /// <summary>
        /// <b>2막에서는 이 패가 도구벨트에 오른다.</b> 출도가 이미 끝났으므로 품에 감출
        /// 까닭이 없고, 서리 앞에 내보이는 물건이 되어 있다.
        ///
        /// 그런데 벨트에 오르는 순간 <b>같은 모형을 두 부품이 다툰다</b> —
        /// <see cref="HeldToolModel"/> 이 켜 놓으면 이쪽이 그 다음 칸에 도로 끈다.
        /// 그래서 벨트에서 마패를 골라도 손에 아무것도 안 들렸다. 오류도 안 났다.
        ///
        /// 벨트가 들고 있는 동안에는 <b>손을 뗀다</b>. 품에서 꺼내는 일(1막의 출도)은
        /// 벨트에 마패가 없을 때의 일이다.
        /// </summary>
        private static bool OnTheBelt { get { return ToolbeltHud.SelectedToolId == "mapae"; } }

        /// <summary>상태 줄에 쓰는 이름표. 다른 안내와 안 겹치게 한 군데에 적어 둔다.</summary>
        private const string HintKey = "마패";

        // <b>언제 마패를 꺼내는지 어떻게 알리나.</b>
        //
        // 이 물음에 여태 세 가지 답을 냈고 앞의 둘은 다 물렸다.
        //
        //   ① <b>누르면 물린다</b> — 띠가 「[F] 를 꾹 누르면 꺼낸다」고 권하고, 누르면
        //      마패가 「아직 이르다」고 막았다. 권한 입과 물린 입이 달라 고장으로 읽힌다.
        //   ② <b>남은 수를 적어 둔다</b> — 띠에 「밝혀 둘 것이 4가지 남았다」를 내내
        //      띄웠다. 물리지는 않지만 이번엔 사건이 <b>모으기 놀이</b>가 된다.
        //      어사는 넷을 채우러 다니는 사람이 아니고, 무엇보다 그 넷이 무엇인지
        //      모르는 채 숫자만 줄어드는 것은 안내가 아니라 <b>점수판</b>이다.
        //
        //   ③ <b>때가 되면 품이 무거워진다</b> — 지금 것. 낼 것이 모자란 동안에는
        //      화면에 <b>아무 말도 없다</b>. 마지막 하나를 챙긴 그 순간 한 마디가 뜨고
        //      (<see cref="_readyLine"/>), 그때부터 상태 줄에 꺼내는 법이 <b>머문다</b>
        //      (<see cref="_readyHint"/>). 곧 <b>안내가 뜨는 것 자체가 신호</b>다 —
        //      셀 것도 없고, 물릴 일도 없다.
        //
        // 어사가 품속 것의 무게로 때를 안다는 것은 이 놀이의 말투이기도 하다.

        [Header("무엇을 꺼내나")]
        [Tooltip("품에서 꺼낼 패의 모형. 평소에는 꺼져 있다")]
        [SerializeField] private GameObject _model;
        [Tooltip("다 꺼냈을 때 모형의 기울기(도). 새겨진 말이 이쪽을 보게 돌려 둔 값")]
        [SerializeField] private Vector3 _faceEuler = new Vector3(90f, 0f, 0f);

        [Header("품속 — 아직 안 꺼낸 자리")]
        [Tooltip("품에 있을 때 모형이 놓이는 자리(다 꺼낸 자리에서 얼마나 떨어져 있나, m). " +
                 "아래로 내려가 있어야 <b>품에서 올라오는</b> 것으로 보인다")]
        [SerializeField] private Vector3 _tuckedOffset = new Vector3(-0.03f, -0.30f, -0.06f);
        [Tooltip("품에 있을 때 더 얹히는 기울기(도). 눕혀져 있다가 서면서 낯짝이 드러난다")]
        [SerializeField] private Vector3 _tuckedTilt = new Vector3(-62f, 0f, 34f);

        [Header("꺼내기")]
#if ENABLE_INPUT_SYSTEM
        [Tooltip("이 키를 <b>꾹 눌러</b> 꺼낸다")]
        [SerializeField] private UnityEngine.InputSystem.Key _raiseKey = UnityEngine.InputSystem.Key.F;
#endif
        [Tooltip("이만큼(초) 잡고 있어야 다 꺼낸 것으로 친다. 팔을 품에 넣었다 빼는 시간이다")]
        [SerializeField] private float _holdSeconds = 1.6f;
        [Tooltip("손을 떼면 이 배로 빨리 도로 들어간다. 넣는 것은 꺼내는 것보다 급하다")]
        [Range(1f, 5f)] [SerializeField] private float _tuckBackFaster = 2.2f;

        [Header("어디서 내보이나")]
        [Tooltip("<b>요에 누웠을 때 뜨는 「출도」 단추로만</b> 내보인다(2026-09-13).\n" +
                 "필수 단서 넷을 채워야 품이 무거워지던 길은 걷었다 — 넷을 다 못 찾은 사람은 " +
                 "들키는 것 말고는 밤을 끝낼 길이 없었다. 어디서나 F 로 꺼내는 길도 막는다: " +
                 "F 는 도구를 드는 글쇠라 되돌릴 수 없는 출도가 손끝에 걸려 있으면 안 된다")]
        [SerializeField] private bool _fromBedOnly = true;

        /// <summary>씬에 하나 — 요의 단추가 부른다.</summary>
        public static RoyalWarrant Instance { get; private set; }

        /// <summary>사건이 채워 주는 「아직 못 챙긴 것」 한 줄(없으면 null). 요의 출도 확인이 덧붙인다.</summary>
        public static System.Func<string> MissingNote;

        private void OnEnable() { Instance = this; }

        /// <summary>지금 내보일 수 있나(이미 드러났거나 내보이는 중이면 아니다).</summary>
        public bool CanRevealNow
        {
            get
            {
                if (_done) return false;
                var gs = GameState.Instance;
                return gs == null || !gs.IdentityRevealed;
            }
        }

        /// <summary><b>곧바로 내보인다</b> — 요의 「출도」 단추. 되돌릴 수 없다.</summary>
        public void RevealNow()
        {
            if (!CanRevealNow) return;
            if (_model != null) _model.SetActive(true);
            StartCoroutine(Cry());
        }

        [Header("낼 것이 있어야 한다")]
        [Tooltip("이 사건. 필수 단서를 다 챙겼는지 여기에 물어본다")]
        [SerializeField] private CaseId _case = CaseId.Case1_Onggojip;
        [Tooltip("이 단서들을 다 가져야 출도할 수 있다. <b>비우면 언제든 나간다</b> — " +
                 "비워 두면 잠행이 시작하자마자 끝날 수 있으니 반드시 채운다")]
        [SerializeField] private string[] _required;

        [Header("말")]
        [SerializeField] private string _speaker = "";
        // <b>「아직 이르다」는 걷었다.</b> 거절문이 남아 있으면 다음에 고치는 사람이
        // 반드시 그쪽도 손보게 된다.
        [TextArea(2, 3)]
        [Tooltip("다 채운 <b>그 순간</b> 한 번. 이것이 곧 '이제 나가도 된다'는 신호다")]
        [SerializeField] private string _readyLine = "품 속의 것이 *무거워진다*. ({0} 꾹 눌러 내보이기)";
        [TextArea(2, 3)]
        [Tooltip("다 채운 뒤 상태 줄에 <b>머무는</b> 한 줄. {0} 자리에 글쇠 이름이 들어간다.\n" +
                 "한 번 스치고 마는 말로 두면 마침 딴 데를 보던 사람은 못 읽고, 그 뒤로 " +
                 "다시 볼 길이 없다 — 출도는 이 놀이에서 되돌릴 수 없는 단 하나의 손짓이다")]
        [SerializeField] private string _readyHint = "품 속의 *마패*가 무거워졌다 — *{0}* 를 꾹 누르면 *출도(出道)*";
        [TextArea(2, 3)]
        [Tooltip("꺼내다가 손을 뗐을 때 한 번. 되돌릴 수 있다는 것을 알려 준다")]
        [SerializeField] private string _tuckLine = "패를 도로 품에 넣는다.";
        [TextArea(2, 3)]
        [SerializeField] private string _cryLine = "암행어사 *출도(出道)*야!";

        [Header("내보인 뒤")]
        [Tooltip("외치고 나서 이만큼(초) 뒤에 넘어간다")]
        [SerializeField] private float _beforeLeave = 2.2f;
        [Tooltip("넘어갈 씬. 비우면 씬은 그대로 두고 이벤트만 부른다")]
        [SerializeField] private string _nextScene = "";
        [Tooltip("내보인 순간 한 번. 씬을 안 넘기고 같은 씬에서 이어갈 때 쓴다")]
        [SerializeField] private UnityEvent _onRevealed;

        /// <summary>낼 것을 다 챙겼나.</summary>
        public bool Ready => Missing() == 0;

        /// <summary>지금 패가 얼마나 나와 있나(0 = 품속, 1 = 다 꺼냄).</summary>
        public float Drawn { get; private set; }

        private bool _done;
        private bool _toldReady;
        private bool _wasDrawing;
        private Vector3 _outPos;

        /// <summary>아직 모자란 필수 단서 수.</summary>
        public int Missing()
        {
            if (_required == null || _required.Length == 0) return 0;
            var j = Journal.Instance;
            if (j == null) return _required.Length;
            int n = 0;
            foreach (var k in _required)
                if (!string.IsNullOrEmpty(k) && !j.HasClue(_case, k)) n++;
            return n;
        }

        private void Awake()
        {
            // 다 꺼냈을 때의 자리는 <b>공통</b>에서 받는다(HeldRig). 도구벨트에는 없지만
            // 손에 드는 자리는 다른 도구와 한 군데에서 정해져야 몸이 어긋나지 않는다.
            HeldRig.Apply(transform, "mapae", _model != null ? _model.transform : null);
            _outPos = _model != null ? _model.transform.localPosition : Vector3.zero;
            Place(0f);
            if (_model != null) _model.SetActive(false);
        }

        private void Update()
        {
            // 벨트가 들고 있으면 <b>다 꺼낸 자리에 그대로 둔다</b>.
            // 그냥 손만 떼면 모형이 품속 자리(-30cm)에 남아 눈 밑으로 내려가 버린다 —
            // 켜지긴 했는데 화면 밖에 있어, 골라도 안 보이는 것은 매한가지가 된다.
            //
            // <b>이 가지가 맨 앞에 와야 한다.</b> 아래의 「신분이 드러났으면 할 일 없다」
            // 보다 뒤에 두었더니 2막에서 여기까지 오지를 못했다 — 2막은 들어서는 순간
            // 신분이 드러난 상태이므로 늘 그 줄에서 되돌아갔고, 그러면 Awake 가
            // Place(0f) 로 품에 넣어 둔 자리가 그대로 남는다. 벨트에서 마패를 골라도
            // HeldToolModel 이 켜기만 할 뿐 몸은 눈에서 33cm 아래에 있어 <b>안 보인다</b>.
            // 마패가 2막부터 도구띠에 늘 있어야 하는 물건이니, 벨트가 먼저다.
            if (OnTheBelt)
            {
                StatusPanel.Clear(HintKey);
                if (_model != null)
                {
                    if (!_model.activeSelf) _model.SetActive(true);
                    Place(1f);
                }
                return;
            }

            // <b>한 번 출도했으면 다시 없다.</b> 관아 씬에도 이 부품이 남아 있어, 맨손으로
            // F 를 꾹 누르면 「암행어사 출도야」가 다시 울리고 관아가 통째로 다시 열렸다 —
            // 소매·유척·문서궤 진행이 그 자리에서 날아간다. 신분이 이미 드러났으면 품 속
            // 마패는 할 일이 없다. 다만 <b>내보이는 마패는 위에서 이미 걸러 냈다</b> —
            // 같은 모형을 도구띠가 쓰므로, 여기서 먼저 물러나면 그 몸이 품속 자리에 굳는다.
            {
                var gs0 = GameState.Instance;
                if (gs0 != null && gs0.IdentityRevealed) { StatusPanel.Clear(HintKey); return; }
            }
            if (_done) { StatusPanel.Clear(HintKey); return; }
            if (_fromBedOnly) { StatusPanel.Clear(HintKey); Move(false); return; }

            bool ready = Missing() == 0;

            // 다 채운 <b>그 순간</b> 한 번만 이른다. 이것이 유일한 신호다 —
            // 벨트에 칸이 늘지도 않고 화면에 아이콘이 뜨지도 않으므로.
            if (ready && !_toldReady)
            {
                _toldReady = true;
                Say(string.Format(_readyLine, KeyName()));
            }

            // <b>안내가 뜨는 것 자체가 신호다.</b> 모자란 동안에는 아무 말도 없다가,
            // 다 채운 그때부터 꺼내는 법이 상태 줄에 머문다. 몇 가지가 남았는지는
            // 끝내 적지 않는다 — 세어 주는 순간 사건이 모으기 놀이가 된다.
            if (ready && !string.IsNullOrEmpty(_readyHint))
                StatusPanel.Set(HintKey, 4, string.Format(_readyHint, KeyName()));
            else StatusPanel.Clear(HintKey);

            // <b>손이 비어 있어야 듣는다.</b>
            //
            // F 는 세 곳이 함께 쓴다 — 돋보기를 눈에 대고(MagnifierLens), 손에 든 것을
            // 들어 올리고(ToolRaise), 마패를 품에서 꺼낸다. 앞의 둘은 <b>손에 무언가
            // 들려 있을 때</b>의 손짓이고 마패는 <b>품</b>에서 나오는 것이므로, 셋이 한
            // 키를 나눠 쓰는 것 자체는 옳다 — 「들어 올린다」는 하나의 손짓이다.
            //
            // 다만 마패가 <b>남의 차례에 말을 얹고 있었다</b>. 돋보기를 눈에 대려고
            // F 를 누르면 마패도 그 F 를 듣고 「아직 이르다」를 뱉었다. 도구를 쓰는데
            // 난데없이 출도 이야기가 뜨니, 도구가 잠긴 줄로 읽힌다.
            //
            // 그러니 <b>맨손일 때만</b> 듣는다. 어사가 돋보기를 눈에 대고 있는 채로
            // 품에서 마패가 올라오는 그림도 말이 안 된다.
            // 치는 동안에도 안 듣는다 — F 는 글자이기도 하다.
            bool emptyHanded = !Typing.Now && string.IsNullOrEmpty(ToolbeltHud.SelectedToolId);

#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var key = emptyHanded && kb != null ? kb[_raiseKey] : null;
            // 톡 누른 것은 이제 안 본다 — 낼 것이 모자랄 때 뱉던 거절문이 없어졌다.
            bool pressing = key != null && key.isPressed;
#else
            bool pressing = false;
#endif

            if (!ready)
            {
                // <b>품에서 나오지도 않고, 아무 말도 안 한다.</b> 왜 안 나오는지는
                // 도구띠의 맨손 줄이 <b>누르기 전부터</b> 적어 두고 있다(BeltLine).
                // 여기서 또 한 마디를 뱉으면 「권했다가 물리는」 그 구조로 되돌아간다.
                Move(false);
                return;
            }

            Move(pressing);
            if (Drawn >= 1f) StartCoroutine(Cry());
        }

        /// <summary>꺼내거나 도로 넣는다. 누르는 동안 올라오고, 떼면 내려간다.</summary>
        private void Move(bool pressing)
        {
            float span = Mathf.Max(0.05f, _holdSeconds);
            float step = Time.deltaTime / span * (pressing ? 1f : -_tuckBackFaster);
            float before = Drawn;
            Drawn = Mathf.Clamp01(Drawn + step);

            if (_model != null && _model.activeSelf != (Drawn > 0.001f))
                _model.SetActive(Drawn > 0.001f);
            Place(Drawn);

            // 꺼내다 만 것을 한 번 알려 준다 — 되돌릴 수 있다는 것이 손끝으로 읽혀야 한다
            bool drawing = pressing && Drawn > 0.02f;
            if (_wasDrawing && !drawing && before > 0.25f) Say(_tuckLine);
            _wasDrawing = drawing;

            if (drawing) Say("패가 품에서 올라온다… " + Mathf.RoundToInt(Drawn * 100f) + "%");
        }

        /// <summary>얼마나 나왔는지에 따라 모형을 앉힌다.</summary>
        private void Place(float k)
        {
            if (_model == null) return;
            float t = Mathf.SmoothStep(0f, 1f, k);
            _model.transform.localPosition = _outPos + _tuckedOffset * (1f - t);
            _model.transform.localRotation =
                Quaternion.Euler(_faceEuler) * Quaternion.Euler(_tuckedTilt * (1f - t));
        }

        /// <summary>
        /// 내보인다 — <b>여기부터 되돌릴 수 없다</b>.
        ///
        /// 외치고, 잠깐 두고, 넘어간다. 곧바로 씬을 갈면 제가 무슨 짓을 했는지 볼 새가 없다.
        /// </summary>
        private IEnumerator Cry()
        {
            _done = true;
            Drawn = 1f;
            Place(1f);
            Say(_cryLine);

            // 이제 소리를 조심할 일이 없다. 어사가 왔는데 발소리를 죽일 까닭이 없다.
            NoiseMeter.Disarm();

            var gs = GameState.Instance;
            if (gs != null) { gs.RevealIdentity(true); gs.SetCaughtInStealth(false); }   // 이제 '어사'다 — 지난 판에 붙들렸던 표시도 걷는다

            _onRevealed?.Invoke();
            yield return new WaitForSeconds(_beforeLeave);

            if (!string.IsNullOrEmpty(_nextScene) &&
                Application.CanStreamedLevelBeLoaded(_nextScene))
            {
                ScreenFade.Blink(0.6f, 0.9f,
                    () => UnityEngine.SceneManagement.SceneManager.LoadScene(_nextScene));
            }
            else
            {
                Debug.LogWarning("[마패] 다음 씬 '" + _nextScene + "' 이 비었거나 빌드 목록에 없다 — 씬은 그대로 두고 이벤트만 불렀다.");
            }
        }

        private string _lastSaid;
        private void Say(string line)
        {
            if (string.IsNullOrEmpty(line) || line == _lastSaid) return;
            _lastSaid = line;
            SubtitleView.Show(_speaker, line, "", Ready && !_done);
        }

        private string KeyName()
        {
#if ENABLE_INPUT_SYSTEM
            return _raiseKey.ToString();
#else
            return "F";
#endif
        }

        private void OnDisable()
        {
            if (Instance == this) Instance = null;
            _lastSaid = null;
            StatusPanel.Clear(HintKey);   // 꺼진 부품이 상태 줄을 붙들고 있으면 안 된다
        }
    }
}
