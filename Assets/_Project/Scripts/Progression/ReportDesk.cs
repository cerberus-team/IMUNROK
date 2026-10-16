using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>장계를 쓰는 자리</b> — 동헌 대청, 층계 머리의 <b>교의(交椅)</b>.
    ///
    /// 앉아야 쓴다. 서서 갈겨쓰는 장계는 없고, 무엇보다 <b>앉는다는 것이 곧 조사가
    /// 끝났다는 뜻</b>이다 — 뜰을 오르내리던 걸음을 멈추고 교의에 앉는 그 한 동작이
    /// 이 사건의 마지막 장면을 연다.
    ///
    /// <b>여기 놓은 것이 세 번 바뀌었다.</b> ① 임시 큐브 다섯 → ② 다리 없이 떠 있던
    /// 경상(經床) → ③ 낙안읍성 평상. 경상은 앉을 데가 아니라 「앉아서 쓴다」와 어긋났고,
    /// 평상은 올라앉을 수는 있으나 <b>판때기가 너무 넓어</b> 대청 한 칸을 먹었다.
    /// 관아 대청에서 수령이 앉는 것은 <b>등받이와 팔걸이가 있는 교의</b>다 —
    /// 그 하나가 자리를 가장 적게 먹고 「여기가 판결하는 자리」라고 가장 잘 말한다.
    ///
    /// <b>층계 머리의 칸에 놓는다.</b> 뜰에서 올라오는 돌층계가 그 칸으로 들므로,
    /// 앉으면 층계 너머로 뜰이 곧게 내다보인다. 불러 세워 물었던 사람들이 섰던
    /// 자리를 마주 보며 글을 쓰게 된다.
    ///
    /// <b>앉는 것은 막지 않는다.</b> 한동안 <see cref="_needsClues"/> 에 G01·G07 을
    /// 걸어 두었는데, 안 캤을 때 <b>아무 말 없이 안 눌릴 뿐</b>이라 고장으로 보였다.
    /// 문지기는 여기가 아니라 <b>봉함</b>에 세운다 — 증좌 줄은 캔 것이 하나도 없으면
    /// 채울 수가 없고, 다 채우기 전에는 마패를 들어도 안 봉해진다
    /// (<see cref="VerdictReport"/>). 무엇을 아직 못 쓰는지는 장계가 제 입으로 말한다.
    ///
    /// 붙이는 곳: 교의 뿌리. <b>콜라이더는 앉을 자리 밑에</b> 둔다 — 자리 밑에 두어야
    /// <see cref="PlayerSeat"/> 의 바닥 재기가 의자를 건너뛴다.
    /// </summary>
    public class ReportDesk : MonoBehaviour, IInspectable, ISelectable
    {
        [Tooltip("여기서 쓸 장계")]
        [SerializeField] private CaseReport _form;

        [Tooltip("이 단서를 다 얻어야 앉을 수 있다. <b>비워 두는 것이 기본</b>이다 — " +
                 "여기서 막으면 안 캔 사람에게는 아무 말 없이 안 눌리는 고장으로 보인다")]
        [SerializeField] private string[] _needsClues = new string[0];

        [Tooltip("이 거리(m) 안에서만 앉을 수 있다")]
        [SerializeField] private float _maxTouchDistance = 2.2f;

        [SerializeField] private string _title = "교의";

        [TextArea(2, 3)] [SerializeField] private string _readyBody =
            "어사가 앉아 판결하는 자리다. 방석 곁에 지필묵이 놓여 있다.";

        [TextArea(2, 3)] [SerializeField] private string _earlyBody =
            "어사가 앉아 판결하는 자리다. 아직 아뢸 것이 여물지 않았다.";

        [SerializeField] private string _readyHint = "(눌러 앉아 장계를 쓰기)";

        [Tooltip("앉을 자리. <b>좌판이 아니라 의자가 선 바닥</b>에 둔다 — PlayerSeat 이 " +
                 "여기 밑의 바닥을 짚고 거기에 앉은 눈높이를 얹는다")]
        [SerializeField] private Transform _seat;

        [Tooltip("앉으면서 <b>바라볼 곳</b>. 비우면 심문 자리가 잡아 둔 쪽(대청 안)을 그대로 본다 — " +
                 "그러면 뜰을 등지고 앉게 되므로, 층계 아래를 가리키는 표식을 넣는다")]
        [SerializeField] private Transform _faceAt;

        private void Awake()
        {
            // 콜라이더는 자식(앉을 자리 밑)에 둔다 — 누름은 <c>GetComponentInParent</c> 로
            // 거슬러 오므로 어디 있든 먹는다. 그러니 <b>자식까지</b> 살펴야 한다.
            if (GetComponentInChildren<Collider>(true) == null)
                Debug.LogWarning($"[{name}] 콜라이더가 없어 눌러도 잡히지 않습니다.", this);
        }

        /// <summary>아뢸 것이 여물었나.</summary>
        public bool Ready
        {
            get
            {
                if (_needsClues == null || _needsClues.Length == 0) return true;
                if (Journal.Instance == null || _form == null) return false;
                foreach (var k in _needsClues)
                    if (!string.IsNullOrEmpty(k) && !Journal.Instance.HasClue(_form.사건, k)) return false;
                return true;
            }
        }

        public string GetInspectTitle() => _title;

        public string GetInspectBody()
            => Ready ? (string.IsNullOrEmpty(_readyHint) ? _readyBody : _readyBody + "\n" + _readyHint)
                     : _earlyBody;

        public void OnInspected() { }

        public void OnHoverEnter() { }
        public void OnHoverExit() { }

        public void OnSelect()
        {
            if (VerdictReport.IsOpen) return;
            var cam = Camera.main;
            if (cam != null && _maxTouchDistance > 0f &&
                ModelBounds.DistanceTo(transform, cam.transform.position) > _maxTouchDistance)
                return;
            if (!Ready) return;
            if (_form == null) { Debug.LogWarning($"[{name}] 쓸 장계가 안 걸려 있습니다.", this); return; }

            // 앉는다. 앉는 일은 이미 있는 부품에 맡긴다 — 신문 자리와 같은 몸짓이라야 한다.
            //
            // 다만 <b>보는 쪽은 다시 잡아 준다</b>. <see cref="PlayerSeat"/> 는 씬에 하나뿐이라
            // 심문 자리가 잡아 둔 「바라볼 것」(대청 안)을 들고 있다. 그대로 앉히면 마지막
            // 장면에서 어사가 <b>뜰을 등지고</b> 벽을 마주 본다.
            if (PlayerSeat.Instance != null && _seat != null)
            {
                if (_faceAt != null) PlayerSeat.Instance.BindSeat(_seat, _faceAt);
                PlayerSeat.Instance.SitAt(_seat);
            }

            VerdictReport.Open(_form, OnReportClosed);
        }

        [Header("봉하고 나서")]
        [Tooltip("장계를 봉하고 판이 닫히면 이 씬(조사청)으로 돌아가 <b>마지막 걸음</b>(HubEnding)을 튼다. " +
                 "비우면 여태처럼 그 자리에 머문다")]
        [SerializeField] private string _afterSealScene = "HubScene";

        /// <summary>
        /// 장계가 닫혔다(봉한 뒤에만 닫힌다). 여태 여기서 아무것도 안 해서, 봉하고 나면
        /// 교의에 앉은 채로 게임이 멎었다 — 끝이 없었다.
        /// </summary>
        private void OnReportClosed()
        {
            var gs = GameState.Instance;
            if (!VerdictReport.ConsumeJustSealed()) return;          // 접기만 한 것이면 그대로 둔다
            if (_form == null || gs == null || gs.GetVerdict(_form.사건) == Verdict.None) return;
            if (string.IsNullOrEmpty(_afterSealScene) || !Application.CanStreamedLevelBeLoaded(_afterSealScene)) return;
            HubEnding.Request();
            gs.EndingPending = true;           // 봉한 직후에 꺼도 이어하면 엔딩을 본다
            if (PlayerSeat.Instance != null) PlayerSeat.Instance.StandNow();
            ScreenFade.Blink(1.6f, 0.1f, () =>
            {
                gs.ExitToHub();
                UnityEngine.SceneManagement.SceneManager.LoadScene(_afterSealScene);
            });
        }

        /// <summary>세우는 도구가 값을 넣어 준다.</summary>
        public void Setup(CaseReport form, Transform seat, string[] needs, Transform faceAt = null)
        {
            _form = form; _seat = seat; _needsClues = needs;
            if (faceAt != null) _faceAt = faceAt;
        }
    }
}
