using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>문이 거꾸로 달려 있어 밖에서는 문으로 안 보인다.</b>
    /// 메뉴: [이문록 ▸ 관아 ▸ ⑮ 문을 밖에서도 문으로]
    ///
    /// 서고 앞에 서면 벽 한 장이 죽 이어진 것처럼 보인다. 계단은 있는데 어디가
    /// 문인지를 알 수가 없다. 그런데 <b>안에 들어가서 같은 벽을 보면</b> 세살에
    /// 문고리에 돌쩌귀까지 갖춘 어엿한 문이다.
    ///
    /// 문짝(SM_Door_Sesal)은 <b>한 면에만 살이 서 있고 뒷면은 창호지</b>다.
    /// 지금은 그 살 있는 면이 열두 짝 죄 <b>안쪽</b>을 보고 있어서, 밖에서는 종이
    /// 뒷면만 보인다. 눈으로만 드러나고 광선으로는 안 잡히는 탈이라, 한 면짜리
    /// 지붕 때와 똑같이 재어 봐야 소용이 없고 <b>찍어 봐야</b> 안다.
    ///
    /// <b>어느 면에 살이 섰는지는 짐작하지 말고 잰다.</b> 처음엔 열두 짝이 다 같은
    /// 각(270,270,0)이길래 살도 다 같은 쪽이려니 하고 한 방향으로 밀었더니,
    /// 동헌 남쪽 세 짝만 모델이 좌우로 뒤집혀 있어 겉짝이 원래 짝 <b>뒤에</b> 숨었다.
    /// 밖은 그대로 종이인데 안까지 종이가 되어 되레 나빠졌다.
    /// 살 있는 쪽은 살대 때문에 <b>그물코가 촘촘하다</b> — 꼭짓점 무게중심이 몸피
    /// 한가운데보다 2cm 쯤 그쪽으로 쏠린다. 그 쏠림의 부호로 방향을 정한다.
    ///
    /// <b>돌려 다는 대신 한 짝을 더 건다.</b> 그냥 180° 돌리면 이번엔 안쪽이
    /// 밋밋해진다 — 서고 안은 플레이어가 문서궤를 뒤지며 오래 머무는 데다.
    /// 그래서 겉에 한 짝을 더 걸어 <b>양쪽 다 살이 보이게</b> 한다. 한옥에서도
    /// 서고·곳간은 덧문을 겹쳐 다는 것이 예사이니 억지가 아니다.
    ///
    /// <b>돌리지 않고 뒤집는다.</b> 180° 돌리면 좌우도 같이 뒤집혀 문고리가
    /// 반대쪽 변으로 간다. 두께 축 하나만 −1 로 뒤집으면 앞뒤만 바뀌고 문고리는
    /// 제자리에 남는다. 다만 <b>뒤집는 기준이 피벗</b>이라 — 이 문짝들은 피벗이
    /// (−1.70, −0.69, −9.87) 처럼 문에서 한참 떨어져 있어서 — 그대로 두면 문이
    /// 딴 데로 날아간다. 뒤집기 전후의 세계 몸피 한가운데를 재어 도로 맞춘다.
    ///
    /// 두 번 눌러도 두 벌이 안 선다(겉문 무리를 통째로 비우고 다시 건다).
    /// </summary>
    public static class GwanaDoors
    {
        /// <summary>이 이름으로 시작하는 그물이 세살문이다.</summary>
        private const string DoorMesh = "SM_Door_Sesal";

        /// <summary>겉짝을 이만큼 바깥(+z)으로 민다. 0 이면 두 종이면이 겹쳐 어른거린다.</summary>
        private const float Gap = 0.03f;

        /// <summary>겉짝을 모아 두는 무리. 세트 프리팹 밑에 끼워 넣으면 덧댄 것이 되어 성가시다.</summary>
        private const string Group = "겉문";

        [MenuItem("이문록/관아/⑮ 문을 밖에서도 문으로")]
        public static void Run()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.name.Contains("Gwana"))
            {
                Debug.LogWarning("[관아] 관아 씬을 열고 누르십시오(지금은 " + scene.name + ").");
                return;
            }

            var log = new System.Text.StringBuilder("[관아] 문을 밖에서도 문으로 보이게 한다\n");

            // ── 지난번 겉짝을 걷어낸다 ──
            var group = Find(scene, Group);
            if (group == null)
            {
                group = new GameObject(Group);
                SceneManager.MoveGameObjectToScene(group, scene);
                Undo.RegisterCreatedObjectUndo(group, "겉문 무리");
            }
            group.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            group.transform.localScale = Vector3.one;
            for (int i = group.transform.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(group.transform.GetChild(i).gameObject);

            // ── 문짝을 모은다 ──
            var doors = new List<Renderer>();
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mf.sharedMesh == null) continue;
                if (!mf.sharedMesh.name.StartsWith(DoorMesh)) continue;
                if (mf.gameObject.scene != scene) continue;
                if (mf.transform.IsChildOf(group.transform)) continue;
                var r = mf.GetComponent<Renderer>();
                if (r != null) doors.Add(r);
            }
            doors.Sort(delegate (Renderer a, Renderer b) { return a.bounds.center.x.CompareTo(b.bounds.center.x); });

            int hung = 0, skipped = 0;
            foreach (var d in doors)
            {
                // 두께가 z 축이 아닌 문은 이 셈이 안 맞는다 — 건드리지 않고 알린다.
                var size = d.bounds.size;
                if (size.z > size.x || size.z > size.y)
                {
                    log.AppendLine("  ※ " + d.name + " 은 두께가 z 가 아니라 건너뛴다 (" + size.ToString("F2") + ")");
                    skipped++;
                    continue;
                }
                Hang(d, group.transform, log);
                hung++;
            }

            log.AppendLine("  · 문짝 " + doors.Count + "짝 가운데 " + hung + "짝에 겉짝을 걸었다"
                         + (skipped > 0 ? " (" + skipped + "짝 건너뜀)" : ""));
            log.AppendLine("  · 겉짝에는 콜라이더를 안 붙인다 — 원래 짝이 이미 막고 있다");

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(log.ToString());
        }

        /// <summary>문짝 하나를 앞뒤로 뒤집어 한 짝 더 건다.</summary>
        private static void Hang(Renderer src, Transform group, System.Text.StringBuilder log)
        {
            var go = src.gameObject;
            var copy = Object.Instantiate(go);
            copy.name = go.name + "_겉";
            Undo.RegisterCreatedObjectUndo(copy, "겉문");
            copy.transform.SetParent(group, true);
            copy.transform.SetPositionAndRotation(go.transform.position, go.transform.rotation);
            copy.transform.localScale = go.transform.lossyScale;

            // 겉짝은 눈으로만 있는 것이다. 원래 짝이 이미 막고 있으니 콜라이더가
            // 겹치면 값만 들고, 뒤집힌 배율에 상자를 물리면 셈도 어긋난다.
            foreach (var c in copy.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(c);

            Vector3 before = Middle(copy);
            float toward = -Mathf.Sign(Bias(src));   // 겉짝의 살은 원래 짝의 살과 반대쪽으로 선다

            // 세계 z 로 향하는 로컬 축을 찾아 그 축만 −1 로 돌린다.
            var t = copy.transform;
            var s = t.localScale;
            float ax = Mathf.Abs(Vector3.Dot(t.right.normalized, Vector3.forward));
            float ay = Mathf.Abs(Vector3.Dot(t.up.normalized, Vector3.forward));
            float az = Mathf.Abs(Vector3.Dot(t.forward.normalized, Vector3.forward));
            if (ax >= ay && ax >= az) s.x = -s.x;
            else if (ay >= az) s.y = -s.y;
            else s.z = -s.z;
            t.localScale = s;

            // 피벗이 문에서 멀어 뒤집으면 딴 데로 간다 — 몸피 한가운데를 도로 맞춘다.
            Vector3 after = Middle(copy);
            t.position += (before - after) + Vector3.forward * (Gap * toward);
            log.AppendLine("  · " + go.name + " — 살이 " + (toward > 0f ? "−z" : "+z") + " 쪽을 보고 있어 겉짝을 "
                         + (toward > 0f ? "+z" : "−z") + " 로 밀었다");
        }

        /// <summary>
        /// <b>살이 어느 쪽에 섰나.</b> 살대는 잘게 짜인 격자라 그 면에 꼭짓점이 몰려 있다.
        /// 꼭짓점 무게중심이 몸피 한가운데보다 +z 면 살은 +z 쪽, −z 면 −z 쪽이다.
        /// (이 집들은 문이 다 ±z 면에 서 있어 세계 z 로 재면 된다.)
        /// </summary>
        private static float Bias(Renderer src)
        {
            var mf = src.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return -1f;
            var vs = mf.sharedMesh.vertices;
            if (vs.Length == 0) return -1f;
            var t = src.transform;
            float sum = 0f;
            for (int i = 0; i < vs.Length; i++) sum += t.TransformPoint(vs[i]).z;
            float bias = sum / vs.Length - src.bounds.center.z;
            return Mathf.Approximately(bias, 0f) ? -1f : bias;
        }

        /// <summary>딸린 그림까지 다 싸안은 세계 몸피의 한가운데.</summary>
        private static Vector3 Middle(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return go.transform.position;
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b.center;
        }

        private static GameObject Find(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == name) return root;
            return null;
        }
    }
}
