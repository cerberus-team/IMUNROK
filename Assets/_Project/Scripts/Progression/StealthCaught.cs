using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>붙들려 간다</b> — 밤에 세 번이나 사람을 부르면 1막이 거기서 끝난다.
    ///
    /// <b>왜 벌이 아니라 길인가.</b> 여태 <see cref="NoiseWarning"/> 도
    /// <see cref="ServantCheck"/> 도 「세 번이면 들킴」이라 적어 두고 그 뒤를 비워
    /// 두었다 — 세 번을 부르든 열 번을 부르든 아무 일도 안 났다. 그러면 소리를
    /// 조심하는 일이 <b>아무 값도 없는 일</b>이 된다.
    ///
    /// 그렇다고 「처음부터 다시」로 두면, 두 시간을 뒤진 사람이 발소리 한 번에
    /// 그것을 다 잃는다. 잠행은 실패해도 <b>이야기가 이어져야</b> 한다.
    ///
    /// 그래서 <b>끌려 나가는 것이 곧 2막의 문</b>이다. 필수 단서를 다 못 캤어도
    /// 넘어간다 — 다만 <b>빈손으로</b> 넘어간다. 못 캔 것은 장계의 빈칸으로 남고,
    /// 관아에서 맞는 낯도 달라진다(<see cref="GwanaUsher"/>): 기별을 받고 문을 열어
    /// 둔 관아가 아니라, <b>웬 사내가 끌려온</b> 관아다. 거기서 마패를 꺼내야
    /// 비로소 어사가 된다.
    ///
    /// 붙이는 곳: 1막 본 씬의 빈 오브젝트 하나.
    /// <c>NoiseWarning._onCaught</c> 와 <c>ServantCheck._onCaught</c> 를 여기
    /// <see cref="Caught"/> 에 물린다.
    /// </summary>
    public class StealthCaught : MonoBehaviour
    {
        [Header("무슨 일이 났는지")]
        [SerializeField] private string _speaker = "늙은 하인";
        [TextArea(2, 3)]
        [Tooltip("붙들리는 순간의 한 마디. *별표*로 감싼 낱말은 도드라진다")]
        [SerializeField] private string _line =
            "…나리, 이 방에 누가 있습니다. *등을 들여라*.";

        [TextArea(2, 3)]
        [Tooltip("그 뒤에 뜨는 한 마디. 밤이 여기서 끝났다는 것을 알린다")]
        [SerializeField] private string _after =
            "붙들려 뜰로 끌려 나왔다. *날이 밝는다*.";

        [Tooltip("첫 마디와 둘째 마디 사이(초)")]
        [SerializeField] private float _between = 3.2f;
        [Tooltip("둘째 마디 뒤 화면이 넘어가기까지(초)")]
        [SerializeField] private float _beforeLeave = 3.4f;

        [Header("어디로")]
        [Tooltip("넘어갈 씬. 빌드 목록에 있어야 한다")]
        [SerializeField] private string _nextScene = "Onggojip_Gwana";

        [SerializeField] private UnityEvent _onCaught;

        private bool _done;

        /// <summary>세 번째로 사람을 불렀다. 여기서 밤이 끝난다.</summary>
        public void Caught()
        {
            if (_done) return;
            _done = true;
            StartCoroutine(Escort());
        }

        private IEnumerator Escort()
        {
            // 이제 소리를 잴 일이 없다 — 이미 들켰다.
            NoiseMeter.Disarm();
            SceneMusic.DuckAll(0f, 0.3f);    // 소리계를 내리면 노래가 도로 차오른다 — 끌려가는 동안은 조용히

            var gs = GameState.Instance;
            if (gs != null)
            {
                gs.SetCaughtInStealth(true);
                // <b>신분은 아직 드러나지 않았다.</b> 마패를 꺼낸 적이 없으니
                // 여기서는 여전히 「나그네」다 — 관아에서 서리 앞에 꺼내야 어사가 된다.
                // 이 길로 왔다는 것 자체가 출도를 <b>안</b> 했다는 뜻이므로 못을 박아 둔다.
                gs.RevealIdentity(false);
            }

            if (!string.IsNullOrEmpty(_line)) SubtitleView.Show(_speaker, _line, "");
            yield return new WaitForSeconds(_between);

            if (!string.IsNullOrEmpty(_after)) SubtitleView.Show("", _after, "");
            _onCaught?.Invoke();
            yield return new WaitForSeconds(_beforeLeave);

            SubtitleView.Hide();
            if (!string.IsNullOrEmpty(_nextScene) &&
                Application.CanStreamedLevelBeLoaded(_nextScene))
            {
                ScreenFade.Blink(0.8f, 1.1f,
                    () => UnityEngine.SceneManagement.SceneManager.LoadScene(_nextScene));
            }
            else
            {
                Debug.LogWarning("[붙들림] 다음 씬 '" + _nextScene + "' 이 비었거나 빌드 목록에 없다.", this);
            }
        }
    }
}
