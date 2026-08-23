using System.Collections;
using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 오작교 암문 개미수열 자물쇠 (2026-08-23).
    ///
    /// ■ 무엇인가
    ///   암문 한복판 석축에 같은 크기·같은 재질의 돌 버튼 둘이 박혀 있다. 왼쪽에 「一」,
    ///   오른쪽에 「二」가 얕게 음각돼 있고, 그것 말고는 주변 석축과 구별되지 않는다.
    ///   정해진 차례대로 여섯 번 누르면 안쪽 빗장이 풀리고 기존 암문 개방 연출이 시작된다.
    ///
    /// ■ 알려 주지 않는다 — 이 퍼즐의 규칙
    ///   맞았는지 틀렸는지 **글자로 알리지 않는다.** 플레이어가 읽을 수 있는 것은
    ///   돌의 움직임과 소리뿐이다.
    ///     · 맞으면  — 눌린 돌이 안으로 들어갔다 거의 되돌아오고, 그때 **양쪽 돌이 함께**
    ///                 한 칸(<see cref="notch"/>)씩 더 깊이 내려앉는다. 쌓이는 것이 눈에 보인다.
    ///     · 틀리면  — 낮게 「툭」, 양쪽이 함께 「덜컥」 주저앉았다가 「툭」 하고 튀어나온다.
    ///                 쌓여 있던 깊이가 통째로 0으로 돌아가는 것을 보고 실패를 안다.
    ///     · 마지막  — 평소와 **똑같은** 「달칵」. 그리고 0.55초의 정적. 그 침묵 동안에는
    ///                 맞았는지 알 수 없고, 그러다 「드르륵—철컥」 하고 빗장이 풀린다.
    ///                 마지막 입력음을 특별하게 만들면 이 연출이 통째로 죽는다.
    ///
    /// ■ 같은 버튼을 연속으로 눌러도 매번 보인다
    ///   눌린 돌은 한 번 들어가 **고정되지 않는다.** 새 안식처(= 한 칸 깊어진 자리)까지
    ///   되돌아오므로 다음 누름의 행정(<see cref="stroke"/>)이 늘 그대로 남아 있다.
    ///   정답이 1 → 1 처럼 이어져도 두 번 다 같은 크기로 움직인다.
    ///
    /// ■ 조작 입력
    ///   <see cref="FocusInteractable"/> 를 그대로 쓴다 — 혼상·혼천의와 같은 포커스 모드다.
    ///   다만 저쪽은 드래그로 돌리는 대상이고 이쪽은 **눌러야** 하므로
    ///   <see cref="FocusInteractable.HandleClick"/> 훅을 새로 뚫었다(기본 구현은 빈 함수라
    ///   기존 대상은 무수정). 포커스 중에는 걷기 컨트롤러가 꺼지고 커서 잠금이 풀리므로
    ///   마우스로 직접 두 돌을 가리킬 수 있다.
    ///
    /// ■ 소리
    ///   프로젝트에 쓸 만한 효과음 에셋이 없어 <see cref="StoneSfx"/> 가 파형을 합성한다.
    ///   나중에 진짜 효과음이 생기면 인스펙터의 AudioClip 칸에 꽂기만 하면 그쪽이 우선한다.
    /// </summary>
    [AddComponentMenu("이문록/암문 개미수열 자물쇠 (AmmunLockPuzzle)")]
    [DisallowMultipleComponent]
    public class AmmunLockPuzzle : FocusInteractable, IDoorPuzzle
    {
        [Header("연결")]
        [Tooltip("풀리면 열릴 문")]
        public SecretStoneDoor door;
        [Tooltip("왼쪽(一) 돌 = 1")]
        public Transform buttonOne;
        [Tooltip("오른쪽(二) 돌 = 2")]
        public Transform buttonTwo;
        [Tooltip("돌이 밀려 들어가는 방향 (돌의 부모 = 석축덩어리 로컬 기준). 석축 서면 법선의 반대")]
        public Vector3 pressAxis = new Vector3(0.99974f, 0f, -0.02299f);
        [Tooltip("카메라가 서는 쪽 (같은 로컬 기준). 석축 서면 바깥 법선")]
        public Vector3 viewNormal = new Vector3(-0.99974f, 0f, 0.02299f);

        [Header("정답")]
        [Tooltip("1 = 왼쪽(一), 2 = 오른쪽(二). 기본값은 개미수열 1 1 1 2 2 1")]
        public int[] answer = { 1, 1, 1, 2, 2, 1 };

        [Header("움직임 (m)")]
        // ⚠️ 이 값들의 합(notch × 답 길이 + stroke)은 돌이 석축 면보다 튀어나온 양
        //    (AmmunPuzzleBuilder.Proud = 0.045)보다 **작아야** 한다. 넘으면 마지막 누름에서
        //    돌이 벽 속으로 잠겨 사라진다. 반대로 너무 작으면 정면에서 시차가 없어
        //    **움직임이 아예 안 읽힌다** (첫 Play에서 0.003/0.013으로 겪었다).
        [Tooltip("정답 하나마다 양쪽 돌이 함께 더 내려앉는 깊이")]
        public float notch = 0.0040f;
        [Tooltip("누르는 동안 안식처보다 더 들어가는 행정")]
        public float stroke = 0.0180f;
        [Tooltip("틀렸을 때 주저앉는 깊이")]
        public float clunkDepth = 0.0130f;
        [Tooltip("튀어나올 때 바깥으로 넘어가는 양")]
        public float popOvershoot = 0.0060f;

        [Header("박자 (초)")]
        public float pressIn = 0.16f;      // 안으로
        public float pressBack = 0.14f;    // 살짝 복귀
        public float silence = 0.55f;      // 마지막 달칵 뒤의 정적 (요구: 0.3~0.7)
        public float exitDelay = 0.45f;    // 빗장이 풀린 뒤 포커스가 물러나기까지

        [Header("소리 (비우면 절차 합성)")]
        public AudioClip clipLatch;
        public AudioClip clipDead;
        public AudioClip clipClunk;
        public AudioClip clipPop;
        public AudioClip clipRelease;
        [Range(0f, 1f)] public float volume = 0.9f;

        AudioSource audioSrc;
        AudioClip genLatch, genDead, genClunk, genPop, genRelease;
        Vector3 restOne, restTwo;     // 안 눌린 원래 자리 (로컬)
        int step;                     // 지금까지 맞게 들어간 입력 수
        bool busy;                    // 연출 중 — 입력을 받지 않는다
        bool finished;

        /// <summary>퍼즐이 이미 풀렸는가 — 문 플래그가 기준이라 씬을 나갔다 와도 유지된다.</summary>
        public bool Solved => door != null && door.PuzzleSolved;

        public override string FocusStatus => null;   // 진행 상황을 글로 보여 주지 않는다
        public override string FocusHint => "돌을 눌러 본다   Esc/우클릭: 물러나기";
        public override bool CanExitFocus => !busy;

        public override Vector3 FocusPoint
        {
            get
            {
                if (buttonOne == null || buttonTwo == null) return transform.position;
                return (buttonOne.position + buttonTwo.position) * 0.5f;
            }
        }

        /// <summary>암문 정면(서쪽)에서 두 돌을 나란히 본다 — 어느 쪽에서 왔든 같은 구도.</summary>
        public override void GetFocusPose(Vector3 currentEyePos, out Vector3 pos, out Quaternion rot)
        {
            var c = FocusPoint;
            var n = transform.parent != null
                  ? transform.parent.TransformDirection(viewNormal).normalized
                  : viewNormal.normalized;
            pos = c + n * focusDistance;
            rot = Quaternion.LookRotation(c - pos);
        }

        void Awake()
        {
            if (buttonOne != null) restOne = buttonOne.localPosition;
            if (buttonTwo != null) restTwo = buttonTwo.localPosition;

            audioSrc = GetComponent<AudioSource>();
            if (audioSrc == null) audioSrc = gameObject.AddComponent<AudioSource>();
            audioSrc.playOnAwake = false;
            audioSrc.spatialBlend = 0.7f;    // 3D로 두되 완전히 죽지 않게
            audioSrc.minDistance = 2f;
            audioSrc.maxDistance = 30f;
            audioSrc.rolloffMode = AudioRolloffMode.Linear;

            // 이미 풀어 둔 상태로 씬에 다시 들어오면 돌은 끝까지 눌린 채로 있어야 한다.
            // ⚠️ Solved(=door.PuzzleSolved)가 아니라 **플래그 자체**를 본다 —
            //    디버그 '진행 조건 무시'가 켜져 있다고 돌이 눌린 채로 시작하면 안 된다.
            if (door != null && !string.IsNullOrEmpty(door.puzzleFlag) && GyeonuWorld.Has(door.puzzleFlag))
            { finished = true; ApplyDepth(Full, Full); }
        }

        void OnDestroy()
        {
            // 런타임 생성 클립은 직접 버린다 (static 캐시 금지 — 다음 세션에서 빈 소리가 된다)
            foreach (var c in new[] { genLatch, genDead, genClunk, genPop, genRelease })
                if (c != null) Destroy(c);
        }

        float Full => answer.Length * notch;

        // ── 진입 ──────────────────────────────────────────

        public void BeginPuzzle(GameObject actor)
        {
            if (Solved || actor == null) return;
            DebugFocusRig.Begin(this, actor);
        }

        public override void Interact(GameObject actor) => BeginPuzzle(actor);

        public override void OnFocusChanged(bool focused)
        {
            if (focused || finished) return;
            // 도중에 물러나면 쌓아 둔 것은 조용히 풀린다 — 다음에 처음부터 다시.
            StopAllCoroutines();
            busy = false;
            if (step > 0) StartCoroutine(SlideBoth(step * notch, 0f, 0.25f));
            step = 0;
        }

        public override void HandleDrag(Vector2 delta) { }   // 이 대상은 돌리지 않는다

        // ── 입력 ──────────────────────────────────────────

        public override void HandleClick(Ray ray)
        {
            if (busy || finished) return;
            int which = Pick(ray);
            if (which == 0) return;
            StartCoroutine(which == answer[step] ? Press(which) : Fail(which));
        }

        /// <summary>레이가 맞은 돌 (1 / 2, 못 맞히면 0).
        /// 콜라이더를 하나씩 직접 쏜다 — 석축덩어리의 차단 박스나 레이어 설정에 휘둘리지 않는다.</summary>
        int Pick(Ray ray)
        {
            float best = float.MaxValue; int hit = 0;
            for (int i = 1; i <= 2; i++)
            {
                var t = i == 1 ? buttonOne : buttonTwo;
                if (t == null) continue;
                var col = t.GetComponent<Collider>();
                RaycastHit h;
                if (col != null && col.Raycast(ray, out h, 20f) && h.distance < best) { best = h.distance; hit = i; }
            }
            return hit;
        }

        // ── 연출 ──────────────────────────────────────────

        /// <summary>정답 한 칸. 눌린 돌이 들어갔다 → 달칵 → 새 안식처로 살짝 복귀.
        /// 그동안 **반대쪽 돌도 함께** 한 칸 내려앉는다 (안쪽 빗장이 한 칸 밀린 것).</summary>
        IEnumerator Press(int which)
        {
            busy = true;
            float from = step * notch;
            step++;
            float rest = step * notch;

            float pressedTo = rest + stroke;
            yield return Ease(from, pressedTo, from, rest, which, pressIn, true);
            Play(clipLatch, ref genLatch, StoneSfx.Latch);
            yield return Ease(pressedTo, rest, rest, rest, which, pressBack, true);

            if (step >= answer.Length) { yield return Succeed(); yield break; }
            busy = false;
        }

        /// <summary>오답. 「툭」 → 양쪽이 함께 「덜컥」 주저앉음 → 잠금 해제 → 함께 「툭」 튀어나옴.
        /// 글자는 한 자도 띄우지 않는다 — 두 돌이 동시에 튀어나오는 것만으로 전달한다.</summary>
        IEnumerator Fail(int which)
        {
            busy = true;
            float held = step * notch;

            // ① 잘못 누른 돌은 조금 들어가다 만다 — 죽은 「툭」
            Play(clipDead, ref genDead, StoneSfx.DeadThud);
            yield return Ease(held, held + 0.008f, held, held, which, 0.07f, true);
            yield return Ease(held + 0.008f, held, held, held, which, 0.05f, true);
            yield return new WaitForSeconds(0.06f);

            // ② 양쪽이 함께 주저앉는다
            Play(clipClunk, ref genClunk, StoneSfx.Clunk);
            yield return SlideBoth(held, held + clunkDepth, 0.09f);
            yield return new WaitForSeconds(0.13f);

            // ③ 쌓인 것이 통째로 풀리며 함께 튀어나온다 (넘어갔다 제자리)
            Play(clipPop, ref genPop, StoneSfx.PopOut);
            yield return SlideBoth(held + clunkDepth, -popOvershoot, 0.10f);
            yield return SlideBoth(-popOvershoot, 0f, 0.09f);

            step = 0;
            busy = false;
        }

        /// <summary>성공. 마지막 「달칵」은 이미 <see cref="Press"/> 가 냈다 — 여기서는
        /// 정적을 두고, 「드르륵—철컥」에 맞춰 기존 암문 개방 연출을 시작한 뒤 물러난다.</summary>
        IEnumerator Succeed()
        {
            finished = true;
            yield return new WaitForSeconds(silence);            // 맞았는지 아직 모른다

            Play(clipRelease, ref genRelease, StoneSfx.Release);
            float clackAt = clipRelease != null ? clipRelease.length * 0.5f : StoneSfx.ClackAt;
            yield return new WaitForSeconds(clackAt);            // 빗장이 걸리는 순간에 맞춘다

            if (door != null) { door.SolvePuzzle(); door.Open(); }

            yield return new WaitForSeconds(exitDelay);
            var rig = FindFirstObjectByType<DebugFocusRig>();
            if (rig != null) rig.ExitFocus();
            busy = false;
        }

        // ── 움직임 도우미 ─────────────────────────────────

        /// <summary>한쪽 돌은 a→b, 반대쪽은 c→d 로 동시에 민다.</summary>
        IEnumerator Ease(float a, float b, float c, float d, int which, float time, bool easeOut)
        {
            float t = 0f;
            while (t < time)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / time);
                u = easeOut ? 1f - (1f - u) * (1f - u) : u * u * (3f - 2f * u);
                float p = Mathf.Lerp(a, b, u), o = Mathf.Lerp(c, d, u);
                if (which == 1) ApplyDepth(p, o); else ApplyDepth(o, p);
                yield return null;
            }
            if (which == 1) ApplyDepth(b, d); else ApplyDepth(d, b);
        }

        IEnumerator SlideBoth(float a, float b, float time)
        {
            float t = 0f;
            while (t < time)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / time);
                u = u * u * (3f - 2f * u);
                float p = Mathf.Lerp(a, b, u);
                ApplyDepth(p, p);
                yield return null;
            }
            ApplyDepth(b, b);
        }

        void ApplyDepth(float one, float two)
        {
            var ax = pressAxis.sqrMagnitude < 1e-6f ? Vector3.right : pressAxis.normalized;
            if (buttonOne != null) buttonOne.localPosition = restOne + ax * one;
            if (buttonTwo != null) buttonTwo.localPosition = restTwo + ax * two;
        }

        void Play(AudioClip asset, ref AudioClip cache, System.Func<AudioClip> make)
        {
            var clip = asset;
            if (clip == null)
            {
                if (cache == null) cache = make();
                clip = cache;
            }
            if (clip != null && audioSrc != null) audioSrc.PlayOneShot(clip, volume);
        }
    }
}
