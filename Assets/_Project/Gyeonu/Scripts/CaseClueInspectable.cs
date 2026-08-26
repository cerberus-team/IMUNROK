using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>물리 오브젝트를 직접 조사했을 때만 사건 단서를 조용히 기록한다.</summary>
    public class CaseClueInspectable : Interactable
    {
        public ClueId clue;
        public string prompt = "살피기";
        public string[] requiredFlags;
        public TimeOfDay[] onlyAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void BindExistingScenes()
        {
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (scene == "Gyeonu_SeonaHouse") Ensure("두루마리_1", ClueId.B6, null, "선아가 남긴 밤 외출과 관측 흔적 살피기");
            else if (scene == "Gyeonu_EunhaDam") Ensure("별길_은하수", ClueId.A5, null, "잡초와 무너진 축대에 묻힌 옛길 살피기");
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
            if (go == null) return null;
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
