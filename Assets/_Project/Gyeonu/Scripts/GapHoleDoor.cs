using System.Collections;
using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 관아 담장 개구멍 — 마을 아이들만 아는, 기어서 지나가는 자리.
    ///
    /// ■ 왜 SecretStoneDoor를 그대로 안 쓰는가
    ///   SecretStoneDoor는 조건이 모자라도 조준하면 "조사하기"가 뜨고 잠김 문구를 보여준다 —
    ///   비밀 문이 있다는 것 자체는 알려 주는 방식이다. 여기는 그보다 엄격하다: 조건(밤 +
    ///   아이들 이야기)을 모두 갖추기 전에는 <see cref="CanInteract"/> 자체가 false라
    ///   DebugInteractor의 조준 표시(◆)도, 문구도 아예 뜨지 않는다 — 담장과 완전히 같다.
    ///
    /// ■ 동작 — 은하담 암문(SecretStoneDoor)과 같은 두 박자 (2026-08-21 2차)
    ///   먼저 <see cref="pushAxis"/>로 살짝 물러난 뒤, 그와 겹쳐 <see cref="slideAxis"/>로
    ///   옆으로 미끄러진다(Apply의 PushEnd/SlideStart 겹침 구간이 SecretStoneDoor와 동일).
    ///   다만 은하담 암문과 달리 <see cref="pushDepth"/>가 작지 않다 — 문짝이 담장 실측
    ///   두께(약 0.63m, 개구멍 높이대의 돌 밑단 기준)를 그대로 가진 두툼한 덩어리라,
    ///   옆으로만 밀면 아직 담장 몸통 안에 걸쳐 있는 채로 겹쳐 보인다. 두께만큼 안으로
    ///   완전히 빠져나온 뒤에야 옆으로 미끄러져야 마당 쪽 빈 허공에서 깨끗이 떨어져 보인다.
    ///
    /// ■ 열린 뒤 다시 닫을 수 있다 (2026-08-21 2차)
    ///   문짝(<see cref="leaf"/>)이 열린 자리에서 콜라이더를 얻는다(닫혀 있는 동안은 꺼 둔다 —
    ///   "구멍" 콜라이더와 같은 자리에 겹쳐 있어 raycast가 갈릴 수 있어서다). 문짝에 붙는
    ///   <see cref="GapHoleLeaf"/>가 "닫기"를 담당— 이 컴포넌트(구멍)는 밀기·들어가기·나가기만 맡는다.
    ///
    /// ■ 콜라이더 — "구멍"에는 상시, "문짝"에는 열렸을 때만
    ///   "구멍" 오브젝트의 BoxCollider 하나가 늘 그 자리를 지키며 raycast·보행 차단을 겸한다.
    ///   문이 열려도 이 콜라이더는그대로 있다 — 들어가는 것은 걸어서가 아니라 클릭으로 짧은
    ///   포복 연출을 트는 방식이기 때문이다.
    /// </summary>
    [AddComponentMenu("이문록/관아 개구멍 (GapHoleDoor)")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public class GapHoleDoor : Interactable
    {
        [Header("눈에 보이는 담장 조각")]
        public Transform leaf;
        [Tooltip("먼저 이 방향(로컬)으로 물러난다 — 담장 실측 두께만큼 빠져나와야 한다")]
        public Vector3 pushAxis = Vector3.left;
        public float pushDepth = 0.70f;
        [Tooltip("물러난 뒤 이 방향(로컬)으로 미끄러진다")]
        public Vector3 slideAxis = Vector3.back;
        public float slideDistance = 1.30f;
        [Tooltip("물러남+미끄러짐 전체 시간(초) — 1.5~2초 권장")]
        public float openDuration = 1.7f;

        [Header("조건 — 밤 + 아이들 이야기")]
        public string requiredFlag = GyeonuWorld.F_개구멍이야기;

        [Header("상태 유지")]
        public string openKey = "";

        [Header("바깥/안쪽 판정")]
        [Tooltip("담장 바깥쪽을 가리키는 월드 방향 (동담이면 +X)")]
        public Vector3 outsideDir = Vector3.right;
        public Transform outsideAnchor;
        public Transform insideAnchor;
        public float crawlDuration = 1.2f;

        [Header("문구")]
        public string promptPush = "밀기";
        public string promptEnter = "들어가기";
        public string promptExit = "나가기";

        // 은하담 암문(SecretStoneDoor)과 같은 겹침 구간 — 물러남이 끝나기 전에 미끄러짐이 시작돼
        // 두 동작이 한 호흡으로 이어진다.
        const float PushEnd = 0.32f;
        const float SlideStart = 0.24f;

        Vector3 _closedLocal;
        float _t;
        bool _open;
        Vector3 _lastActorPos;
        bool _crawling;
        Collider _leafCollider;

        public bool IsOpen => _open;
        public bool IsCrawling => _crawling;

        void Awake()
        {
            if (leaf != null)
            {
                _closedLocal = leaf.localPosition;
                _leafCollider = leaf.GetComponent<Collider>();
            }

            if (!string.IsNullOrEmpty(openKey) && GyeonuWorld.Has(openKey))
            {
                _open = true;
                _t = 1f;
                Apply(1f);
            }
            if (_leafCollider != null) _leafCollider.enabled = _open;
        }

        public override bool CanInteract(GameObject actor)
        {
            _lastActorPos = actor.transform.position;
            if (_crawling) return false;
            if (_open) return true;
            return (GyeonuWorld.Night && GyeonuWorld.Has(requiredFlag)) || GyeonuWorld.DebugIgnoreConditions;
        }

        public override string Prompt
        {
            get
            {
                if (!_open) return promptPush;
                return IsOutside(_lastActorPos) ? promptEnter : promptExit;
            }
        }

        bool IsOutside(Vector3 pos) => Vector3.Dot(pos - transform.position, outsideDir.normalized) > 0f;

        public override void Interact(GameObject actor)
        {
            if (_crawling) return;

            if (!_open)
            {
                Open();
                return;
            }

            var walker = actor.GetComponentInParent<DebugWalkController>();
            if (walker == null) return;

            bool outside = IsOutside(walker.transform.position);
            Transform target = outside ? insideAnchor : outsideAnchor;
            if (target == null) return;

            StartCoroutine(CrawlRoutine(walker, target.position, target.rotation));
        }

        void Open()
        {
            _open = true;
            GyeonuWorld.Set(openKey);
            if (_leafCollider != null) _leafCollider.enabled = true;
            DebugToast.Show("돌 하나가 안으로 물러났다가 옆으로 밀려나며 좁은 틈이 드러난다.", 3f);
        }

        /// <summary>문짝(GapHoleLeaf)이 부르는 진입점. 포복 중에는 닫지 못한다.</summary>
        public void Close()
        {
            if (!_open || _crawling) return;
            _open = false;
            GyeonuWorld.Set(openKey, false);
            DebugToast.Show("돌이 도로 밀려들어와 담장이 감쪽같이 아문다.", 3f);
        }

        void Update()
        {
            float target = _open ? 1f : 0f;
            if (Mathf.Approximately(_t, target)) return;
            _t = Mathf.MoveTowards(_t, target, Time.deltaTime / Mathf.Max(0.05f, openDuration));
            Apply(_t);
            if (Mathf.Approximately(_t, 0f) && _leafCollider != null) _leafCollider.enabled = false;
        }

        /// <summary>0=닫힘, 1=완전히 열림. SecretStoneDoor와 같은 겹침 완급 — 물러남은 ease-out,
        /// 미끄러짐은 smoothstep. t가 줄어들면(닫힘) 자동으로 미끄러짐→물러남 순서로 뒤집힌다.</summary>
        void Apply(float t)
        {
            if (leaf == null) return;
            float push = Mathf.Clamp01(t / PushEnd);
            push = 1f - (1f - push) * (1f - push);
            float slide = Mathf.Clamp01((t - SlideStart) / (1f - SlideStart));
            slide = slide * slide * (3f - 2f * slide);
            leaf.localPosition = _closedLocal
                               + pushAxis.normalized * (pushDepth * push)
                               + slideAxis.normalized * (slideDistance * slide);
        }

        /// <summary>몸을 낮춰 기어서 통과하는 짧은 연출. 그동안 조작을 끄고 CharacterController도
        /// 꺼서(지오메트리에 걸리지 않게) 위치를 직접 보간한다.</summary>
        IEnumerator CrawlRoutine(DebugWalkController walker, Vector3 targetPos, Quaternion targetRot)
        {
            _crawling = true;
            var tr = walker.transform;
            var cc = walker.GetComponent<CharacterController>();
            var eye = walker.eye;

            Vector3 startPos = tr.position;
            Quaternion startRot = tr.rotation;
            float startEyeY = eye != null ? eye.localPosition.y : 1.7f;
            float startPitch = walker.Pitch;
            const float crouchEyeY = 0.85f;
            const float crouchPitch = 22f;

            walker.enabled = false;
            if (cc != null) cc.enabled = false;

            float t = 0f;
            float dur = Mathf.Max(0.1f, crawlDuration);
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                float e = k * k * (3f - 2f * k);
                tr.SetPositionAndRotation(Vector3.Lerp(startPos, targetPos, e), Quaternion.Slerp(startRot, targetRot, e));

                float dip = Mathf.Sin(k * Mathf.PI);   // 0 → 1(중간, 가장 낮음) → 0
                if (eye != null)
                {
                    var ep = eye.localPosition;
                    ep.y = Mathf.Lerp(startEyeY, crouchEyeY, dip);
                    eye.localPosition = ep;
                }
                walker.Pitch = Mathf.Lerp(startPitch, crouchPitch, dip);
                yield return null;
            }

            tr.SetPositionAndRotation(targetPos, targetRot);
            if (eye != null)
            {
                var ep = eye.localPosition;
                ep.y = startEyeY;
                eye.localPosition = ep;
            }
            walker.Pitch = 0f;

            if (cc != null) cc.enabled = true;
            walker.enabled = true;
            _crawling = false;
        }
    }
}
