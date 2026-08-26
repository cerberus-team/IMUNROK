using UnityEngine;

namespace IMUNROK.Ui
{
    /// <summary>
    /// 상호작용 대상의 공통 베이스 (동작 측).
    /// 입력 측(DebugInteractor — 임시 마우스 / 추후 VR 컨트롤러·공통 인터랙션 시스템)은
    /// 이 인터페이스만 호출한다 — 입력 방식을 갈아끼워도 대상 코드는 무수정.
    /// 공통 담당자의 인터랙션 시스템이 생기면 그 어댑터가 Interact()를 호출하면 된다.
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        [Tooltip("조준 시 표시되는 이름")]
        public string displayName = "상호작용";

        /// <summary>현재 상태에 맞는 행동 문구 (예: 열기/닫기).</summary>
        public virtual string Prompt => "사용";

        public virtual bool CanInteract(GameObject actor) => true;

        /// <summary>입력 측이 호출하는 단일 진입점.</summary>
        public abstract void Interact(GameObject actor);
    }
}
