using System.Collections;
using UnityEngine;
using IMUNROK.Common;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 혼천의 여덟 방위 퍼즐 (2026-08-23) — 선행 조건 · 정답 판정 · 나침반 UI · 성공 연출.
    ///
    /// 조작(무엇이 어떻게 도는가, 어디에 물리는가)은 <see cref="HoncheonuiFocusRings"/>가 쥐고,
    /// 여기는 **무엇이 정답인가**와 **다 맞으면 무슨 일이 벌어지는가**만 안다. 암문 자물쇠와 같은 결이다.
    ///
    /// ■ 선행 조건 — 선아의 관측 수기
    ///   메모가 없으면 조사해도 포커스에 들어가지 못한다. "왜 안 되지"가 아니라
    ///   "아직 읽을 것이 남았구나"로 읽히도록, 조건을 설명하지 않고 눈에 보이는 것만 말한다.
    ///
    /// ■ 개별 정답은 알려 주지 않는다
    ///   고리 하나가 맞았는지 틀렸는지 화면에 표시하지 않는다. 여섯이 **한꺼번에** 맞는 순간에만
    ///   반응이 있다. 나침반은 "지금 어디에 놓였는가"만 보여 준다 — 그것은 정답 정보가 아니라
    ///   기준 표점(금색 구슬)을 대신하는 눈금일 뿐이다.
    ///
    /// ■ 바깥부터 맞춰야 하는 이유
    ///   부모 고리를 돌리면 자식의 방위가 그만큼 함께 밀린다(실제 계층이 그렇다).
    ///   그래서 안쪽을 먼저 맞춰 놓아도 바깥을 건드리는 순간 다 밀린다 — 자연히 바깥부터가 된다.
    ///   자세한 방위 정의는 <see cref="HoncheonuiFocusRings"/> 주석 참고.
    /// </summary>
    [RequireComponent(typeof(HoncheonuiFocusRings))]
    public class HoncheonuiPuzzle : MonoBehaviour
    {
        [Header("연결")]
        public HoncheonuiFocusRings rings;

        [Header("정답 (고리 순서대로, 0=北 1=北東 2=東 3=南東 4=南 5=南西 6=西 7=北西)")]
        [Tooltip("자오환 남(4) · 적도환 동(2) · 황도환 북서(7) · 소형환1 서(6) · 소형환2 북(0) · 소형환3 남동(3)")]
        public int[] answer = { 4, 2, 7, 6, 0, 3 };

        [Header("진행 조건")]
        [Tooltip("이 플래그가 있어야 퍼즐을 시작할 수 있다 (선아의 관측 수기)")]
        public string requireFlag = GyeonuWorld.F_혼천의메모;
        [Tooltip("다 맞추면 서는 플래그. 한 번 서면 다시 풀지 않는다")]
        public string solvedFlag = GyeonuWorld.F_혼천의퍼즐;

        [Header("문구")]
        [TextArea] public string lockedHint = "고리가 여섯 겹으로 얽혀 있다. 어느 것부터 손대야 할지 알 수 없다.";
        [TextArea] public string solvedNow = "여섯 고리가 한꺼번에 물린다. 혼천의가 멎었다.";
        // ⚠️ 여기서 촛대를 알려 주면 안 된다 — 혼상을 돌리는 단계가 통째로 없어진다 (2026-08-23 수정).
        //    다음에 할 일만 가리킨다.
        [TextArea] public string solvedAfter = "고리가 이르는 대로 하늘이 섰다. 이제 안쪽 방의 혼상을 맞출 차례다.";

        [Header("소리 (비우면 절차 합성)")]
        public AudioClip frictionClip;
        public AudioClip detentClip;
        public AudioClip latchClip;

        // ── 나침반 ────────────────────────────────────────
        // 2026-08-26: 그리는 일은 통째로 CompassPanel(월드 캔버스)로 옮겼다.
        //   치수·색·"이름표가 테두리 바깥에 앉으니 여백을 더 두어야 한다"는 교훈까지 그쪽에 있다.
        //   여기는 "지금 보여 줄 때인가"만 판단한다.

        bool focused;
        bool solved;
        bool running;

        AudioSource oneShot, loopSrc;
        AudioClip madeFriction, madeDetent, madeLatch;
        CompassPanel compass;

        /// <summary>다 맞췄는가 (세션 유지).</summary>
        public bool Solved => solved;

        void Awake()
        {
            if (rings == null) rings = GetComponent<HoncheonuiFocusRings>();
            if (rings != null)
            {
                rings.puzzle = this;
                rings.RingDetent += OnDetent;
                rings.RingSettled += OnSettled;
                rings.FocusChanged += f => focused = f;
            }

            oneShot = gameObject.AddComponent<AudioSource>();
            oneShot.playOnAwake = false; oneShot.spatialBlend = 1f;
            oneShot.minDistance = 1.2f; oneShot.maxDistance = 12f;

            loopSrc = gameObject.AddComponent<AudioSource>();
            loopSrc.playOnAwake = false; loopSrc.loop = true; loopSrc.spatialBlend = 1f;
            loopSrc.minDistance = 1.0f; loopSrc.maxDistance = 8f; loopSrc.volume = 0f;

            // 이미 풀어 둔 세션이면 굳은 채로 시작한다
            if (!string.IsNullOrEmpty(solvedFlag) && GyeonuWorld.Has(solvedFlag))
            {
                solved = true;
                if (rings != null) rings.Locked = true;
            }
        }

        void OnDestroy()
        {
            if (rings != null)
            {
                rings.RingDetent -= OnDetent;
                rings.RingSettled -= OnSettled;
            }
            Kill(ref madeFriction); Kill(ref madeDetent); Kill(ref madeLatch);
            if (compass != null) Destroy(compass.gameObject);
        }

        static void Kill<T>(ref T o) where T : Object
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
            o = null;
        }

        // ── 선행 조건 ─────────────────────────────────────

        /// <summary>포커스에 들어가도 되는가. 안 되면 여기서 안내를 띄운다.</summary>
        public bool AllowFocus()
        {
            if (solved || GyeonuWorld.DebugIgnoreConditions) return true;
            if (string.IsNullOrEmpty(requireFlag) || GyeonuWorld.Has(requireFlag)) return true;
            DebugToast.ShowPinned(lockedHint);
            return false;
        }

        // ── 소리 ──────────────────────────────────────────

        AudioClip Friction => frictionClip != null ? frictionClip
                            : (madeFriction != null ? madeFriction : madeFriction = BrassSfx.Friction());
        AudioClip Detent => detentClip != null ? detentClip
                          : (madeDetent != null ? madeDetent : madeDetent = BrassSfx.Detent());
        AudioClip LatchClip => latchClip != null ? latchClip
                             : (madeLatch != null ? madeLatch : madeLatch = BrassSfx.Latch());

        void OnDetent(int i)
        {
            if (running) return;
            oneShot.pitch = Random.Range(0.96f, 1.05f);   // 똑같은 소리가 반복되면 기계로 들린다
            oneShot.PlayOneShot(Detent, 0.85f);
        }

        void Update()
        {
            // 마찰음 — 드래그 세기를 따라 오르내린다. 켜고 끄면 딸깍거리므로 볼륨만 흔든다.
            if (rings == null) return;
            bool want = focused && !solved && !running && rings.DragSpeed > 0.5f;
            if (want && !loopSrc.isPlaying) { loopSrc.clip = Friction; loopSrc.Play(); }
            float target = want ? Mathf.Clamp01(rings.DragSpeed / 14f) * 0.45f : 0f;
            loopSrc.volume = Mathf.MoveTowards(loopSrc.volume, target, Time.deltaTime * 3.5f);
            loopSrc.pitch = 0.80f + Mathf.Clamp01(rings.DragSpeed / 22f) * 0.45f;
            if (loopSrc.volume <= 0.001f && loopSrc.isPlaying) loopSrc.Stop();
        }

        /// <summary>
        /// 나침반을 판에 넘긴다 (2026-08-26 — 예전에는 <c>OnGUI</c>였다).
        ///
        /// 이 표시가 없으면 <b>여섯 고리가 어느 방위에 있는지 알 길이 없어 퍼즐을 풀 수 없다.</b>
        /// IMGUI라 HMD에는 아예 안 보였다 — 조사에서 최우선으로 꼽힌 문제다.
        /// 자리·크기·색은 <see cref="CompassPanel"/> 이 IMGUI 값 그대로 옮겨 담았다.
        /// </summary>
        void LateUpdate()
        {
            bool show = focused && rings != null && rings.RingCount > 0;
            if (!show)
            {
                if (compass != null) compass.gameObject.SetActive(false);
                return;
            }
            if (compass == null) compass = CompassPanel.Create(transform, rings);
            if (!compass.gameObject.activeSelf) compass.gameObject.SetActive(true);
            compass.solved = solved;
        }

        // ── 판정 ──────────────────────────────────────────

        void OnSettled(int i)
        {
            if (solved || running) return;
            if (!AllCorrect()) return;
            StartCoroutine(SuccessSeq());
        }

        bool AllCorrect()
        {
            if (rings == null || answer == null) return false;
            int n = Mathf.Min(rings.RingCount, answer.Length);
            if (n == 0) return false;
            for (int i = 0; i < n; i++)
                if (rings.Slot(i) != answer[i]) return false;
            return true;
        }

        IEnumerator SuccessSeq()
        {
            running = true;
            solved = true;
            loopSrc.Stop();
            oneShot.pitch = 1f;
            oneShot.PlayOneShot(LatchClip, 1f);
            rings.Locked = true;                          // 고리가 굳는다
            DebugToast.ShowPinned(solvedNow);

            yield return new WaitForSeconds(1.5f);        // 울림이 잦아들 동안 굳은 혼천의를 본다

            var rig = FindFirstObjectByType<DebugFocusRig>();
            if (rig != null) rig.ExitFocus();             // 포커스 종료

            // 여기서 여는 것은 **혼상 조작**뿐이다. 촛대 잠금은 혼상을 다 돌린 뒤에 풀린다
            // (HonsangFocusOrb) — 그래야 혼상을 돌리는 단계가 살아 있다.
            if (!string.IsNullOrEmpty(solvedFlag)) GyeonuWorld.Set(solvedFlag);
            Journal.Instance.AddClue(CaseId.Case3_Gyeonu, "honcheonui_solved",
                "혼천의의 여섯 고리를 수기가 이르는 방위에 맞췄다.");

            yield return new WaitForSeconds(0.8f);
            DebugToast.ShowPinned(solvedAfter);
            Debug.Log("[혼천의] 여덟 방위 정답 — 혼상에 불을 넣을 수 있다");
            running = false;
        }

    }
}
