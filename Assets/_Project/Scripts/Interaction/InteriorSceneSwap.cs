using UnityEngine;
using UnityEngine.Events;

namespace IMUNROK.Common
{
    /// <summary>
    /// 사랑채를 <b>두 채로 갈아 끼운다</b> — 밖에서는 받아온 원본, 안에서는 상자로 지은 실내.
    ///
    /// 왜 두 채인가: 받아온 사랑채 한 채가 <b>234,735 삼각형</b>이고 상자로 지은 실내는
    /// 9,300 이다. 스물다섯 배 차이라, 방 안에 앉아 있는 동안은 상자 쪽이 낫다.
    /// 자리와 치수를 원본에서 재서 지었으므로 이미 놓아둔 보료·문갑·경상이 그대로 맞는다.
    ///
    /// <b>고친 것</b>: 예전에는 들어가는 일만 있었다. 한 번 들어서면 원본은 영영 꺼진 채였고,
    /// 아궁이를 보러 마당으로 되돌아 나오면 집이 상자인 채로 서 있었다 — "내부 씬이 자꾸
    /// 깨진다"가 이것이었다. 이제 <b>어디에 서 있는지를 매 프레임 보고</b> 그때그때 갈아 끼운다.
    /// 드나드는 횟수에 제한이 없다.
    ///
    /// 갈아 끼우는 선은 방바닥이 정한다(<see cref="_floor"/>). 문지방에 걸터서서 깜빡이지
    /// 않도록, 들어설 때보다 나설 때의 선을 조금 넉넉히 잡는다.
    ///
    /// 붙이는 법: 빈 오브젝트에 붙이고 _showWhileInside 에 지은 실내, _hideWhileInside 에
    /// 원본 사랑채, _floor 에 방바닥을 연결한다.
    /// </summary>
    public class InteriorSceneSwap : MonoBehaviour
    {
        [Header("실내를 다른 씬에 두었을 때")]
        [Tooltip("실내가 들어 있는 씬 이름. 채우면 시작할 때 얹어 올린다. " +
                 "비우면 이 씬 안의 _showWhileInside 를 켜고 끈다(옛 방식)")]
        [SerializeField] private string _sceneName = "";

        [Tooltip("실내 씬의 뿌리 오브젝트 이름. 그 밑에서 구조·소품·방바닥을 찾는다")]
        [SerializeField] private string _interiorRootName = "사랑채_실내";

        [Header("두 채")]
        [Tooltip("실내에 들어설 때 켤 것 — 상자로 지은 사랑채 실내(구조)")]
        [SerializeField] private GameObject _showWhileInside;

        [Tooltip("실내에 있는 동안 꺼둘 것 — 받아온 원본 사랑채")]
        [SerializeField] private GameObject _hideWhileInside;

        [Tooltip("실내에 있는 동안만 켤 세간(소품). 비우면 늘 켜 둔다. " +
                 "세간만 40만 삼각형이라 마당에 서 있는 동안은 끄는 편이 낫다")]
        [SerializeField] private GameObject _propsWhileInside;

        [Header("안팎을 가르는 선")]
        [Tooltip("방바닥. 이 넓이가 곧 '안'이다. 비우면 _showWhileInside 전체를 쓴다")]
        [SerializeField] private Transform _floor;

        [Tooltip("방바닥 가장자리에서 이만큼 밖까지는 아직 '안'으로 친다(m). 문지방 두께쯤")]
        [SerializeField] private float _enterMargin = 0.3f;

        [Tooltip("나설 때는 이만큼 더 나가야 '밖'이 된다(m). 문지방에 걸터서서 " +
                 "집이 깜빡이는 것을 막는다")]
        [SerializeField] private float _leaveMargin = 1.2f;

        [Tooltip("방 위아래 여유(m). 마루에서 천장까지 넉넉히")]
        [SerializeField] private float _height = 3.2f;

        [Header("이벤트")]
        [Tooltip("실내로 들어선 순간")]
        [SerializeField] private UnityEvent _onEntered;
        [Tooltip("실내에서 나온 순간")]
        [SerializeField] private UnityEvent _onLeft;

        private bool _inside;
        private Bounds _room;
        private bool _measured;

        /// <summary>지금 실내에 있나.</summary>
        public bool Inside => _inside;

        private void Start()
        {
            if (!string.IsNullOrEmpty(_sceneName)) LoadInterior();
            Measure();
            Apply();          // 씬을 켠 자리(마당)에 맞춰 시작한다
        }

