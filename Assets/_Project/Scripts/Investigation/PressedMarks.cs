using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>눌린 자국</b> — 맨눈에는 빈 종이, 돋보기로 보면 글이 있다.
    ///
    /// 여태 돋보기가 하던 일은 "작게 적힌 것을 크게 보는 것"뿐이었다. 그것만으로는
    /// 도구가 아니라 <b>글자 크기 조절기</b>다. 유리를 대야 비로소 <b>있는 줄도 몰랐던 것</b>이
    /// 나타나야 도구가 된다.
    ///
    /// 그래서 이 종이는 두 장으로 되어 있다:
    ///   · 맨눈이 보는 장 — 아무것도 안 쓰인 한지
    ///   · 유리만 보는 장 — 그 위에 놓고 쓴 편지가 눌러 남긴 자국
    ///
    /// 가리는 방법은 <b>레이어</b>다. 자국 장을 눈의 카메라가 안 보는 레이어에 두고,
    /// 렌즈 카메라만 그 레이어를 찍게 한다. 그러면 숨기고 드러내는 장치가 따로 필요 없다 —
    /// 유리를 통해 보면 있고, 치우면 없다. 각도를 돌려도, 멀리서 보아도 그대로다.
    /// 조건을 세거나 시간을 재지 않으므로 어긋날 구석이 없다.
    ///
    /// 붙이는 곳: 자국이 그려진 장(맨눈 종이의 자식으로, 겹쳐 놓는다).
    /// 단서는 <b>들여다본 뒤에</b> 적힌다 — 지나가다 눈이 스친 것으로 읽었다 할 수 없다.
    /// </summary>
    public class PressedMarks : MonoBehaviour, IMagnifiable
    {
        /// <summary>눈은 못 보고 렌즈만 보는 레이어의 이름.</summary>
        public const string LayerName = "눌린자국";

        [Header("무엇이 읽히나")]
        [SerializeField] private string _title = "눌린 자국";
        [TextArea(2, 4)]
        [Tooltip("다 읽었을 때 종이 밖에 적히는 것")]
        [SerializeField] private string _readOut = "";

        [Header("읽고 나면 수첩에")]
        [SerializeField] private bool _recordClue = true;
        [SerializeField] private CaseId _clueCase = CaseId.Case1_Onggojip;
        [Tooltip("어느 단서를 굳히는가. 이미 적힌 단서면 그 줄을 고쳐 적고, 없으면 새로 적는다")]
        [SerializeField] private string _clueKey = "J09";
        [TextArea(2, 4)]
        [Tooltip("고쳐 적을 문구. 같은 이야기를 두 줄로 늘리지 않기 위한 것이다")]
        [SerializeField] private string _clueText = "";
        [Tooltip("수첩에서 다시 볼 종이(자국이 그려진 면)")]
        [SerializeField] private Texture2D _cluePage;

        [Header("드러나기")]
        [Tooltip("이 물건도 함께 자국 레이어로 옮긴다(비우면 자기 자신만)")]
        [SerializeField] private Transform[] _alsoHide;

        private bool _recorded;

        private void Awake()
        {
            int layer = LayerMask.NameToLayer(LayerName);
            if (layer < 0)
            {
                Debug.LogWarning($"[눌린자국] '{LayerName}' 레이어가 없습니다 — " +
                                 "Project Settings ▸ Tags and Layers 에 추가하십시오. " +
                                 "없으면 맨눈에도 자국이 보입니다.", this);
                return;
            }

            Move(transform, layer);
            if (_alsoHide != null)
                foreach (var t in _alsoHide) if (t != null) Move(t, layer);
        }

        private static void Move(Transform root, int layer)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;
        }

        /// <summary>
        /// 유리 한가운데로 들여다보는 중. 다 들여다보면 그제야 읽은 것으로 친다.
        ///
        /// 자국은 <b>보이는 것</b>과 <b>읽는 것</b>이 다르다. 유리를 스치듯 지나가도 자국은
        /// 보이지만, 무엇이 적혔는지 알려면 들여다보고 있어야 한다.
        /// </summary>
        public void OnMagnifiedGaze(float progress)
        {
            if (progress < 1f || _recorded) return;
            _recorded = true;

            if (_recordClue && !string.IsNullOrEmpty(_clueKey) && Journal.Instance != null)
            {
                // 이미 그 단서가 있으면 <b>줄을 늘리지 않고 고쳐 적는다</b>.
                // 필적이 다르다는 것과 그 두 줄을 연습한 자국이 있다는 것은 한 가지 일이다.
                if (!Journal.Instance.UpgradeClue(_clueCase, _clueKey, _clueText))
                    Journal.Instance.AddClue(_clueCase, _clueKey, _clueText);
                if (_cluePage != null)
                    Journal.Instance.AttachDocument(_clueCase, _clueKey, _cluePage, _title, _clueText, _readOut);
            }

            WorldNote.Show(transform, string.IsNullOrEmpty(_readOut)
                ? _title + "  —  수첩에 적어 두었다"
                : _readOut);
        }
    }
}
