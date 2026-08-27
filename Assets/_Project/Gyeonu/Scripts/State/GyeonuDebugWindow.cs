using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 제3사건 상태 창 (2026-08-25) — Play 중 <b>F9</b>.
    /// 점수 3축·보유 단서·진행 플래그·시간대·날씨를 한눈에 보고 직접 만진다.
    ///
    /// ■ 왜 씬에 두지 않고 스스로 뜨는가
    ///   씬은 8개고 배경 배치는 건드리지 않기로 했다. 씬 파일을 하나도 고치지 않으려고
    ///   <see cref="RuntimeInitializeOnLoadMethod"/> 로 자기 오브젝트를 만들어
    ///   DontDestroyOnLoad에 얹는다 — 어느 씬에서 Play를 시작해도 F9면 뜬다.
    ///
    /// ■ 창이 떠 있는 동안
    ///   커서를 풀고 <see cref="DebugWalkController.uiOpen"/> 를 켜 이동·시선을 멈춘다
    ///   (소지품 판과 같은 방식). 닫으면 조작이 돌아온다.
    ///
    /// ■ 이것은 디버그 UI다
    ///   IMGUI라 VR에는 보이지 않는다. 최종 저널·점수 표시는 별도 월드 UI로 만든다.
    /// </summary>
    [AddComponentMenu("")]
    public class GyeonuDebugWindow : MonoBehaviour
    {
        public const Key ToggleKey = Key.F9;

        static GyeonuDebugWindow _inst;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _inst = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Spawn()
        {
            if (_inst != null) return;
            var go = new GameObject("[제3사건_상태창]");
            DontDestroyOnLoad(go);
            _inst = go.AddComponent<GyeonuDebugWindow>();
        }

        /// <summary>창이 떠 있는가. 다른 입력이 비켜야 할 때 본다.</summary>
        public static bool Visible { get; private set; }

        // 폭 640 — 이보다 좁으면 머리글의 "예상 엔딩"과 시간대가 스크롤바에 잘린다 (2026-08-25 실측)
        Rect _rect = new Rect(16f, 16f, 640f, 640f);
        Vector2 _scroll;
        DebugWalkController _walk;
        bool _walkWasOpen;

        // 접었다 펴는 구획
        bool _sScore = true, _sClue = true, _sTalk, _sFlag, _sWorld = true, _sTool;

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb[ToggleKey].wasPressedThisFrame) Toggle();
        }

        void Toggle()
        {
            Visible = !Visible;

            if (_walk == null) _walk = FindFirstObjectByType<DebugWalkController>(FindObjectsInactive.Exclude);

            if (Visible)
            {
                _walkWasOpen = _walk != null && _walk.uiOpen;
                if (_walk != null) _walk.uiOpen = true;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                if (_walk != null) _walk.uiOpen = _walkWasOpen;
                if (!_walkWasOpen)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }

        void OnGUI()
        {
            if (!Visible) return;

            _rect.height = Mathf.Min(Screen.height - 32f, 700f);
            _rect = GUILayout.Window(GetInstanceID(), _rect, Draw, "제3사건 상태  (F9로 닫기)");
        }

        void Draw(int id)
        {
            _scroll = GUILayout.BeginScrollView(_scroll);

            Header();
            if (Section("① 점수 3축", ref _sScore)) Scores();
            if (Section("② 단서 · 모순", ref _sClue)) Clues();
            if (Section("③ 대화 (견우 · 수령 · 그 밖)", ref _sTalk)) Talk();
            if (Section("④ 진행 플래그", ref _sFlag)) Flags();
            if (Section("⑤ 시간대 · 날씨 · 진행", ref _sWorld)) World();
            if (Section("⑥ 도구", ref _sTool)) Tools();

            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        // ── 구획 머리 ────────────────────────────────────────

        static bool Section(string title, ref bool open)
        {
            GUILayout.Space(4f);
            open = GUILayout.Toggle(open, (open ? "▼ " : "▶ ") + title, GUI.skin.button);
            return open;
        }

        // ── 헤더 ─────────────────────────────────────────────

        void Header()
        {
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label("신뢰 " + GyeonuCase.Trust + "   증거 " + GyeonuCase.Evidence + "   경계 " + GyeonuCase.Alert);
            GUILayout.FlexibleSpace();
            GUILayout.Label(GyeonuCase.TimeLabel + " · " + (GyeonuCase.Rain ? "비" : "맑음"));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label("저널: " + GyeonuCase.StageLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label("입증 " + (GyeonuCase.Proven ? "○" : "×") + "   예상 엔딩: " + GyeonuCase.EndingLabel);
            GUILayout.EndHorizontal();

            if (GyeonuCase.GyeonuLocked)
                GUILayout.Label("⚠ 견우 모욕 3회 — 신뢰도 0 고정, 핵심 대화 잠김 (지도 획득 불가)");
            if (GyeonuCase.C5LostForever)
                GUILayout.Label("⚠ 장부 소각 — C5 영구 소실");
            if (GyeonuCase.NightsRemaining > 0)
                GUILayout.Label("⚠ 서고 진입까지 남은 밤 " + GyeonuCase.NightsRemaining);
        }

        // ── ① 점수 ───────────────────────────────────────────

        void Scores()
        {
            Axis("신뢰도", GyeonuCase.Trust, GyeonuCase.SetTrust);
            Axis("증거도", GyeonuCase.Evidence, GyeonuCase.SetEvidence);
            Axis("경계도", GyeonuCase.Alert, GyeonuCase.SetAlert);

            GUILayout.BeginHorizontal();
            GUILayout.Label("임계 발화:", GUILayout.Width(70f));
            foreach (Threshold t in System.Enum.GetValues(typeof(Threshold)))
                GUILayout.Label((GyeonuCase.Fired(t) ? "●" : "○") + t, GUILayout.Width(88f));
            GUILayout.EndHorizontal();
        }

        static void Axis(string name, int value, System.Action<int> set)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name + " " + value, GUILayout.Width(78f));
            int v = Mathf.RoundToInt(GUILayout.HorizontalSlider(value, 0f, 100f, GUILayout.Width(240f)));
            if (GUILayout.Button("−10", GUILayout.Width(38f))) v = value - 10;
            if (GUILayout.Button("−5", GUILayout.Width(34f))) v = value - 5;
            if (GUILayout.Button("+5", GUILayout.Width(34f))) v = value + 5;
            if (GUILayout.Button("+10", GUILayout.Width(38f))) v = value + 10;
            GUILayout.EndHorizontal();
            if (v != value) set(Mathf.Clamp(v, 0, 100));
        }

        // ── ② 단서 ───────────────────────────────────────────

        void Clues()
        {
            GUILayout.Label("체크 = 보유. [제] = 견우에게 제시함 (신뢰도 반영, 1회만)");

            char series = ' ';
            foreach (var info in ClueTable.All)
            {
                if (info.Series != series)
                {
                    series = info.Series;
                    GUILayout.Label(series == 'A' ? "— A 전설과 옛길 —"
                                  : series == 'B' ? "— B 올해의 실종 —" : "— C 진범 —");
                }

                bool has = GyeonuCase.HasClue(info.id);
                GUILayout.BeginHorizontal();

                bool now = GUILayout.Toggle(has, "", GUILayout.Width(18f));
                if (now != has) { if (now) GyeonuCase.AddClue(info.id); else GyeonuCase.RemoveClue(info.id); }

                string tail = (info.evidence != 0 ? "  증" + info.evidence : "")
                            + (info.trust != 0 ? "  신" + (info.trust > 0 ? "+" : "") + info.trust : "")
                            + (info.redHerring ? "  (미끼)" : "");
                GUILayout.Label(ClueTable.Label(info.id) + tail);
                GUILayout.FlexibleSpace();

                bool presented = GyeonuCase.HasPresented(info.id);
                GUI.enabled = has && !presented && info.trust != 0;
                if (GUILayout.Button(presented ? "[제]" : "제시", GUILayout.Width(44f)))
                    GyeonuCase.PresentClueToGyeonu(info.id);
                GUI.enabled = true;

                GUILayout.EndHorizontal();
            }

            GUILayout.Space(4f);
            GUILayout.Label("— 모순 (각 증거도 +10) —");
            foreach (ContradictionId m in System.Enum.GetValues(typeof(ContradictionId)))
            {
                bool has = GyeonuCase.HasContradiction(m);
                bool now = GUILayout.Toggle(has, ClueTable.Label(m));
                if (now != has) { if (now) GyeonuCase.AddContradiction(m); else GyeonuCase.RemoveContradiction(m); }
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("핵심 3종 (C1·C2·C3) 지급"))
                foreach (var c in ClueTable.CoreClues) GyeonuCase.AddClue(c);
            if (GUILayout.Button("단서 전부 지급"))
                foreach (var i in ClueTable.All) GyeonuCase.AddClue(i.id);
            GUILayout.EndHorizontal();
        }

        // ── ③ 대화 ───────────────────────────────────────────

        void Talk()
        {
            GUILayout.Label("견우 — 호의 " + GyeonuCase.GyeonuFavorCount + "/3   압박 "
                          + GyeonuCase.GyeonuPressureCount + "/3   모욕 " + GyeonuCase.GyeonuInsultCount + "/3");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("호의 +5")) GyeonuCase.ReportGyeonuTone(TalkTone.Favor);
            if (GUILayout.Button("중립 0")) GyeonuCase.ReportGyeonuTone(TalkTone.Neutral);
            if (GUILayout.Button("압박 −5")) GyeonuCase.ReportGyeonuTone(TalkTone.Pressure);
            if (GUILayout.Button("모욕 −15")) GyeonuCase.ReportGyeonuTone(TalkTone.Insult);
            GUILayout.EndHorizontal();
            GUILayout.Label("⚠ AI 등급 판정은 아직 없다 — 여기 버튼이 판정 결과를 대신 넣는 자리다.");

            GUILayout.Space(4f);
            GUILayout.Label("수령에게 캐묻기 (경계도)");
            foreach (AlertTopic t in System.Enum.GetValues(typeof(AlertTopic)))
            {
                int delta, max;
                ClueTable.Alert(t, out delta, out max);
                if (max <= 0) continue;
                if (GUILayout.Button(ClueTable.Title(t) + "  (+" + delta + ", 최대 " + max + "회)"))
                    GyeonuCase.AskMagistrate(t);
            }

            GUILayout.Space(4f);
            GUILayout.Label("암행어사 신분 — 실제 증명만 반영 (말뿐인 허세는 0)");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("주모에게 증명 (소문 +40)")) GyeonuCase.RevealIdentity(NpcId.Jumo);
            if (GUILayout.Button("수령에게 증명 (+40)")) GyeonuCase.RevealIdentity(NpcId.Magistrate);
            if (GUILayout.Button("견우에게 증명 (0)")) GyeonuCase.RevealIdentity(NpcId.Gyeonu);
            GUILayout.EndHorizontal();
            GUILayout.Label("신분 공개 " + (GyeonuCase.IdentityRevealed ? "○" : "×")
                          + "   마을에 소문 " + (GyeonuCase.IdentitySpreadInVillage ? "○" : "×"));

            GUILayout.Space(4f);
            GUILayout.Label("NPC 무례 누적 (3회면 태도가 닫힌다)");
            foreach (NpcId n in System.Enum.GetValues(typeof(NpcId)))
            {
                if (n == NpcId.Gyeonu) continue;
                int c = GyeonuCase.NpcInsultCount(n);
                if (c == 0 && n != NpcId.Jumo && n != NpcId.Mother && n != NpcId.FestivalMerchant) continue;
                GUILayout.BeginHorizontal();
                GUILayout.Label(n + "  " + c + "/3" + (GyeonuCase.NpcHostile(n) ? "  ← 닫힘" : ""), GUILayout.Width(220f));
                if (GUILayout.Button("무례", GUILayout.Width(50f))) GyeonuCase.ReportNpcInsult(n);
                if (GUILayout.Button("회복", GUILayout.Width(50f))) GyeonuCase.RecoverNpc(n);
                GUILayout.EndHorizontal();
            }
        }

        // ── ④ 플래그 ─────────────────────────────────────────

        void Flags()
        {
            foreach (var e in GyeonuWorld.Catalog)
            {
                bool has = GyeonuWorld.Has(e.key);
                bool now = GUILayout.Toggle(has, e.label + "   (" + e.key + ")");
                if (now != has) GyeonuWorld.Set(e.key, now);
            }

            GUILayout.Space(4f);
            GUILayout.Label("소지품 — 아이템 id가 곧 단서 코드다");
            var cat = Inventory.Catalog;
            foreach (var it in cat)
            {
                if (it == null) continue;
                GUILayout.BeginHorizontal();
                GUILayout.Label((Inventory.Has(it) ? "● " : "○ ") + it.Key + " " + it.displayName);
                GUILayout.FlexibleSpace();
                GUI.enabled = !Inventory.Has(it);
                if (GUILayout.Button("지급", GUILayout.Width(50f))) Inventory.Add(it);
                GUI.enabled = Inventory.Has(it);
                if (GUILayout.Button("회수", GUILayout.Width(50f))) Inventory.Remove(it);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
        }

        // ── ⑤ 세계 ───────────────────────────────────────────

        void World()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("시간대", GUILayout.Width(60f));
            foreach (TimeOfDay t in System.Enum.GetValues(typeof(TimeOfDay)))
            {
                bool on = GyeonuCase.Time == t;
                string n = t == TimeOfDay.Day ? "낮" : t == TimeOfDay.EarlyNight ? "초밤" : "늦은 밤";
                if (GUILayout.Toggle(on, n, GUI.skin.button, GUILayout.Width(70f)) && !on) GyeonuCase.Time = t;
            }
            GUILayout.Space(12f);
            GUILayout.Label("날씨", GUILayout.Width(40f));
            if (GUILayout.Toggle(!GyeonuCase.Rain, "맑음", GUI.skin.button, GUILayout.Width(56f)) && GyeonuCase.Rain)
                GyeonuCase.Rain = false;
            if (GUILayout.Toggle(GyeonuCase.Rain, "비", GUI.skin.button, GUILayout.Width(56f)) && !GyeonuCase.Rain)
                GyeonuCase.Rain = true;
            GUILayout.EndHorizontal();

            GUILayout.Label("밤 씬 이동 " + GyeonuCase.NightSceneTransitions
                          + "/3 (또는 B5 획득 → 늦은 밤)   시드됨 " + (GyeonuCase.TimeSeeded ? "○" : "×"));

            GUILayout.BeginHorizontal();
            GUILayout.Label("막", GUILayout.Width(30f));
            foreach (Act a in System.Enum.GetValues(typeof(Act)))
            {
                bool on = GyeonuCase.CurrentAct == a;
                if (GUILayout.Toggle(on, a.ToString(), GUI.skin.button, GUILayout.Width(96f)) && !on)
                    GyeonuCase.CurrentAct = a;
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            bool k = GUILayout.Toggle(GyeonuCase.HasSeonaHouseKey, " 선아 집 열쇠");
            if (k != GyeonuCase.HasSeonaHouseKey) GyeonuCase.HasSeonaHouseKey = k;
            GUILayout.Space(20f);
            bool r = GUILayout.Toggle(GyeonuCase.SeonaRescued, " 선아 구출");
            if (r != GyeonuCase.SeonaRescued) GyeonuCase.SeonaRescued = r;
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("밤 하나 넘기기", GUILayout.Width(120f))) GyeonuCase.ConsumeNight();
            GUILayout.EndHorizontal();
        }

        // ── ⑥ 도구 ───────────────────────────────────────────

        void Tools()
        {
            bool ig = GUILayout.Toggle(GyeonuWorld.DebugIgnoreConditions, " 진행 조건 무시 (모든 잠금 통과)");
            if (ig != GyeonuWorld.DebugIgnoreConditions) GyeonuWorld.DebugIgnoreConditions = ig;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("임계 되돌리기")) GyeonuCase.ClearThresholds();
            if (GUILayout.Button("배점 검산 로그")) Debug.Log("[제3사건] 검산\n" + ClueTable.SelfCheck());
            if (GUILayout.Button("엔딩 확정")) GyeonuCase.FinishCase();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("저장")) GyeonuSave.Save();
            if (GUILayout.Button("불러오기")) GyeonuSave.Load();
            if (GUILayout.Button("저장 삭제")) GyeonuSave.Delete();
            GUILayout.EndHorizontal();

            GUI.color = new Color(1f, 0.8f, 0.8f);
            if (GUILayout.Button("전체 초기화 (새 판)"))
            {
                GyeonuCase.ResetAll();
                Inventory.Clear();
            }
            GUI.color = Color.white;
        }
    }
}
