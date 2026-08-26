using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using IMUNROK.Common;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>관아의 세 칸을 갈라 놓는다.</b>
    /// 메뉴: [이문록 ▸ 관아 ▸ ㉔ 동헌 안은 심문 · 대청은 판결 · 문서고는 자료]
    ///
    /// <b>여태 자리를 세 번 옮겼고 세 번 다 틀렸다.</b> 뜰에 세우고 마루에서 내려다보게
    /// 했고(⑫), 대청 앞을 막아 방을 지으려 했고(㉒), 심문을 통째로 문서고로 옮겼다(㉓).
    /// 마지막 것이 특히 그럴듯했다 — "문 열고 들어가는 진짜 실내는 문서고 하나뿐"이라
    /// 적어 두기까지 했다. 그런데 그건 <b>재는 자리를 잘못 잡아서</b> 나온 말이다.
    ///
    /// 다시 재 보니 동헌 옆칸에는 방이 <b>있다</b>. 북쪽 칸을 안에서 사방으로 쏘면
    /// x 12.59 · 16.92 에서 벽, z 7.25 에서 벽, 위로 6.44 에서 지붕이 걸린다. 안에서
    /// 사진을 찍으면 <b>새까맣다</b> — 하늘도 나무도 안 보이니 갇힌 칸이다. 전에
    /// "그 너머는 바깥"이라 한 것은 바닥이 대청(2.19)에서 끊기고 기단(1.65)이
    /// 드러나 있어서였는데, 그건 밖이라는 뜻이 아니라 <b>이 칸에 마룻널이 안 깔렸다</b>는
    /// 뜻이었다. 널을 깔면 방이 된다.
    ///
    /// <b>세 칸이 하는 일이 다르다.</b>
    ///
    /// <code>
    ///   동헌 <b>안</b>  (세살문 안, z 4.6~7.2)   심문 — 마주 앉아 묻는다
    ///   동헌 <b>대청</b>(앞이 트인 마루)          판결 — 교의에 앉아 장계를 봉한다
    ///   <b>문서고</b>   (여닫이 여섯 짝)          자료 — 대장을 뒤진다
    /// </code>
    ///
    /// 묻는 자리와 판결하는 자리가 <b>갈려 있어야</b> 하는 까닭은 격식이 아니라 이야기다.
    /// 안에서 물어 알아낸 것을 들고 나와 대청에 앉아 적는다 — 나오는 그 몇 걸음이
    /// "이제 다 물었다"는 매듭이 된다. 한자리에서 묻고 적으면 그 매듭이 없다.
    ///
    /// <b>자리는 사람이 잡아 둔 그대로 쓴다.</b> 서안 한 벌과 걸상 둘이 이미 방 한가운데
    /// (14.95, 5.90)에 x 를 두고 마주 놓여 있다. 그 위에 다시 걸상을 들이지 않고
    /// <b>있는 것에 표식만 얹는다</b> — 문서고에 있던 걸상 두 벌은 걷어낸다.
    ///
    /// <b>문은 셋 다 밖으로 접힌다.</b> 세살문은 프리팹 안에 들어 있어 부모를 새로
    /// 만들 수가 없는데, <see cref="SwingDoor"/> 는 애초에 그 사정을 알고 지은 것이라
    /// 조각들의 자리를 <b>직접</b> 놓는다. 문짝 · 겉짝 · 맞댐대 셋을 한 벌로 묶어
    /// 바깥 세로변에서 돌린다.
    ///
    /// 두 번 눌러도 두 벌이 안 선다.
    /// </summary>
    public static class GwanaThreeRooms
    {
        /// <summary>대청 마루 높이. 방에도 이 높이로 널을 깐다 — 문턱에 턱이 없어야 한다.</summary>
        private const float Maru = 2.19f;

        /// <summary>북쪽 방의 네 벽(안쪽 면). 쏘아서 잰 값이다.</summary>
        private const float RoomX0 = 12.62f, RoomX1 = 16.89f, RoomZ0 = 4.45f, RoomZ1 = 7.22f;

        /// <summary>세살문이 서 있는 줄.</summary>
        private const float DoorZ = 4.57f;

        /// <summary>문짝 한 벌의 한가운데 x. 문선(12.80·14.02·15.23·16.45) 사이마다 하나씩이다.</summary>
        private static readonly float[] LeafX = { 13.41f, 14.62f, 15.84f };

        /// <summary>문짝 너비의 반. 돌쩌귀는 한가운데서 이만큼 왼쪽이다.</summary>
        private const float LeafHalf = 0.56f;

        /// <summary>사람이 앉는 두 자리 — 사람이 놓아 둔 걸상 그 자리다.</summary>
        private static readonly Vector3 JudgeSeat = new Vector3(15.77f, Maru, 5.92f);   // 어사 — 서쪽(-x)을 본다
        private static readonly Vector3 GuiltSeat = new Vector3(14.25f, Maru, 5.88f);   // 죄인 — 동쪽(+x)을 본다

        /// <summary>서안 한가운데. 발은 여기 걸려 둘 사이를 가른다.</summary>
        private const float DeskX = 14.95f, DeskZ = 5.90f;

        [MenuItem("이문록/관아/㉔ 동헌 안은 심문 · 대청은 판결 · 문서고는 자료")]
        public static void Run()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.name.Contains("Gwana"))
            {
                Debug.LogWarning("[관아] 관아 씬을 열고 누르십시오(지금은 " + scene.name + ").");
                return;
            }

            var log = new System.Text.StringBuilder("[관아] 세 칸을 갈라 놓는다\n");
            Floor(scene, log);
            Doors(scene, log);
            Room(scene, log);
            Summon(scene, log);
            Verdict(scene, log);
            Archive(scene, log);
            Travel(scene, log);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(log.ToString());
        }

        // ── ① 방에 마룻널을 깐다 ──────────────────

        /// <summary>
        /// 이 칸에는 바닥이 없다. 대청 널이 z 4.5 에서 끊기고 그 너머는 기단(1.65)이라,
        /// 문을 열고 들어서면 <b>반 자를 헛디딘다</b>. 대청과 같은 높이로 한 장 깐다.
        /// </summary>
        private static void Floor(Scene scene, System.Text.StringBuilder log)
        {
            var t = Find(scene, "동헌방_마루");
            if (t == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "동헌방_마루";
                Undo.RegisterCreatedObjectUndo(go, "동헌방 마루");
                t = go.transform;
            }
            float w = RoomX1 - RoomX0, d = RoomZ1 - RoomZ0;
            t.SetParent(null, true);
            t.position = new Vector3((RoomX0 + RoomX1) * 0.5f, Maru - 0.05f, (RoomZ0 + RoomZ1) * 0.5f);
            t.rotation = Quaternion.identity;
            t.localScale = new Vector3(w, 0.10f, d);

            var mat = WoodMaterial(scene);
            var mr = t.GetComponent<MeshRenderer>();
            if (mr != null && mat != null) mr.sharedMaterial = mat;

            log.AppendLine("── 방에 마룻널을 깔았다 " + w.ToString("F2") + " × " + d.ToString("F2")
                         + "m, 윗면 y " + Maru.ToString("F2") + " (대청과 같은 높이)");
        }

        /// <summary>문서고 마루가 쓰는 나뭇결을 그대로 빌린다 — 같은 집의 같은 널이다.</summary>
        private static Material WoodMaterial(Scene scene)
        {
            var maru = Find(scene, "마루");
            if (maru == null) return null;
            foreach (var r in maru.GetComponentsInChildren<MeshRenderer>(true))
                if (r.sharedMaterial != null) return r.sharedMaterial;
            return null;
        }

        // ── ② 세살문 셋을 여닫이로 ────────────────

        /// <summary>
        /// 북쪽 세 짝에 돌쩌귀를 단다. 셋 다 <b>왼쪽 세로변</b>에서 대청 쪽(-z)으로
        /// 접힌다 — 한 짝씩 제 칸 밖으로 나가 접히므로 서로 부딪지 않는다.
        ///
        /// 문짝 한 벌은 그림이 셋이다: 문짝 제 몸 · 뒷면을 메우는 겉짝 · 한가운데
        /// 맞댐대. 셋을 한 벌로 묶어 같이 돌린다. 전에 겉짝에 여닫이를 따로 물려
        /// <b>유령이 여섯 개</b> 선 적이 있다 — 겉짝은 도는 것이 아니라 <b>딸려 도는</b> 것이다.
        /// </summary>
        private static void Doors(Scene scene, System.Text.StringBuilder log)
        {
            var don = Find(scene, "Donheon");
            var outer = Find(scene, "겉문");
            if (don == null) { log.AppendLine("── 동헌 문을 못 찾았다"); return; }

            string[] leaf = { "SM_Door_Sesal_r40", "SM_Door_Sesal_r40_02", "SM_Door_Sesal_r40_03" };
            int made = 0;
            var doors = new List<SwingDoor>();

            for (int i = 0; i < leaf.Length; i++)
            {
                var body = Deep(don, leaf[i]);
                if (body == null) continue;

                var parts = new List<Transform> { body };
                if (outer != null)
                {
                    var skin = Deep(outer, leaf[i] + "_겉");
                    var strip = Deep(outer, leaf[i] + "_맞댐대");
                    if (skin != null) parts.Add(skin);
                    if (strip != null) parts.Add(strip);
                }

                var sd = body.GetComponent<SwingDoor>();
                if (sd == null) { sd = Undo.AddComponent<SwingDoor>(body.gameObject); made++; }

                var so = new SerializedObject(sd);
                var pp = so.FindProperty("_parts");
                pp.arraySize = parts.Count;
                for (int k = 0; k < parts.Count; k++) pp.GetArrayElementAtIndex(k).objectReferenceValue = parts[k];
                so.FindProperty("_hinge").vector3Value = new Vector3(LeafX[i] - LeafHalf, Maru, DoorZ);
                so.FindProperty("_openAngle").floatValue = 85f;      // + 면 대청 쪽(-z)으로 접힌다
                so.FindProperty("_title").stringValue = "세살문";
                so.FindProperty("_shutBody").stringValue = "동헌 안으로 드는 문이다. 닫혀 있다.";
                so.FindProperty("_openBody").stringValue = "동헌 안으로 드는 문이다. 열려 있다.";
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(sd);
                doors.Add(sd);
            }
            log.AppendLine("── 세살문 " + doors.Count + "짝을 여닫이로(새로 단 것 " + made + ")");
            _northDoors = doors;
        }

        private static List<SwingDoor> _northDoors = new List<SwingDoor>();

        // ── ③ 방 안 — 앉는 자리 · 발 · 불 · 부르기판 ──

        private static void Room(Scene scene, System.Text.StringBuilder log)
        {
            // 사람이 놓아 둔 서안 한 벌을 <b>서안_자리 밑에서 꺼낸다</b>. 서안_자리는
            // 판결하는 자리(ReportDesk)라, 심문 소품이 그 밑에 매달려 있으면 판결 자리를
            // 옮길 때마다 심문 소품이 따라온다.
            var props = Find(scene, "임시소품");
            if (props != null && props.parent != null)
            {
                Undo.SetTransformParent(props, null, "심문 서안 꺼내기");
                props.name = "심문_서안";
                log.AppendLine("── 서안 한 벌을 서안_자리 밑에서 꺼내 심문_서안 으로 세웠다");
            }

            // 표식만 걸상 위에 얹는다. 걸상은 사람이 놓아 둔 것을 그대로 쓴다.
            Put(scene, "어사_자리", JudgeSeat, 270f);
            Put(scene, "대청_앞자리", GuiltSeat, 90f);      // 이름은 대청 시절의 자취다
            log.AppendLine("── 마주 앉는 자리: 어사 x " + JudgeSeat.x.ToString("F2")
                         + " · 죄인 x " + GuiltSeat.x.ToString("F2") + " (사이 "
                         + (JudgeSeat.x - GuiltSeat.x).ToString("F2") + "m, 서안을 두고)");

            // 문서고에 갖다 둔 걸상 두 벌은 걷어낸다. 방에는 이미 앉을 것이 있다.
            int gone = 0;
            foreach (var n in new[] { "죄인_걸상", "어사_걸상" })
            {
                var t = Find(scene, n);
                if (t == null) continue;
                Undo.DestroyObjectImmediate(t.gameObject);
                gone++;
            }
            if (gone > 0) log.AppendLine("── 문서고에 있던 걸상 " + gone + "벌을 걷어냈다");

            // 발은 둘 사이에 건다. 여기서는 x 를 두고 마주 보므로 90도라야 시선을 가로막는다.
            // <b>바닥까지 안 내린다</b> — 서안이 그 자리에 있다. 상판(2.89) 바로 위에서 멈춘다.
            var veil = Find(scene, "발");
            if (veil != null)
            {
                Undo.RecordObject(veil, "발 옮기기");
                veil.SetPositionAndRotation(new Vector3(DeskX, 4.20f, DeskZ), Quaternion.Euler(0f, 90f, 0f));
                var cv = veil.GetComponent<CourtVeil>();
                if (cv != null)
                {
                    var so = new SerializedObject(cv);
                    so.FindProperty("_length").floatValue = 1.25f;   // 4.20 → 2.95, 상판 바로 위
                    so.FindProperty("_width").floatValue = 2.20f;    // 방이 z 로 2.65m 밖에 안 된다
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(cv);
                }
                log.AppendLine("── 발을 방 안 서안 위로 (내려도 상판 위에서 멈춘다)");
            }

            Lights(scene, log);

            // 부르기판을 방 안으로. <b>사정거리를 좁힌다</b> — 9m 면 대청의 교의까지
            // 덮어, 장계를 봉하러 앉은 자리에서도 사람 이름표가 뜬다.
            var bench = Find(scene, "_부르기판");
            if (bench != null)
            {
                Undo.RecordObject(bench, "부르기판 옮기기");
                bench.position = new Vector3(DeskX, Maru, DeskZ);
                var ib = bench.GetComponent<InterrogationBench>();
                if (ib != null)
                {
                    var so = new SerializedObject(ib);
                    so.FindProperty("_from").objectReferenceValue = Find(scene, "어사_자리");
                    so.FindProperty("_radius").floatValue = 4.0f;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(ib);
                }
                log.AppendLine("── 부르기판을 방 안으로 (사정거리 9 → 4m, 대청에서는 안 뜬다)");
            }
        }

        /// <summary>
        /// <b>어두워서 못 쓰던 두 자리에 빛을 넣는다.</b>
        ///
        /// 동헌은 해를 등지고 앉은 데다 처마가 깊어, 대청도 방도 낮인데 <b>새까맣다</b>.
        /// 방은 갇혀 있으니 그렇다 치더라도, 대청은 사람 얼굴을 보고 판결을 적는
        /// 자리인데 아무것도 안 보인다. 없는 해를 억지로 돌리는 대신 세 등을 놓는다:
        ///
        ///   · <b>방 갓등</b>   — 위에서 아래로. 서안과 마루를 밝힌다
        ///   · <b>창빛</b>      — 세살문 쪽에서 방 안으로. <b>그림자를 안 지운다</b>,
        ///                        곧 창호지를 넘어 드는 낮빛 노릇을 한다
        ///   · <b>대청등</b>    — 판결 자리 위. 장계를 적을 만큼만
        ///
        /// 점광(Point)이 아니라 <b>갓등(Spot)</b>인 까닭: 점광은 사방으로 나가 벽을 넘어
        /// 옆 칸까지 새고, 그림자를 켜면 여섯 면을 다 굽느라 비싸다. 갓등은 한 면이면 된다.
        /// </summary>
        private static void Lights(Scene scene, System.Text.StringBuilder log)
        {
            Lamp(scene, "동헌방_빛", new Vector3(14.76f, 4.60f, DeskZ), new Vector3(90f, 0f, 0f),
                 4.6f, 130f, 3.0f, LightShadows.Soft);

            // 문 쪽에서 방으로 비껴 든다. 그림자를 끄면 문짝을 통과하므로 창호지가 된다.
            Lamp(scene, "동헌방_창빛", new Vector3(LeafX[1], 3.70f, DoorZ - 0.30f), new Vector3(18f, 0f, 0f),
                 5.5f, 120f, 2.0f, LightShadows.None);

            Lamp(scene, "대청_빛", new Vector3(14.80f, 5.40f, 0f), new Vector3(90f, 0f, 0f),
                 5.8f, 120f, 3.0f, LightShadows.Soft);

            log.AppendLine("── 등 셋: 방 갓등 · 창빛(문을 넘어 든다) · 대청등(판결 자리)");
        }

        private static void Lamp(Scene scene, string name, Vector3 pos, Vector3 euler,
                                 float range, float angle, float intensity, LightShadows shadows)
        {
            var t = Find(scene, name);
            if (t == null)
            {
                var go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, name);
                go.AddComponent<Light>();
                t = go.transform;
            }
            t.SetParent(null, true);
            t.SetPositionAndRotation(pos, Quaternion.Euler(euler));
            var lt = t.GetComponent<Light>();
            if (lt == null) lt = Undo.AddComponent<Light>(t.gameObject);
            lt.type = LightType.Spot;
            lt.range = range;
            lt.spotAngle = angle;
            lt.intensity = intensity;
            lt.color = new Color(1f, 0.97f, 0.92f);
            lt.shadows = shadows;
            EditorUtility.SetDirty(lt);
        }

        // ── ④ 불려 오는 길 ────────────────────────

        /// <summary>
        /// 뜰에서 방까지 네 다리로 걷는다. 곧장 그으면 기단 벽에 코를 박으므로
        /// 계단 아래를 한 번 밟고, 대청에 올라선 뒤 <b>가운데 문</b>으로 들어간다.
        /// </summary>
        private static void Summon(Scene scene, System.Text.StringBuilder log)
        {
            var stair = Spot(scene, "뜰_계단아래", new Vector3(7.40f, 0f, 0f), 90f);
            var porch = Spot(scene, "문_앞자리", new Vector3(11.90f, Maru, 0f), 90f);
            var atDoor = Spot(scene, "동헌_방문앞", new Vector3(LeafX[1], Maru, 3.30f), 0f);
            var inside = Spot(scene, "동헌_방안", new Vector3(LeafX[1], Maru, 5.10f), 0f);

            var front = Find(scene, "대청_앞자리");
            var judge = Find(scene, "어사_자리");
            var via = new[] { stair, porch, atDoor, inside };

            int n = 0;
            foreach (var cs in Object.FindObjectsByType<CourtSummon>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var so = new SerializedObject(cs);
                var vp = so.FindProperty("_via");
                vp.arraySize = via.Length;
                for (int i = 0; i < via.Length; i++) vp.GetArrayElementAtIndex(i).objectReferenceValue = via[i];
                so.FindProperty("_frontSpot").objectReferenceValue = front;
                so.FindProperty("_faceTarget").objectReferenceValue = judge;
                so.FindProperty("_walkBool").stringValue = "Walking";
                so.FindProperty("_sitBool").stringValue = "Sitting";

                var dp = so.FindProperty("_doors");
                dp.arraySize = _northDoors.Count;
                for (int i = 0; i < _northDoors.Count; i++) dp.GetArrayElementAtIndex(i).objectReferenceValue = _northDoors[i];

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(cs);
                n++;
            }
            log.AppendLine("── " + n + "사람이 뜰 → 계단 → 대청 → 세살문 → 방으로 걸어 들어와 앉는다");
        }

        // ── ⑤ 판결은 대청 교의에서 ────────────────

        /// <summary>
        /// 교의와 앉는 자리는 그대로다. 다만 서안 <b>실물</b>이 방으로 들어가 버려
        /// 판결 자리가 휑해졌으므로, 같은 널로 한 벌을 더 놓는다(걸상은 없다 —
        /// 여기 앉는 것은 교의다).
        /// </summary>
        private static void Verdict(Scene scene, System.Text.StringBuilder log)
        {
            var anchor = Find(scene, "서안_자리");
            if (anchor == null) { log.AppendLine("── 서안_자리를 못 찾았다"); return; }

            var desk = Find(scene, "판결_서안");
            if (desk == null)
            {
                var src = Find(scene, "심문_서안");
                if (src == null) { log.AppendLine("── 베낄 서안이 없다"); return; }

                var go = new GameObject("판결_서안");
                Undo.RegisterCreatedObjectUndo(go, "판결 서안");
                desk = go.transform;
                foreach (var part in new[] { "상판", "다리_앞", "다리_뒤" })
                {
                    var piece = Deep(src, part);
                    if (piece == null) continue;
                    var copy = Object.Instantiate(piece.gameObject, desk);
                    copy.name = part;
                    copy.transform.localPosition = piece.position - src.position;
                    copy.transform.localRotation = piece.rotation * Quaternion.Inverse(src.rotation);
                }
            }
            desk.SetParent(null, true);
            desk.SetPositionAndRotation(anchor.position, anchor.rotation);
            log.AppendLine("── 판결 자리에 서안 한 벌 (교의 앞 " + anchor.position.x.ToString("F2") + ")");
        }

        // ── ⑥ 문서고는 자료만 ─────────────────────

        /// <summary>
        /// 문서고에서 심문 채비를 걷어낸다. 여닫이 여섯 짝은 이제 <b>사람이 제 손으로</b>
        /// 연다 — 불려 온 사람을 따라 저 혼자 열리던 것을 끊는다(그 줄은 ④에서
        /// 동헌 세살문으로 옮겨 갔다).
        /// </summary>
        private static void Archive(Scene scene, System.Text.StringBuilder log)
        {
            int gone = 0;
            foreach (var n in new[] { "문서고_문앞", "문서고_문안" })
            {
                var t = Find(scene, n);
                if (t == null) continue;
                Undo.DestroyObjectImmediate(t.gameObject);
                gone++;
            }
            int shelf = 0, chest = 0;
            var shelves = Find(scene, "서가");
            var chests = Find(scene, "문서궤들");
            if (shelves != null) shelf = shelves.childCount;
            if (chests != null) chest = chests.childCount;
            log.AppendLine("── 문서고는 자료만: 서가 " + shelf + " · 문서궤 " + chest
                         + " (심문 표식 " + gone + "개를 걷어냈다, 문은 손으로 연다)");
        }

        // ── ⑦ 지도에 방을 올린다 ──────────────────

        private static void Travel(Scene scene, System.Text.StringBuilder log)
        {
            var board = Object.FindFirstObjectByType<TravelBoard>(FindObjectsInactive.Include);
            if (board == null) { log.AppendLine("── 지도를 못 찾았다"); return; }

            var spot = Spot(scene, "갈곳_동헌 안", new Vector3(LeafX[1], Maru, 5.05f), 0f);

            var so = new SerializedObject(board);
            var stops = so.FindProperty("_stops");
            for (int i = 0; i < stops.arraySize; i++)
                if (stops.GetArrayElementAtIndex(i).FindPropertyRelative("이름").stringValue == "동헌 안")
                { log.AppendLine("── 지도에 이미 '동헌 안'이 있다"); return; }

            int at = stops.arraySize;
            stops.arraySize = at + 1;
            var row = stops.GetArrayElementAtIndex(at);
            row.FindPropertyRelative("이름").stringValue = "동헌 안";
            row.FindPropertyRelative("자리").objectReferenceValue = spot;
            row.FindPropertyRelative("여기").floatValue = 3.5f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(board);
            log.AppendLine("── 지도에 '동헌 안'을 올렸다(대청 · 문서고 · 마당과 나란히)");
        }

        // ── 잔심부름 ──────────────────────────────

        private static void Put(Scene scene, string name, Vector3 pos, float yaw)
        {
            var t = Find(scene, name);
            if (t == null) return;
            Undo.RecordObject(t, "자리 옮기기");
            t.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        }

        private static Transform Spot(Scene scene, string name, Vector3 pos, float yaw)
        {
            var t = Find(scene, name);
            if (t == null)
            {
                var go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, "자리");
                t = go.transform;
            }
            t.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            return t;
        }

        private static Transform Deep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        private static Transform Find(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root.transform;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t;
            }
            return null;
        }
    }
}