        /// <summary>
        /// 실내 씬을 얹어 올린다.
        ///
        /// 왜 시작할 때 미리 올리나: 불러오는 데 걸리는 틈이 <b>중문을 넘는 순간</b>에 오면
        /// 집이 눈앞에서 뒤늦게 나타난다. 실내는 9,300 삼각형뿐이라 처음부터 들고 있어도
        /// 부담이 없고, 어차피 마당에 있는 동안은 꺼 두므로 그려지지도 않는다.
        /// 씬을 나눈 값은 <b>파일을 따로 여닫는 것</b>에 있지 불러오는 시점에 있지 않다.
        /// </summary>
        private void LoadInterior()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetSceneByName(_sceneName).isLoaded) return;
            if (!Application.CanStreamedLevelBeLoaded(_sceneName))
            {
                Debug.LogWarning($"[{name}] 실내 씬 '{_sceneName}' 을 빌드 설정에서 못 찾았습니다.", this);
                return;
            }
            UnityEngine.SceneManagement.SceneManager.LoadScene(_sceneName, UnityEngine.SceneManagement.LoadSceneMode.Additive);
        }

        /// <summary>
        /// 올라온 실내 씬에서 켜고 끌 것을 이름으로 찾아 문다.
        /// 씬을 건너뛰는 참조는 저장되지 않으므로 인스펙터로는 못 잇는다.
        /// </summary>
        public void BindInterior(UnityEngine.SceneManagement.Scene interior)
        {
            foreach (var root in interior.GetRootGameObjects())
            {
                if (root.name != _interiorRootName) continue;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "구조" && t.parent == root.transform) _showWhileInside = t.gameObject;
                    else if (t.name == "소품" && t.parent == root.transform) _propsWhileInside = t.gameObject;
                    else if (t.name == "장판바닥") _floor = t;
                }
            }
            _measured = false;
            Measure();
            Apply();
        }

        private void Update()
        {
            var cam = Camera.main;
            if (cam == null || !_measured) return;

            // 들어설 때와 나설 때의 선이 다르다. 문지방 위에서 왔다 갔다 하면
            // 집 한 채가 매 프레임 켜졌다 꺼진다.
            float margin = _inside ? _leaveMargin : _enterMargin;
            var box = _room;
            box.Expand(new Vector3(margin * 2f, 0f, margin * 2f));

            bool now = box.Contains(new Vector3(cam.transform.position.x, box.center.y, cam.transform.position.z));
            if (now == _inside) return;

            _inside = now;
            Apply();
            if (now) _onEntered?.Invoke(); else _onLeft?.Invoke();
        }

        /// <summary>방의 넓이를 잰다. 한 번만.</summary>
        private void Measure()
        {
            Transform src = _floor != null ? _floor : (_showWhileInside != null ? _showWhileInside.transform : null);
            if (src == null) return;

            var rs = src.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return;

            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            b.size = new Vector3(b.size.x, _height, b.size.z);
            _room = b;
            _measured = true;
        }

        private void Apply()
        {
            if (_showWhileInside != null && _showWhileInside.activeSelf != _inside)
                _showWhileInside.SetActive(_inside);

            if (_hideWhileInside != null && _hideWhileInside.activeSelf == _inside)
                _hideWhileInside.SetActive(!_inside);

            if (_propsWhileInside != null && _propsWhileInside.activeSelf != _inside)
                _propsWhileInside.SetActive(_inside);
        }

        // ── 밖에서 부르는 신호(옛 배선과 호환) ──

        /// <summary>실내로 들여보낸다. 자리와 무관하게 곧바로 갈아 끼운다.</summary>
        public void EnterInside()
        {
            if (!_measured) Measure();
            if (_inside) return;
            _inside = true;
            Apply();
            _onEntered?.Invoke();
        }

        /// <summary>마당으로 되돌린다.</summary>
        public void ExitOutside()
        {
            if (!_inside) return;
            _inside = false;
            Apply();
            _onLeft?.Invoke();
        }

        private void OnDrawGizmosSelected()
        {
            if (!_measured) Measure();
            if (!_measured) return;
            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.8f);
            Gizmos.DrawWireCube(_room.center, _room.size + new Vector3(_enterMargin * 2f, 0f, _enterMargin * 2f));
            Gizmos.color = new Color(1f, 0.7f, 0.3f, 0.6f);
            Gizmos.DrawWireCube(_room.center, _room.size + new Vector3(_leaveMargin * 2f, 0f, _leaveMargin * 2f));
        }
    }
}
