using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common
{
    /// <summary>
    /// 저절로 저장한다 — <b>이어하기가 있으려면 저장이 있어야 한다</b>.
    ///
    /// <see cref="SaveSystem"/> 도 <see cref="TitleGate"/> 의 이어하기도 진작 있었는데,
    /// 정작 <c>Save()</c> 를 부르는 데가 디버그 키(F5) 하나뿐이었다. 그래서 저장 파일이
    /// 생기지 않고, 파일이 없으니 표제에 이어하기가 <b>한 번도 뜬 적이 없다</b>.
    /// 있는 기능이 아무도 부르지 않아 죽어 있었던 것이다.
    ///
    /// <b>언제 저장하나</b>
    ///   · 물증을 하나 주울 때마다 — 이 게임에서 진행이란 곧 수첩에 적힌 것이다.
    ///   · 사건에 들어설 때·판결을 내릴 때(<see cref="GameState.OnCaseChanged"/>).
    ///   · 조사청에 들어설 때.
    ///   · 앱을 끄거나 헤드셋을 벗을 때 — 퀘스트에서는 이쪽이 진짜 종료다.
    ///
    /// <b>표제·어전에서는 저장하지 않는다</b>(<see cref="_armed"/>). 아직 아무 일도
    /// 안 한 사람에게 저장 파일이 생기면, 다음에 켰을 때 "하던 데부터"가 떠 있는데
    /// 눌러 봐야 아무것도 하지 않은 조사청이다. 조사청에 처음 들어선 순간부터 센다 —
    /// 어명을 듣고 봉서를 받은 뒤라야 이어할 것이 생긴다.
    ///
    /// <b>몰아서 쓴다</b>: 단서 하나에 한 번씩 곧이곧대로 쓰면 파일을 자주 건드린다.
    /// 적을 것이 생겼다고 표시만 해 두고 <see cref="Cooldown"/> 마다 한 번 쓴다.
    ///
    /// 씬에 둘 필요 없다. 게임이 시작되면 스스로 붙는다.
    /// </summary>
    public class Autosave : MonoBehaviour
    {
        /// <summary>이만큼(초)에 한 번만 쓴다.</summary>
        private const float Cooldown = 2f;

        /// <summary>여기 들어서면 저장을 시작한다.</summary>
        private const string HubScene = "HubScene";

        private static Autosave _instance;

        private bool _armed;
        private bool _dirty;
        private float _wait;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_instance != null) return;
            var go = new GameObject("[자동저장]");
            _instance = go.AddComponent<Autosave>();
            DontDestroyOnLoad(go);
        }

        // 구독해 둔 상대를 <b>들고 있는다</b>.
        //
        // 뗄 때 Journal.Instance 를 다시 물어보고 있었는데, 그 물음이 문제였다 —
        // 씬을 닫는 중에 이미 사라진 뒤라면 그 게터가 <b>떼기 위해 하나를 새로
        // 만든다</b>. 정리가 끝난 뒤에 태어난 것이라 치울 사람이 없다:
        // "Some objects were not cleaned up when closing the scene ... [Journal]"
        // 이 그 소리였다. 붙을 때 잡아 둔 그것에서 떼면 새로 만들 일이 없다.
        private Journal _journal;
        private GameState _state;

        private void OnEnable()
        {
            _journal = Journal.Instance;
            _state = GameState.Instance;
            if (_journal != null) _journal.OnClueAdded += OnClue;
            if (_state != null) _state.OnCaseChanged += OnCase;
            SceneManager.sceneLoaded += OnScene;

            // 저장을 불러와 들어온 판이면 이미 이어할 것이 있다.
            if (!_armed && Progressed()) _armed = true;
        }

        private void OnDisable()
        {
            if (_journal != null) _journal.OnClueAdded -= OnClue;
            if (_state != null) _state.OnCaseChanged -= OnCase;
            _journal = null;
            _state = null;
            SceneManager.sceneLoaded -= OnScene;
        }

        private void OnClue(ClueEntry _) => Mark();

        private void OnCase(CaseId _) { if (Progressed()) _armed = true; Mark(); }

        private void OnScene(Scene s, LoadSceneMode m)
        {
            if (s.name != HubScene) return;
            _armed = true;
            Mark();
        }

        /// <summary>사건에 손을 댄 적이 있나. 아직이면 저장할 것도 없다.</summary>
        private static bool Progressed()
        {
            foreach (CaseId id in System.Enum.GetValues(typeof(CaseId)))
                if (GameState.Instance.GetStatus(id) != CaseStatus.NotStarted) return true;
            return false;
        }

        private void Mark()
        {
            if (!_armed) return;
            _dirty = true;
        }

        private void Update()
        {
            if (!_dirty) return;
            _wait -= Time.unscaledDeltaTime;
            if (_wait > 0f) return;
            Flush();
        }

        private void Flush()
        {
            _dirty = false;
            _wait = Cooldown;
            SaveSystem.Save();
        }

        private void OnApplicationQuit() { if (_dirty) Flush(); }

        /// <summary>헤드셋을 벗으면 여기로 온다. 퀘스트에서는 이쪽이 진짜 종료다.</summary>
        private void OnApplicationPause(bool paused) { if (paused && _dirty) Flush(); }

        /// <summary>
        /// 처음부터 하는 사람을 위해 지금 판을 비운다. 파일은 지우지 않는다 —
        /// 새 판이 나아가면 그때 덮어쓴다. 잘못 눌러 지난 판이 사라지는 일이 없어야 한다.
        /// </summary>
        public static void BeginNewGame()
        {
            GameState.Instance.ResetAll();
            Journal.Instance.ClearAll();
            if (_instance != null) { _instance._armed = false; _instance._dirty = false; }
            Debug.Log("[자동저장] 처음부터 — 지금 판을 비웠습니다(저장 파일은 그대로).");
        }
    }
}
