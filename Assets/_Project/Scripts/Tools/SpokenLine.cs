using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 한 마디를 자막으로 띄우는 부품. 이벤트에서 <see cref="Play"/> 를 부르면 된다.
    ///
    /// 왜 필요한가: 지금까지 대사를 띄우는 길은 저마다 제 부품 안에 박혀 있었다 —
    /// 대문 순번표, 마름, 심문창이 각각 제 대사를 들고 제 시간표로 띄운다. 그래서
    /// "여기서 이 말 한 마디만 하면 된다" 하는 자리에 쓸 것이 없었고, 없으니 또 다른
    /// 부품 안에 대사를 박아 넣게 된다.
    ///
    /// 이것은 대사 한 줄과 띄울 시간만 든다. 누가 언제 부를지는 이벤트가 정한다.
    /// </summary>
    public class SpokenLine : MonoBehaviour
    {
        [Tooltip("말하는 이. 비우면 이름표 없이 대사만 뜬다")]
        [SerializeField] private string _speaker = "";

        [TextArea(2, 4)]
        [SerializeField] private string _text = "";

        [Tooltip("대사 아래 작은 글씨(무엇을 하라는 신호)")]
        [SerializeField] private string _hint = "";

        [Tooltip("이만큼 뒤에 내린다(초). 0이면 다른 대사가 덮을 때까지 남는다")]
        [SerializeField] private float _seconds = 4f;

        /// <summary>자막을 띄운다.</summary>
        public void Play()
        {
            if (string.IsNullOrEmpty(_text)) return;
            SubtitleView.Show(_speaker, _text, _hint);
            CancelInvoke(nameof(Hide));
            if (_seconds > 0f) Invoke(nameof(Hide), _seconds);
        }

        /// <summary>
        /// 내린다.
        ///
        /// 내가 띄운 것이 아직 떠 있을 때만 내린다 — 그 사이에 다른 대사가 덮었으면
        /// 남의 말을 지우게 된다.
        /// </summary>
        public void Hide()
        {
            CancelInvoke(nameof(Hide));
            if (SubtitleView.IsShowing) SubtitleView.Hide();
        }
    }
}
