using UnityEngine;
using UnityEngine.Events;

namespace IMUNROK.Common
{
    /// <summary>
    /// 조사종이 — 사건에 들어서는 순간 눈앞에서 펼쳐지는 봉서의 내용.
    /// 언제, 어디서, 누가 무엇을 고했는가. 다 읽고 놓으면 접혀 수첩 첫 장으로 들어간다.
    ///
    /// 왜 도구가 아닌가: 도구벨트는 <b>어느 사건에서나 똑같은 것</b>들이 있는 자리다
    /// (수첩·지도·등불·돋보기). 여기에 사건마다 내용이 바뀌는 칸을 하나 끼우면 벨트의
    /// 뜻이 흐려지고, 사건이 늘 때마다 벨트를 손봐야 한다. 조사종이는 "이 사건에 대해
    /// 내가 아는 것"이고, 수첩이 정확히 그걸 담는 물건이다.
    ///
    /// 접혀서 수첩으로 들어가는 것은 말이 아니라 동작이어야 한다. 눈으로 그 길을 한 번
    /// 봐 두면 "아까 그거 어디 갔지"가 안 생긴다.
    ///
    /// 들이밀 수 없는 이유: 처음부터 쥐고 있던 것은 증거가 아니라 출발점이다.
    /// 그래서 수첩에는 남되 심문의 '들이밀기' 단추는 붙지 않는다
    /// (<see cref="ClueEntry.presentable"/> = false).
    ///
    /// 붙이는 법: 사건 씬의 빈 오브젝트에 붙이고 _caseId·_document·_briefText 를 채운다.
    /// 두루마리 프리팹은 비워 두면 _Common/Prefabs 에서 저절로 찾는다.
    /// </summary>
    public class CaseBriefing : MonoBehaviour
    {
        [Header("어느 사건인가")]
        [SerializeField] private CaseId _caseId = CaseId.Case1_Onggojip;

        [Header("종이에 적힌 것")]
        [Tooltip("두루마리에 그려질 사건 문서 그림. [이문록 ▸ 사건 문서 굽기] 로 만든 것")]
        [SerializeField] private Texture2D _document;

        [Tooltip("수첩 첫 장에 남을 한 줄. 그림을 못 읽는 사람도 이건 읽는다")]
        [TextArea]
        [SerializeField] private string _briefText = "봉서에 이르기를 — 옹신골 옹당촌의 옹덕구가 제 집에서 쫓겨났다 한다.";

        [Header("펼침")]
        [Tooltip("비우면 Assets/_Project/_Common/Prefabs/두루마리 를 쓴다")]
        [SerializeField] private GameObject _scrollPrefab;
        [Tooltip("눈에서 이만큼 앞에 펼친다(m)")]
        [SerializeField] private float _distance = 0.75f;
        [Tooltip("눈높이에서 이만큼 내려 잡는다(m). 고개를 조금 숙여 읽는 자세가 편하다")]
        [SerializeField] private float _drop = 0.12f;
        [Tooltip("켜지면 스스로 펼친다. 끄면 Present() 를 불러서 펼친다")]
        [SerializeField] private bool _onStart = true;
        [Tooltip("씬에 들어설 때 GameState 에 이 사건으로 들어왔다고 알린다")]
        [SerializeField] private bool _enterCaseOnStart = true;

        [Header("접힌 뒤")]
        [Tooltip("종이가 접혀 수첩으로 들어간 뒤 실행 — 여기서 사건을 시작하면 된다")]
        [SerializeField] private UnityEvent _onDone;

        private const string DefaultPrefab = "Assets/_Project/_Common/Prefabs/두루마리.prefab";

        private ScrollUnroll _scroll;
        private bool _shown;

        private void Start()
        {
            if (_enterCaseOnStart)
            {
                GameState.Instance.StartCase(_caseId);
                GameState.Instance.EnterCase(_caseId);
            }

            // 수첩 첫 장에는 펼치기 전에 미리 적어 둔다. 플레이어가 종이를 안 읽고
            // 지나가도 개요는 남아 있어야 한다. 단서 목록에 넣지 않는 까닭은
            // 조사종이가 주워 온 물증이 아니라 처음부터 쥐고 있던 출발점이기 때문이다.
            Journal.Instance.SetBrief(_caseId, _briefText);

            if (_onStart) Present();
        }

        /// <summary>조사종이를 눈앞에 펼친다.</summary>
        public void Present()
        {
            if (_shown) return;
            _shown = true;

            // 읽는 자리는 하나뿐이다. 문서나 수첩이 펴져 있으면 그쪽이 닫힌다.
            ReadingFocus.Claim(ReadingFocus.Panel.Briefing, Finish);

            var prefab = _scrollPrefab;
#if UNITY_EDITOR
            if (prefab == null) prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(DefaultPrefab);
#endif
            if (prefab == null)
            {
                Debug.LogWarning("[CaseBriefing] 두루마리 프리팹이 없어 종이를 못 펼칩니다. " +
                                 "[이문록 ▸ 두루마리 만들기] 로 만든 뒤 연결하세요.", this);
                Finish();
                return;
            }

            var cam = Camera.main;
            if (cam == null) { Debug.LogWarning("[CaseBriefing] 카메라를 못 찾았습니다.", this); Finish(); return; }

            var go = Instantiate(prefab);
            go.name = "조사종이";
            _scroll = go.GetComponent<ScrollUnroll>();
            if (_scroll == null) { Debug.LogWarning("[CaseBriefing] 프리팹에 ScrollUnroll 이 없습니다.", this); Finish(); return; }

            if (_document != null) _scroll.SetDocument(_document);

            // 종이는 윗축에 매달려 아래로 자란다. 그대로 눈높이에 두면 글이 죄 아래에
            // 걸리므로, 다 폈을 때의 절반만큼 올려 달아 한가운데가 눈에 오게 한다.
            Vector3 eye = cam.transform.position;
            Vector3 at = eye + cam.transform.forward * _distance + Vector3.up * (_scroll.FullHeight * 0.5f - _drop);
            go.transform.position = at;

            Vector3 face = at - eye;
            face.y = 0f;
            if (face.sqrMagnitude > 0.0001f) go.transform.rotation = Quaternion.LookRotation(face);

            _scroll.OnRolled.AddListener(Finish);
            _scroll.SetInstant(0f);
            _scroll.Unroll();
        }

        /// <summary>종이가 접혔다 — 치우고 사건을 시작한다.</summary>
        private void Finish()
        {
            ReadingFocus.Release(ReadingFocus.Panel.Briefing);
            _shown = false;
            if (_scroll != null)
            {
                _scroll.OnRolled.RemoveListener(Finish);
                Destroy(_scroll.gameObject, 0.1f);
                _scroll = null;
            }
            _onDone?.Invoke();
        }
    }
}
