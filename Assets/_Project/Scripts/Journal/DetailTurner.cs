using UnityEngine;
using UnityEngine.EventSystems;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>상세에서 물건을 끌어 돌린다.</b>
    ///
    /// 견우 소지품 판의 상세는 왼쪽 그림자리를 <b>끌어서 돌려 보는</b> 자리다 —
    /// 물건을 손에 들고 이리저리 비춰 보는 손짓이고, 그것이 「자세히 본다」의 알맹이다.
    /// 우리 판에는 그림 한 장이 못 박혀 있었다. 크게 보이기는 하는데 <b>만질 수가 없어서</b>,
    /// 자세히 보려면 결국 판을 닫고 종이를 따로 펴야 했다.
    ///
    /// <b>우리 물건은 대개 종이다.</b> 그래서 저쪽처럼 3D를 굴리는 대신 <b>종이를 돌린다</b> —
    /// 좌우로 끌면 넘어가고, 위아래로 끌면 기울어진다. 절반을 넘겨 돌리면 <b>뒷면</b>이
    /// 나온다(뒤에 새겨진 것이 있는 종이가 있다). 손에 든 종이가 실제로 그렇게 돈다.
    ///
    /// 붙이는 곳: 상세의 그림자리(<see cref="JournalPanel"/> 가 코드로 붙인다).
    /// </summary>
    public class DetailTurner : MonoBehaviour, IDragHandler, IBeginDragHandler, IPointerClickHandler
    {
        /// <summary>실제로 돌릴 것 — 종이 그림.</summary>
        public RectTransform Page;

        /// <summary>뒷면 그림. 없으면 뒤집혀도 앞면이 그대로 보인다.</summary>
        public Texture2D Back;

        /// <summary>앞면 그림.</summary>
        public Texture2D Front;

        /// <summary>그림을 그리는 것.</summary>
        public UnityEngine.UI.RawImage Image;

        [Tooltip("끄는 만큼 도는 정도(도/화소)")]
        public float Sensitivity = 0.45f;

        private float _yaw, _pitch;
        private bool _showingBack;

        /// <summary>다른 물건을 띄울 때 자세를 처음으로 돌린다.</summary>
        public void ResetView()
        {
            _yaw = 0f; _pitch = 0f;
            _showingBack = false;
            Apply();
        }

        public void OnBeginDrag(PointerEventData e) { }

        public void OnDrag(PointerEventData e)
        {
            if (Page == null) return;
            _yaw += e.delta.x * Sensitivity;
            _pitch -= e.delta.y * Sensitivity;
            // 위아래는 조금만 — 종이는 손목으로 기울이는 것이지 공중제비를 도는 것이 아니다.
            _pitch = Mathf.Clamp(_pitch, -50f, 50f);
            Apply();
        }

        /// <summary>눌러서 한 번에 뒤집는다. 끌기를 모르는 사람에게 남겨 두는 길이다.</summary>
        public void OnPointerClick(PointerEventData e)
        {
            if (Page == null || e.dragging) return;
            _yaw = Mathf.Abs(Mathf.DeltaAngle(_yaw, 0f)) < 90f ? 180f : 0f;
            Apply();
        }

        private void Apply()
        {
            Page.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);

            // <b>반 바퀴를 넘기면 뒷면이다.</b> 종이에는 두께가 없으므로, 돌아간 각으로만
            // 앞뒤를 가른다. 뒷면 그림이 없으면 앞면을 그대로 두되 좌우를 뒤집어
            // 「뒤에서 본 종이」로 보이게 한다.
            bool back = Mathf.Abs(Mathf.DeltaAngle(_yaw, 0f)) > 90f;
            if (back == _showingBack || Image == null) return;
            _showingBack = back;

            if (back && Back != null) Image.texture = Back;
            else Image.texture = Front;

            // 뒷면일 때는 글씨가 거울처럼 뒤집히지 않게 한 번 더 뒤집어 준다.
            var s = Page.localScale;
            s.x = back ? -Mathf.Abs(s.x) : Mathf.Abs(s.x);
            Page.localScale = s;
        }
    }
}
