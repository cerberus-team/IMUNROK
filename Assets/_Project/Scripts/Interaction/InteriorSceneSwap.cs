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
        [SerializeField] private string _sceneName = "";

        [Tooltip("실내에 들어설 때 켤 것 — 상자로 지은 사랑채 실내. 이걸 걸면 씬을 따로 안 불러온다")]
        [SerializeField] private GameObject _showWhileInside;

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
            // 실내가 같은 씬 안에 있으면 켜고 끄기만 하면 된다.
            //
            // 예전엔 실내를 따로 만든 씬으로 두고 겹쳐 불러왔다. 그럴 이유가 없어졌다 —
            // 실내는 8,844 삼각형뿐이라 꺼둔 채 들고 있어도 부담이 없고, 무거운 것은
            // 원본 사랑채(203,652)인데 그건 어느 쪽이든 들어갈 때 끈다. 화면에 그려지는
            // 양은 씬을 나누든 합치든 똑같다.
            //
            // 나눠 두면 오히려 손해가 있었다. 유니티는 씬을 건너뛰는 참조를 저장하지 못해
            // 방과 그 방에서 쓰는 표식·단서를 한 데 둘 수가 없었고, 불러오기가 비동기라
            // 복동이 앉는 동작이 화면 밝아진 뒤에야 시작되기도 했다.
            if (_showWhileInside != null)
            {
                _showWhileInside.SetActive(true);
                if (_hideWhileInside != null) _hideWhileInside.SetActive(false);
                _onReady?.Invoke();
                yield break;
            }

            if (string.IsNullOrEmpty(_sceneName)) { _onReady?.Invoke(); yield break; }

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
