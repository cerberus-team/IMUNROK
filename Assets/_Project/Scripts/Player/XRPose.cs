using UnityEngine;
using UnityEngine.XR;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>머리와 손이 있는 자리를 이 오브젝트에 옮겨 적는다.</b>
    ///
    /// 헤드셋을 켜면 눈에 보이는 그림은 저절로 스테레오가 되지만, <b>게임이 아는 카메라
    /// 자리</b>는 그대로다. 그래서 고개를 돌려 장롱을 보고 있어도 짚는 광선은 엉뚱한
    /// 데로 나간다 — 돋보기가 무엇을 들여다보는지, 등불이 종이 뒤에 갔는지, 종이가
    /// 눈앞 어디에 걸리는지를 재는 셈이 전부 <c>Camera.main.transform</c> 을 보기 때문이다.
    ///
    /// 그러니 <b>추적된 자세를 실제로 트랜스폼에 넣어 주는</b> 것이 있어야 한다. 그 일만
    /// 하는 것이 이 부품이다. 머리에 붙이면 머리가 되고, 손에 붙이면 손이 된다.
    ///
    /// <b>그리기 직전에 한 번 더</b> 넣는다(onBeforeRender). 프레임 앞머리에서 잰 자세로
    /// 그리면 고개를 빨리 돌릴 때 한 프레임씩 늦게 따라와 멀미가 난다.
    /// </summary>
    public class XRPose : MonoBehaviour
    {
        [Tooltip("어느 마디를 따라가나 — 머리(Head)·왼손(LeftHand)·오른손(RightHand)")]
        [SerializeField] private XRNode _node = XRNode.Head;

        [Tooltip("자리도 따라가나. 끄면 방향만 따라간다")]
        [SerializeField] private bool _position = true;

        [Tooltip("이 마디가 아직 안 잡힐 때 쓸 자리(제 부모 기준). 손이 꺼져 있을 때 " +
                 "손 모형이 발밑에 떨어져 있지 않게 한다")]
        [SerializeField] private Vector3 _restPosition = new Vector3(0.2f, 1.0f, 0.25f);

        /// <summary>지금 이 마디가 실제로 잡히고 있나. 손이 꺼져 있으면 거짓.</summary>
        public bool Tracked { get; private set; }

        private void OnEnable() { Application.onBeforeRender += Apply; }
        private void OnDisable() { Application.onBeforeRender -= Apply; }
        private void Update() { Apply(); }

        [Tooltip("<b>안 쓰고 있는 헤드셋의 자세는 안 받는다</b>(머리에만 쓴다).\n\n" +
                 "링크를 켜 두고 헤드셋을 책상에 내려놓으면, 오큘러스는 장치를 " +
                 "<b>멀쩡히 잡혔다</b>고 하면서 자리는 추적 원점(0,0,0)을 준다. 그 값을 " +
                 "그대로 넣으면 눈이 몸 뿌리로 내려앉는데, 몸 뿌리는 <b>바닥</b>이다 — " +
                 "화면이 마루에 깔린다. 재 보고 알았다(눈 y −0.80, 마루 −0.807).\n\n" +
                 "쓰고 있는지는 헤드셋이 알려 준다(userPresence). 그것을 못 읽는 장치를 " +
                 "위해 높이도 함께 본다")]
        [SerializeField] private bool _ignoreWhenNotWorn = true;

        [Tooltip("머리가 몸 뿌리에서 이보다 낮게 잡히면 <b>안 쓴 것</b>으로 본다(m). " +
                 "웅크려도 0.6m 밑으로 내려가지는 않는다")]
        [SerializeField] private float _minHeadHeight = 0.6f;

        private void Apply()
        {
            var dev = InputDevices.GetDeviceAtXRNode(_node);
            if (!dev.isValid) { Tracked = false; return; }

            // ── 안 쓰고 있는 머리는 안 받는다 ──────────
            //
            // 「잡혔다」와 「쓰고 있다」는 다른 말이다. 링크만 켜 두고 헤드셋을 내려놓으면
            // 장치는 잡히되 자리는 원점이고, 그 원점이 곧 <b>발밑</b>이다.
            if (_ignoreWhenNotWorn && _node == XRNode.Head)
            {
                bool worn;
                if (dev.TryGetFeatureValue(CommonUsages.userPresence, out worn) && !worn)
                { Tracked = false; Rest(); return; }
            }

            bool got = false;
            if (dev.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rot))
            {
                transform.localRotation = rot;
                got = true;
            }
            if (_position && dev.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 pos))
            {
                // userPresence 를 안 주는 장치도 있다. 그때는 <b>높이</b>로 가른다 —
                // 사람 머리가 제 발밑에 붙어 있을 수는 없다.
                if (_ignoreWhenNotWorn && _node == XRNode.Head && pos.y < _minHeadHeight)
                { Tracked = false; Rest(); return; }

                transform.localPosition = pos;
                got = true;
            }
            Tracked = got;
        }

        /// <summary>
        /// 아직 안 잡히는 동안 놓아 둘 자리로 물린다.
        ///
        /// <b>여기 놓아 두는 자리가 곧 「안 썼을 때의 자세」다.</b> 손은 발밑에 떨어져
        /// 있지 않게 하려고 둔 것이었는데, 머리에도 같은 것이 필요하다는 것을
        /// 뒤늦게 알았다 — 머리의 쉬는 자리는 <b>선 사람의 눈높이</b>다.
        /// </summary>
        public void Rest()
        {
            transform.localPosition = _restPosition;
            transform.localRotation = Quaternion.identity;
        }
    }
}
