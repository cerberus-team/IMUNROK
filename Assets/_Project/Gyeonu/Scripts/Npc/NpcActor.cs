using System;
using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// NPC 한 명의 몸짓 (2026-08-25). 최종기획안 11차 「23. 모션 운용 원칙」을 그대로 옮긴 것이다.
    ///
    /// ■ 왜 애니메이터 파라미터가 아니라 여기서 고르는가
    ///   기획의 규칙은 "무엇을 재생하는가"가 아니라 <b>"무엇이 무엇을 밀어내는가"</b>다.
    ///   전이 조건(파라미터)으로 짜면 그 우선순위가 11개의 애니메이터 그래프에 흩어져
    ///   "GiveKey 중에 LookAround가 끼어들었다" 같은 사고를 그래프를 열어 봐야 알 수 있다.
    ///   그래서 그래프는 <b>상태만 늘어놓은 평평한 판</b>으로 두고(<see cref="EditorTools.NpcRig"/>),
    ///   고르는 일은 전부 이 한 파일이 한다. 규칙이 한 곳에 있으면 한 곳만 고치면 된다.
    ///
    /// ■ 우선순위 6단계 (문서 「23. 모션 우선순위」)
    ///     1 스토리   GiveKey · StandingUp · Secret · Thank
    ///     2 대화     SitTalk · StandTalk · Talk · SitGun 조건 대사
    ///     3 이동     Walk
    ///     4 상태전환 SitToStand · StandToSit
    ///     5 랜덤     LookAround · Jump · Clap · SitGun 랜덤
    ///     6 Idle
    ///   숫자가 <b>작을수록</b> 세다. 위가 돌면 아래는 요청 자체가 버려진다(줄 서지 않는다) —
    ///   3초 뒤에 뒤늦게 튀어나오는 손짓이 제일 어색하기 때문이다.
    ///
    /// ■ 클립 길이를 왜 들고 다니는가
    ///   런타임의 <see cref="RuntimeAnimatorController.animationClips"/> 는 이름이
    ///   <c>Gyeonu_Meshy_SmartRig|Gyeonu_Idle</c> 처럼 FBX 안 이름이라 상태 이름(<c>Idle</c>)과
    ///   맞지 않는다. 매 프레임 대조하느니 설치할 때 표를 구워 둔다 —
    ///   <see cref="EditorTools.NpcRig"/> 가 <see cref="motions"/> 를 채운다.
    /// </summary>
    [DisallowMultipleComponent]
    public class NpcActor : MonoBehaviour
    {
        /// <summary>낮을수록 세다. 문서 「23. 모션 우선순위」의 1~6 그대로.</summary>
        public enum Pri
        {
            Story = 1,
            Talk = 2,
            Move = 3,
            Transition = 4,
            Random = 5,
            Idle = 6,
        }

        /// <summary>애니메이터 상태 하나. 설치할 때 구워 둔다.</summary>
        [Serializable]
        public class Motion
        {
            public string state = "";
            public float length = 1f;
            [Tooltip("켜져 있으면 스스로 되돌아 도는 상태다 — 끝을 기다리지 않는다")]
            public bool loops = false;
        }

        [Header("연결")]
        public NpcProfile profile;
        public Animator animator;

        [Tooltip("설치할 때 구운 상태 표. 손으로 고칠 일은 없다")]
        public Motion[] motions = Array.Empty<Motion>();

        [Header("평상시 랜덤 — 이 자리의 것")]
        [Tooltip("비우면 프로필에 적힌 것을 쓴다. " +
                 "같은 인물이라도 자리마다 달라야 할 때 여기서 덮어쓴다 — " +
                 "견우는 낮에 Idle 중심이지만 밤에는 LookAround 를 12~20초로 돌리고, " +
                 "상인은 은하담에서 서서 마시고 주막에서는 앉아 마신다")]
        public NpcProfile.RandomMotion[] overrideRandom = Array.Empty<NpcProfile.RandomMotion>();

        [Tooltip("첫 랜덤까지 더 기다릴 시간(초). 음수면 프로필의 값을 쓴다. " +
                 "아이 3인의 시작을 몇 초씩 어긋내는 자리다 (문서 「28. 그룹 연출」)")]
        public float overrideStartDelay = -1f;

        /// <summary>지금 이 자리에서 쓸 랜덤 표.</summary>
        NpcProfile.RandomMotion[] Randoms =>
            (overrideRandom != null && overrideRandom.Length > 0)
                ? overrideRandom
                : (profile != null ? profile.randomMotions : Array.Empty<NpcProfile.RandomMotion>());

        float StartDelay =>
            overrideStartDelay >= 0f ? overrideStartDelay : (profile != null ? profile.randomStartDelay : 0f);

        [Header("맞물림")]
        [Tooltip("모션을 바꿀 때 겹치는 시간(초)")]
        public float fade = 0.18f;

        [Tooltip("한 상태가 끝나기 이만큼 전에 기본 자세로 돌아가기 시작한다")]
        public float returnLead = 0.12f;

        // ── 지금 상태 ────────────────────────────────────────────
        string _baseState = "";
        string _playing = "";
        Pri _pri = Pri.Idle;
        float _until = -1f;          // 이 시각이 지나면 기본 자세로 (loops면 -1)
        float _baseRepeat = -1f;     // 되돌지 않는 기본 자세를 다시 트는 시각
        bool _held;                  // 놓아 줄 때까지 잡아 두는가 (대화 중 Talk)
        bool _randomPaused;
        float[] _nextRandom = Array.Empty<float>();
        bool _ready;

        public string BaseState => _baseState;
        public string Playing => _playing;
        public Pri CurrentPriority => _pri;

        /// <summary>지금 자기보다 약한 것을 밀어내고 있는가 — 스토리 이벤트가 도는 중인지 볼 때 쓴다.</summary>
        public bool IsBusy => _pri < Pri.Random;

        void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (profile != null && !string.IsNullOrEmpty(profile.idleState)) _baseState = profile.idleState;
        }

        void OnEnable()
        {
            _ready = animator != null && animator.runtimeAnimatorController != null;
            if (!_ready) return;
            ScheduleAllRandom(true);
            _pri = Pri.Idle;
            _held = false;
            _until = -1f;
            GoTo(_baseState, 0f);
        }

        // ─────────────────────────────────────────────────────────
        //  바깥에서 부르는 것
        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// 모션 하나를 요청한다. 지금 도는 것이 더 세면 <b>버린다</b>(false).
        /// </summary>
        /// <param name="hold">
        /// 켜면 <see cref="Release"/> 를 부를 때까지 잡아 둔다. 대화 중 Talk가 이쪽이다 —
        /// 클립이 4초라 끝날 때마다 기본 자세로 튕기면 말하다 말고 굳는 것처럼 보인다.
        /// </param>
        public bool Play(string state, Pri pri, bool hold = false)
        {
            if (!_ready || string.IsNullOrEmpty(state)) return false;
            var m = Find(state);
            if (m == null)
            {
                Debug.LogWarning("[모션] " + name + " 에 '" + state + "' 상태가 없다.");
                return false;
            }
            // 문서 「23-3」 특수·이동·대화 모션이 겹치지 않게. 같은 등급끼리는 나중 것이 이긴다.
            if (pri > _pri && (_held || _until < 0f || Time.time < _until)) return false;

            _pri = pri;
            _held = hold;
            _playing = state;
            _until = (m.loops || hold) ? -1f : Time.time + Mathf.Max(0.1f, m.length - returnLead);
            GoTo(state, fade);
            // 문서 「23-4」 이벤트 전용 모션 재생 중에는 평상시 랜덤 루프를 일시 정지한다.
            if (pri <= Pri.Move) ScheduleAllRandom(false);
            return true;
        }

        /// <summary>잡아 두었던 모션을 놓아 준다 — 대화가 끝났을 때.</summary>
        public void Release()
        {
            if (!_held) return;
            _held = false;
            _until = Time.time;      // 다음 Update가 기본 자세로 되돌린다
        }

        /// <summary>
        /// 기본 자세 자체를 바꾼다 (앉음↔섬, 자리 이동 뒤의 복귀 자세).
        /// 문서 「23-5」 이동이 끝나면 <b>그 위치에 맞는</b> Idle로 돌아간다.
        /// </summary>
        public void SetBase(string state, bool snap = false)
        {
            if (string.IsNullOrEmpty(state) || _baseState == state) return;
            _baseState = state;
            if (!_ready) return;
            if (_pri >= Pri.Random && !_held) GoTo(_baseState, snap ? 0f : fade);
        }

        /// <summary>평상시 랜덤을 멈추거나 다시 돌린다. 스토리 연출이 앞뒤로 부른다.</summary>
        public void PauseRandom(bool on)
        {
            _randomPaused = on;
            if (!on) ScheduleAllRandom(true);
        }

        /// <summary>지금 도는 것을 버리고 곧장 기본 자세로 — 씬 상태가 통째로 바뀔 때.</summary>
        public void ResetToBase()
        {
            _held = false;
            _pri = Pri.Idle;
            _until = -1f;
            if (_ready) GoTo(_baseState, fade);
            ScheduleAllRandom(true);
        }

        // ─────────────────────────────────────────────────────────
        void Update()
        {
            if (!_ready) return;

            // ① 끝난 모션을 기본 자세로 되돌린다
            if (!_held && _until >= 0f && Time.time >= _until)
            {
                _until = -1f;
                _pri = Pri.Idle;
                GoTo(_baseState, fade);
                ScheduleAllRandom(true);
            }

            // ①-2 되돌지 않는 기본 자세를 다시 튼다 (상인의 SitDrinking)
            if (_baseRepeat > 0f && Time.time >= _baseRepeat && _pri >= Pri.Random && !_held)
                GoTo(_baseState, fade);

            // ② 평상시 랜덤 — Idle일 때만 (문서 「23-1·2」)
            if (_randomPaused || _pri < Pri.Random || _held) return;
            var rs = Randoms;
            if (rs == null) return;

            for (int i = 0; i < rs.Length && i < _nextRandom.Length; i++)
            {
                if (Time.time < _nextRandom[i]) continue;
                var r = rs[i];
                _nextRandom[i] = Time.time + UnityEngine.Random.Range(r.minInterval, r.maxInterval);
                // 확률이 낮은 것은 때가 돼도 그냥 넘긴다 — '낮은 확률로 SitGun' 이 이 뜻이다.
                if (UnityEngine.Random.value > r.chance) continue;
                Play(r.state, Pri.Random);
                break;                 // 한 프레임에 둘을 겹치지 않는다
            }
        }

        void ScheduleAllRandom(bool restart)
        {
            var rs = Randoms;
            if (rs == null) { _nextRandom = Array.Empty<float>(); return; }
            int n = rs.Length;
            if (_nextRandom.Length != n) _nextRandom = new float[n];
            if (!restart) { for (int i = 0; i < n; i++) _nextRandom[i] = float.MaxValue; return; }

            float delay = StartDelay;
            for (int i = 0; i < n; i++)
            {
                var r = rs[i];
                // ⚠️ 문서 「23-6」 NPC별 주기를 완전히 같게 하지 않는다. 여기에 자리마다 다른
                //    시작 지연을 더해 아이 3인의 시작을 몇 초씩 어긋낸다 (문서 「28. 그룹 연출」).
                _nextRandom[i] = Time.time + delay + UnityEngine.Random.Range(r.minInterval, r.maxInterval);
            }
        }

        void GoTo(string state, float f)
        {
            if (string.IsNullOrEmpty(state)) return;
            _playing = state;
            if (f <= 0.001f) animator.Play(state, 0, 0f);
            else animator.CrossFadeInFixedTime(state, f, 0, 0f);

            // ⚠️ 기본 자세가 <b>되돌지 않는 클립</b>일 수 있다. 상인의 SitDrinking(15.2초)이 그렇다 —
            //    그래프에 자기 전이가 없어(이름에 Idle·Walk·Talk가 없다) 끝나면 마지막 한 장에
            //    굳어 버린다. 앉아서 굳은 사람은 인형처럼 보이므로 여기서 다시 튼다.
            //    한 번 도는 연출 모션(GiveKey·Clap…)은 이 길로 오지 않는다 — 저쪽은 _until 이 맡는다.
            _baseRepeat = -1f;
            if (state != _baseState) return;
            var m = Find(state);
            if (m != null && !m.loops) _baseRepeat = Time.time + Mathf.Max(0.3f, m.length - fade);
        }

        Motion Find(string state)
        {
            foreach (var m in motions) if (m != null && m.state == state) return m;
            return null;
        }

        /// <summary>그 상태가 이 인물에게 있는가 — 설치 검증에 쓴다.</summary>
        public bool Has(string state) => Find(state) != null;
    }
}
