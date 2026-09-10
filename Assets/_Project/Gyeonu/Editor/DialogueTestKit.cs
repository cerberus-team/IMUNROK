using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Gyeonu.EditorTools
{
    /// <summary>
    /// 대화 시스템 손검증 도구 (2026-08-25). Play 중에 메뉴로 눌러 한 단계씩 확인한다.
    ///
    /// ■ 왜 있는가
    ///   대화는 <b>걷기 → 조준 → 클릭 → 타자 → 제시</b> 가 줄줄이 이어져야 한 바퀴가 돈다.
    ///   배치안을 셋 견주려면 그 바퀴를 세 번 돌려야 하는데, 매번 마을 끝에서 걸어오면
    ///   확인이 아니라 노동이 된다. 여기 모아 둔 것은 전부 <b>검증 전용</b>이고 게임 흐름에
    ///   손대지 않는다 — 씬에 아무것도 남기지 않으며, 빌드에도 들어가지 않는다(Editor 폴더).
    /// </summary>
    public static class DialogueTestKit
    {
        const string Npc = "Gyeonu";

        [MenuItem("Tools/이문록/대화 검증/① 견우 앞에 서기 + 단서 채우기", true)]
        [MenuItem("Tools/이문록/대화 검증/② 배치안 A — 좌측판", true)]
        [MenuItem("Tools/이문록/대화 검증/③ 배치안 B — 하단띠", true)]
        [MenuItem("Tools/이문록/대화 검증/④ 배치안 C — 말풍선", true)]
        [MenuItem("Tools/이문록/대화 검증/⑤ 증거 제시 판 열기", true)]
        [MenuItem("Tools/이문록/대화 검증/⑥ 대화 끝내기", true)]
        [MenuItem("Tools/이문록/대화 검증/상태 보기", true)]
        static bool OnlyInPlay() => Application.isPlaying;

        [MenuItem("Tools/이문록/대화 검증/① 견우 앞에 서기 + 단서 채우기")]
        public static void Stand()
        {
            EditorApplication.isPaused = false;
            Application.runInBackground = true;

            var npc = GameObject.Find(Npc);
            var walk = Object.FindFirstObjectByType<DebugWalkController>();
            if (npc == null || walk == null) { Debug.LogError("[대화검증] 견우 또는 워커가 없다."); return; }

            var cc = walk.GetComponent<CharacterController>();
            Vector3 stand = npc.transform.position + npc.transform.forward * 2.6f;
            stand.y = 0.08f;
            if (cc != null) cc.enabled = false;
            walk.transform.position = stand;
            Vector3 look = npc.transform.position - stand; look.y = 0f;
            walk.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
            if (cc != null) cc.enabled = true;
            walk.Pitch = 0f;

            // 정보 단서 여럿 + 물건 단서 하나(C4 서책) — 제시 판에 두 갈래가 다 보이게
            foreach (var code in new[] { "A3", "A6", "A7", "B3", "B4", "B6", "B7", "C6", "C7" })
                if (ClueTable.TryParse(code, out var id)) GyeonuCase.AddClue(id);
            var book = Inventory.Find("C4");
            if (book != null && Inventory.Add(book))
            {
                // ⚠️ 처음 얻은 물건은 소지품 판이 스스로 조사 화면을 띄운다(autoShowOnPickup).
                //    검증에서는 그 화면이 대화창을 통째로 가리므로 곧바로 닫는다.
                var input = walk.eye.GetComponent<InventoryInput>();
                if (input != null) input.ClosePanel();
            }

            Debug.Log("[대화검증] 견우 앞 " + stand + " / 단서 " + GyeonuCase.ClueCount + "종, 소지품 " + Inventory.Count + "개");
        }

        [MenuItem("Tools/이문록/대화 검증/② 배치안 A — 좌측판")]
        public static void LayoutA() => Talk(DialogueLayout.A_좌측판);

        [MenuItem("Tools/이문록/대화 검증/③ 배치안 B — 하단띠")]
        public static void LayoutB() => Talk(DialogueLayout.B_하단띠);

        [MenuItem("Tools/이문록/대화 검증/④ 배치안 C — 말풍선")]
        public static void LayoutC() => Talk(DialogueLayout.C_말풍선);

        /// <summary>
        /// 배치안을 바꿔 다시 말을 건다.
        ///
        /// ⚠️ 물러나기와 다시 들어가기를 <b>같은 프레임에</b> 하면 안 된다 — 포커스 리그는
        ///    Exit 보간이 도는 동안 Idle이 아니라서 새 진입을 조용히 흘려보낸다. 그래서
        ///    "물러난 뒤 Idle이 되면 들어간다"를 <see cref="EditorApplication.update"/> 로 기다린다
        ///    (2026-08-25 실측: 한 번에 하면 대화가 열리지 않고 걷기로 돌아가 버렸다).
        /// </summary>
        static void Talk(DialogueLayout layout)
        {
            EditorApplication.isPaused = false;
            var npc = GameObject.Find(Npc);
            var talk = npc != null ? npc.GetComponent<NpcDialogue>() : null;
            var walk = Object.FindFirstObjectByType<DebugWalkController>();
            if (talk == null || walk == null) { Debug.LogError("[대화검증] 대화 상대가 없다."); return; }

            talk.layout = layout;
            var rig = walk.eye.GetComponent<DebugFocusRig>();
            if (rig == null || !rig.IsFocusing)
            {
                talk.Interact(walk.eye.gameObject);
                Debug.Log("[대화검증] 배치안 " + layout + " 로 대화 시작");
                return;
            }

            rig.ExitFocus();
            _pendingLayout = layout;
            EditorApplication.update -= WaitAndTalk;
            EditorApplication.update += WaitAndTalk;
        }

        static DialogueLayout _pendingLayout;

        static void WaitAndTalk()
        {
            if (!Application.isPlaying) { EditorApplication.update -= WaitAndTalk; return; }
            var walk = Object.FindFirstObjectByType<DebugWalkController>();
            var npc = GameObject.Find(Npc);
            var talk = npc != null ? npc.GetComponent<NpcDialogue>() : null;
            if (walk == null || talk == null) { EditorApplication.update -= WaitAndTalk; return; }
            var rig = walk.eye.GetComponent<DebugFocusRig>();
            if (rig != null && rig.IsFocusing) return;      // 아직 물러나는 중
            EditorApplication.update -= WaitAndTalk;
            talk.layout = _pendingLayout;
            talk.Interact(walk.eye.gameObject);
            Debug.Log("[대화검증] 배치안 " + _pendingLayout + " 로 대화 시작");
        }

        [MenuItem("Tools/이문록/대화 검증/⑤ 증거 제시 판 열기")]
        public static void OpenPresent()
        {
            EditorApplication.isPaused = false;
            var ui = DialogueUI.Instance;
            if (ui == null || !ui.IsOpen) { Debug.LogError("[대화검증] 대화 중이 아니다."); return; }
            Click(ui, DialogueHotspot.Kind.단서열기);
        }

        [MenuItem("Tools/이문록/대화 검증/⑥ 대화 끝내기")]
        public static void EndTalk()
        {
            EditorApplication.isPaused = false;
            var walk = Object.FindFirstObjectByType<DebugWalkController>();
            var rig = walk != null ? walk.eye.GetComponent<DebugFocusRig>() : null;
            if (rig != null) rig.ExitFocus();
        }

        [MenuItem("Tools/이문록/대화 검증/상태 보기")]
        public static void Status()
        {
            var ui = DialogueUI.Instance;
            var npc = GameObject.Find(Npc);
            var talk = npc != null ? npc.GetComponent<NpcDialogue>() : null;
            var sess = talk != null ? talk.Session : null;
            Debug.Log("[대화검증] 배치안=" + (ui != null ? ui.Layout.ToString() : "―")
                + " 대화창=" + (ui != null && ui.IsOpen) + " 제시판=" + (ui != null && ui.IsPresenting)
                + " 대답기=" + (sess != null ? sess.BackendName : "―")
                + "\n신뢰도=" + GyeonuCase.Trust + " 단서=" + GyeonuCase.ClueCount
                + " 마이크=" + (VoiceInput.HasMicrophone ? VoiceInput.DeviceName : "없음")
                + " API키=" + GyeonuGeminiResponder.HasKey
                + (sess != null ? "\n" + sess.Dump() : ""));
        }

        /// <summary>
        /// 판 위의 자리를 <b>실제 광선으로</b> 가리켜 누른다.
        /// ⚠️ PointAt과 ClickHovered를 다른 호출로 나누면 안 된다 — 그 사이 프레임에
        ///    포커스 리그가 진짜 마우스 자리로 호버를 지운다(2026-08-25 실측).
        /// </summary>
        static void Click(DialogueUI ui, DialogueHotspot.Kind kind)
        {
            var cam = Camera.main;
            foreach (var spot in ui.GetComponentsInChildren<DialogueHotspot>(true))
            {
                if (spot.kind != kind || !spot.gameObject.activeInHierarchy) continue;
                ui.PointAt(cam.ScreenPointToRay(cam.WorldToScreenPoint(spot.transform.position)));
                ui.ClickHovered();
                return;
            }
            Debug.LogWarning("[대화검증] " + kind + " 자리를 찾지 못했다.");
        }

        /// <summary>대화창에 글을 채워 보낸다 (타자 대신).</summary>
        public static void Say(string text)
        {
            var ui = DialogueUI.Instance;
            var npc = GameObject.Find(Npc);
            var talk = npc != null ? npc.GetComponent<NpcDialogue>() : null;
            if (ui == null || !ui.IsOpen || talk == null || talk.Session == null) { Debug.LogError("[대화검증] 대화 중이 아니다."); return; }
            EditorApplication.isPaused = false;
            talk.Session.Ask(text);
        }

        /// <summary>제시 판에서 n번째 것을 골라 내민다.</summary>
        public static void PresentIndex(int n)
        {
            var npc = GameObject.Find(Npc);
            var talk = npc != null ? npc.GetComponent<NpcDialogue>() : null;
            if (talk == null || talk.Session == null) { Debug.LogError("[대화검증] 대화 중이 아니다."); return; }
            EditorApplication.isPaused = false;
            var list = talk.Session.Presentables();
            if (n < 0 || n >= list.Count) { Debug.LogError("[대화검증] 범위 밖: " + n + " / " + list.Count); return; }
            var item = list[n];
            if (ClueTable.TryParse(item.Key, out var id)) talk.Session.Present(id);
        }

        /// <summary>
        /// 목소리 경로 되먹임 검증 (2026-08-25).
        /// 사람이 마이크에 대고 말하는 것을 원격에서 흉내 낼 수 없으므로, <b>Gemini TTS로 만든
        /// 한국어 음성</b>을 <see cref="VoiceInput.EncodeWav"/> 로 굽고 받아쓰기에 넣어
        /// "WAV 굽기 → 업로드 → 한국어 받아쓰기"가 실제로 도는지 확인한다.
        ///
        /// <paramref name="b64Path"/> 는 24kHz·16비트·모노 PCM을 base64로 적어 둔 파일이다.
        /// </summary>
        public static void VoiceRoundTrip(string b64Path)
        {
            if (!Application.isPlaying) { Debug.LogError("[대화검증] Play 중에만 된다."); return; }
            var runner = Object.FindFirstObjectByType<DebugWalkController>();
            if (runner == null) { Debug.LogError("[대화검증] 코루틴을 돌릴 것이 없다."); return; }

            byte[] pcm = System.Convert.FromBase64String(System.IO.File.ReadAllText(b64Path).Trim());
            var samples = new float[pcm.Length / 2];
            for (int i = 0; i < samples.Length; i++)
                samples[i] = (short)(pcm[i * 2] | (pcm[i * 2 + 1] << 8)) / 32768f;

            byte[] wav = VoiceInput.EncodeWav(samples, 24000);
            Debug.Log("[대화검증] 시험용 WAV " + wav.Length + "바이트 (" + (samples.Length / 24000f).ToString("F1") + "초) — 받아쓰기 보냄");

            GyeonuGeminiResponder.Transcribe(runner, wav,
                t => Debug.Log("[대화검증] ✅ 받아 적음: \"" + t + "\""),
                e => Debug.LogError("[대화검증] ❌ 받아쓰기 실패: " + e));
        }

        /// <summary>제시 목록을 로그로 — 물건과 정보가 한 목록에 있는지 확인한다.</summary>
        public static void DumpPresentables()
        {
            var npc = GameObject.Find(Npc);
            var talk = npc != null ? npc.GetComponent<NpcDialogue>() : null;
            if (talk == null || talk.Session == null) { Debug.LogError("[대화검증] 대화 중이 아니다."); return; }
            var sb = new System.Text.StringBuilder("[대화검증] 내밀 수 있는 것\n");
            var list = talk.Session.Presentables();
            for (int i = 0; i < list.Count; i++)
                sb.AppendLine("  " + i + ". " + list[i].displayName +
                              (list[i].modelPrefab != null ? "  〈물건·모형있음〉" : "  〈정보〉"));
            Debug.Log(sb.ToString());
        }
    }
}
