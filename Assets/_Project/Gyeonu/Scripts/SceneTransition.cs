using System.Collections;
using IMUNROK.Common;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 씬 전환의 단일 진입점 — 암전 → 로드 → 스폰 배치 → 밝힘.
    ///
    /// 쓰는 법(어디서든):
    ///   SceneTransition.Go("Gyeonu_EunhaDam", "SpawnPoint_FromVillage");
    ///
    /// ■ 왜 러너가 DontDestroyOnLoad인가
    ///   전환을 시작한 오브젝트(문·트리거)는 씬과 함께 파괴된다. 코루틴을 그 위에서 돌리면
    ///   LoadScene 순간 중단되어 "암전된 채로 멈춤"이 된다. 그래서 씬에 속하지 않는
    ///   러너를 하나 만들어 그쪽에서 돌린다. 암전 쿼드도 이 러너가 들고 있다.
    ///
    /// ■ 도착 위치를 마커 이름으로 찾는 이유
    ///   씬끼리 월드 좌표를 맞출 필요가 없어진다(관측실·집무실·선아집은 각각 원점 기준
    ///   독립 좌표계다). 출구는 "어느 씬의 어느 스폰"만 알면 되고, 스폰을 옮기면
    ///   전환도 따라온다.
    ///
    /// ■ 도착 방향
    ///   스폰 마커의 yaw를 그대로 쓴다. 들어온 방향을 바라보게 마커를 놓아야
    ///   도착하자마자 뒤돌아 서 있는 어색함이 없다. 시선 핏치는 0으로 편다.
    ///
    /// ■ 공통 시스템(GameState) 연동
    ///   사건 씬으로 들어가면 StartCase + EnterCase를, 조사청으로 나가면 ExitToHub를
    ///   보장한다. 씬 단독 Play로 시작해도 수첩이 이 사건 단서를 보여주게 하는 목적이다.
    ///   (GameState·Journal 둘 다 Instance 접근 시 자동 생성이라 null 참조는 나지 않는다.)
    /// </summary>
    [AddComponentMenu("")]
    public class SceneTransition : MonoBehaviour
    {
        public const string HubSceneName = "HubScene";

        /// <summary>디버그 워커를 스폰 마커 위에 얹을 때 주는 여유 — 워커 설치 스크립트들과 같은 값.</summary>
        const float SpawnLift = 0.1f;

        static SceneTransition _runner;
        static ScreenFader _fader;

        /// <summary>전환이 진행 중인가. 출구들이 중복 발동을 막는 데 쓴다.</summary>
        public static bool IsTransitioning { get; private set; }

        /// <summary>마지막 전환으로 도착한 시각(Time.time). 도착 직후 재발동 방지에 쓴다.</summary>
        public static float LastArrivalTime { get; private set; } = -999f;

        /// <summary>
        /// 플레이어를 도착 스폰에 세운 직후 발생. 출구들이 "지금 내 볼륨 안에서 시작했는가"를
        /// 다시 판정하는 신호다.
        ///
        /// 왜 필요한가: 씬이 로드되면 출구의 Start()가 먼저 돌고, 플레이어 배치는 그 다음 프레임이다.
        /// 그래서 Start() 시점의 플레이어 위치는 **아직 옮겨지기 전**(씬에 저장된 기본 자리)이라
        /// 볼륨 판정이 무의미하다. 배치가 끝난 뒤 다시 물어봐야 맞다.
        /// </summary>
        public static event System.Action PlayerPlaced;

        /// <summary>
        /// 씬을 전환한다. 이미 전환 중이면 무시한다.
        /// </summary>
        /// <param name="sceneName">목적지 씬 이름(Build Settings에 등록돼 있어야 한다)</param>
        /// <param name="spawnName">도착 씬에서 플레이어를 세울 마커 이름. 비우면 씬 기본 위치.</param>
        public static void Go(string sceneName, string spawnName,
                              float fadeOut = 0.45f, float fadeIn = 0.55f)
        {
            if (IsTransitioning)
            {
                Debug.Log("[씬전환] 이미 전환 중 — 무시: " + sceneName);
                return;
            }
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("[씬전환] 목적지 씬 이름이 비어 있다.");
                return;
            }
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[씬전환] '{sceneName}' 을 로드할 수 없다 — Build Settings에 등록됐는지 확인할 것.");
                return;
            }

            EnsureRunner();
            _runner.StartCoroutine(_runner.Run(sceneName, spawnName, fadeOut, fadeIn));
        }

        static void EnsureRunner()
        {
            if (_runner != null) return;
            var go = new GameObject("[씬전환]");
            DontDestroyOnLoad(go);
            _runner = go.AddComponent<SceneTransition>();
            _fader = go.AddComponent<ScreenFader>();
        }

        IEnumerator Run(string sceneName, string spawnName, float fadeOut, float fadeIn)
        {
            IsTransitioning = true;

            yield return Fade(0f, 1f, fadeOut);

            var op = SceneManager.LoadSceneAsync(sceneName);
            while (op != null && !op.isDone) yield return null;

            // 새 씬의 Awake/Start가 한 번 돌게 두고 나서 배치한다
            // (워커·카메라가 아직 없는 상태에서 찾으면 놓친다).
            yield return null;

            SyncCaseState(sceneName);

            // 초밤에 씬을 3번 옮기면 늦은 밤이 된다 (문서 「3. 초밤 → 늦은 밤 전환」 보조 경로).
            // 정규 경로는 견우마을 귀환(B5)이고, 그쪽은 단서 획득이 알아서 처리한다.
            GyeonuCase.NotifySceneTransition();

            // ⚠️ 배치보다 **먼저** 찍는다. 배치 직후 프레임에 물리가 트리거를 잡는데,
            //    그때 도착 시각이 아직 이전 값이면 armDelay가 통과되어 곧바로 되돌아간다
            //    (도착 스폰이 복귀 트리거에 닿아 있으면 무한 핑퐁, 2026-08-17).
            LastArrivalTime = Time.time;
            PlacePlayer(spawnName, sceneName);
            PlayerPlaced?.Invoke();

            // 배치 직후 한 프레임 더 — 카메라가 새 위치로 옮겨진 뒤에 밝혀야
            // 밝히는 첫 프레임에 이전 위치가 스치지 않는다.
            yield return null;

            LastArrivalTime = Time.time;
            yield return Fade(1f, 0f, fadeIn);

            IsTransitioning = false;
        }

        IEnumerator Fade(float from, float to, float duration)
        {
            if (_fader == null) yield break;

            if (duration <= 0f) { _fader.SetAlpha(to); yield break; }

            float t = 0f;
            _fader.SetAlpha(from);
            while (t < duration)
            {
                // 씬 로드 직후 Time.deltaTime이 크게 튀는 프레임이 있어 상한을 건다.
                t += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                float k = Mathf.Clamp01(t / duration);
                _fader.SetAlpha(Mathf.Lerp(from, to, k * k * (3f - 2f * k)));
                yield return null;
            }
            _fader.SetAlpha(to);
        }

        /// <summary>도착 씬에서 스폰 마커를 찾아 플레이어를 세운다.</summary>
        static void PlacePlayer(string spawnName, string sceneName)
        {
            if (string.IsNullOrEmpty(spawnName)) return;

            var marker = GameObject.Find(spawnName);
            if (marker == null)
            {
                Debug.LogWarning($"[씬전환] '{sceneName}' 에 스폰 마커 '{spawnName}' 가 없다 — 씬 기본 위치를 쓴다.");
                return;
            }

            Vector3 pos = marker.transform.position + Vector3.up * SpawnLift;
            Quaternion rot = Quaternion.Euler(0f, marker.transform.eulerAngles.y, 0f);

            var walker = FindFirstObjectByType<DebugWalkController>(FindObjectsInactive.Exclude);
            if (walker != null)
            {
                // CharacterController가 켜져 있으면 위치 대입이 무시된다(내부 위치를 따로 들고 있다).
                var cc = walker.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                walker.transform.SetPositionAndRotation(pos, rot);
                walker.Pitch = 0f;                       // 시선을 수평으로 편다
                if (cc != null) cc.enabled = true;
                Debug.Log($"[씬전환] {sceneName} / {spawnName} 도착 — {pos.ToString("F2")} yaw {rot.eulerAngles.y:F0}");
                return;
            }

            // 워커가 없는 씬(추후 VR 리그) — 카메라 리그의 최상위를 옮긴다.
            var cam = Camera.main;
            if (cam != null)
            {
                var root = cam.transform.root;
                root.SetPositionAndRotation(pos, rot);
                Debug.Log($"[씬전환] {sceneName} / {spawnName} 도착 (카메라 리그) — {pos.ToString("F2")}");
                return;
            }

            Debug.LogWarning($"[씬전환] '{sceneName}' 에 옮길 플레이어(워커/카메라)를 찾지 못했다.");
        }

        /// <summary>사건 진입/이탈을 공통 상태에 반영한다.</summary>
        static void SyncCaseState(string sceneName)
        {
            var gs = GameState.Instance;   // 없으면 자동 생성된다

            if (sceneName == HubSceneName)
            {
                gs.ExitToHub();
                return;
            }

            // 제3사건 씬은 전부 "Gyeonu" 로 시작한다 (씬 이름 규약)
            if (!sceneName.StartsWith("Gyeonu")) return;

            if (gs.CurrentCase != CaseId.Case3_Gyeonu)
            {
                gs.StartCase(CaseId.Case3_Gyeonu);
                gs.EnterCase(CaseId.Case3_Gyeonu);
            }
        }
    }
}
