using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>복명(復命)</b> — 어사가 돌아와 임금께 아뢰고, 어전이 저문다.
    ///
    /// 인트로가 「봉서를 <b>받는</b> 것」이었으니 아웃트로는 「봉서를 <b>돌려드리는</b>
    /// 것」이라야 한다. 그 대구가 이야기를 닫는다. 여태는 왕이 판결을 글자로 읊고
    /// 화면이 툭 꺼졌는데, 그것은 끝나는 것이 아니라 <b>꺼지는</b> 것이었다.
    ///
    /// 다섯 걸음이다.
    ///
    /// <b>① 봉서 셋이 어도를 따라 올라간다</b>
    ///   인트로에서 왕 쪽에서 굴러 <b>내려온</b> 그 셋이, 이번에는 발치에서 왕 쪽으로
    ///   굴러 <b>올라간다</b>. 한 통이 올라갈 때마다 그 사건의 판결이 한 줄 뜬다.
    ///   플레이어가 한 일 셋이 <b>눈에 보이는 물건</b>으로 정리된다 — 글자로 읊는
    ///   것과는 남는 것이 다르다.
    ///
    /// <b>② 발 너머에 그림자가 앉는다</b>
    ///   <b>발은 끝내 안 걷힌다.</b> 대신 뒷불이 한 켜 차오르고, 그제야 발에
    ///   앉은 사람의 그림자가 진다 — 어깨와 익선관의 윤곽만. 그것이 한 번
    ///   고개를 숙였다 든다.
    ///
    ///   앞서 여기서 발을 걷고 <b>빈 어좌</b>를 보였는데, 그것이 틀렸다.
    ///   「왕은 어디에 갔나」라는 물음이 생기고 이야기에는 답할 것이 없다.
    ///   대답할 수 없는 것을 묻게 만드는 연출은 <b>여운이 아니라 구멍</b>이다.
    ///   왕은 거기 계신다 — 다만 끝내 안 보인다. 있는 줄 알면서 못 보는 것이
    ///   없는 것보다 멀다. 그리고 「이야기를 걷어내고 사실만 좇으라」던 목소리의
    ///   임자만은 끝까지 <b>확인되지 않은 채</b> 남는 것이, 이 게임의 태도와 맞는다.
    ///
    /// <b>③ 어사가 일어나 물러난다</b>
    ///   신하는 임금에게 등을 보이지 않고 물러난다. 그 걸음을 그대로 쓴다 —
    ///   꿇었던 몸이 펴지며 눈이 올라가고, 올려다보던 각이 평평해지고, 어전이
    ///   한 칸으로 줄어든다. 「아득하다」가 말이 아니라 <b>거리</b>가 된다.
    ///   멀어지는 것은 왕이 아니라 <b>나</b>다.
    ///
    ///   물러나는 그 순간 <b>하늘을 검정으로 덮는다</b>. 어전 바깥은 아무것도 안
    ///   지어 둔 곳이라, 안 덮으면 흰 하늘이 드러나 어전이 <b>흰 종이 위의 모형
    ///   상자</b>가 된다. 덮으면 어둠 속에 홀로 켜진 한 칸이 된다.
    ///
    /// <b>④ 촛불이 하나씩 꺼진다</b>
    ///   어둠 위로 제목이 뜨고 불이 하나씩 진다. <b>뒷불이 맨 마지막</b>이다 —
    ///   그림자가 마지막으로 사라져야 저무는 것이 된다.
    ///
    /// <b>⑤ 크레딧은 종이 위에</b>
    ///   이 게임의 모든 글은 종이 위에 있었는데 크레딧만 자막 바에 있었다. 그릇이
    ///   틀렸으니 짜쳤던 것이다. 한지 한 장을 어둠에 띄우고 그 위에 적는다.
    ///
    /// 다섯 다 <b>있는 것을 다시 쓰는</b> 일이다 — 새로 지은 것은 이 차례와 발 너머 윤곽뿐이다.
    /// </summary>
    public class OutroCeremony : MonoBehaviour
    {
        [Header("올려 보낼 봉서")]
        [Tooltip("두루마리 프리팹. 비우면 _Project/Prefabs/두루마리 를 찾아 쓴다")]
        [SerializeField] private GameObject _scrollPrefab;

        [Tooltip("봉서가 굴러 나가기 시작하는 자리(어사의 발치). 비우면 카메라 발밑에서 뽑는다")]
        [SerializeField] private Transform _from;

        [Tooltip("봉서가 멎는 자리(어좌 앞). 비우면 어좌에서 뽑는다")]
        [SerializeField] private Transform _to;

        [Tooltip("한 통이 굴러 올라가는 데 걸리는 시간(초)")]
        [SerializeField] private float _rollSeconds = 2.2f;

        [Tooltip("봉서 사이 간격(m). 셋이 나란히 굴러가지 않게 옆으로 벌린다")]
        [SerializeField] private float _spread = 0.55f;

        [Header("발 너머 — 그림자")]
        [Tooltip("발 너머 불이 차올라 윤곽이 또렷해지는 데 걸리는 시간(초)")]
        [SerializeField] private float _shadowSeconds = 2.8f;
        [Tooltip("발 너머 불이 몇 배까지 차오르나. 2.4배(=1.2→2.9)가 잰 값이다. " +
                 "6까지 올려 봤더니 어전이 통째로 환해져 어둠이 거짓말이 됐다")]
        [SerializeField] private float _shadowGain = 2.4f;
        [Tooltip("고개를 숙이는 각(도). 크면 조는 것이 되고 작으면 안 움직인 것이 된다")]
        [SerializeField] private float _nodDegrees = 7f;
        [Tooltip("숙였다 드는 데 걸리는 시간(초)")]
        [SerializeField] private float _nodSeconds = 1.8f;

        [Header("물러나기")]
        [Tooltip("어사가 뒤로 물러나는 거리(m). 0 이면 안 물러난다. " +
                 "어전 앞 문지방이 z -3.5 라 4m 를 넘기면 건물 밖으로 나가 버린다")]
        [SerializeField] private float _recedeBy = 4.0f;
        [Tooltip("물러나며 몸이 펴지는 높이(m). 꿇었던 사람이 일어서는 만큼이다")]
        [SerializeField] private float _recedeRise = 0.67f;
        [Tooltip("물러나는 데 걸리는 시간(초). 걸음이라야 하므로 서두르지 않는다")]
        [SerializeField] private float _recedeSeconds = 7.5f;

        [Header("저물기")]
        [Tooltip("촛불 하나가 지는 데 걸리는 시간(초)")]
        [SerializeField] private float _snuffSeconds = 1.1f;
        [Tooltip("촛불 사이 간격(초)")]
        [SerializeField] private float _snuffGap = 0.8f;

        [Header("제목")]
        [Tooltip("눈앞에 뜰 제목 그림(표제에 쓴 것). 비우면 글씨로 적는다")]
        [SerializeField] private Texture2D _titleImage;
        [SerializeField] private string _titleText = "異 聞 錄";
        [Tooltip("제목이 배어 나오는 데 걸리는 시간(초)")]
        [SerializeField] private float _titleFade = 2.2f;

        // 씬에서 찾아 두는 것들
        private Transform _veil, _rail, _seat, _king, _back;
        private float _backHome;
        private readonly List<Light> _candles = new List<Light>();
        private readonly List<float> _candleHome = new List<float>();
        private GameObject _titleGo;
        private CanvasGroup _titleGroup;

        private void Awake() { Gather(); }

        /// <summary>
        /// 씬에서 쓸 것을 그러모은다. <b>이름으로 찾는다</b> — 어전은 도구가 지은 것이라
        /// 인스펙터로 하나하나 꽂아 두면 다시 지을 때마다 줄이 끊긴다.
        /// </summary>
        private void Gather()
        {
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name == "발") _veil = t;
                else if (t.name == "발장대") _rail = t;
                else if (t.name == "어좌") _seat = t;
                else if (t.name == "왕") _king = t;
                // <b>그림자를 만드는 불</b>이지 밝히는 불이 아니다. 어좌_뒷불은
                // 병풍을 앞에서 비추는 딴 불이라 여기서 찾는 것이 아니다 —
                // 그 둘을 한 불로 묶어 두었더니 그림자가 40배로 부풀었다.
                else if (t.name == "왕_그림자불") { _back = t; }
            }
            if (_back != null)
            {
                var bl = _back.GetComponent<Light>();
                if (bl != null) _backHome = bl.intensity;
            }
            foreach (var li in FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (li.type != LightType.Point) continue;
                // 그림자불은 여기 안 넣는다 — <b>맨 마지막에</b> 따로 끈다.
                // 그림자가 다른 촛불과 같이 사라지면 저무는 차례가 없어진다.
                if (_back != null && li.transform == _back) continue;
                _candles.Add(li);
                _candleHome.Add(li.intensity);
            }
        }

        /// <summary>
        /// 봉서 한 통을 왕에게 올려 보낸다. 왕이 그 사건의 판결을 읊는 그 줄에서 부른다.
        /// </summary>
        /// <param name="which">몇 번째 사건인가(0·1·2) — 옆으로 벌리는 데 쓴다</param>
        public void SendScroll(int which)
        {
            StartCoroutine(RollUp(which));
        }

        private IEnumerator RollUp(int which)
        {
            var prefab = _scrollPrefab;
#if UNITY_EDITOR
            if (prefab == null)
                prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Prefabs/두루마리.prefab");
#endif
            if (prefab == null) yield break;

            var cam = Camera.main;
            Vector3 start = _from != null ? _from.position
                          : (cam != null ? cam.transform.position + cam.transform.forward * 1.1f
                                           - Vector3.up * 0.95f
                                         : Vector3.zero);
            // 멎는 자리는 어좌 <b>앞바닥</b>이다. 어좌의 y 를 그대로 쓰면 앉는 자리
            // 높이라 봉서가 <b>공중에 뜬다</b> — 처음에 그렇게 해서 셋이 어좌에
            // 파묻혀 보이지도 않았다. 바닥을 짚어 그 위에 놓는다.
            // 그리고 <b>발 앞에서 멎는다</b>. 어좌 앞까지 보냈더니 발을 지나쳐 버려
            // 셋 다 발 뒤로 사라졌다 — 굴러가는 것을 보라고 만든 것인데 정작
            // 안 보인다. 어사는 발 앞까지다. 그 너머는 임금의 자리다.
            Vector3 end;
            if (_to != null) end = _to.position;
            else if (_seat != null)
            {
                // <b>발의 부모는 원점에 있다.</b> 살만 앞으로 나가 있어서, 부모의 z 를
                // 쓰면 발이 z 0 에 있는 줄 알고 1.75m 나 앞에서 멎는다. 살을 짚어야 한다 —
                // 발 너머 윤곽을 맞추면서 같은 자리에서 한 번 더 밟은 함정이다.
                float veilZ = _veil == null ? _seat.position.z - 0.70f
                            : (_veil.childCount > 0 ? _veil.GetChild(0).position.z : _veil.position.z);
                float stopZ = veilZ - 0.45f;
                end = new Vector3(_seat.position.x, _seat.position.y, stopZ);
                RaycastHit floor;
                float y = start.y;
                if (Physics.Raycast(end + Vector3.up * 2f, Vector3.down, out floor, 6f,
                                    ~0, QueryTriggerInteraction.Ignore)) y = floor.point.y + 0.05f;
                end.y = y;
            }
            else end = start + Vector3.forward * 4f;

            // 셋이 한 줄로 겹쳐 가지 않게 옆으로 벌린다
            float side = (which - 1) * _spread;
            start.x += side;
            end.x += side * 0.45f;      // 왕 앞에서는 다시 모인다

            var go = Instantiate(prefab, start, Quaternion.Euler(0f, 0f, 90f));
            go.name = "복명_봉서_" + which;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);

            // 굴러가는 각은 간 거리를 둘레로 나눈 값이다 — 그래야 미끄러지지 않는다
            float radius = 0.07f;
            var r = go.GetComponentInChildren<Renderer>(true);
            if (r != null) radius = Mathf.Max(0.02f, r.bounds.size.y * 0.5f);
            float dist = Vector3.Distance(start, end);
            float spin = dist / (2f * Mathf.PI * radius) * 360f;
            var home = go.transform.rotation;

            float t = 0f;
            float dur = Mathf.Max(0.05f, _rollSeconds);
            while (t < 1f)
            {
                t += Time.deltaTime / dur;
                // 굴러 나가 스르르 멎는다
                float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 2.2f);
                go.transform.position = Vector3.Lerp(start, end, e);
                go.transform.rotation = home * Quaternion.AngleAxis(-spin * e, Vector3.right);
                yield return null;
            }
            go.transform.position = end;
        }

        /// <summary>
        /// 발을 걷고, 제목을 띄우고, 불을 하나씩 끈 뒤 부른 쪽에 돌려준다.
        /// </summary>
        public void Close(Action then)
        {
            StartCoroutine(CloseRoutine(then));
        }

        private IEnumerator CloseRoutine(Action then)
        {
            SubtitleView.Hide();

            var back = _back != null ? _back.GetComponent<Light>() : null;

            // ── ② 발 너머에 그림자가 앉는다 ───────────
            //
            // <b>발은 안 걷는다.</b> 뒷불만 한 켜 차오른다 — 그러면 살 틈으로 새던
            // 어둠이 사람 꼴로 모이고, 발에 그 그림자가 진다.
            // 걷어서 보여 주는 것보다 <b>안 걷고 비치게 두는</b> 쪽이 멀다.
            if (back != null && _shadowGain > 1f)
            {
                float g = 0f;
                while (g < 1f)
                {
                    g += Time.deltaTime / Mathf.Max(0.05f, _shadowSeconds);
                    back.intensity = Mathf.Lerp(_backHome, _backHome * _shadowGain,
                                                Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(g)));
                    yield return null;
                }
            }
            yield return new WaitForSeconds(0.9f);

            // 고개를 한 번 숙였다 든다. <b>살아 있다는 표는 이것 하나면 된다</b> —
            // 여기서 손을 들거나 일어서면 그때부터는 <b>안 보이는 사람</b>이 아니라
            // <b>안 보여 주는 사람</b>이 된다.
            if (_king != null && _nodDegrees > 0.1f)
            {
                Quaternion home = _king.localRotation;
                float n = 0f;
                float nd = Mathf.Max(0.1f, _nodSeconds);
                while (n < 1f)
                {
                    n += Time.deltaTime / nd;
                    // 숙였다 드는 한 번 — 0에서 올라갔다 다시 0으로 내려온다
                    float a = Mathf.Sin(Mathf.Clamp01(n) * Mathf.PI);
                    // 앞(발 쪽)은 -z 다. x 를 양수로 돌리면 뒤로 젖혀진다.
                    _king.localRotation = home * Quaternion.Euler(-_nodDegrees * a, 0f, 0f);
                    yield return null;
                }
                _king.localRotation = home;
            }
            yield return new WaitForSeconds(1.1f);

            // ── ③ 어사가 물러난다 ────────────────────
            //
            // 카메라가 아니라 <b>카메라가 달린 몸통</b>을 옮긴다. 걸음이 머리의
            // 자리를 매 프레임 도로 쓰므로, 카메라를 직접 옮겨 봐야 한 프레임
            // 만에 도로 제자리로 간다. 몸통(root)을 옮기면 둘 다 맞는다.
            var cam = Camera.main;
            if (cam != null && _recedeBy > 0.01f)
            {
                Transform body = cam.transform.root;

                // <b>하늘을 끈다.</b> 물러나면 어전의 앞 문지방을 넘게 되는데, 그 바깥은
                // 아무것도 안 지어 둔 곳이라 <b>하얀 하늘</b>이 드러난다 — 재 보니
                // 어전이 흰 종이 위에 놓인 모형 상자처럼 보였다. 검정으로 덮으면
                // 그 순간 어전은 <b>어둠 속에 홀로 켜진 한 칸</b>이 된다. 그게 이 장면이다.
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;

                Vector3 from = body.position;
                Vector3 away = -cam.transform.forward;
                away.y = 0f;                                  // 물러나는 것이지 뜨는 것이 아니다
                if (away.sqrMagnitude < 0.0001f) away = Vector3.back;
                Vector3 to = from + away.normalized * _recedeBy + Vector3.up * _recedeRise;

                // 몸이 펴지면 눈이 올라가고, 올려다보던 각이 저절로 <b>평평해진다</b>.
                // 각을 손으로 적지 않고 <b>어좌를 겨눠</b> 뽑는다 — 어전을 다시 지어
                // 자리가 달라져도 구도가 안 무너진다.
                bool aim = body == cam.transform && _seat != null;
                Quaternion rFrom = body.rotation;
                Quaternion rTo = aim ? Quaternion.LookRotation(
                                          new Vector3(_seat.position.x, _seat.position.y + 0.36f,
                                                      _seat.position.z) - to, Vector3.up)
                                     : rFrom;

                float r = 0f;
                float rd = Mathf.Max(0.1f, _recedeSeconds);
                while (r < 1f)
                {
                    r += Time.deltaTime / rd;
                    // 뗄 때와 멎을 때가 다 느려야 <b>걸음</b>이 된다
                    float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(r));
                    body.position = Vector3.Lerp(from, to, e);
                    if (aim) body.rotation = Quaternion.Slerp(rFrom, rTo, e);
                    yield return null;
                }
            }
            yield return new WaitForSeconds(0.8f);

            // ── ④ 제목이 어둠 위에 뜬다 ───────────────
            BuildTitle();
            float k = 0f;
            while (k < 1f)
            {
                k += Time.deltaTime / Mathf.Max(0.05f, _titleFade);
                if (_titleGroup != null) _titleGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k));
                yield return null;
            }
            yield return new WaitForSeconds(1.2f);

            // ── ⑤ 촛불이 하나씩 진다 ──────────────────
            //
            // <b>뒷불이 맨 마지막</b>이다. 어전의 불이 먼저 지고, 그림자만 남았다가,
            // 그것도 진다. 함께 꺼 버리면 저무는 <b>차례</b>가 없어진다.
            if (back != null) { _candles.Add(back); _candleHome.Add(back.intensity); }

            for (int i = 0; i < _candles.Count; i++)
            {
                if (_candles[i] == null) continue;
                float from = _candleHome[i];
                float s = 0f;
                while (s < 1f)
                {
                    s += Time.deltaTime / Mathf.Max(0.05f, _snuffSeconds);
                    if (_candles[i] != null) _candles[i].intensity = Mathf.Lerp(from, 0f, Mathf.Clamp01(s));
                    // 제목도 함께 스러진다 — 마지막 불과 함께 사라져야 한 몸이 된다
                    if (_titleGroup != null && i == _candles.Count - 1)
                        _titleGroup.alpha = 1f - Mathf.Clamp01(s);
                    yield return null;
                }
                if (i < _candles.Count - 1) yield return new WaitForSeconds(_snuffGap);
            }

            RenderSettings.ambientIntensity = 0f;
            if (_titleGo != null) Destroy(_titleGo);
            yield return new WaitForSeconds(1.0f);

            if (then != null) then();
        }

        /// <summary>
        /// 제목. 월드 캔버스로 세운다 — 화면 붙박이(Overlay)는 이 자리에서
        /// <b>아예 안 보인다</b>.
        ///
        /// <b>카메라에 매단다.</b> 앞서는 어좌 위 한자리에 못 박아 두었는데, 이제는
        /// 어사가 물러나므로 그렇게 두면 제목이 <b>멀어지면서 쪼그라든다</b>.
        /// 눈앞 2.4m 에 매달면 물러나든 말든 크기가 그대로다.
        /// </summary>
        private void BuildTitle()
        {
            var cam = Camera.main;
            if (cam == null) return;

            _titleGo = new GameObject("복명_제목", typeof(Canvas), typeof(CanvasGroup));
            var canvas = _titleGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            _titleGroup = _titleGo.GetComponent<CanvasGroup>();
            _titleGroup.alpha = 0f;

            var rt = canvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(900f, 420f);

            // 눈앞에 매단다. <b>발장대 언저리까지 올린다</b> — 처음에 0.34m 에 매달았더니
            // 하필 왕의 윤곽 위에 정확히 겹쳐서, 애써 세운 그림자를 제목이 통째로
            // 가려 버렸다. 마지막까지 보라는 것은 제목이 아니라 <b>그 윤곽</b>이다.
            rt.SetParent(cam.transform, false);
            rt.localScale = Vector3.one * 0.0016f;
            rt.localPosition = new Vector3(0f, 0.82f, 2.40f);
            rt.localRotation = Quaternion.identity;

            if (_titleImage != null)
            {
                var img = new GameObject("그림", typeof(UnityEngine.UI.RawImage));
                var irt = img.GetComponent<RectTransform>();
                irt.SetParent(rt, false);
                float ar = _titleImage.height / Mathf.Max(1f, (float)_titleImage.width);
                irt.sizeDelta = new Vector2(760f, 760f * ar);
                img.GetComponent<UnityEngine.UI.RawImage>().texture = _titleImage;
            }
            else
            {
                var txtGo = new GameObject("글", typeof(UnityEngine.UI.Text));
                var trt = txtGo.GetComponent<RectTransform>();
                trt.SetParent(rt, false);
                trt.sizeDelta = rt.sizeDelta;
                var txt = txtGo.GetComponent<UnityEngine.UI.Text>();
                txt.font = UiFont.Resolve(null);
                txt.fontSize = 150;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.color = UiLook.Text;
                txt.horizontalOverflow = HorizontalWrapMode.Overflow;
                txt.verticalOverflow = VerticalWrapMode.Overflow;
                txt.text = _titleText;
            }
        }
    }
}
