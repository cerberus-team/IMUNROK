using System.Collections.Generic;
using UnityEngine;
using IMUNROK.Common;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 소지품 보관소 (2026-08-23). 씬을 넘나들어도 유지된다.
    ///
    /// ■ 왜 GyeonuWorld와 따로인가
    ///   GyeonuWorld는 "무엇을 했는가"(플래그)를 담고, 여기는 "무엇을 지녔는가"(물건)를 담는다.
    ///   담는 것이 bool이 아니라 에셋 참조라 자료형이 다르고, UI가 목록 그대로 그려야 한다.
    ///   다만 지속 방식은 GyeonuWorld와 똑같다 — 정적 필드 = **세션(플레이 실행) 단위**.
    ///
    /// ■ 세이브가 생기면
    ///   <see cref="CaptureKeys"/> / <see cref="RestoreKeys"/> 로 id 목록만 직렬화하면 된다.
    ///   에셋 자체는 <see cref="Catalog"/>(Resources)로 다시 찾는다.
    /// </summary>
    public static class Inventory
    {
        /// <summary>소지품 에셋이 놓이는 곳 — 세이브 복원·디버그 획득이 여기서 찾는다.</summary>
        public const string ResourcePath = "GyeonuItems";

        static readonly List<InventoryItem> _items = new List<InventoryItem>();

        /// <summary>목록이 바뀌면 발생 (획득·소모·초기화). UI가 구독해 다시 그린다.</summary>
        public static event System.Action Changed;

        /// <summary>새로 획득했을 때만 발생 — 알림 연출용.</summary>
        public static event System.Action<InventoryItem> Added;

        // 도메인 리로드가 꺼진 프로젝트 — 정적 필드가 플레이 세션을 넘겨 살아남으므로 직접 초기화
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _items.Clear();
            Changed = null;
            Added = null;
        }

        public static IReadOnlyList<InventoryItem> Items => _items;
        public static int Count => _items.Count;

        public static bool Has(InventoryItem item) => item != null && Has(item.Key);

        public static bool Has(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            foreach (var it in _items) if (it != null && it.Key == key) return true;
            return false;
        }

        /// <summary>획득. 이미 가진 것이면 아무 일도 없고 false.</summary>
        public static bool Add(InventoryItem item)
        {
            if (item == null || Has(item)) return false;
            _items.Add(item);

            // 단서 코드가 곧 아이템 id다 — 수첩에도 남길 물건이면 여기서 한 번에 처리한다.
            if (!string.IsNullOrEmpty(item.journalKey))
            {
                string text = string.IsNullOrEmpty(item.journalText) ? FirstLine(item.description) : item.journalText;
                Journal.Instance.AddClue(CaseId.Case3_Gyeonu, item.journalKey, text);
            }

            Debug.Log($"[소지품] 획득: {item.displayName} ({item.Key})");
            Added?.Invoke(item);
            Changed?.Invoke();
            return true;
        }

        /// <summary>소모·양도 등으로 잃음.</summary>
        public static bool Remove(InventoryItem item)
        {
            if (item == null || !_items.Remove(item)) return false;
            Changed?.Invoke();
            return true;
        }

        public static void Clear()
        {
            if (_items.Count == 0) return;
            _items.Clear();
            Debug.Log("[소지품] 비움");
            Changed?.Invoke();
        }

        // ── 세이브 훅 (아직 세이브 시스템 없음 — 자리만 잡아 둔다) ──

        public static List<string> CaptureKeys()
        {
            var keys = new List<string>(_items.Count);
            foreach (var it in _items) if (it != null) keys.Add(it.Key);
            return keys;
        }

        public static void RestoreKeys(IEnumerable<string> keys)
        {
            _items.Clear();
            if (keys != null)
                foreach (var k in keys)
                {
                    var it = Find(k);
                    if (it != null) _items.Add(it);
                    else Debug.LogWarning($"[소지품] 복원 실패 — Resources/{ResourcePath} 에 '{k}'가 없다");
                }
            Changed?.Invoke();
        }

        /// <summary>Resources/GyeonuItems 안의 모든 소지품 정의.</summary>
        public static InventoryItem[] Catalog => Resources.LoadAll<InventoryItem>(ResourcePath);

        /// <summary>id로 정의를 찾는다 (디버그 획득·세이브 복원용).</summary>
        public static InventoryItem Find(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            foreach (var it in Catalog) if (it != null && it.Key == key) return it;
            return null;
        }

        static string FirstLine(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            int i = s.IndexOf('\n');
            return (i < 0 ? s : s.Substring(0, i)).Trim();
        }
    }
}
