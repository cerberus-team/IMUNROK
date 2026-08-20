using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 안내창 귀퉁이에 붙는 <b>닫기 표</b>. 레이로 누르면 정해진 일을 한다.
    ///
    /// 왜 필요한가: 자막·안내는 스스로 사라지거나 다음 말이 밀어내기 전에는 눈앞을
    /// 가린 채로 있다. 헤드셋에는 Esc 키가 없으니, 안 보고 싶을 때 치울 방법이
    /// <b>보이는 물건</b>으로 하나는 있어야 한다.
    ///
    /// 콜라이더는 안내가 떠 있는 동안에만 켠다. 꺼진 채로 눈앞에 남아 있으면
    /// 보이지도 않는 것이 뒤쪽 물건으로 가는 레이를 가로챈다 — 이 프로젝트에서
    /// 이미 한 번 겪은 탈이다.
    /// </summary>
    public class NoticeCloseTab : MonoBehaviour, ISelectable
    {
        private System.Action _onClose;
        private BoxCollider _box;

        /// <summary>누르면 할 일을 건다.</summary>
        public void Bind(System.Action onClose, Vector3 sizeInCanvasUnits)
        {
            _onClose = onClose;
            _box = GetComponent<BoxCollider>();
            if (_box == null) _box = gameObject.AddComponent<BoxCollider>();
            _box.size = sizeInCanvasUnits;
            _box.isTrigger = true;
        }

        /// <summary>안내가 떠 있는 동안에만 누를 수 있게 한다.</summary>
        public void SetActive(bool on)
        {
            if (_box == null) _box = GetComponent<BoxCollider>();
            if (_box != null) _box.enabled = on;
        }

        public void OnHoverEnter() { }
        public void OnHoverExit() { }
        public void OnSelect() => _onClose?.Invoke();
    }
}
