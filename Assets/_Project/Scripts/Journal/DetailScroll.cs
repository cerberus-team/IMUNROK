using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>상세 칸의 글을 휠로 내려 읽는다.</b>
    ///
    /// 증거 설명을 견우 판만큼 늘리자(무엇인가 · 생김새 · 유리로 읽은 것 · 무엇이 이상한가)
    /// 글이 칸 높이를 넘었다. 칸은 틀(RectMask2D)이라 넘친 줄은 <b>말없이 잘린다</b> —
    /// 가장 중요한 끝줄(의심 한 줄)이 늘 거기 있다. 글씨를 줄여 우겨넣지 않고 굴린다.
    ///
    /// 붙이는 곳: 글틀(<see cref="JournalPanel"/> 이 코드로 붙인다).
    /// </summary>
    public class DetailScroll : MonoBehaviour, IScrollHandler
    {
        /// <summary>굴릴 글.</summary>
        public TextMeshProUGUI Text;

        /// <summary>아래에 더 있을 때만 뜨는 표(「▼ 휠로 더 읽기」). 없어도 된다.</summary>
        public TextMeshProUGUI More;

        [Tooltip("휠 한 칸에 내려가는 거리(판 단위)")]
        public float Step = 70f;

        private float _y;

        /// <summary>새 글을 얹었으면 맨 위로.</summary>
        public void ResetTop()
        {
            _y = 0f;
            Apply();
        }

        public void OnScroll(PointerEventData e)
        {
            if (Mathf.Abs(e.scrollDelta.y) < 0.001f) return;
            // 휠 한 칸의 값이 장치마다 1 이기도 120 이기도 하다 — 방향만 쓴다.
            _y -= Mathf.Sign(e.scrollDelta.y) * Step;
            Apply();
        }

        private void Apply()
        {
            if (Text == null) return;
            var box = (RectTransform)transform;
            float view = box.rect.height;
            Text.ForceMeshUpdate();
            float need = Mathf.Max(view, Text.preferredHeight + 8f);
            _y = Mathf.Clamp(_y, 0f, need - view);

            var rt = Text.rectTransform;
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, need);
            // 글의 윗변을 틀의 윗변에 붙이고, 내려 읽은 만큼 올린다.
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, view * 0.5f - need * 0.5f + _y);

            // 잘린 끝을 모르고 지나가지 않게, 아래에 남은 글이 있으면 표를 띄운다.
            if (More != null) More.gameObject.SetActive(need - view - _y > 4f);
        }
    }
}
