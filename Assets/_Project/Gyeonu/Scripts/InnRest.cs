using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 주막 평상에서 「쉬기」 (2026-09-10). 문서 「3. 시간대 — 3단계」의 낮 → 초밤 → 늦은 밤을 플레이어가 넘기는 유일한 정상 경로다.
    ///
    /// <code>
    ///   낮    → 쉰다 → 초밤    (주모·아이들·상인은 아직 있다. 견우는 선아 집 마당·은하담으로)
    ///   초밤  → 쉰다 → 늦은 밤 (마을 NPC 퇴장. 관아 잠입 가능)
    ///   늦은 밤 → 쉰다 → 낮  (날이 밝는다. 놓친 낮의 일을 다음 날 다시 할 수 있다 — 침대에서 자는 것과 같다)
    /// </code>
    ///
    /// ■ 두 번 눌러 확인한다
    ///   시간대는 되돌릴 수 없으므로 첫 클릭은 "여기서 쉬면 날이 저물 것이다"만 띄우고, 잠시 안에 한 번 더 누를 때만 쉰다.
    ///   안내는 하단 고정 문구(DebugToast.ShowPinned)라 자리를 뜨면 사라지고, 확인도 함께 풀린다.
    ///
    /// ■ 주모가 없어도 쉴 수 있다
    ///   늦은 밤에는 주모가 퇴장하지만 평상은 남는다. 평상에 앉는 데 주모의 허락이 필요하지 않다.
    ///   ⚠️ 날씨는 여기서 손대지 않는다 — 비는 신뢰도 70에서 한 번 오고 엔딩까지 그대로다 (GyeonuCase.RestAtInn).
    ///
    /// ■ 씬에 없어도 스스로 붙는다
    ///   에디터 빌더(InnRestBuilder)가 평상 빈 모서리에 자리를 만들어 저장하지만, 빠져 있어도 마을 씬이 열릴 때 <see cref="Ensure"/> 가 만든다.
    ///   낮→밤을 넘길 길이 조용히 사라지면 1막이 통째로 막힌다.
    /// </summary>
    public class InnRest : Interactable
    {
        public const string SpotName = "주막_쉬는자리";
        /// <summary>주막 평상(Low_Wooden_Bench) 북서쪽 빈 모서리 — 상인이 앉는 동쪽과 술상을 피한 자리.</summary>
        public static readonly Vector3 SpotPosition = new Vector3(-3.4f, 0.62f, -16.0f);
        public static readonly Vector3 SpotSize = new Vector3(1.2f, 0.5f, 1.2f);

        [Header("문구")]
        [TextArea(2, 4)]
        public string confirmDay =
            "평상에 걸터앉았다. 여기서 쉬면 날이 저물 것이다.\n한 번 더 누르면 해가 질 때까지 쉰다.";
        [TextArea(2, 4)]
        public string confirmEvening =
            "평상에 걸터앉았다. 여기서 쉬면 밤이 깊어질 것이다. 주막도 문을 닫을 시각이다.\n한 번 더 누르면 밤이 깊을 때까지 쉰다.";
        [TextArea(2, 4)]
        public string confirmLate =
            "평상에 몸을 뉘었다. 여기서 쉬면 날이 밝을 것이다.\n한 번 더 누르면 아침까지 잔다.";
        [TextArea(2, 4)]
        public string afterDay =
            "해가 저물었다. 주막에 등불이 걸리고 마을이 어스름에 잠긴다.";
        [TextArea(2, 4)]
        public string afterEvening =
            "밤이 깊었다. 마을 사람들은 잠들었고 관아를 지키는 눈도 드물어졌다.";
        [TextArea(2, 4)]
        public string afterLate =
            "날이 밝았다. 마을이 다시 깨어나고 사람들이 하나둘 밖으로 나온다.";

        [Header("연출")]
        [Tooltip("첫 클릭 뒤 이 시간(초) 안에 다시 누르면 쉰다. 안내 두 줄을 읽고 결정할 여유를 준다")]
        public float confirmWindow = 15f;
        public float fadeOut = 0.7f;
        public float hold = 1.0f;
        public float fadeIn = 0.9f;

        float _armedUntil = -1f;
        bool _resting;
        static ScreenFader _fader;

        public override string Prompt => "쉬기";

        public override bool CanInteract(GameObject actor) => !_resting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Ensure();
        }

        static void OnSceneLoaded(Scene s, LoadSceneMode mode) => Ensure();

        /// <summary>마을 씬에 쉬는 자리가 없으면 만든다. 이미 있으면 아무 일도 없다.</summary>
        public static InnRest Ensure()
        {
            if (SceneManager.GetActiveScene().name != "Gyeonu") return null;
            var existing = FindFirstObjectByType<InnRest>(FindObjectsInactive.Include);
            if (existing != null) return existing;
            var go = GameObject.Find(SpotName);
            if (go == null)
            {
                go = new GameObject(SpotName);
                go.transform.position = SpotPosition;
            }
            var box = go.GetComponent<BoxCollider>();
            if (box == null) box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;            // 걷는 길을 막지 않는다 — 트리거 하나뿐인 부피는 조준면으로 잡힌다 (DebugInteractor)
            box.size = SpotSize;
            var rest = go.AddComponent<InnRest>();
            rest.displayName = "주막 평상";
            return rest;
        }

        public override void Interact(GameObject actor)
        {
            if (_resting) return;

            // 안내 문구가 아직 떠 있고(자리를 뜨면 사라진다) 시간 안이면 확인된 것으로 본다
            bool armed = Time.time <= _armedUntil && DebugToast.PinnedActive;
            if (!armed)
            {
                _armedUntil = Time.time + confirmWindow;
                DebugToast.ShowPinned(ConfirmText());
                return;
            }

            _armedUntil = -1f;
            StartCoroutine(Rest());
        }

        IEnumerator Rest()
        {
            _resting = true;
            DebugToast.HidePinned();
            var walk = FindFirstObjectByType<DebugWalkController>();
            if (walk != null) walk.enabled = false;

            var fader = Fader();
            yield return FadeTo(fader, 1f, fadeOut);

            var before = GyeonuCase.Time;
            GyeonuCase.RestAtInn();                       // 하늘·조명·NPC 일정은 GyeonuWorld.Changed 로 따라온다
            yield return new WaitForSeconds(hold);

            yield return FadeTo(fader, 0f, fadeIn);
            if (walk != null) walk.enabled = true;
            DebugToast.Show(AfterText(before), 5f);
            _resting = false;
        }

        string ConfirmText()
        {
            switch (GyeonuCase.Time)
            {
                case TimeOfDay.Day: return confirmDay;
                case TimeOfDay.EarlyNight: return confirmEvening;
                default: return confirmLate;
            }
        }

        string AfterText(TimeOfDay before)
        {
            switch (before)
            {
                case TimeOfDay.Day: return afterDay;
                case TimeOfDay.EarlyNight: return afterEvening;
                default: return afterLate;
            }
        }

        static IEnumerator FadeTo(ScreenFader f, float target, float seconds)
        {
            if (f == null) yield break;
            float from = f.Alpha, t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                f.SetAlpha(Mathf.Lerp(from, target, Mathf.Clamp01(t / seconds)));
                yield return null;
            }
            f.SetAlpha(target);
        }

        /// <summary>씬 전환이 쓰는 암전판이 있으면 그것을, 없으면 하나 만든다.</summary>
        static ScreenFader Fader()
        {
            if (_fader != null) return _fader;
            _fader = FindFirstObjectByType<ScreenFader>(FindObjectsInactive.Include);
            if (_fader != null) return _fader;
            var go = new GameObject("[쉬기_암전]");
            DontDestroyOnLoad(go);
            _fader = go.AddComponent<ScreenFader>();
            return _fader;
        }
    }
}
