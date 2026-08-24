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

        // ── 나침반 UI 치수 ────────────────────────────────
        // ⚠️ 중심을 반지름만큼만 띄우면 안 된다 — 이름표가 테두리 **바깥**(반지름+18)에 앉고
        //    글자 높이의 절반이 더 나가므로, 그만큼 여유를 두지 않으면 北과 西가 화면 밖으로 잘린다
        //    (2026-08-23 실측: 중심 106·반지름 74에서 위·왼쪽이 잘렸다).
        const float Radius = 68f;
        const float LabelOut = 18f;                       // 테두리에서 이름표까지
        const float Margin = 30f;                          // 화면 모서리에서 띄우는 여백
        static readonly Vector2 Center = new Vector2(Radius + LabelOut + Margin, Radius + LabelOut + Margin);

        static readonly Color BrassDim = new Color(0.62f, 0.50f, 0.30f, 0.75f);
        static readonly Color BrassLit = new Color(1.00f, 0.86f, 0.55f, 1f);
        static readonly Color SelColor = new Color(0.95f, 0.44f, 0.28f, 1f);   // 고른 고리 — 주홍
        static readonly Color Faint = new Color(0.80f, 0.74f, 0.62f, 0.55f);

        bool focused;
        bool solved;
        bool running;

        AudioSource oneShot, loopSrc;
        AudioClip madeFriction, madeDetent, madeLatch;
        Texture2D dialTex, needleTex, dotTex;
        GUIStyle bigStyle, smallStyle, hintStyle;

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
            Kill(ref dialTex); Kill(ref needleTex); Kill(ref dotTex);
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

        // ── 나침반 UI ─────────────────────────────────────

        void OnGUI()
        {
            if (!focused || rings == null || rings.RingCount == 0) return;
            EnsureUi();

            int sel = rings.Selected;
            int selSlot = rings.Slot(sel);

            // 어느 방위에 고리가 하나라도 놓였는가
            bool[] occupied = new bool[8];
            for (int i = 0; i < rings.RingCount; i++)
            {
                int s = rings.Slot(i);
                if (s >= 0 && s < 8) occupied[s] = true;
            }

            // 판
            GUI.color = Color.white;
            float pad = Radius + 14f;
            GUI.DrawTexture(new Rect(Center.x - pad, Center.y - pad, pad * 2f, pad * 2f), dialTex);

            // 여덟 눈금 + 이름표
            for (int s = 0; s < 8; s++)
            {
                bool isSel = (s == selSlot);
                Color c = isSel ? SelColor : (occupied[s] ? BrassLit : Faint);

                Vector2 tick = Polar(s * 45f, Radius - 9f);
                GUI.color = c;
                float dsz = isSel ? 11f : (occupied[s] ? 9f : 6f);
                GUI.DrawTexture(new Rect(tick.x - dsz * 0.5f, tick.y - dsz * 0.5f, dsz, dsz), dotTex);

                var style = CompassNames.Cardinal(s) ? bigStyle : smallStyle;
                Vector2 lab = Polar(s * 45f, Radius + LabelOut);
                float w = CompassNames.Cardinal(s) ? 30f : 34f;
                float h = CompassNames.Cardinal(s) ? 26f : 18f;
                var old = style.normal.textColor;
                style.normal.textColor = c;
                GUI.Label(new Rect(lab.x - w * 0.5f, lab.y - h * 0.5f, w, h), CompassNames.Han[s], style);
                style.normal.textColor = old;
            }

            // 고리 바늘 — 고른 것은 맨 위에 굵고 길게, 색도 다르게
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < rings.RingCount; i++)
                {
                    bool isSel = (i == sel);
                    if ((pass == 0) == isSel) continue;          // 고른 고리를 나중에 그려 위로 올린다
                    Needle(rings.Bearing(i), isSel ? Radius - 16f : Radius - 30f,
                           isSel ? 13f : 8f, isSel ? SelColor : BrassDim);
                }

            // 가운데 축
            GUI.color = BrassLit;
            GUI.DrawTexture(new Rect(Center.x - 5f, Center.y - 5f, 10f, 10f), dotTex);

            // ⚠️ 안내 줄을 판 크기(pad)에 붙이면 **南 이름표와 겹친다** — 이름표는 판 바깥
            //    (반지름+LabelOut)에 앉기 때문이다 (2026-08-23 실측). 이름표보다 더 내려 적는다.
            GUI.color = new Color(1f, 0.92f, 0.78f, 0.8f);
            GUI.Label(new Rect(Center.x - pad, Center.y + Radius + LabelOut + 14f, pad * 2f, 20f),
                      solved ? "고리가 모두 물렸다" : "휠 — 고리 전환", hintStyle);
            GUI.color = Color.white;
        }

        static Vector2 Polar(float bearingDeg, float r)
        {
            // 0° = 위(北), 시계 방향. GUI는 y가 아래로 커진다
            float a = bearingDeg * Mathf.Deg2Rad;
            return new Vector2(Center.x + Mathf.Sin(a) * r, Center.y - Mathf.Cos(a) * r);
        }

        void Needle(float bearing, float len, float width, Color c)
        {
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(bearing, Center);
            GUI.color = c;
            GUI.DrawTexture(new Rect(Center.x - width * 0.5f, Center.y - len, width, len), needleTex);
            GUI.matrix = m;
        }

        void EnsureUi()
        {
            // ⚠️ 런타임 생성 텍스처를 static으로 캐시하지 말 것 — 도메인 리로드가 꺼진 프로젝트에서
            //    참조만 다음 플레이 세션으로 살아남고 내용이 죽는다 (비네트에서 이미 당한 함정).
            if (dialTex == null) dialTex = MakeDial();
            if (needleTex == null) needleTex = MakeNeedle();
            if (dotTex == null) dotTex = MakeDot();
            if (bigStyle == null)
            {
                bigStyle = new GUIStyle(GUI.skin.label) { fontSize = 21, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
                hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
            }
        }

        /// <summary>어두운 원판 + 놋쇠 테두리 + 안쪽 실선 하나.</summary>
        static Texture2D MakeDial()
        {
            const int res = 192;
            var t = new Texture2D(res, res, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
            float c = (res - 1) * 0.5f;
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    Color col = new Color(0f, 0f, 0f, 0f);
                    if (d < 0.90f) col = new Color(0.055f, 0.048f, 0.042f, 0.74f);          // 바탕
                    if (d > 0.855f && d < 0.925f) col = new Color(0.72f, 0.56f, 0.30f, 0.95f); // 테두리
                    if (d > 0.545f && d < 0.565f) col = new Color(0.55f, 0.45f, 0.28f, 0.55f); // 안쪽 실선
                    // 가장자리 한 픽셀만 부드럽게
                    if (d > 0.925f && d < 0.955f) col = new Color(0.72f, 0.56f, 0.30f, 0.95f * (1f - (d - 0.925f) / 0.03f));
                    t.SetPixel(x, y, col);
                }
            t.Apply();
            return t;
        }

        /// <summary>끝으로 갈수록 가늘어지는 바늘 (위가 끝).</summary>
        static Texture2D MakeNeedle()
        {
            const int w = 16, h = 64;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
            for (int y = 0; y < h; y++)
            {
                float u = y / (float)(h - 1);                 // 0 = 중심 쪽, 1 = 끝
                float half = Mathf.Lerp(3.4f, 0.9f, u * u);
                for (int x = 0; x < w; x++)
                {
                    float dx = Mathf.Abs(x - (w - 1) * 0.5f);
                    float a = Mathf.Clamp01(half - dx + 0.5f);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            t.Apply();
            return t;
        }

        static Texture2D MakeDot()
        {
            const int res = 24;
            var t = new Texture2D(res, res, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
            float c = (res - 1) * 0.5f;
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 3.2f)));
                }
            t.Apply();
            return t;
        }
    }
}
