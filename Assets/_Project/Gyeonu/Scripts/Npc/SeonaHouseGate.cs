using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 선아 집 문의 잠금 (2026-09-10). 문서 「제1부·1막」의 "견우 신뢰도 40 → 열쇠 → 선아 집" 게이트가 이 부품이다.
    ///
    /// ■ 왜 진행 조건(requiredFlags)이 아니라 잠금(locked)인가
    ///   <see cref="OfficeDayGate"/> 와 같은 이유다 — 잠긴 문은 조준 문구부터 '살펴보기'로 바뀌어야
    ///   들어갈 수 없다는 것이 누르기 전에 드러난다. 조건 문구("{0}이 없다")는 물건이 모자란다는 안내라
    ///   빗장이 걸린 문에는 맞지 않는다.
    ///
    /// ■ 열쇠는 둘 중 하나면 된다
    ///   실물 소지품(SEONA_HOUSE_KEY → <see cref="GyeonuWorld.F_선아집열쇠"/>)이 정식 경로이고,
    ///   디버그 창의 「선아 집 열쇠」 토글(<see cref="GyeonuCase.HasSeonaHouseKey"/>)도 같은 플래그를 세운다.
    ///
    /// ■ 씬에 없어도 스스로 붙는다
    ///   에디터 빌더(SeonaHouseKeyBuilder)가 '출구_선아집'에 붙여 저장하지만, 혹시 빠져 있어도
    ///   마을 씬이 열릴 때 <see cref="Ensure"/> 가 한 번 붙인다 — 잠금이 조용히 사라지면 게이트가 무의미해진다.
    /// </summary>
    [RequireComponent(typeof(SceneExit))]
    public class SeonaHouseGate : MonoBehaviour
    {
        /// <summary>마을 씬에서 선아 집으로 들어가는 출구 오브젝트 이름.</summary>
        public const string ExitName = "출구_선아집";

        [TextArea(2, 5)]
        public string lockedMessage =
            "문에 빗장이 단단히 걸려 있다. 밀어도 꿈쩍하지 않는다.\n" +
            "이 집 열쇠를 지닌 사람이 마을 어딘가에 있을 것이다.";

        [Tooltip("열쇠가 있을 때의 조준 문구")]
        public string openPrompt = "들어가기";

        SceneExit exit;
        bool _appliedLocked;
        bool _first = true;

        static bool HasKey =>
            GyeonuCase.HasSeonaHouseKey
            || GyeonuWorld.Has(GyeonuWorld.F_선아집열쇠)
            || GyeonuWorld.DebugIgnoreConditions;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Ensure();
        }

        static void OnSceneLoaded(Scene s, LoadSceneMode mode) => Ensure();

        /// <summary>마을 씬의 '출구_선아집'에 없으면 붙인다. 이미 있으면 아무 일도 없다.</summary>
        public static SeonaHouseGate Ensure()
        {
            var go = GameObject.Find(ExitName);
            if (go == null || go.GetComponent<SceneExit>() == null) return null;
            var g = go.GetComponent<SeonaHouseGate>();
            return g != null ? g : go.AddComponent<SeonaHouseGate>();
        }

        void Awake() { exit = GetComponent<SceneExit>(); }

        void Update()
        {
            bool locked = !HasKey;
            if (!_first && locked == _appliedLocked) return;
            _first = false;
            _appliedLocked = locked;

            exit.locked = locked;
            if (locked) exit.lockedMessage = lockedMessage;
            else exit.promptText = openPrompt;
        }
    }
}
