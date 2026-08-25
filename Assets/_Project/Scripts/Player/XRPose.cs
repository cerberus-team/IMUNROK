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

        private void Apply()
        {
            var dev = InputDevices.GetDeviceAtXRNode(_node);
            if (!dev.isValid) { Tracked = false; return; }

            bool got = false;
            if (dev.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rot))
            {
                transform.localRotation = rot;
                got = true;
            }
            if (_position && dev.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 pos))
            {
                transform.localPosition = pos;
                got = true;
            }
            Tracked = got;
        }

        /// <summary>아직 안 잡히는 동안 놓아 둘 자리로 물린다.</summary>
        public void Rest()
        {
            if (Tracked) return;
            transform.localPosition = _restPosition;
            transform.localRotation = Quaternion.identity;
        }
    }
}
