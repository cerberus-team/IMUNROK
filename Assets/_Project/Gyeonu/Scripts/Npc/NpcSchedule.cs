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
        /// 지금 조건으로 <b>즉시</b> 다시 판정한다 — 보고 있어도 바꾼다 (2026-09-11).
        ///
        /// 디버그 메뉴가 "조건을 갖추고 그 사람 앞에 세워 준다"를 하려면 이 문이 필요하다.
        /// 평소의 보류 규칙(눈앞에서 사라지거나 나타나지 않게)은 <b>플레이가 흘러가는 동안</b>의
        /// 것이라, 자리와 시간대를 한꺼번에 갈아 끼우는 디버그에는 맞지 않는다.
        /// ⚠️ 대화 중에는 이것도 아무 일을 하지 않는다 — 그 규칙만은 깨지 않는다.
        /// </summary>
        /// <summary>
        /// 씬의 모든 일정을 <b>즉시</b> 다시 판정한다 (2026-09-11). 주막에서 쉬어 시간대가 넘어가는
        /// 순간처럼 <b>화면이 캄캄한 동안</b> 부른다 — 플레이어가 눈을 감고 있던 셈이라 보류 규칙을
        /// 건너뛰어도 눈앞에서 사라지거나 솟아나는 사람이 없다.
        ///
        /// 왜 필요한가: 쉬는 자리(주막 평상)와 상인 자리는 5m 거리다. 낮에 만나지 못한 상인은 초밤이
        /// 되어야 주막에 서는데, 쉬고 일어난 플레이어가 그쪽을 보고 있으면 보류 규칙에 걸려
        /// 고개를 돌릴 때까지 나타나지 않았다 — 다른 NPC 는 멀어서 바로 바뀌는데 상인만 늦었다 (실측).
        /// </summary>
        public static void RefreshAll()
        {
            foreach (var s in FindObjectsByType<NpcSchedule>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (s != null) s.RefreshNow();
        }

        public void RefreshNow()
        {
            _first = true;
            _next = 0f;
            Apply(force: true);
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

        /// <summary>
        /// 바깥에서 건 <b>덧조건</b> (2026-09-11). <c>false</c> 를 돌려주면 그 사이에는 보이지 않는다.
        ///
        /// 연출이 사람을 잠시 감춰야 할 때 쓴다 — 견우가 문 안에 있다가 나오는 전달 연출
        /// (<see cref="GyeonuGiveKey"/>)이 첫 손님이다. <b>보이고 안 보이고를 두 곳에서 만지면
        /// 반드시 어긋나므로</b>, 감추려는 쪽은 이 문만 걸고 실제 켜고 끄기는 여기에 맡긴다.
        /// 코드에서만 꽂는다 — 인스펙터에 노출할 것이 아니다.
        /// </summary>
        public System.Func<bool> externalGate;

        public bool Wanted()
        {
            if (externalGate != null && !externalGate()) return false;
            // 엔딩 연출 중(재회가 끝나 날이 밝는 순간부터)에는 구출 뒤 인물(견우·선아)만 남는다 (2026-09-13).
            // 관아가 낮으로 바뀌면 수령 자리가 살아나는데, 그 아침에 수령이 대청에 앉아 있으면 엔딩이 어긋난다.
            if (GyeonuCase.CurrentAct == Act.Ending && rescueState != Rescue.구출_뒤에만) return false;
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
