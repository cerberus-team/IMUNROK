using System.Collections;
using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>향후 아이 노래 연출이 실제 완료됐을 때 A6을 기록할 연결 지점.</summary>
    public class ChildSongTrigger : MonoBehaviour
    {
        // TODO: 실제 노래 연출의 완료 이벤트가 연결된 뒤에만 true로 전환한다.
        // 현재는 접근/대화만으로 들리지 않은 노래 단서가 생기지 않도록 명시적으로 꺼 둔다.
        public bool grantEnabled = false;
        public float hearDelay = 2.5f;
        bool running;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureOnExistingScene()
        {
            var child = GameObject.Find("VillageChild_01");
            if (child == null || child.GetComponent<ChildSongTrigger>() != null) return;
            child.AddComponent<ChildSongTrigger>();
            var trigger = child.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 7f;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!grantEnabled || running || GyeonuCase.HasFlag(GyeonuWorld.F_아이노래들음)) return;
            if (other.GetComponentInParent<Camera>() == null && !other.CompareTag("Player")) return;
            running = true;
            StartCoroutine(HearSong());
        }

        IEnumerator HearSong()
        {
            yield return new WaitForSeconds(hearDelay);
            if (grantEnabled && !GyeonuCase.HasFlag(GyeonuWorld.F_아이노래들음))
            {
                GyeonuCase.SetFlag(GyeonuWorld.F_아이노래들음);
                GyeonuCase.AddClue(ClueId.A6);
            }
            running = false;
        }
    }
}
