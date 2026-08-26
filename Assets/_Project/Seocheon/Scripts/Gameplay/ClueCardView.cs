// 조각 카드 한 장. 전 카드가 같은 프리팹·같은 배경이며 앞면에는 지목한 어절만 씁니다.
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IMUNROK.Seocheon
{
    /// <summary>
    /// 카드 한 장.
    ///
    /// ★유효/오답에 따라 달라지는 것이 ★하나도 없습니다.
    ///   배경 스프라이트도, 글자색도, 크기도 전부 화면 쪽에서 ★같은 값으로 주입됩니다.
    ///   카드 스스로는 자기가 유효한지 알지 못하고, 알 필요도 없습니다.
    ///
    /// ★예외는 결합 결과 카드뿐입니다. 이미 판정이 끝난 것이라 숨길 대상이 아닙니다.
    /// </summary>
    public sealed class ClueCardView : MonoBehaviour
    {
        [Tooltip("카드 배경. ★나중에 조선풍 프레임 이미지로 갈아끼우는 슬롯입니다")]
        [SerializeField] private Image background;
        [Tooltip("앞면 글자 — 지목한 어절(수집 카드) 또는 결과 문구(결합 카드)")]
        [SerializeField] private TMP_Text faceLabel;
        [Tooltip("결합 유형 표식(모순/연결/결론). 수집 카드에서는 꺼집니다")]
        [SerializeField] private GameObject kindBadge;
        [SerializeField] private TMP_Text kindLabel;
        [Tooltip("선택 표시 테두리")]
        [SerializeField] private GameObject selectedFrame;

        private string entryId = string.Empty;

        public string EntryId { get { return entryId; } }
        public RectTransform Rect { get { return (RectTransform)transform; } }

        /// <summary>카드를 눌렀을 때. 인자는 이 카드.</summary>
        public event Action<ClueCardView> Clicked;

        /// <summary>
        /// 카드를 채웁니다. 배경·글자색은 ★호출자가 전 카드에 같은 값을 넘깁니다.
        /// </summary>
        /// <param name="showFaceText">
        /// ★수집 카드는 false — 앞면에 ★그림만 남습니다(무엇을 들었는지 감춥니다).
        /// ★결합 결과 카드는 true — 이미 판정이 끝났으니 알아낸 것을 바로 읽게 합니다.
        /// </param>
        public void Bind(SeocheonClueRecord record, Sprite face, Color textColor, bool showFaceText)
        {
            entryId = record != null ? record.entryId : string.Empty;
            if (background != null && face != null) background.sprite = face;

            if (faceLabel != null)
            {
                faceLabel.gameObject.SetActive(showFaceText);
                faceLabel.text = (showFaceText && record != null) ? record.faceText : string.Empty;
                faceLabel.color = textColor;
            }

            bool derived = record != null && record.isDerived;
            if (kindBadge != null) kindBadge.SetActive(derived);
            if (kindLabel != null && derived) kindLabel.text = record.combineKind;
            SetSelected(false);
        }

        public void SetSelected(bool value)
        {
            if (selectedFrame != null) selectedFrame.SetActive(value);
        }

        /// <summary>화면이 매 프레임 포인터를 넘겨 줍니다(카드마다 Update 를 돌리지 않습니다).</summary>
        public bool ContainsPointer(Vector2 screenPoint, Camera uiCamera)
        {
            return RectTransformUtility.RectangleContainsScreenPoint(Rect, screenPoint, uiCamera);
        }

        public void RaiseClicked()
        {
            if (Clicked != null) Clicked(this);
        }
    }
}
