using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>문을 문답게 만든다.</b> 메뉴: [이문록 ▸ 관아 ▸ ⑮ 문을 밖에서도 문으로]
    ///
    /// 세 가지를 한다 — 겉짝 걸기, 맞댐대 대기, 여닫이 달기.
    ///
    /// <b>㉠ 문이 거꾸로 달려 밖에서는 벽이었다.</b>
    /// 서고 앞에 서면 벽 한 장이 죽 이어진 것처럼 보였다. 계단은 있는데 어디가
    /// 문인지를 알 수가 없었다. 그런데 안에 들어가 같은 벽을 보면 세살에 문고리에
    /// 돌쩌귀까지 갖춘 어엿한 문이다. 문짝(SM_Door_Sesal)은 <b>한 면에만 살이 서 있고
    /// 뒷면은 창호지</b>인데, 그 살 있는 면이 열두 벌 죄 <b>안쪽</b>을 보고 있었다.
    /// 눈으로만 드러나고 광선으로는 안 잡히는 탈이라, 한 면짜리 지붕 때와 똑같이
    /// 재어 봐야 소용이 없고 <b>찍어 봐야</b> 안다.
    ///
    /// 돌려 다는 대신 <b>겉에 한 벌을 더 건다</b>. 그냥 180° 돌리면 이번엔 안쪽이
    /// 밋밋해진다 — 서고 안은 플레이어가 문서궤를 뒤지며 오래 머무는 데다.
    /// 한옥에서도 서고·곳간은 덧문을 겹쳐 다는 것이 예사이니 억지가 아니다.
    ///
    /// <b>돌리지 않고 뒤집는다.</b> 180° 돌리면 좌우도 같이 뒤집혀 문고리가 반대쪽
    /// 변으로 간다. 두께 축 하나만 −1 로 뒤집으면 앞뒤만 바뀌고 문고리는 제자리에
    /// 남는다. 다만 <b>뒤집는 기준이 피벗</b>이라 — 이 문짝들은 피벗이
    /// (−1.70, −0.69, −9.87) 처럼 문에서 한참 떨어져 있어서 — 그대로 두면 문이 딴 데로
    /// 날아간다. 뒤집기 전후의 세계 몸피 한가운데를 재어 도로 맞춘다.
    ///
    /// <b>어느 면에 살이 섰는지는 짐작하지 말고 잰다.</b> 처음엔 열두 벌이 다 같은
    /// 각(270,270,0)이길래 살도 다 같은 쪽이려니 하고 한 방향으로 밀었더니, 동헌 남쪽
    /// 세 벌만 모델이 좌우로 뒤집혀 있어 겉짝이 원래 짝 <b>뒤에</b> 숨었다. 밖은 그대로
    /// 종이인데 안까지 종이가 되어 되레 나빠졌다. 살 있는 쪽은 살대 때문에 <b>그물코가
    /// 촘촘하다</b> — 꼭짓점 무게중심이 몸피 한가운데보다 2cm 쯤 그쪽으로 쏠린다.
    /// 그 쏠림의 부호로 방향을 정한다.
    ///
    /// <b>㉡ 맞댄 자리가 비쳐 보인다.</b>
    /// 문짝 하나는 그림으로 두 짝짜리 <b>한 벌</b>이고, 그 한가운데(문고리 두 개가
    /// 마주 보는 자리)가 실낱같이 트여 방 안이 들여다보인다. 서고는 벌과 벌이
    /// 맞닿는 데도 마찬가지다 — 그물은 x 가 정확히 −2.0000 에서 만나는데도,
    /// 변두리가 깎여 있어 칼날처럼 얇아진 자리로 빛이 샌다.
    ///
    /// 동헌은 세트를 만든 이가 벌 사이에 문선(SM_Munseon_02, 9cm)을 세워 두었다.
    /// 벌 <b>한가운데</b>는 어디에도 없다. 그래서 <b>맞댐대</b>를 댄다 — 실제 쌍여닫이도
    /// 한쪽 짝에 맞댐대를 붙여 마주 대는 틈을 덮는다. 없는 것을 새로 만드는 것이 아니라
    /// 빠진 것을 채우는 것이다.
    ///
    /// <b>㉢ 여닫이.</b> 문짝 하나는 그물로 한 덩이라 가운데를 갈라 따로 돌릴 수 없다.
    /// 그래서 <b>한 벌을 통째로</b> 바깥 세로변에서 돌린다(사분합문이 두 벌씩 접혀
    /// 열리는 꼴). 겉짝과 맞댐대도 같이 돈다. 자세한 것은 <see cref="SwingDoor"/>.
    ///
    /// 여닫이는 <b>서고 앞 세 칸</b>에만 단다. 동헌 옆문은 어사가 앉은 등 뒤라 열 일이
    /// 없고, 세트 프리팹 안이라 손대면 덧댐이 남는다.
    ///
    /// 두 번 눌러도 두 벌이 안 선다(덧댄 무리를 통째로 비우고 다시 세운다).
    /// </summary>
    public static class GwanaDoors
    {
        /// <summary>이 이름으로 시작하는 그물이 세살문이다.</summary>
        private const string DoorMesh = "SM_Door_Sesal";

        /// <summary>겉짝을 이만큼 밀어 낸다. 0 이면 두 종이면이 겹쳐 어른거린다.</summary>
        private const float Gap = 0.03f;

        /// <summary>맞댐대 너비. 트인 실금이 2cm 남짓이라 넉넉히 덮는다.</summary>
        private const float BattenWide = 0.06f;

        /// <summary>맞댐대가 문 두께보다 이만큼씩 더 나온다 — 덧댄 것이라 도톰한 것이 맞다.</summary>
        private const float BattenProud = 0.006f;

        /// <summary>다 열었을 때의 각.</summary>
        private const float OpenAngle = 85f;

        /// <summary>덧댄 것을 모아 두는 무리. 세트 프리팹 밑에 끼워 넣으면 덧댐이 되어 성가시다.</summary>
        private const string Group = "겉문";

        /// <summary>여닫이를 다는 집. 나머지는 붙박이로 둔다.</summary>
        private const string SwingHouse = "문서고";

        /// <summary>문짝 한 벌과 거기 딸린 것들.</summary>
        private class Panel
        {
            public Renderer 원래;
            public Bounds 몸피;          // 겉짝을 걸기 전의 세계 몸피
            public float 바깥;           // 겉이 어느 쪽인가 (+1 이면 +z)
            public Transform 겉짝;
            public readonly List<Transform> 덧댄것 = new List<Transform>();
        }

        [MenuItem("이문록/관아/⑮ 문을 밖에서도 문으로")]
        public static void Run()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.name.Contains("Gwana"))
            {
                Debug.LogWarning("[관아] 관아 씬을 열고 누르십시오(지금은 " + scene.name + ").");
                return;
            }

            var log = new System.Text.StringBuilder("[관아] 문을 문답게 만든다\n");

            var group = Ready(scene);
            var panels = Gather(scene, group, log);
            foreach (var p in panels) Hang(p, group.transform);
            Battens(panels, group.transform, log);
            int swung = Swing(panels, log);

            log.AppendLine("  · 문짝 " + panels.Count + "벌에 겉짝을 걸고 맞댐대를 댔다");
            log.AppendLine("  · 그중 " + swung + "벌을 여닫이로 달았다 (" + SwingHouse + " 앞 칸)");
            log.AppendLine("  · 덧댄 것에는 콜라이더를 안 붙인다 — 원래 짝이 이미 막고 있다");

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(log.ToString());
        }

        /// <summary>덧댄 무리를 비워 둔다.</summary>
        private static GameObject Ready(Scene scene)
        {
            var group = Find(scene, Group);
            if (group == null)
            {
                group = new GameObject(Group);
                SceneManager.MoveGameObjectToScene(group, scene);
                Undo.RegisterCreatedObjectUndo(group, "덧댄 문 무리");
            }
            group.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            group.transform.localScale = Vector3.one;
            for (int i = group.transform.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(group.transform.GetChild(i).gameObject);
            return group;
        }

        /// <summary>씬에 선 문짝을 모으고, 겉이 어느 쪽인지 재 둔다.</summary>
        private static List<Panel> Gather(Scene scene, GameObject group, System.Text.StringBuilder log)
        {
            var list = new List<Panel>();
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mf.sharedMesh == null || !mf.sharedMesh.name.StartsWith(DoorMesh)) continue;
                if (mf.gameObject.scene != scene) continue;
                if (mf.transform.IsChildOf(group.transform)) continue;
                var r = mf.GetComponent<Renderer>();
                if (r == null) continue;

                var size = r.bounds.size;
                if (size.z > size.x || size.z > size.y)
                {
                    log.AppendLine("  ※ " + r.name + " 은 두께가 z 가 아니라 건너뛴다 (" + size.ToString("F2") + ")");
                    continue;
                }
                list.Add(new Panel { 원래 = r, 몸피 = r.bounds, 바깥 = -Mathf.Sign(Bias(r)) });
            }
            list.Sort(delegate (Panel a, Panel b) { return a.몸피.center.x.CompareTo(b.몸피.center.x); });
            return list;
        }

        /// <summary>문짝 한 벌을 앞뒤로 뒤집어 겉에 한 벌 더 건다.</summary>
        private static void Hang(Panel p, Transform group)
        {
            var go = p.원래.gameObject;
            var copy = Object.Instantiate(go);
            copy.name = go.name + "_겉";
            Undo.RegisterCreatedObjectUndo(copy, "겉문");
            copy.transform.SetParent(group, true);
            copy.transform.SetPositionAndRotation(go.transform.position, go.transform.rotation);
            copy.transform.localScale = go.transform.lossyScale;

            // <b>겉짝은 눈으로만 있는 것이다.</b> 콜라이더도 부품도 다 걷어낸다.
            //
            // 콜라이더: 원래 짝이 이미 막고 있으니 겹치면 값만 들고, 뒤집힌 배율에
            // 상자를 물리면 셈도 어긋난다.
            //
            // 부품: 이걸 안 걷어서 <b>여닫이가 열두 개</b>가 되었다. 두 번째로 누를 때
            // 원래 짝에는 이미 SwingDoor 가 붙어 있고, 그 몸을 통째로 베끼니 겉짝도
            // 그 부품을 물려받는다. 그러고는 <b>지난번에 지워진 조각들</b>을 가리킨 채
            // 유령으로 서서, 언제 무엇을 움직일지 모르는 것이 여섯 개 생긴다.
            // 겉짝이 지녀야 할 것은 그림뿐이다.
            foreach (var c in copy.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(c);
            foreach (var mb in copy.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb != null) Object.DestroyImmediate(mb);

            Vector3 before = Middle(copy);

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
            t.position += (before - after) + Vector3.forward * (Gap * p.바깥);
            p.겉짝 = t;
        }

        /// <summary>
        /// <b>맞댄 자리마다 맞댐대를 댄다.</b>
        ///
        /// 대는 데가 두 가지다. 하나는 <b>벌 한가운데</b> — 문고리 두 개가 마주 보는
        /// 자리로, 열두 벌이 다 트여 있다. 또 하나는 <b>벌과 벌이 맞닿는 데</b> —
        /// 서고 앞처럼 문선 없이 두 벌이 그냥 붙어 선 자리다. 동헌은 세트에 문선이
        /// 이미 서 있어 여기서는 안 걸린다.
        ///
        /// 맞댐대는 문 두께(원래 짝 + 겉짝)를 <b>다 관통</b>하게 세운다. 겉에만 대면
        /// 안에서 볼 때 실금이 그대로 남는다.
        /// </summary>
        private static void Battens(List<Panel> panels, Transform group, System.Text.StringBuilder log)
        {
            int mid = 0, joint = 0;
            foreach (var p in panels) { Batten(p, p.몸피.center.x, "_맞댐대", group); mid++; }

            for (int i = 0; i + 1 < panels.Count; i++)
            {
                var a = panels[i]; var b = panels[i + 1];
                if (Mathf.Abs(a.몸피.center.z - b.몸피.center.z) > 0.05f) continue;   // 다른 벽
                float gap = b.몸피.min.x - a.몸피.max.x;
                if (gap < -0.02f || gap > 0.02f) continue;                            // 문선이 섰거나 남남이다
                // 오른 벌이 지고 있게 한다 — 열 때 함께 비켜난다.
                Batten(b, (a.몸피.max.x + b.몸피.min.x) * 0.5f, "_맞댐대_이음", group);
                joint++;
            }
            log.AppendLine("  · 맞댐대 — 벌 한가운데 " + mid + "군데, 벌이 맞닿는 데 " + joint + "군데");
        }

        private static void Batten(Panel p, float x, string suffix, Transform group)
        {
            var b = p.몸피;
            float zLo = b.min.z, zHi = b.max.z;
            if (p.겉짝 != null)
            {
                var c = p.겉짝.GetComponent<Renderer>();
                if (c != null) { zLo = Mathf.Min(zLo, c.bounds.min.z); zHi = Mathf.Max(zHi, c.bounds.max.z); }
            }

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = p.원래.name + suffix;
            Undo.RegisterCreatedObjectUndo(go, "맞댐대");
            go.transform.SetParent(group, true);
            go.transform.rotation = Quaternion.identity;
            go.transform.position = new Vector3(x, (b.min.y + b.max.y) * 0.5f, (zLo + zHi) * 0.5f);
            go.transform.localScale = new Vector3(BattenWide, b.size.y, (zHi - zLo) + BattenProud * 2f);

            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null && p.원래.sharedMaterial != null) mr.sharedMaterial = p.원래.sharedMaterial;

            p.덧댄것.Add(go.transform);
        }

        /// <summary>
        /// <b>맞닿아 선 벌끼리 한 칸으로 묶어 여닫이를 단다.</b>
        ///
        /// 돌쩌귀는 칸 <b>바깥쪽</b> 세로변이다. 두 벌이 서로 반대로 열려야 하므로
        /// 여는 각의 부호를 손으로 적지 않고 <b>재서</b> 정한다 — 돌쩌귀에서 자유변으로
        /// 가는 방향을 바깥쪽으로 돌리려면 어느 쪽으로 돌려야 하는지를 묻는다.
        /// </summary>
        private static int Swing(List<Panel> panels, System.Text.StringBuilder log)
        {
            var mine = new List<Panel>();
            foreach (var p in panels)
                if (p.원래.transform.root != null && p.원래.transform.root.name == SwingHouse) mine.Add(p);
            if (mine.Count == 0) return 0;

            int done = 0;
            int at = 0;
            while (at < mine.Count)
            {
                // 맞닿아 선 벌들을 한 칸으로 묶는다
                int j = at;
                while (j + 1 < mine.Count && Mathf.Abs(mine[j + 1].몸피.min.x - mine[j].몸피.max.x) <= 0.02f) j++;

                float bayLo = mine[at].몸피.min.x, bayHi = mine[j].몸피.max.x;
                float bayMid = (bayLo + bayHi) * 0.5f;

                var doors = new List<SwingDoor>();
                for (int k = at; k <= j; k++)
                {
                    var p = mine[k];
                    var d = p.원래.GetComponent<SwingDoor>();
                    if (d == null) d = Undo.AddComponent<SwingDoor>(p.원래.gameObject);
                    doors.Add(d);
                }
                for (int k = at; k <= j; k++)
                {
                    var p = mine[k];
                    // 돌쩌귀는 칸 한가운데에서 먼 쪽 변
                    bool hingeLeft = Mathf.Abs(p.몸피.min.x - bayMid) > Mathf.Abs(p.몸피.max.x - bayMid);
                    float hx = hingeLeft ? p.몸피.min.x : p.몸피.max.x;
                    var hinge = new Vector3(hx, p.몸피.center.y, p.몸피.center.z);

                    Vector3 free = new Vector3(hingeLeft ? 1f : -1f, 0f, 0f);   // 돌쩌귀 → 자유변
                    Vector3 outward = new Vector3(0f, 0f, p.바깥);              // 밖으로 열린다
                    float angle = Mathf.Sign(Vector3.SignedAngle(free, outward, Vector3.up)) * OpenAngle;

                    var parts = new List<Transform> { p.원래.transform };
                    if (p.겉짝 != null) parts.Add(p.겉짝);
                    parts.AddRange(p.덧댄것);

                    var others = new List<SwingDoor>();
                    for (int m = 0; m < doors.Count; m++) if (m != k - at) others.Add(doors[m]);

                    doors[k - at].Setup(parts.ToArray(), hinge, angle, others.ToArray());
                    EditorUtility.SetDirty(doors[k - at]);
                    done++;
                }
                log.AppendLine("  · 여닫이 한 칸 — x " + bayLo.ToString("F2") + "~" + bayHi.ToString("F2")
                             + " 에 " + (j - at + 1) + "벌, 밖(" + (mine[at].바깥 > 0 ? "+z" : "−z") + ")으로 " + OpenAngle + "°");
                at = j + 1;
            }
            return done;
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
