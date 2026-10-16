using System.Collections;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>걷힌 팔을 눈앞에 두고 유척을 댄다.</b>
    ///
    /// 소매를 걷게 하면(<see cref="SleeveOrder"/>) 이 오브젝트가 켜진다. 여태 켜지는 것은
    /// <b>보이지 않는 상자 하나</b>였다 — 팔 모형이 없으니 무엇을 재는지 보이지 않았고, 유척은
    /// 화면 한가운데로 그 상자를 겨누어야 재어졌다. 「두 치 남짓」이 사건을 끝내는 장면인데
    /// 그 장면이 화면에 없었다.
    ///
    /// 이제 켜지는 순간 팔을 <b>물증 보는 판의 무대</b>(<see cref="EvidenceStage"/>)에 올린다.
    /// 끌어서 돌려 보고, 도구띠에서 유척을 고른 뒤 <see cref="ToolRaise"/>(F)를 누르고 있으면
    /// 놋쇠 자가 팔 안쪽 흉터 위에 놓이고 눈금을 읽는다. 다 읽으면 옆의 <see cref="Measurable"/> 이
    /// 재어 수첩에 적는다(G07 · G08). 판을 닫았다가도 팔을 짚으면 다시 올라온다.
    ///
    /// 甲 과 乙 에 똑같이 붙인다 — 같은 자를 양쪽 팔에 대는 것이 어사의 일이다.
    ///
    /// <b>팔은 못 박아 세운다</b>(2026-10-17). 물증처럼 끌어 돌리게 두었더니 흉터가 뒤로 넘어가고,
    /// 판째 기울어 팔이 납작한 그림 한 장으로 보였다. 이제 정해 둔 자세(<see cref="_stagePose"/>)
    /// 그대로 크게 서고, 할 수 있는 일은 둘뿐이다 — <b>돋보기로 들여다보기 · 유척으로 재기</b>.
    /// 판이 떠 있는 동안 도구띠는 그 둘(과 맨손)로 좁혀진다(<see cref="ToolbeltHud.Only"/>).
    /// </summary>
    [RequireComponent(typeof(Measurable))]
    public class ArmMeasure : MonoBehaviour, ISelectable
    {
        [Header("무대에 올릴 팔")]
        [SerializeField] private GameObject _armPrefab;
        // <b>모형은 ㄴ 자로 굽은 팔이다</b>(2026-10-17, 꼭짓점을 꺼내 재 봤다). 아래팔은 모형의 x 로 눕고
        // 손은 그 끝, 팔꿈치 위 소매 자락은 z 로 선다. 흉터는 −y 면에 그려져 있다.
        // 여태 (−70,0,0) 으로 세웠더니 소매 자락이 <b>카메라를 똑바로 보아</b> 고리 하나로 접혀 보였고,
        // 팔은 곧은 막대처럼 누웠다. (0,0,0) 에 화면 안 180° 면 ㄴ 을 좌우로 뒤집은 꼴 —
        // 소매가 오른쪽에 서고 아래팔이 왼쪽으로 뻗어 손이 왼쪽 끝에 온다. 흉터 면이 눈을 본다.
        //
        // <b>칸 이름을 새로 지었다.</b> 옛 칸(_armEuler · _screenRoll)은 씬의 두 팔에 이미 값이 박혀 있어,
        // 코드의 기본값을 아무리 고쳐도 열려 있는 씬은 옛 자세(−70°)로 팔을 세웠다. 새 이름의 칸은
        // 씬에 적힌 값이 없으므로 아래 기본값이 그대로 든다.
        [Tooltip("무대에 세우는 자세(모형 축 기준 오일러). 0,0,180 = ㄴ 을 좌우로 뒤집은 꼴 — " +
                 "소매가 오른쪽 위로 서고 손이 왼쪽 끝, 흉터 면이 눈을 본다")]
        [SerializeField] private Vector3 _stagePose = new Vector3(0f, 0f, 180f);
        // 아래팔을 길이 축으로 굴리던 칸(_scarToFront · _scarFacing)은 걷었다(2026-10-17).
        // 굴린 까닭은 <b>덧칠한 얼룩</b>을 흉터로 잘못 알고 그것을 앞으로 내려던 것이었다. 진짜 흉터는
        // 아래팔 한가운데를 길게 가로지르는 줄이고, 굴리지 않은 ┘ 자세에서 정면을 본다.
        [SerializeField] private string _title = "왼팔 안쪽";
        [Tooltip("무대에 올렸을 때의 처음 거리(1 = 팔 전체가 꽉 차게). 휠로 더 당기고 민다")]
        [SerializeField] private float _startZoom = 0.62f;

        [Header("유척")]
        [Tooltip("자 모형(유척.fbx). 실제 크기 그대로 둔다 — 한 자가 한 자로 보여야 치수가 믿긴다")]
        [SerializeField] private GameObject _rulerPrefab;
        [SerializeField] private Material _rulerMaterial;
        [Tooltip("팔 프리팹 뿌리 기준으로 자를 놓을 자리(흉터 위)")]
        [SerializeField] private Vector3 _rulerLocalPos = new Vector3(0.09f, 0.145f, 0.01f);
        [SerializeField] private Vector3 _rulerLocalEuler = new Vector3(0f, 0f, 9f);
        [Tooltip("눈금을 읽는 데 드는 시간(초)")]
        [SerializeField] private float _measureSeconds = 1.8f;

        [Header("흉터 자리")]
        // <b>흉터는 모형 위의 선분으로 잡는다</b>(2026-10-17). 처음엔 바탕색 그림에서 붉은 칸을 찾아 그 UV 로
        // 잡았는데, 그 붉은 칸은 <b>예전에 덧칠한 얼룩</b>이었다 — 진짜 흉터(모형에 처음부터 그려 둔 것)가 아니었다.
        // 게다가 이 모형의 그림은 잘게 쪼개져 있어 흉터 하나가 UV 여러 조각에 걸친다.
        // 그래서 게임과 같은 자세로 팔을 그려 흉터 화소를 골라내고, 그 화소들이 모형의 어디에 있는지 되짚어
        // <b>흉터 줄의 양 끝</b>을 적었다. 값은 그물 둘레 상자 기준(0~1)이라 모형의 크기·불러온 배율과 상관없다.
        [Tooltip("흉터 줄의 한 끝 — 팔 그물 둘레 상자 안의 자리(0~1). 손목 쪽")]
        [SerializeField] private Vector3 _scarLineFrom = new Vector3(0.5058f, 0.1522f, 0.2763f);
        [Tooltip("흉터 줄의 다른 끝 — 팔꿈치 쪽")]
        [SerializeField] private Vector3 _scarLineTo = new Vector3(0.3129f, 0.2221f, 0.3226f);
        [Tooltip("흉터로 치는 너비 — 둘레 상자의 가장 긴 변에 대한 몫. 흉터 줄에서 이만큼 안이면 흉터 위다")]
        [SerializeField] private float _scarLineReach = 0.035f;

        [Header("돋보기로 들여다본 것")]
        // <b>돋보기로 본 것은 유척으로 잰 줄에 덧붙는다</b>(2026-10-17). 다른 물증처럼 유리로 끝까지 들여다보면
        // 같은 단서 줄 뒤에 한 줄이 붙는다. 다만 이 줄의 임자(G07·G08)는 <b>유척으로 재야</b> 서는 증좌라,
        // 돋보기로 먼저 봤다고 그 줄을 세워 주지는 않는다 — 본 것을 쥐고 있다가 재는 순간 함께 적는다.
        [Tooltip("흉터가 있는 팔(甲)을 유리로 들여다봤을 때 덧붙는 줄")]
        [TextArea(1, 3)] [SerializeField] private string _lensNoteOnScar = "돋보기로 보니 오래전에 아문 흉터다 — 근래에 낸 상처가 아니다.";
        [Tooltip("흉터가 없는 팔(乙)을 유리로 들여다봤을 때 덧붙는 줄")]
        [TextArea(1, 3)] [SerializeField] private string _lensNoteNoScar = "돋보기로 들여다봐도 왼팔 안쪽에 흉터가 없다.";
        [Tooltip("유리를 대고 있어야 하는 시간(초)")]
        [SerializeField] private float _lensSeconds = 1.2f;

        [Header("수첩")]
        [Tooltip("잰 뒤 수첩 카드에 걸 이름")]
        [SerializeField] private string _clueTitle = "";
        [Tooltip("수첩 카드에 걸 그림(팔을 찍어 둔 것). 모형만 걸면 카드가 빈 칸이 된다")]
        [SerializeField] private Texture2D _clueImage;

        private const string HintKey = "유척";
        private const string Magnify = "magnify";
        private const string Ruler = "yucheok";
        private Measurable _m;
        private bool _open;
        private float _dwell;
        private bool _propShown;
        private bool _lensNoted;        // 돋보기로 다 들여다봤나
        private bool _lensPending;      // 봤는데 아직 덧붙일 줄이 없다(유척으로 재면 그때)
        private float _lensDwell;
        private bool _hasLast;          // 마지막으로 댄 자리 — 다 잰 뒤 자는 거기 남는다
        private Vector3 _lastPos;
        private Quaternion _lastRot;

        private void Awake() { _m = GetComponent<Measurable>(); Register(); }

        // ── 팔 모형의 자세는 이 부품이 정한다 ──
        // 수첩은 잰 순간의 자세를 적어 두고 저장까지 한다. 그래서 자세를 고치기 전에 재 둔 저장이 있으면
        // 수첩에서 「손에 들고 본다」로 꺼낼 때 <b>옛 자세</b>(누운 팔·돌아가는 판·반짝이는 살갗)로 떴다가,
        // 다시 재면 새 자세로 덮여 멀쩡해 보였다(2026-10-17). 이제 무대에 팔을 올리는 길은 어디서 오든
        // 여기 적힌 지금 자세를 쓴다(<see cref="TryPose"/>). 팔 오브젝트는 소매를 걷기 전까지 꺼져 있어
        // Awake 가 안 돌므로, 소매 명령(<see cref="SleeveOrder"/>)이 씬을 켤 때 대신 불러 준다.
        private static readonly System.Collections.Generic.Dictionary<GameObject, Vector3> s_poses
            = new System.Collections.Generic.Dictionary<GameObject, Vector3>();

        /// <summary>이 팔 모형의 지금 자세를 알려 둔다.</summary>
        public void Register()
        {
            if (_armPrefab != null) s_poses[_armPrefab] = Pose;
        }

        /// <summary>이 모형이 걷힌 팔이면 지금 자세를 준다(못 박아 세운다).</summary>
        public static bool TryPose(GameObject prefab, out Vector3 pose)
        {
            pose = default;
            if (prefab == null) return false;
            if (s_poses.TryGetValue(prefab, out pose)) return true;
            // 프리팹 대신 같은 이름의 것이 왔을 때(저장에서 이름으로 되살린 모형)
            foreach (var kv in s_poses)
                if (kv.Key != null && kv.Key.name == prefab.name) { pose = kv.Value; return true; }
            return false;
        }

        /// <summary>
        /// 무대에 세우는 자세 — <see cref="_stagePose"/> 그대로.
        /// 무대의 회전축은 늘 바로 서 있고 카메라가 그 −z 에서 보므로, z 로 앞에 곱하면 그대로 화면 안의 돌림이다.
        /// 수첩에도 이 값을 건다 — 꺼내 보는 자세가 재던 자세와 같아야 한다.
        /// </summary>
        private Vector3 Pose => _stagePose;

        private void OnEnable()
        {
            // 소매가 걷힌 그 칸에는 인물의 대답이 먼저 뜬다 — 한 박자 쉬고 올린다.
            if (Application.isPlaying) StartCoroutine(OpenSoon());
        }

        private IEnumerator OpenSoon()
        {
            yield return new WaitForSeconds(0.6f);
            Open();
        }

        /// <summary>팔을 무대에 올린다.</summary>
        public void Open()
        {
            if (_armPrefab == null) return;
            DocumentView.Show(null, _title, "", null, null, true);
            DocumentView.SetStageModel(_armPrefab, Pose, true);
            EvidenceStage.EnablePicking();   // 커서로 팔 위를 짚어 유척을 댄다
            // 크기는 판이 정한다 — 팔의 긴 쪽이 화면 가로의 육 할쯤(DocumentView.FixedArmShare).
            // 예전의 _startZoom 은 정사각 그림 시절 값이라 이제 안 쓴다.
            ToolbeltHud.Only(Magnify, Ruler);
            _open = true;
            _dwell = 0f;
            _propShown = false;
            _hasLast = false;
        }

        private void OnDisable()
        {
            if (_open) ToolbeltHud.Unlimit();
            _open = false;
            StatusPanel.Clear(HintKey);
        }

        private void Update()
        {
            if (!_open) return;
            if (!DocumentView.IsOpen || !EvidenceStage.HasModel)
            {
                _open = false;
                _propShown = false;
                ToolbeltHud.Unlimit();
                StatusPanel.Clear(HintKey);
                return;
            }

            string held = ToolbeltHud.SelectedToolId;
            bool ruler = held == Ruler;
            bool lens = held == Magnify;
            bool laying = ruler && ToolRaise.Held;

            if (lens) { ShowRuler(false); _dwell = 0f; LookThroughLens(); return; }
            _lensDwell = Mathf.Max(0f, _lensDwell - Time.deltaTime * 2f);

            if (_m.Measured)
            {
                // 잰 뒤에도 유척은 댄 자리에 남는다 — 다만 돋보기로 들여다보는 동안은 걷어 유리를 가리지 않는다
                ShowRuler(!lens && _hasLast);
                if (!lens && _hasLast) EvidenceStage.PlaceProp(_lastPos, _lastRot);
                StatusPanel.Set(HintKey, 1, "▶ " + _m.ResultLine());
                return;
            }

            if (!ruler)
            {
                ShowRuler(false);
                _dwell = 0f;
                StatusPanel.Set(HintKey, 1, "▶ Q 로 *돋보기* 나 *유척* 을 고른다 — 이 팔에는 그 둘만 댈 수 있다");
                return;
            }

            // ── 손에 든 유척 — 커서를 따라 팔 위를 떠다닌다 ──
            if (!AimAtArm(out var hit))
            {
                ShowRuler(false);
                _dwell = Mathf.Max(0f, _dwell - Time.deltaTime * 2f);
                StatusPanel.Set(HintKey, 1, "▶ 유척을 팔 위로 가져간다 — 흉터에 대고 " + Controls.Raise + " 누르고 있는다");
                return;
            }

            bool pressing = ToolRaise.Held;
            ShowRuler(true);
            LayRuler(hit, pressing);

            bool onScar = _m.Bare || OnScar(hit);
            if (!pressing)
            {
                _dwell = Mathf.Max(0f, _dwell - Time.deltaTime * 2f);
                StatusPanel.Set(HintKey, 1, onScar
                    ? "▶ 흉터 위다 — " + Controls.Raise + " 누르고 있으면 유척이 닿아 눈금을 읽는다"
                    : NearScar(hit) ? "▶ 흉터 가까이다 — 조금 더 옮긴다"
                    : "▶ 흉터를 찾아 그 위로 옮긴 뒤 " + Controls.Raise + " 댄다");
                return;
            }
            if (!onScar)
            {
                // <b>한 칸 미끄러졌다고 처음부터 다시 읽게 하지 않는다.</b> 여태 흉터 밖으로 한 프레임만 나가도
                // 0 으로 돌렸더니, 손이 조금만 떨려도 1.8 초가 영영 안 찼다 — 증좌가 아예 안 잡혔던 까닭.
                // 밖에 있는 동안만 천천히 빠진다.
                _dwell = Mathf.Max(0f, _dwell - Time.deltaTime);
                StatusPanel.Set(HintKey, 1, NearScar(hit) ? "▶ 흉터 가까이다 — 조금 더 옮긴다"
                                                          : "▶ 그 자리에는 잴 것이 없다 — 흉터 위에 댄다");
                return;
            }

            _dwell += Time.deltaTime;
            int pct = Mathf.RoundToInt(Mathf.Clamp01(_dwell / Mathf.Max(0.1f, _measureSeconds)) * 100f);
            StatusPanel.Set(HintKey, 1, "▶ 눈금을 읽는 중… " + pct + "%");
            if (_dwell < _measureSeconds) return;

            _m.Measure();
            if (!string.IsNullOrEmpty(_clueTitle) && Journal.Instance != null && !string.IsNullOrEmpty(_m.ClueKey))
            {
                Journal.Instance.SetTitle(_m.Case, _m.ClueKey, _clueTitle);
                if (_clueImage != null) Journal.Instance.AddClue(_m.Case, _m.ClueKey, "", _clueImage);   // 이미 있으면 그림만 건다
                // 수첩에서 다시 돌려 볼 수 있게 팔을 건다.
                Journal.Instance.AttachDocument(_m.Case, _m.ClueKey, null, _clueTitle, "", null, null,
                                                _armPrefab, Pose);
                Journal.Instance.SetDocumentFixed(_m.Case, _m.ClueKey);   // 수첩에서 꺼내도 같은 자세로
            }
            if (_lensPending) { AppendLensNote(); _lensPending = false; }
            SubtitleView.Show("", _m.ResultLine(), "", true);
            // 수첩은 같은 key 를 두 번 안 적는다 — 전에 재 둔 적이 있으면 알림이 안 뜨므로 여기서 직접 띄운다
            ClueToast.Show(!string.IsNullOrEmpty(_clueTitle) ? _clueTitle : _m.ResultLine());
        }

        private string LensNote => _m.Bare ? _lensNoteNoScar : _lensNoteOnScar;   // 칸 이름을 갈았다 — 옛 칸에는 「데인 자국」 문구가 씬에 박혀 있다

        /// <summary>
        /// 돋보기를 든 동안. 유리 한가운데가 흉터 위(흉터가 없는 팔은 팔 어디든)에 머물면 다 들여다본 것으로 친다.
        /// </summary>
        private void LookThroughLens()
        {
            string next = _m.Measured ? "" : " — 재려면 Q 로 *유척*";
            if (_lensNoted)
            {
                StatusPanel.Set(HintKey, 1, "▶ " + LensNote + next);
                return;
            }
            if (!MagnifierLens.Peering)
            {
                _lensDwell = Mathf.Max(0f, _lensDwell - Time.deltaTime * 2f);
                StatusPanel.Set(HintKey, 1, "▶ " + Controls.Raise + " 돋보기로 팔 안쪽을 들여다본다" + next);
                return;
            }
            // 유리는 손에 들려 흔들린다 — 흉터 줄보다 조금 넓게 본다
            bool over = DocumentView.StageViewportAtRay(MagnifierLens.Sight, out var uv)
                        && EvidenceStage.Pick(uv, out var hit)
                        && (_m.Bare || ScarDistance(hit) <= Reach * 1.5f);
            if (!over)
            {
                _lensDwell = Mathf.Max(0f, _lensDwell - Time.deltaTime);
                StatusPanel.Set(HintKey, 1, _m.Bare ? "▶ 유리를 팔 안쪽에 댄다" : "▶ 유리를 흉터 위로 옮긴다");
                return;
            }
            _lensDwell += Time.deltaTime;
            int pct = Mathf.RoundToInt(Mathf.Clamp01(_lensDwell / Mathf.Max(0.1f, _lensSeconds)) * 100f);
            StatusPanel.Set(HintKey, 1, "▶ 들여다보는 중… " + pct + "%");
            if (_lensDwell < _lensSeconds) return;

            _lensNoted = true;
            if (_m.Measured) { AppendLensNote(); ClueToast.Show(LensNote); }
            else { _lensPending = true; ClueToast.Show(LensNote + " (유척으로 재면 함께 적힌다)"); }
        }

        /// <summary>유척으로 잰 줄 뒤에 돋보기로 본 것을 붙인다. 이미 붙어 있으면 그대로 둔다.</summary>
        private void AppendLensNote()
        {
            var j = Journal.Instance;
            string key = _m.ClueKey;
            if (j == null || string.IsNullOrEmpty(key)) return;
            string line = LensNote;
            var all = j.GetClues(_m.Case);
            for (int i = 0; i < all.Count; i++)
            {
                var e = all[i];
                if (e == null || e.key != key) continue;
                if (!string.IsNullOrEmpty(e.text) && e.text.Contains(line)) return;
                j.UpgradeClue(_m.Case, key, (e.text ?? "") + System.Environment.NewLine + System.Environment.NewLine + line);
                return;
            }
        }

        /// <summary>커서가 팔 위에 있나 — 무대 그림 위의 자리를 무대 카메라의 선으로 바꿔 팔을 짚는다.</summary>
        private static bool AimAtArm(out RaycastHit hit)
        {
            hit = default;
#if ENABLE_INPUT_SYSTEM
            var m = UnityEngine.InputSystem.Mouse.current;
            if (m == null) return false;
            return DocumentView.StageViewportAt(m.position.ReadValue(), out var uv) && EvidenceStage.Pick(uv, out hit);
#else
            return false;
#endif
        }

        /// <summary>
        /// 짚은 자리에 자를 눕힌다 — 길이는 아래팔을 따라, 납작한 면은 살갗에 붙게.
        /// 들고만 있을 때는 한 뼘 위에 떠 있고, 누르고 있으면 닿는다.
        /// </summary>
        private void LayRuler(RaycastHit hit, bool pressing)
        {
            // <b>살갗의 법선을 따르지 않는다.</b> 처음엔 맞은 면의 법선대로 자를 눕혔는데, 그물은 잔 삼각형이라
            // 커서가 한 칸만 움직여도 법선이 이리저리 튀어 자가 지직지직 떨었다. 자는 <b>눈을 향해</b> 납작하게
            // 들고(무대 카메라는 +z 를 본다), 길이만 아래팔의 결을 따른다. 자리도 매끄럽게 따라간다.
            Vector3 n = Vector3.back;
            Vector3 along = Vector3.ProjectOnPlane(EvidenceStage.ModelAxis(Vector3.right), n);
            if (along.sqrMagnitude < 1e-6f) along = Vector3.right;
            along.Normalize();
            // 자 모형은 x 가 길이다 — x 를 팔의 결에, y 를 눈 쪽에 둔다
            var rot = Quaternion.LookRotation(Vector3.Cross(along, n), n);
            float lift = EvidenceStage.ModelSize * (pressing ? 0.004f : 0.03f);
            var target = hit.point + n * lift;
            if (!_rulerLive) { _rulerPos = target; _rulerLive = true; }
            float k = 1f - Mathf.Exp(-14f * Time.deltaTime);   // 손이 따라가는 빠르기
            _rulerPos = Vector3.Lerp(_rulerPos, target, k);
            EvidenceStage.PlaceProp(_rulerPos, rot);
            if (pressing) { _lastPos = _rulerPos; _lastRot = rot; _hasLast = true; }
        }

        private bool _rulerLive;     // 자가 지금 팔 위에 떠 있나(처음 뜰 때는 따라가지 않고 그 자리에 선다)
        private Vector3 _rulerPos;

        /// <summary>짚은 자리가 흉터인가 — 맞은 면의 UV 가 흉터 둘레 안이면.</summary>
        /// <summary>
        /// 짚은 자리가 흉터 줄에서 얼마나 떨어졌나 — 둘레 상자 가장 긴 변에 대한 몫(0 = 줄 위).
        /// 그물 안의 자리로 잰다: 맞은 점을 그물이 달린 자리로 되돌려, 둘레 상자 기준의 양 끝과 견준다.
        /// </summary>
        private float ScarDistance(RaycastHit hit)
        {
            var mc = hit.collider as MeshCollider;
            if (mc == null || mc.sharedMesh == null) return float.MaxValue;
            var b = mc.sharedMesh.bounds;
            Vector3 local = mc.transform.InverseTransformPoint(hit.point);
            Vector3 a = b.min + Vector3.Scale(_scarLineFrom, b.size);
            Vector3 c = b.min + Vector3.Scale(_scarLineTo, b.size);
            Vector3 ac = c - a;
            float t = Mathf.Clamp01(Vector3.Dot(local - a, ac) / Mathf.Max(1e-12f, ac.sqrMagnitude));
            float d = Vector3.Distance(local, a + ac * t);
            float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            return d / Mathf.Max(1e-9f, longest);
        }

        /// <summary>흉터로 치는 너비 — 씬에 박힌 옛 값(0.035)이 남아 있어도 0.05 아래로는 안 줄인다.</summary>
        private float Reach => Mathf.Max(_scarLineReach, 0.05f);

        private bool OnScar(RaycastHit hit) => ScarDistance(hit) <= Reach;

        /// <summary>흉터 가까이(너비의 세 배 안)에 있나 — 「거의 다 왔다」를 알려 주려고.</summary>
        private bool NearScar(RaycastHit hit) => ScarDistance(hit) <= Reach * 3f;

        private void ShowRuler(bool on)
        {
            if (!on) _rulerLive = false;
            if (on == _propShown) return;
            _propShown = on;
            if (!on) { EvidenceStage.ClearProp(); return; }
            if (_rulerPrefab == null) return;
            EvidenceStage.ShowProp(_rulerPrefab, _rulerLocalPos, Quaternion.Euler(_rulerLocalEuler), _rulerMaterial);
        }

        // ── 걷힌 팔을 짚으면 다시 올린다 ──
        public void OnHoverEnter() { }
        public void OnHoverExit() { }
        public void OnSelect() { if (!DocumentView.IsOpen) Open(); }
    }
}
