using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common
{
    /// <summary>
    /// 실내로 들어가는 순간, 가볍게 지은 실내 씬을 불러오고 무거운 원본 건물을 끈다.
    ///
    /// 왜 필요한가: 받아온 고택은 실내에 들어가 있어도 화면 부담의 80%가 바깥채·담장이다.
    /// 사랑채 도착 지점에서 재보면 실내 것 129개 20만 삼각형에 실외 것 724개 85만 삼각형이
    /// 같이 그려졌다. 실내에 있는 동안은 실내만 그리면 퀘스트 예산 안에 들어온다.
    ///
    /// 원본 사랑채(20만 삼각형)를 끄고 상자로 지은 사랑채(9천 삼각형)를 대신 세운다.
    /// 자리와 치수를 원본에서 재서 지었으므로, 이미 놓아둔 보료·문갑·경상은 그대로 맞는다.
    ///
    /// 붙이는 법: 빈 오브젝트에 붙이고
    ///   · _sceneName 에 실내 씬 이름
    ///   · _hideWhileInside 에 원본 사랑채를 묶어 둔 오브젝트
    ///   · 중문 TeleportZone 의 _onTeleported 에 EnterInside() 를 건다
    ///
    /// 실내 씬에는 혼자 열어볼 때 쓰는 빛·카메라가 _미리보기 묶음으로 들어 있다.
    /// 본 씬과 겹치면 해가 둘이 되므로 불러오는 즉시 끈다.
    /// </summary>
    public class InteriorSceneSwap : MonoBehaviour
    {
        [Tooltip("불러올 실내 씬 이름. 빌드 설정에 들어 있어야 한다")]
        [SerializeField] private string _sceneName = "Onggojip_사랑채";

        [Tooltip("실내에 있는 동안 꺼둘 것 — 원본 사랑채를 묶어 둔 오브젝트")]
        [SerializeField] private GameObject _hideWhileInside;

        [Tooltip("실내 씬 안에서 꺼둘 묶음 이름(미리보기용 빛·카메라). 본 씬 조명을 쓰기 위해서다")]
        [SerializeField] private string _previewGroupName = "_미리보기";

        [Tooltip("불러오기가 끝난 뒤 실행")]
        [SerializeField] private UnityEngine.Events.UnityEvent _onReady;

        private bool _done;

        /// <summary>중문을 지나 사랑채로 들어선 순간. TeleportZone 의 _onTeleported 에 건다.</summary>
        public void EnterInside()
        {
            if (_done) return;
            _done = true;
            StartCoroutine(SwapRoutine());
        }

        private IEnumerator SwapRoutine()
        {
            if (!Application.CanStreamedLevelBeLoaded(_sceneName))
            {
                Debug.LogWarning($"[{name}] 실내 씬 '{_sceneName}' 을 빌드 설정에서 못 찾았습니다. " +
                                 "원본 사랑채를 그대로 씁니다.", this);
                yield break;
            }

            var op = SceneManager.LoadSceneAsync(_sceneName, LoadSceneMode.Additive);
            while (op != null && !op.isDone) yield return null;

            var scene = SceneManager.GetSceneByName(_sceneName);
            if (scene.IsValid())
                foreach (var root in scene.GetRootGameObjects())
                    if (root.name.StartsWith(_previewGroupName)) root.SetActive(false);   // 해가 둘이 되면 안 된다

            if (_hideWhileInside != null) _hideWhileInside.SetActive(false);

            _onReady?.Invoke();
        }
    }
}
