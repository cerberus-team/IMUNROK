// NPC 오브젝트에 붙이고 공통 심문 캐릭터 에셋을 지정해 E키 심문 요청 연결점으로 사용합니다.
using System;
using IMUNROK.Common;
using UnityEngine;

namespace IMUNROK.Seocheon
{
    public sealed class SeocheonNpc : MonoBehaviour
    {
        [Tooltip("공통 심문 시스템에 전달할 캐릭터 데이터")]
        [SerializeField] private InterrogationCharacter character;
        [Tooltip("E키 입력을 이 컴포넌트가 직접 감지할지 여부")]
        [SerializeField] private bool listenForInteract;

        public InterrogationCharacter Character => character;
        public event Action<InterrogationCharacter> InterrogationRequested;

        private void Update()
        {
            if (listenForInteract && SeocheonInput.InteractPressedThisFrame)
                RequestInterrogation();
        }

        public void RequestInterrogation()
        {
            if (character == null)
            {
                Debug.LogWarning($"[SeocheonNpc] {name}에 심문 캐릭터가 지정되지 않았습니다.", this);
                return;
            }

            // TODO(Common): 공통 심문 시스템의 외부 시작 API가 제공되면 여기서 character를 전달합니다.
            InterrogationRequested?.Invoke(character);
        }
    }
}
