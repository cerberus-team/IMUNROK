using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 물리 오브젝트를 직접 조사했을 때만 사건 단서를 조용히 기록한다.
    ///
    /// ■ 바인딩은 씬이 열릴 때마다 (2026-09-10)
    ///   예전에는 <c>RuntimeInitializeOnLoadMethod(AfterSceneLoad)</c> 로 한 번만 붙였다.
    ///   그 훅은 앱이 뜰 때 딱 한 번 돌기 때문에, 마을에서 출발해 선아 집·집무실로 들어가면
    ///   조사할 물건에 컴포넌트가 붙지 않아 B6·C5를 얻을 길이 없었다 (Play 실측).
    ///   그래서 <see cref="SceneManager.sceneLoaded"/> 에 걸어 두고, 이미 붙어 있으면 다시 붙이지 않는다.
    /// </summary>
    public class CaseClueInspectable : Interactable
    {
        public ClueId clue;
        public string prompt = "살피기";
        public string[] requiredFlags;
        public TimeOfDay[] onlyAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            BindScene(SceneManager.GetActiveScene().name);
        }

        static void OnSceneLoaded(Scene s, LoadSceneMode mode) => BindScene(s.name);

        /// <summary>씬 이름에 맞는 조사 오브젝트에 컴포넌트를 붙인다. 이미 있으면 값만 맞춘다.</summary>
        public static void BindScene(string scene)
        {
            if (scene == "Gyeonu_SeonaHouse")
                Ensure("두루마리_1", ClueId.B6, null, "선아가 남긴 밤 외출과 관측 흔적 살피기");
            else if (scene == "Gyeonu_GwanaOffice")
            {
                var c = Ensure("장부더미_동_1", ClueId.C5, null, "장부의 필체와 먹색 살피기")
                     ?? Ensure("문서장", ClueId.C5, null, "장부의 필체와 먹색 살피기");
                if (c != null) c.onlyAt = new[] { TimeOfDay.LateNight };
            }
        }

        static CaseClueInspectable Ensure(string objectName, ClueId id, string[] flags, string prompt)
        {
            var go = GameObject.Find(objectName);
            if (go == null) { Debug.LogWarning("[단서] 조사 오브젝트 '" + objectName + "' 이 씬에 없다 — " + id + " 을 붙이지 못했다."); return null; }
            var c = go.GetComponent<CaseClueInspectable>();
            if (c == null) c = go.AddComponent<CaseClueInspectable>();
            c.clue = id; c.requiredFlags = flags; c.prompt = prompt;
            return c;
        }

        public override string Prompt => prompt;

        void Awake()
        {
            if (GetComponentInChildren<Collider>() != null) return;
            var rs = GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            var box = gameObject.AddComponent<BoxCollider>();
            box.center = transform.InverseTransformPoint(b.center);
            Vector3 s = transform.InverseTransformVector(b.size);
            box.size = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        }

        public override bool CanInteract(GameObject actor)
        {
            if (GyeonuCase.HasClue(clue) || !GyeonuCase.HasAllFlags(requiredFlags)) return false;
            if (onlyAt != null && onlyAt.Length > 0)
            {
                bool ok = false;
                foreach (var t in onlyAt) if (t == GyeonuCase.Time) { ok = true; break; }
                if (!ok) return false;
            }
            return true;
        }

        public override void Interact(GameObject actor)
        {
            if (CanInteract(actor)) GyeonuCase.AddClue(clue);
        }
    }
}
