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
