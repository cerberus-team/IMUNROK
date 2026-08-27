using System;
using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// NPC 한 명이 <b>지금 이 자리에 있어야 하는가</b> (2026-08-25).
    /// 문서 「제5부」의 위치표(견우·수령·주모·상인·아이들·최초의 두 사람)를 그대로 옮긴 것이다.
    ///
    /// ■ 왜 '이동'이 아니라 '켜고 끄기'인가
    ///   상인에게는 <b>Walk 모션이 없다</b>(문서 「27」). 은하담에서 주막까지 미끄러져 가면
    ///   발이 얼음판을 타는 것처럼 보인다. 그래서 자리마다 인스턴스를 하나씩 두고 켜고 끈다.
    ///   같은 방식이 씬을 가로지르는 이동에도 그대로 통한다 — 은하담과 주막은 아예 다른 씬이라
    ///   어차피 하나의 오브젝트가 걸어갈 수 없다.
    ///
    /// ■ ⚠️ 눈앞에서 사라지면 안 된다 (문서 「27」)
    ///   "플레이어가 은하담에 없을 때 또는 20m 이상 떨어져 있을 때만" 교체한다. 그래서 조건이
    ///   바뀌어도 <b>보이는 동안에는 미룬다</b>. 거리와 시야를 둘 다 본다 — 20m 안이라도 등을
    ///   돌리고 있으면 바꿔도 들키지 않고, 반대로 30m 밖이라도 정면으로 보고 있으면 미룬다.
    ///
    /// ■ 처음 한 번은 미루지 않는다
    ///   씬을 여는 순간에는 아무도 '보고 있던' 상태가 아니다. 첫 판정은 즉시 적용한다 —
    ///   안 그러면 낮에 들어간 씬에서 밤 NPC가 한 프레임 서 있다가 사라진다.
    /// </summary>
    public class NpcSchedule : MonoBehaviour
    {
        public enum Rescue { 상관없음 = 0, 구출_전에만 = 1, 구출_뒤에만 = 2 }

        [Header("언제 서 있는가")]
        [Tooltip("비우면 아무 때나. 문서의 위치표를 그대로 옮긴다")]
        public TimeOfDay[] activeAt = Array.Empty<TimeOfDay>();

        [Tooltip("이 플래그가 전부 서 있어야 한다 (최초의 두 사람 — 지도 해독 후 등장)")]
        public string[] requireFlags = Array.Empty<string>();

        [Tooltip("이 플래그가 하나라도 서 있으면 사라진다 (은하담 상인 — 주막으로 옮겨 간 뒤)")]
        public string[] forbidFlags = Array.Empty<string>();

        [Tooltip("이 시간대에는 위의 플래그 조건을 건너뛴다. " +
                 "상인의 '낮 후반 ~ 초밤 주막'이 이 한 줄로 표현된다 — " +
                 "낮에는 은하담에서 만난 뒤에야 주막에 있고, 초밤이면 만났든 아니든 주막에 있다")]
        public TimeOfDay[] ignoreFlagsAt = Array.Empty<TimeOfDay>();

        public Rescue rescueState = Rescue.상관없음;

        [Header("눈앞에서 바꾸지 않기")]
        [Tooltip("이 거리 안에서 플레이어가 이쪽을 보고 있으면 교체를 미룬다(m)")]
        public float guardDistance = 20f;

        [Tooltip("시야로 칠 각도(도). 카메라 정면에서 이만큼 안쪽이면 '보고 있다'")]
        public float guardAngle = 70f;

        [Tooltip("판정 간격(초). 매 프레임 볼 일이 아니다")]
        public float checkInterval = 0.5f;

        float _next;
        bool _first = true;
        bool _visible = true;

        Renderer[] _renderers;
        Collider[] _colliders;
        NpcActor _actor;
        NpcDialogue _talk;
        Animator _animator;

        /// <summary>지금 이 자리에 서 있는가 — 다른 연출이 물어볼 때 쓴다.</summary>
        public bool Visible => _visible;

        void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _colliders = GetComponentsInChildren<Collider>(true);
            _actor = GetComponent<NpcActor>();
            _talk = GetComponent<NpcDialogue>();
            _animator = GetComponentInChildren<Animator>(true);
        }

        void OnEnable()
        {
            _first = true;
            _next = 0f;
            Apply(force: true);
        }

        void Update()
        {
            if (Time.time < _next) return;
            _next = Time.time + checkInterval;
            Apply(force: false);
        }

        /// <summary>
        /// ⚠️ <c>SetActive(false)</c> 로 끄지 않는다 — 꺼진 오브젝트는 <see cref="Update"/> 가 돌지 않아
        /// <b>스스로 다시 켜질 수 없다.</b> 그래서 보이는 것·만져지는 것만 끄고 이 부품은 계속 깨어 있다.
        /// 플레이어가 겪는 결과는 같다(안 보이고, 조준되지 않고, 말을 걸 수 없다).
        /// </summary>
        void Apply(bool force)
        {
            // ⚠️ 마주 서서 이야기하는 동안에는 절대 바꾸지 않는다. 상인은 <b>말을 건 그 순간</b>
            //    '만났다' 플래그가 서서 은하담 자리의 조건이 깨진다 — 그대로 두었더니
            //    대답을 기다리는 사이에 눈앞에서 사라졌다 (2026-08-25 실측).
            //    시야·근접 보류보다도 먼저 끊어 대화가 끝날 때까지 절대 바꾸지 않는다.
            if (_talk != null && _talk.Session != null) return;

            bool want = Wanted();
            if (want == _visible && !_first) return;

            // 첫 판정은 곧바로 — 씬을 여는 순간엔 아무도 보고 있지 않다.
            if (!force && !_first && PlayerWatching(transform))
                return; // 시간이 얼마나 지났든 시야·근접 상태에서는 강제로 바꾸지 않는다.
            _first = false;
            if (want == _visible) return;
            _visible = want;

            foreach (var r in _renderers) if (r != null) r.enabled = want;
            foreach (var c in _colliders) if (c != null) c.enabled = want;
            if (_animator != null) _animator.enabled = want;
            if (_actor != null) _actor.enabled = want;
            if (_talk != null) _talk.enabled = want;

            // 다시 나타날 때는 기본 자세부터 (문서 「23-5」 이동이 끝나면 그 위치에 맞는 Idle로)
            if (want && _actor != null) _actor.ResetToBase();
        }

        public bool Wanted()
        {
            if (activeAt != null && activeAt.Length > 0)
            {
                bool ok = false;
                foreach (var t in activeAt) if (t == GyeonuCase.Time) { ok = true; break; }
                if (!ok) return false;
            }
            bool skipFlags = false;
            foreach (var t in ignoreFlagsAt) if (t == GyeonuCase.Time) { skipFlags = true; break; }
            if (!skipFlags)
            {
                foreach (var f in requireFlags) if (!GyeonuCase.HasFlag(f)) return false;
                foreach (var f in forbidFlags) if (GyeonuCase.HasFlag(f)) return false;
            }

            if (rescueState == Rescue.구출_전에만 && GyeonuCase.SeonaRescued) return false;
            if (rescueState == Rescue.구출_뒤에만 && !GyeonuCase.SeonaRescued) return false;
            return true;
        }

        bool PlayerWatching(Transform who)
        {
            var cam = Camera.main;
            if (cam == null) return false;
            Vector3 to = who.position + Vector3.up * 1.4f - cam.transform.position;
            if (to.sqrMagnitude > guardDistance * guardDistance) return false;
            return Vector3.Angle(cam.transform.forward, to) <= guardAngle;
        }
    }
}
