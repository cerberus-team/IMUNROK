using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common
{
    /// <summary>
    /// 사랑채 안팎을 <b>씬째로 갈아 끼운다</b> — 마당에 서면 마당 씬만, 방에 들면 실내 씬만.
    ///
    /// <b>왜 이렇게 바꿨나</b>: 예전에는 두 씬을 다 올려 둔 채 <c>SetActive</c> 로 켜고 껐다.
    /// 그러면 안 그려질 뿐 <b>메모리에는 그대로</b> 남고, 벽 너머 고택이 창호를 뚫고 그려지는
    /// 것을 막느라 '높이로 잘라 접는'(Fold) 잔손이 붙어 있었다. 재어 보니 마당에 서 있을 때
    /// 3,507,683 삼각형, 접고 방에 들어가도 745,822 였다. 퀘스트 한 프레임 예산이 20~50만이다.
    ///
    /// 그래서 <b>김명관고택을 Onggojip_마당 씬으로 떼어냈다</b>(3,322,792). 플레이어·NPC·
    /// 표식·HUD 는 Onggojip 껍데기 씬(184,891)에 남아 늘 올라와 있고, 문턱을 넘을 때
    /// 마당과 실내를 <b>진짜로 언로드/로드</b>한다. 접을 일도, 켜고 끌 일도 없어졌다.
    ///
    /// <b>아궁이 때문이 아니었다</b>: 예전 주석이 아궁이를 들먹인 것은, 한 번 방에 들면
    /// 되돌아 나올 수 없어 아궁이를 보러 마당에 나갔을 때 집이 상자인 채였기 때문이다.
    /// 그건 증상이고 원인은 늘 삼각형이었다. 드나드는 횟수에 제한이 없는 것은 그대로다.
    ///
    /// <b>바꾸는 순간은 어둠으로 덮는다</b>: 씬을 진짜로 불러오면 틈이 생긴다. VR 에서
    /// 눈앞의 세상이 뒤늦게 나타나면 멀미가 나므로 <see cref="ScreenFade"/> 로 암전한다 —
    /// 순간이동에 이미 쓰는 방식이라 손에 익은 연출이다.
    ///
    /// <b>방 넓이를 미리 재어 둔다</b>(<see cref="_roomBox"/>): 안팎을 가르는 선은 방바닥이
    /// 정하는데, 마당에 서 있는 동안 실내 씬은 <b>올라와 있지도 않다</b>. 그러니 실려 있는
    /// 값으로 판단해야 한다. 재는 것은 에디터에서 한 번 — [이문록 ▸ 사랑방 ▸ 방 넓이 재기].
    ///
    /// 문지방에 걸터서서 씬이 깜빡이지 않도록, 들어설 때보다 나설 때의 선을 넉넉히 잡는다.
    /// </summary>
    public class InteriorSceneSwap : MonoBehaviour
    {
        [Header("갈아 끼울 두 씬")]
        [Tooltip("실내가 들어 있는 씬 이름")]
        [SerializeField] private string _sceneName = "Onggojip_사랑방";

        [Tooltip("마당·고택이 들어 있는 씬 이름. 방에 드는 동안 통째로 내린다")]
        [SerializeField] private string _yardScene = "Onggojip_마당";

        [Tooltip("실내 씬의 뿌리 오브젝트 이름. 잇는 쪽에서 쓴다")]
        [SerializeField] private string _interiorRootName = "사랑채_실내";

        [Header("안팎을 가르는 선")]
        [Tooltip("방바닥이 차지하는 넓이. 실내 씬이 안 올라와 있어도 판단해야 하므로 미리 재어 둔다. " +
                 "[이문록 ▸ 사랑방 ▸ 방 넓이 재기] 로 채운다")]
        [SerializeField] private Bounds _roomBox;

        [Tooltip("방바닥 가장자리에서 이만큼 밖까지는 아직 '안'으로 친다(m). 문지방 두께쯤")]
        [SerializeField] private float _enterMargin = 0.15f;

        [Tooltip("나설 때는 이만큼 더 나가야 '밖'이 된다(m). 문지방에 걸터서서 씬이 깜빡이는 것을 막는다")]
        [SerializeField] private float _leaveMargin = 0.55f;

        [Tooltip("방 위아래 여유(m). 마루에서 천장까지 넉넉히")]
        [SerializeField] private float _height = 3.2f;

        [Header("암전")]
        [Tooltip("어두워지는 데 걸리는 시간(초)")]
        [SerializeField] private float _fadeOut = 0.25f;

        [Tooltip("다시 밝아지는 데 걸리는 시간(초)")]
        [SerializeField] private float _fadeIn = 0.35f;

        [Header("방에 든 동안만 켤 것")]
        [Tooltip("문을 열었을 때 발밑에 있어야 할 간이 마당 바닥. 마당 씬을 통째로 내리므로 " +
                 "이것이 없으면 문 너머가 허공이 된다. 비워 두어도 돌아간다")]
        [SerializeField] private GameObject _groundWhileInside;

        [Header("이벤트")]
        [Tooltip("실내로 들어선 순간")]
        [SerializeField] private UnityEvent _onEntered;
        [Tooltip("실내에서 나온 순간")]
        [SerializeField] private UnityEvent _onLeft;

        private bool _inside;
        private bool _busy;

        /// <summary>지금 실내에 있나.</summary>
        public bool Inside => _inside;

        /// <summary>씬을 갈아 끼우는 중인가. 도중에 다른 처리가 끼어들면 안 될 때 본다.</summary>
        public bool Busy => _busy;

        private void Start()
        {
            if (_roomBox.size.sqrMagnitude < 0.01f)
                Debug.LogWarning($"[{name}] 방 넓이가 비어 있습니다. " +
                                 "[이문록 ▸ 사랑방 ▸ 방 넓이 재기] 를 한 번 눌러 주십시오.", this);

            if (_groundWhileInside != null) _groundWhileInside.SetActive(false);

            // 껍데기 씬만 올라온 채로 시작할 수 있다(허브에서 곧장 들어온 경우).
            // 마당을 올려 두고, 혹시 편집 중에 같이 열려 있던 실내 씬은 내린다.
            if (!IsLoaded(_yardScene)) SceneManager.LoadScene(_yardScene, LoadSceneMode.Additive);
            if (IsLoaded(_sceneName)) StartCoroutine(DropAtStart());
        }

        private IEnumerator DropAtStart()
        {
            yield return null;
            if (IsLoaded(_sceneName)) yield return SceneManager.UnloadSceneAsync(_sceneName);
        }

        private void Update()
        {
            if (_busy) return;
            var cam = Camera.main;
            if (cam == null || _roomBox.size.sqrMagnitude < 0.01f) return;

            // 들어설 때와 나설 때의 선이 다르다. 문지방 위에서 왔다 갔다 하면
            // 씬 두 장이 매 프레임 오르내린다.
            float margin = _inside ? _leaveMargin : _enterMargin;
            var box = _roomBox;
            box.size = new Vector3(box.size.x, _height, box.size.z);
            box.Expand(new Vector3(margin * 2f, 0f, margin * 2f));

            bool now = box.Contains(new Vector3(cam.transform.position.x, box.center.y, cam.transform.position.z));
            if (now != _inside) StartCoroutine(Swap(now));
        }

        /// <summary>
        /// 어둠으로 덮고, 한쪽을 내리고 다른 쪽을 올린 뒤 다시 밝힌다.
        ///
        /// 내리기를 먼저 하는 까닭: 두 씬이 겹쳐 올라오는 순간이 없어야 한다.
        /// 그 순간을 허용하면 최악의 한 프레임이 예전과 똑같아진다 — 그것을 없애려고 나눈 것이다.
        /// </summary>
        private IEnumerator Swap(bool goInside)
        {
            _busy = true;

            ScreenFade.To(1f, _fadeOut);
            yield return new WaitForSecondsRealtime(_fadeOut);

            string drop = goInside ? _yardScene : _sceneName;
            string lift = goInside ? _sceneName : _yardScene;

            if (IsLoaded(drop)) yield return SceneManager.UnloadSceneAsync(drop);

            if (!IsLoaded(lift))
            {
                if (Application.CanStreamedLevelBeLoaded(lift))
                    yield return SceneManager.LoadSceneAsync(lift, LoadSceneMode.Additive);
                else
                    Debug.LogWarning($"[{name}] 씬 '{lift}' 을 빌드 설정에서 못 찾았습니다.", this);
            }

            if (_groundWhileInside != null) _groundWhileInside.SetActive(goInside);

            _inside = goInside;
            yield return null;               // 한 프레임 두어 올라온 것이 확실히 그려지게

            ScreenFade.To(0f, _fadeIn);
            _busy = false;

            if (goInside) _onEntered?.Invoke(); else _onLeft?.Invoke();
        }

        private static bool IsLoaded(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            var s = SceneManager.GetSceneByName(sceneName);
            return s.IsValid() && s.isLoaded;
        }

        // ── 밖에서 부르는 신호(옛 배선과 호환) ──

        /// <summary>실내로 들여보낸다. 자리와 무관하게 갈아 끼운다.</summary>
        public void EnterInside() { if (!_inside && !_busy) StartCoroutine(Swap(true)); }

        /// <summary>마당으로 되돌린다.</summary>
        public void ExitOutside() { if (_inside && !_busy) StartCoroutine(Swap(false)); }

        /// <summary>
        /// 실내 씬이 올라왔을 때 잇는 쪽(<see cref="SarangbangBinder"/>)이 부른다.
        /// 예전에는 여기서 켜고 끌 것을 찾아 물었지만, 이제 씬째로 오르내리므로 할 일이 없다.
        /// 자리를 남겨 두는 것은 씬에 배선된 호출을 깨뜨리지 않기 위해서다.
        /// </summary>
        public void BindInterior(Scene interior) { }

        /// <summary>재어 둔 방 넓이. 에디터 도구가 채운다.</summary>
        public Bounds RoomBox { get => _roomBox; set => _roomBox = value; }

        /// <summary>실내 씬 이름. 에디터 도구가 읽는다.</summary>
        public string RoomSceneName => _sceneName;

        /// <summary>실내 뿌리 이름. 에디터 도구가 읽는다.</summary>
        public string InteriorRootName => _interiorRootName;
    }
}
