// 수첩에 적힌 조각을 견우 소지품 판이 읽을 수 있는 물건(IUiItem)으로 감쌉니다. ★서천에는 소지품이 없어 조각이 곧 증거입니다.
using System;
using System.Collections.Generic;
using IMUNROK.Ui;
using UnityEngine;

namespace IMUNROK.Seocheon
{
    /// <summary>
    /// 수첩 한 줄을 <see cref="IUiItem"/> 로 감싼 것.
    ///
    /// ■ ★유효/오답이 드러나면 안 된다
    ///   여기에는 <c>clueId</c> 도, "유효" 같은 표식도 <b>한 글자도 나오지 않는다</b>.
    ///   카드 앞면은 지목한 어절, 뒷면은 그 말이 들어 있던 문장과 누가 한 말인지뿐이다.
    ///   판정은 나중에 <see cref="ClueCombiner"/> 가 결합할 때만 일어난다.
    ///   ⚠️ 이 규칙을 깨는 가장 쉬운 길은 "설명에 단서 번호를 덧붙이면 편하겠다"는 생각이다.
    ///
    /// ■ 결합 결과 카드는 갈라도 된다
    ///   그건 이미 판정이 끝난 것이라 감출 것이 없다 — 맞춰 본 것이라고 그대로 적는다.
    ///
    /// ■ 돌려 볼 실물이 없다
    ///   <see cref="ModelPrefab"/> 이 null 이면 견우 조사 화면이 <b>빈 막</b>으로 뜬다(문서 확인).
    ///   말은 만질 수 있는 것이 아니므로 그것이 맞다.
    /// </summary>
    public sealed class SeocheonClueItem : IUiItem
    {
        private readonly SeocheonClueRecord rec;

        public SeocheonClueItem(SeocheonClueRecord record) { rec = record; }

        /// <summary>감싼 기록. ★clueId 를 읽는 것은 <b>말 상대</b>뿐이다 — 화면이 아니라.</summary>
        public SeocheonClueRecord Record { get { return rec; } }

        public string Key { get { return rec != null ? rec.entryId : string.Empty; } }

        /// <summary>카드 앞면 — 지목한 어절. 결합 카드는 결과 문구.</summary>
        public string DisplayName
        {
            get
            {
                if (rec == null) return string.Empty;
                return string.IsNullOrEmpty(rec.faceText) ? rec.sentence : rec.faceText;
            }
        }

        /// <summary>뒷면 — 원문 문장과 출처. ★조각의 뜻은 적지 않는다.</summary>
        public string Description
        {
            get
            {
                if (rec == null) return string.Empty;
                var sb = new System.Text.StringBuilder();
                sb.Append(rec.sentence).Append("\n\n");
                if (rec.isDerived)
                {
                    // ★이건 감출 것이 없다 — 이미 맞춰 본 결과다.
                    sb.Append("― 맞춰 본 것");
                    if (!string.IsNullOrEmpty(rec.combineKind)) sb.Append(" · ").Append(rec.combineKind);
                }
                else
                {
                    sb.Append("― ").Append(string.IsNullOrEmpty(rec.sourceNpc) ? "들은 말" : rec.sourceNpc + "의 말");
                }
                return sb.ToString();
            }
        }

        public GameObject ModelPrefab { get { return null; } }
        public Vector3 PreviewEuler { get { return Vector3.zero; } }
        public float PreviewZoom { get { return 1f; } }

        /// <summary>「사용하기」는 없다. 내미는 단추는 판이 제시 모드에서 따로 낸다.</summary>
        public bool ShowUseButton { get { return false; } }
        public string UseLabel { get { return string.Empty; } }
        public string UseNotReadyHint { get { return string.Empty; } }

        /// <summary>★어절을 지목할 때마다 조사 화면이 튀어나오면 대화가 끊긴다.</summary>
        public bool AutoShowOnPickup { get { return false; } }
    }

