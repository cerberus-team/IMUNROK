using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>이 씬은 짜 둔 그림을 그대로 쓴다</b>는 표. 값이 없다 — 있느냐 없느냐가 전부다.
    ///
    /// 어전은 <b>연출</b>이다. 왕이 발 너머에서 어명을 내리고 봉서 셋이 어도를 따라
    /// 굴러 내려오는 그림은, 카메라가 어디를 보고 있느냐까지 정해 두고 짠 것이다.
    ///
    /// <b>2026-09-05 — 지금 이 표는 아무 일도 하지 않는다.</b> 이것을 읽던 것은
    /// 헤드셋 장비 하나뿐이었고, 그것을 걷어냈다. 헤드셋을 쓰면 카메라 자리가 사람의
    /// 목으로 넘어가 — 어명이 흐르는 동안 천장을 보고 있어도 게임이 아무 말을 못 하는 —
    /// 그 일을 막으려고 둔 표였는데, 막을 일 자체가 없어졌다.
    ///
    /// <b>그런데도 지우지 않았다.</b> 이 부품이 인트로씬 카메라에 실제로 얹혀 있어서,
    /// 스크립트만 지우면 씬에 <b>못 찾는 스크립트</b> 구멍이 남는다. 씬에서 부품을
    /// 먼저 떼고 나서 지워야 한다 — 유니티를 열고 할 일이라 여기서는 손대지 않는다.
    /// </summary>
    public class FixedView : MonoBehaviour
    {
        /// <summary>지금 씬이 고정 화면인가. 지금은 읽는 사람이 없다.</summary>
        public static bool Here
        {
            get
            {
                return FindFirstObjectByType<FixedView>(FindObjectsInactive.Exclude) != null;
            }
        }
    }
}
