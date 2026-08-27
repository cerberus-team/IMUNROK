using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 석축 암문 — 평소에는 그냥 돌벽이고, 열리면 안으로 드는 통로가 드러난다.
    ///
    /// ■ 왜 LockedDoor를 쓰지 않는가
    ///   LockedDoor는 경첩으로 도는 목문이다. 이쪽은 **석판이 안쪽으로 밀려 들어갔다가 옆으로
    ///   미끄러지는** 움직임이라야 석축에 난 문처럼 보인다. 잠금·상태 유지 구조는 같은 방식
    ///   (GyeonuWorld 키)을 쓰되 회전 대신 이동을 한다.
    ///
    /// ■ 조건은 나중에 붙는다
    ///   <see cref="requiredFlag"/> 를 비워 두면 클릭만으로 열린다(지금 상태). 퍼즐이 생기면
    ///   그 키를 넣기만 하면 "퍼즐을 풀어야 열리는 문"이 된다 — 코드는 손댈 필요가 없다.
    /// </summary>
    [AddComponentMenu("이문록/석축 암문 (SecretStoneDoor)")]
    [DisallowMultipleComponent]
    public class SecretStoneDoor : Interactable
    {
        [Header("움직이는 석판")]
        [Tooltip("열릴 때 밀려나는 문짝")]
        public Transform leaf;
        [Tooltip("안으로 밀리는 깊이(m)")]
        public float pushDepth = 0.35f;
        [Tooltip("안으로 밀리는 방향 (로컬). 남향 문이면 +Z, 서향 문이면 +X")]
        public Vector3 pushAxis = Vector3.forward;
        [Tooltip("옆으로 미끄러지는 거리(m)")]
        public float slideDistance = 1.15f;
        [Tooltip("미끄러지는 방향 (로컬)")]
        public Vector3 slideAxis = Vector3.right;
        public float duration = 2.2f;

        [Header("열린 뒤 드러나는 것")]
        [Tooltip("문이 열려야 보이는 통로 속 (평소 꺼 둔다)")]
        public GameObject revealed;

        [Header("조건 ① 단서 (비우면 누구나 알아본다)")]
        public string requiredFlag = "";
        [TextArea] public string lockedMessage = "돌 틈에 손을 대 보지만 꿈쩍도 하지 않는다.";

        [Header("조건 ② 밤에만 열린다 (2026-08-19)")]
        [Tooltip("켜면 낮에는 단서가 있어도 열리지 않는다 — 비밀 통로는 인적이 끊긴 밤에만 쓴다")]
        public bool nightOnly = true;
        [TextArea] public string dayMessage = "다리 위로 오가는 인기척이 잦다. 남의 눈이 없는 밤에 다시 와야겠다.";

        [Header("조건 ③ 퍼즐 (자리만 잡아 둠 — 비우면 통과)")]
        [Tooltip("퍼즐을 풀면 세워질 GyeonuWorld 키. **비워 두면 퍼즐이 없는 것으로 보고 그냥 열린다.**\n" +
                 "실제 퍼즐이 생기면 여기에 키 이름을 넣고, 퍼즐 쪽에서 SolvePuzzle() 또는 " +
                 "GyeonuWorld.Set(그 키) 한 줄만 부르면 된다 — 이 스크립트는 손댈 필요가 없다.")]
        public string puzzleFlag = "";
        [TextArea] public string puzzleMessage = "돌에 손을 얹었지만 무엇을 어떻게 눌러야 할지 모르겠다.";

        [Tooltip("실제 퍼즐 컴포넌트 (IDoorPuzzle 구현 — 예: AmmunLockPuzzle).\n" +
                 "꽂혀 있으면 조건(단서·밤)을 다 갖췄을 때 **문이 열리는 대신 퍼즐이 시작된다.**\n" +
                 "비워 두면 예전처럼 puzzleMessage만 뜬다.")]
        public MonoBehaviour puzzle;

        IDoorPuzzle Puzzle => puzzle as IDoorPuzzle;

        [Header("문구")]
        [TextArea] public string openMessage = "돌이 안으로 밀리더니 옆으로 미끄러진다. 어둠 속으로 계단이 이어진다.";

        [Header("커서 문구")]
        [Tooltip("단서가 없을 때 — 문인 줄 모르므로 '조사하기'")]
        public string promptUnknown = "조사하기";
        [Tooltip("단서가 있을 때 — 문인 줄 알므로 '문 열기'")]
        public string promptKnown = "문 열기";
        [Tooltip("이미 열린 뒤")]
        public string promptOpened = "들어가기";
        [Tooltip("퍼즐이 꽂혀 있고 아직 못 풀었을 때 — 눌러도 열리지 않으므로 '문 열기'라고 하면 안 된다")]
        public string promptPuzzle = "살펴보기";

        [Header("상태 유지")]
        [Tooltip("열린 상태를 기억할 GyeonuWorld 키")]
        public string openKey = "";

        Vector3 _closedLocal;
        float _t;
        bool _open;

        /// <summary>단서를 얻어 "여기가 문"이라는 걸 아는 상태인가.</summary>
        public bool Known => string.IsNullOrEmpty(requiredFlag)
                             || GyeonuWorld.Has(requiredFlag)
                             || GyeonuWorld.DebugIgnoreConditions;

        /// <summary>퍼즐이 풀렸는가. puzzleFlag가 비어 있으면 퍼즐 자체가 없는 것으로 본다.</summary>
        public bool PuzzleSolved => string.IsNullOrEmpty(puzzleFlag)
                                    || GyeonuWorld.Has(puzzleFlag)
                                    || GyeonuWorld.DebugIgnoreConditions;

        // 커서 문구는 **시간대와 무관**하다 (2026-08-20 사용자 지정).
        // 낮이라 못 여는 것은 눌러 봐야 아는 정보이므로 조준 단계에서 미리 알려 주지 않는다.
        public override string Prompt =>
            _open ? promptOpened
          : !Known ? promptUnknown
          : (Puzzle != null && !PuzzleSolved) ? promptPuzzle
          : promptKnown;
        public bool IsOpen => _open;

        /// <summary>퍼즐 쪽이 부를 진입점. 퍼즐이 없으면(플래그 비었으면) 할 일이 없다.</summary>
        public void SolvePuzzle()
        {
            if (!string.IsNullOrEmpty(puzzleFlag)) GyeonuWorld.Set(puzzleFlag);
        }

        void Awake()
        {
            if (leaf != null) _closedLocal = leaf.localPosition;

            if (!string.IsNullOrEmpty(openKey) && GyeonuWorld.Has(openKey))
            {
                _open = true;
                _t = 1f;
                Apply(1f);
            }
            else if (revealed != null) revealed.SetActive(false);
        }

        public override void Interact(GameObject actor)
        {
            if (_open) return;   // 이미 열렸으면 전환은 SceneExit이 맡는다

            // 우선순위 (2026-08-20 재정렬): ① 단서 → ② 밤 → ③ 퍼즐 → 열림
            //   단서를 **먼저** 본다. 단서가 없으면 애초에 문인 줄 모르므로, 낮이든 밤이든
            //   "그냥 석축"이라는 같은 문구가 나와야 한다. 낮/밤 문구를 먼저 걸면
            //   아무것도 모르는 플레이어에게 "밤에 다시 오라"는 힌트를 흘리게 된다.
            if (!Known)
            {
                DebugToast.ShowPinned(lockedMessage);
                return;
            }

            if (nightOnly && !GyeonuWorld.Night && !GyeonuWorld.DebugIgnoreConditions)
            {
                DebugToast.ShowPinned(dayMessage);
                return;
            }

            // ③ 퍼즐 — 조건을 다 갖췄으면 문이 열리는 게 아니라 **퍼즐이 시작된다** (2026-08-23).
            //    한 번 풀면 puzzleFlag가 세션에 남으므로 다음부터는 여기를 그냥 지나 곧장 열린다.
            if (!PuzzleSolved)
            {
                if (Puzzle != null) { Puzzle.BeginPuzzle(actor); return; }
                DebugToast.ShowPinned(puzzleMessage);
                return;
            }

            Open();
        }

        /// <summary>실제 개방 — 퍼즐이 풀렸을 때 퍼즐 쪽에서도 부른다.</summary>
        public void Open()
        {
            if (_open) return;
            _open = true;
            GyeonuWorld.Set(openKey);
            if (revealed != null) revealed.SetActive(true);
            if (!string.IsNullOrEmpty(openMessage)) DebugToast.ShowPinned(openMessage);
        }

        void Update()
        {
            float target = _open ? 1f : 0f;
            if (Mathf.Approximately(_t, target)) return;
            _t = Mathf.MoveTowards(_t, target, Time.deltaTime / Mathf.Max(0.05f, duration));
            Apply(_t);
        }

        // 두 동작의 구간. **살짝 겹쳐 둔다** — 겹치지 않으면 밀림이 끝나는 순간 속도가 0으로
        // 뚝 떨어졌다가 미끄러짐이 0에서 다시 붙어, 문이 한 번 멈췄다 가는 것처럼 보인다.
        // 겹쳐 두면 물러나던 돌이 그대로 옆으로 흘러 하나의 동작으로 읽힌다 (2026-08-20).
        const float PushEnd    = 0.32f;   // 안으로 밀림이 끝나는 진행도
        const float SlideStart = 0.24f;   // 옆으로 미끄러짐이 시작되는 진행도 (PushEnd보다 앞)

        /// <summary>0=닫힘, 1=완전히 열림.
        /// 앞 구간은 안으로 밀고(감속하며 슬롯 안쪽에 닿듯), 그와 겹쳐 옆으로 미끄러진다.</summary>
        void Apply(float t)
        {
            if (leaf == null) return;
            float push = Mathf.Clamp01(t / PushEnd);
            push = 1f - (1f - push) * (1f - push);                          // 감속 (ease-out)
            float slide = Mathf.Clamp01((t - SlideStart) / (1f - SlideStart));
            slide = slide * slide * (3f - 2f * slide);                      // 가속 후 감속 (smoothstep)
            leaf.localPosition = _closedLocal
                               + (pushAxis.sqrMagnitude < 0.0001f ? Vector3.forward : pushAxis.normalized) * (pushDepth * push)
                               + slideAxis.normalized * (slideDistance * slide);
        }
    }
}