    /// <summary>
    /// 수첩을 견우 소지품 판에 꽂는 어댑터.
    ///
    /// ■ ★왜 씬 이름을 보는가
    ///   <see cref="RuntimeInitializeOnLoadMethod"/> 는 <b>어느 씬에서든</b> 돈다.
    ///   그냥 꽂으면 견우 사건에서도 소지품이 서천 수첩으로 바뀐다 —
    ///   <see cref="UiItems.Source"/> 는 프로젝트에 하나뿐인 자리이기 때문이다.
    ///   그래서 <b>서천 씬일 때만</b> 꽂고, 씬이 바뀌면 다시 판단한다.
    ///
    /// ■ ★도메인 리로드가 꺼져 있다
    ///   정적 값이 플레이 세션을 넘겨 살아남으므로 <b>세션마다 다시 꽂아야</b> 한다
    ///   (꾸러미 <c>UiItems</c> 주석의 경고 그대로). 그 일을 아래 두 진입점이 한다.
    /// </summary>
    public sealed class SeocheonClueItems : IUiItemSource
    {
        private const string ScenePrefix = "Seocheon";

        private static SeocheonClueItems instance;

        private readonly List<IUiItem> items = new List<IUiItem>();
        private readonly Dictionary<string, SeocheonClueItem> byId = new Dictionary<string, SeocheonClueItem>();
        private bool hooked;

        public static SeocheonClueItems Instance
        {
            get { if (instance == null) instance = new SeocheonClueItems(); return instance; }
        }

        // ── IUiItemSource ────────────────────────────────
        public IReadOnlyList<IUiItem> Items { get { return items; } }
        public event Action Changed;
        public event Action<IUiItem> Added;

        /// <summary>entryId 로 카드를 찾습니다. 없으면 null.</summary>
        public SeocheonClueItem Find(string entryId)
        {
            if (string.IsNullOrEmpty(entryId)) return null;
            SeocheonClueItem found;
            return byId.TryGetValue(entryId, out found) ? found : null;
        }

        /// <summary>
        /// 수첩과 목록을 맞춥니다. 이미 만든 카드는 <b>그대로 둡니다</b> —
        /// 미리보기 캐시가 <see cref="IUiItem.Key"/> 로 구분하므로 매번 새로 지으면 헛일이 됩니다.
        /// </summary>
        public void Refresh()
        {
            IReadOnlyList<SeocheonClueRecord> recs = SeocheonClueStore.Records;

            // 수첩이 통째로 비워졌으면(세이브 되돌리기 등) 우리도 처음부터 다시 짓는다.
            if (recs.Count < items.Count) { items.Clear(); byId.Clear(); }

            bool grew = false;
            for (int i = 0; i < recs.Count; i++)
            {
                SeocheonClueRecord r = recs[i];
                if (r == null || string.IsNullOrEmpty(r.entryId)) continue;
                if (byId.ContainsKey(r.entryId)) continue;

                var made = new SeocheonClueItem(r);
                byId[r.entryId] = made;
                items.Add(made);
                grew = true;
                if (Added != null) Added(made);
            }

            if (grew && Changed != null) Changed();
        }

        // ─────────────────────────────────────────────────
        //  꽂기
        // ─────────────────────────────────────────────────

        /// <summary>★세션마다 처음부터. 지난 플레이의 카드가 섞여 들어오지 않게 한다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            instance = null;
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= OnSceneChanged;
        }

        /// <summary>씬이 다 뜬 뒤에 판단한다 — 그때라야 활성 씬 이름을 믿을 수 있다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= OnSceneChanged;
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnSceneChanged;
            Apply(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        private static void OnSceneChanged(UnityEngine.SceneManagement.Scene from,
                                           UnityEngine.SceneManagement.Scene to)
        {
            Apply(to.name);
        }

        private static void Apply(string sceneName)
        {
            bool mine = !string.IsNullOrEmpty(sceneName)
                     && sceneName.StartsWith(ScenePrefix, StringComparison.Ordinal);
            if (mine)
            {
                Instance.Hook();
                UiItems.Source = Instance;
                Instance.Refresh();
            }
            else if (instance != null && ReferenceEquals(UiItems.Source, instance))
            {
                // ★남의 사건에 우리 수첩을 남겨 두지 않는다.
                instance.Unhook();
                UiItems.Source = null;
            }
        }

        private void Hook()
        {
            if (hooked) return;
            hooked = true;
            SeocheonClueStore.Changed += Refresh;   // 결합 결과는 저장소가 알려 준다
        }

        private void Unhook()
        {
            if (!hooked) return;
            hooked = false;
            SeocheonClueStore.Changed -= Refresh;
        }
    }
}
