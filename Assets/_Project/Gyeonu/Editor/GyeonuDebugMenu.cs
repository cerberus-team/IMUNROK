using UnityEditor;
using UnityEngine;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 배경 검증용 디버그 스위치 모음. 게임 진행 조건이 아직 안 붙은 동안
    /// 잠긴 길을 열어 보고, 시간대를 갈아 보고, 별 길을 켜 보는 데 쓴다.
    ///
    /// ⚠️ 전부 **Play 중 세션 상태**를 만진다. Play를 멈추면 도메인 리로드로 초기화된다.
    /// </summary>
    public static class GyeonuDebugMenu
    {
        const string Root = "Tools/이문록/디버그/";

        // ── 진행 조건 ─────────────────────────────────────

        [MenuItem(Root + "진행 조건 무시 (토글)", priority = 400)]
        static void ToggleIgnore()
        {
            GyeonuWorld.DebugIgnoreConditions = !GyeonuWorld.DebugIgnoreConditions;
            Debug.Log("[디버그] 진행 조건 무시 = " + GyeonuWorld.DebugIgnoreConditions);
        }

        [MenuItem(Root + "진행 조건 무시 (토글)", validate = true)]
        static bool ToggleIgnoreValidate()
        {
            Menu.SetChecked(Root + "진행 조건 무시 (토글)", GyeonuWorld.DebugIgnoreConditions);
            return true;
        }

        [MenuItem(Root + "견우마을 조건 충족 (지도 획득 + 길 밝힘)", priority = 401)]
        static void GrantGyeonuVillage()
        {
            GyeonuWorld.Set(GyeonuWorld.F_비밀지도획득);
            GyeonuWorld.Set(GyeonuWorld.F_타공지도_길밝힘);
            Debug.Log("[디버그] 비밀지도 획득 + 타공지도 길 밝힘 — 견우마을로 갈 수 있다");
        }

        [MenuItem(Root + "견우마을 조건 되돌리기", priority = 402)]
        static void RevokeGyeonuVillage()
        {
            GyeonuWorld.Set(GyeonuWorld.F_비밀지도획득, false);
            GyeonuWorld.Set(GyeonuWorld.F_타공지도_길밝힘, false);
            Debug.Log("[디버그] 견우마을 조건 해제 — 다시 막힌다");
        }

        [MenuItem(Root + "비밀문 잠금 해제", priority = 403)]
        static void UnlockSecretDoor()
        {
            GyeonuWorld.Set(GyeonuWorld.F_비밀문_해제);
            foreach (var d in Object.FindObjectsByType<LockedDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                d.Unlock();
            Debug.Log("[디버그] 비밀문 잠금 해제");
        }

        [MenuItem(Root + "암문 단서 획득", priority = 406)]
        static void GrantAmmunClue()
        {
            GyeonuWorld.Set(GyeonuWorld.F_암문단서);
            Debug.Log("[디버그] 암문 단서 획득 — 밤에 문 앞까지 가면 문틈 빛이 보인다 (길에는 표시 없음)");
        }

        [MenuItem(Root + "암문 단서 되돌리기", priority = 407)]
        static void RevokeAmmunClue()
        {
            GyeonuWorld.Set(GyeonuWorld.F_암문단서, false);
            Debug.Log("[디버그] 암문 단서 해제 — 물가가 다시 아무것도 아닌 곳이 된다");
        }

        [MenuItem(Root + "암문 퍼즐 조건 충족 (C4 서책 + 밤)", priority = 410)]
        static void GrantAmmunPuzzlePrereq()
        {
            var item = AssetDatabase.LoadAssetAtPath<InventoryItem>(
                "Assets/_Project/Gyeonu/Resources/GyeonuItems/Item_C4_아버지유품서책.asset");
            if (item != null) Inventory.Add(item);      // 획득하면 worldFlag(암문단서)가 함께 선다
            else GyeonuWorld.Set(GyeonuWorld.F_암문단서);
            GyeonuWorld.Night = true;
            Debug.Log("[디버그] C4 서책 소지 + 밤 — 이제 암문을 조사하면 돌 자물쇠 퍼즐이 시작된다 (정답 1 1 1 2 2 1)");
        }

        [MenuItem(Root + "암문 자물쇠 풀린 것으로 (퍼즐 건너뛰기)", priority = 411)]
        static void SolveAmmunPuzzle()
        {
            GyeonuWorld.Set(GyeonuWorld.F_암문퍼즐);
            Debug.Log("[디버그] 암문 자물쇠 해제 — 조사하면 곧장 열린다");
        }

        [MenuItem(Root + "암문 자물쇠 되잠그기", priority = 412)]
        static void RelockAmmunPuzzle()
        {
            GyeonuWorld.Set(GyeonuWorld.F_암문퍼즐, false);
            Debug.Log("[디버그] 암문 자물쇠 되잠금 — 다시 풀어야 한다");
        }

        // 관측실 사슬: 혼천의 성공 → 혼상 회전 → 촛대 → 점등. 한 칸씩 건너뛸 수 있게 나눠 뒀다.
        [MenuItem(Root + "관측실 ① 혼천의 퍼즐 풀린 것으로", priority = 413)]
        static void SolveHoncheonui()
        {
            GyeonuWorld.Set(GyeonuWorld.F_혼천의메모);
            GyeonuWorld.Set(GyeonuWorld.F_혼천의퍼즐);
            foreach (var p in Object.FindObjectsByType<HoncheonuiPuzzle>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (p.rings != null) p.rings.Locked = true;
            Debug.Log("[디버그] 혼천의 통과 — 이제 **혼상을 돌릴 수** 있다 (촛대는 아직 잠겨 있다)");
        }

        [MenuItem(Root + "관측실 ② 혼상 회전까지 끝난 것으로", priority = 414)]
        static void TurnHonsang()
        {
            SolveHoncheonui();
            GyeonuWorld.Set(GyeonuWorld.F_혼상회전);
            IMUNROK.Gyeonu.LanternPickup.PickupAllowed = true;
            Debug.Log("[디버그] 혼상 회전 통과 — 작업실 촛대를 집을 수 있다");
        }

        [MenuItem(Root + "관측실 사슬 되돌리기 (혼천의·혼상·촛대)", priority = 415)]
        static void ResetObservatoryChain()
        {
            GyeonuWorld.Set(GyeonuWorld.F_혼천의퍼즐, false);
            GyeonuWorld.Set(GyeonuWorld.F_혼상회전, false);
            GyeonuWorld.Set(GyeonuWorld.F_촛대소지, false);
            GyeonuWorld.Set(GyeonuWorld.F_혼상점등, false);
            IMUNROK.Gyeonu.LanternPickup.PickupAllowed = false;
            Debug.Log("[디버그] 관측실 사슬 해제 — Play를 다시 시작해야 고리·촛대가 처음으로 돌아간다");
        }

        [MenuItem(Root + "관아 개구멍 이야기 들음", priority = 408)]
        static void GrantGapHoleStory()
        {
            GyeonuWorld.Set(GyeonuWorld.F_개구멍이야기);
            Debug.Log("[디버그] 개구멍 이야기 들음 — 밤에 담장 그 자리에 가면 '밀기'가 뜬다");
        }

        [MenuItem(Root + "관아 개구멍 이야기 되돌리기", priority = 409)]
        static void RevokeGapHoleStory()
        {
            GyeonuWorld.Set(GyeonuWorld.F_개구멍이야기, false);
            Debug.Log("[디버그] 개구멍 이야기 해제 — 담장이 다시 그냥 담장으로 보인다");
        }

        [MenuItem(Root + "진행 상태 전부 초기화", priority = 404)]
        static void ResetAll()
        {
            GyeonuWorld.ResetAll();
            Debug.Log("[디버그] 진행 상태 초기화");
        }

        [MenuItem(Root + "현재 진행 상태 보기", priority = 405)]
        static void Dump()
        {
            var flags = GyeonuWorld.CaptureFlags();
            Debug.Log($"[디버그] 시간대={GyeonuWorld.SkyKey}  조건무시={GyeonuWorld.DebugIgnoreConditions}\n" +
                      "플래그: " + (flags.Length == 0 ? "(없음)" : string.Join(", ", flags)));
        }

        // ── 시간대 ────────────────────────────────────────

        [MenuItem(Root + "시간대 — 낮/밤 토글", priority = 420)]
        static void ToggleNight()
        {
            GyeonuWorld.Night = !GyeonuWorld.Night;
            Debug.Log("[디버그] 시간대 = " + GyeonuWorld.SkyKey);
        }

        [MenuItem(Root + "시간대 — 맑음/비 토글", priority = 421)]
        static void ToggleRain()
        {
            GyeonuWorld.Rain = !GyeonuWorld.Rain;
            Debug.Log("[디버그] 시간대 = " + GyeonuWorld.SkyKey);
        }

        // ── 소지품 ────────────────────────────────────────

        [MenuItem(Root + "소지품 — 시험 서책 획득", priority = 430)]
        static void GrantTestItem()
        {
            var item = UnityEditor.AssetDatabase.LoadAssetAtPath<InventoryItem>(
                "Assets/_Project/Gyeonu/Resources/GyeonuItems/Item_C4_아버지유품서책.asset");
            if (item == null)
            {
                Debug.LogWarning("[디버그] 시험 아이템이 아직 없다 — Tools ▸ 이문록 ▸ 소지품 ▸ 시험 아이템 만들기 먼저");
                return;
            }
            if (Inventory.Add(item)) Debug.Log("[디버그] 소지품에 넣음: " + item.displayName + " (Play 중 I 키로 열어 볼 것)");
            else Debug.Log("[디버그] 이미 지니고 있다: " + item.displayName);
        }

        // 견우 대화가 아직 없다 — 원래는 견우에게서 "받기"로 얻는 물건이다 (2026-08-23).
        [MenuItem(Root + "소지품 — 비밀지도 받기 (A1)", priority = 429)]
        static void GrantSecretMap()
        {
            var item = UnityEditor.AssetDatabase.LoadAssetAtPath<InventoryItem>(
                "Assets/_Project/Gyeonu/Resources/GyeonuItems/Item_A1_타공비밀지도.asset");
            if (item == null)
            {
                Debug.LogWarning("[디버그] 비밀지도가 아직 없다 — Tools ▸ 이문록 ▸ 소지품 ▸ 비밀지도 만들기 먼저");
                return;
            }
            if (Inventory.Add(item)) Debug.Log("[디버그] 소지품에 넣음: " + item.displayName + " (Play 중 I 키)");
            else Debug.Log("[디버그] 이미 지니고 있다: " + item.displayName);
        }

        [MenuItem(Root + "소지품 — 선아의 관측 수기 살피기 (혼천의 메모)", priority = 428)]
        static void GrantHoncheonMemo()
        {
            var item = UnityEditor.AssetDatabase.LoadAssetAtPath<InventoryItem>(
                "Assets/_Project/Gyeonu/Resources/GyeonuItems/Item_HONCHEON_MEMO_선아의관측수기.asset");
            if (item == null)
            {
                Debug.LogWarning("[디버그] 관측 수기가 아직 없다 — Tools ▸ 이문록 ▸ 소지품 ▸ 혼천의 퍼즐 준비 먼저");
                return;
            }
            if (Inventory.Add(item)) Debug.Log("[디버그] 소지품에 넣음: " + item.displayName + " (혼천의 퍼즐이 열린다)");
            else Debug.Log("[디버그] 이미 지니고 있다: " + item.displayName);
        }

        [MenuItem(Root + "소지품 — 전부 획득 (Resources 안의 모든 정의)", priority = 431)]
        static void GrantAllItems()
        {
            int n = 0;
            foreach (var it in Inventory.Catalog) if (Inventory.Add(it)) n++;
            Debug.Log($"[디버그] 소지품 {n}개 추가 (현재 {Inventory.Count}개)");
        }

        [MenuItem(Root + "소지품 — 비우기", priority = 432)]
        static void ClearInventory()
        {
            Inventory.Clear();
            Debug.Log("[디버그] 소지품 비움");
        }

        // ── 집무실 쌍학월도 렌즈 퍼즐 (2026-08-24) ────────

        [MenuItem(Root + "집무실 ① 렌즈 퍼즐 풀린 것으로 (문갑 열림)", priority = 416)]
        static void SolveLensPuzzle()
        {
            GyeonuWorld.Set(GyeonuWorld.F_렌즈퍼즐);
            foreach (var f in Object.FindObjectsByType<FurnitureParts>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (f.unlockFlag == GyeonuWorld.F_렌즈퍼즐) f.SetOpen(true);
            Debug.Log("[디버그] 렌즈 퍼즐 통과 — 문갑이 열렸다. 서랍 속 열쇠를 꺼낼 수 있다");
        }

        [MenuItem(Root + "집무실 ② 수령의 열쇠까지 손에 넣은 것으로", priority = 417)]
        static void GrantSuryeongKey()
        {
            SolveLensPuzzle();
            var it = Inventory.Find("SURYEONG_KEY");
            if (it == null) { Debug.LogWarning("[디버그] Resources/GyeonuItems 에 SURYEONG_KEY 가 없다"); return; }
            Inventory.Add(it);
            Debug.Log("[디버그] 수령의 비밀 열쇠 획득 — 병풍 뒤 비밀문을 클릭하면 열린다");
        }

        [MenuItem(Root + "집무실 렌즈 퍼즐 되돌리기", priority = 418)]
        static void ResetLensPuzzle()
        {
            GyeonuWorld.Set(GyeonuWorld.F_렌즈퍼즐, false);
            GyeonuWorld.Set(GyeonuWorld.F_수령열쇠, false);
            GyeonuWorld.Set(GyeonuWorld.F_비밀문_해제, false);
            GyeonuWorld.Set(GyeonuWorld.F_비밀문_열림, false);
            var it = Inventory.Find("SURYEONG_KEY");
            if (it != null) Inventory.Remove(it);
            Debug.Log("[디버그] 렌즈 퍼즐·열쇠 해제 — Play를 다시 시작해야 구슬이 서안으로 돌아간다");
        }

        // ── 종막 서고 장부 (2026-08-24) ───────────────────
        //   C1(아버지 검수 기록)·C3(선아 풀이표)는 아직 얻을 길이 없다 —
        //   관측실 조사와 선아 구출이 붙기 전까지 여기서 지급한다.

        [MenuItem(Root + "서고 ① 단서 지급 (C3 풀이표 + C1 검수 기록)", priority = 450)]
        static void GrantLedgerClues()
        {
            int n = 0;
            foreach (var id in new[] { "C3", "C1" })
            {
                var it = FindItem(id);
                if (it == null) { Debug.LogWarning("[디버그] " + id + " 정의가 없다 — 종막 장부 퍼즐 만들기 먼저"); continue; }
                if (Inventory.Add(it)) n++;
            }
            Debug.Log("[디버그] 서고 단서 " + n + "개 지급 — 이제 찬장 장부를 읽을 수 있다");
        }

        [MenuItem(Root + "서고 ② C3만 지급 (1단계만 열기)", priority = 451)]
        static void GrantLedgerC3()
        {
            var it = FindItem("C3");
            if (it == null) { Debug.LogWarning("[디버그] C3 정의가 없다"); return; }
            Inventory.Add(it);
            Debug.Log("[디버그] 선아의 풀이표만 지급 — 1단계는 되고 2단계는 막힌다");
        }

        [MenuItem(Root + "서고 ③ 1단계(장부 배열) 풀린 것으로", priority = 452)]
        static void SolveLedger1()
        {
            var puz = Object.FindFirstObjectByType<LedgerPuzzle>(FindObjectsInactive.Include);
            if (puz == null) { Debug.LogWarning("[디버그] 이 씬에 서고 장부가 없다"); return; }
            GrantLedgerClues();
            puz.DebugSolveStage1();
            Debug.Log("[디버그] 장부 배열 완료 — 2단계로 넘어갔다");
        }

        [MenuItem(Root + "서고 ④ 2단계(기물 대조)까지 풀린 것으로", priority = 453)]
        static void SolveLedger2()
        {
            var puz = Object.FindFirstObjectByType<LedgerPuzzle>(FindObjectsInactive.Include);
            if (puz == null) { Debug.LogWarning("[디버그] 이 씬에 서고 장부가 없다"); return; }
            GrantLedgerClues();
            puz.DebugSolveStage2();
            Debug.Log("[디버그] 기물 대조 완료 — 창고방 서랍장에 표시가 뜬다");
        }

        [MenuItem(Root + "서고 장부 되돌리기", priority = 454)]
        static void ResetLedger()
        {
            foreach (var f in new[] { GyeonuWorld.F_장부복원, GyeonuWorld.F_기물대조, GyeonuWorld.F_후고단서,
                                      GyeonuWorld.F_아버지검수기록, GyeonuWorld.F_선아풀이표 })
                GyeonuWorld.Set(f, false);
            foreach (var id in new[] { "C1", "C2", "C3" })
            {
                var it = Inventory.Find(id);
                if (it != null) Inventory.Remove(it);
            }
            Debug.Log("[디버그] 서고 장부 초기화 — Play를 다시 시작해야 기물이 무작위 자리로 돌아간다");
        }

        static InventoryItem FindItem(string id)
        {
            foreach (var it in Inventory.Catalog) if (it != null && it.Key == id) return it;
            return null;
        }

        // ── 별 길 안내 ────────────────────────────────────

        [MenuItem(Root + "별 길 켜기/끄기 (비밀지도)", priority = 440)]
        static void ToggleStarPath()
        {
            var guide = Object.FindFirstObjectByType<StarPathGuide>(FindObjectsInactive.Include);
            if (guide == null)
            {
                Debug.LogWarning("[디버그] 이 씬에 StarPathGuide가 없다 (은하담 씬에서 실행할 것)");
                return;
            }
            guide.Show(!guide.IsVisible);
            Debug.Log("[디버그] 별 길 = " + (guide.IsVisible ? "켜짐" : "꺼짐") + " (Play 중에는 M 키로도 된다)");
        }
    }
}
