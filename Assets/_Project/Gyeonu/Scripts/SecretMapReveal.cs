using System.Collections;
using UnityEngine;
using IMUNROK.Common;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 타공 비밀지도 "펼쳐보기" 연출 (2026-08-23).
    ///
    /// ■ 무엇이 일어나는가
    ///   조건이 맞으면 — 관측실에서 혼상에 불이 들어와 있으면 — 플레이어가 지도를 들어 올리고,
    ///   시선이 천천히 천장으로 올라가고, 종이에 뚫린 구멍마다 별빛이 배어 나와 길이 된다.
    ///   잠시 머무른 뒤 지도를 내리고 조작이 돌아오며 견우마을 길이 열린다.
    ///   **정답 판정은 없다.** 조건이 맞으면 반드시 이렇게 된다 — 퍼즐이 아니라 연출이다.
    ///
    /// ■ 왜 혼상 점등을 조건으로 삼나
    ///   지도는 글이 아니라 **구멍**이다. 뒤에서 빛이 오지 않으면 아무것도 아니다.
    ///   혼상이 켜져 방이 어두워지고 천장에 별이 깔린 그 순간에만 구멍이 별과 맞물린다.
    ///
    /// ■ 혼상 시퀀스와 같은 방식
    ///   <see cref="HonsangController"/>.IgniteSeq를 그대로 본떴다 —
    ///   코루틴 하나가 시간축을 쥐고, 걷기(<see cref="DebugWalkController"/>)를 잠그고,
    ///   카메라 틸트는 데스크톱 워커가 있을 때만 건드리며(VR은 HMD가 시선을 쥔다),
    ///   끝에서 조작을 돌려준다. 카메라 목표 각도·시야각도 혼상 쪽 값을 그대로 물려받아
    ///   두 연출이 이어 붙었을 때 화면이 튀지 않는다.
    ///
    /// ■ 조건 미달일 때
    ///   막지 않고 **문구만** 띄운다. 어디서 써도 눌리고, 다만 아무 일도 안 일어난다.
    ///   "왜 안 되지"가 아니라 "여기서는 아니구나"로 읽히도록.
    /// </summary>
    public class SecretMapReveal : MonoBehaviour
    {
        // ── 안내 문구 ─────────────────────────────────────
        // 조건을 설명하지 않는다. 지금 눈에 보이는 것만 말하고, 무엇이 모자란지는 스스로 깨닫게 둔다.
        // (관측실이 아닌 곳의 문구는 SecretMapUse가 쥔다 — 여기는 관측실 안의 사정만 안다)
        const string MsgNoLight = "구멍 사이로 스밀 빛이 없다. 지금은 아무것도 보이지 않는다.";
        const string MsgBusy = "지금은 지도를 펼 겨를이 없다.";
        const string MsgDone = "구멍을 이어 흐르던 별빛이 눈에 남았다. 이제 그 길을 따라갈 수 있다.";

        // ── 시간표 (총 10.4초) ────────────────────────────
        //   0.15~2.10  ① 지도가 아래에서 올라와 눈앞에 선다
        //   1.10~4.30  ② 시선이 천천히 천장으로 (혼상 점등과 같은 각도·시야각)
        //   3.20~7.10  ③ 구멍마다 별빛이 배어 나와 앞에서 뒤로 길을 그린다
        //   7.10~8.90  ④ 머무른다
        //   8.90~10.20 ⑤ 지도를 내린다
        //  10.40       ⑥ 조작 복귀 · 견우마을 해금
        const float TLift0 = 0.15f, TLift1 = 2.10f;
        const float TGaze0 = 1.10f, TGaze1 = 4.30f;
        const float TPath0 = 3.20f, TPath1 = 7.10f;
        const float TDrop0 = 8.90f, TDrop1 = 10.20f;
        const float TEnd = 10.40f;

        // ── 손에 든 지도의 자리 (눈 기준 로컬) ─────────────
        // 지도는 눈(카메라) 밑에 달린다 — 시선이 천장으로 올라가도 지도가 함께 따라 올라가
        // 별 아래 지도가 겹쳐 보인다. 값은 가로 0.62m 지도가 시야를 반쯤 채우는 거리.
        static readonly Vector3 PosLow = new Vector3(0f, -0.55f, 0.44f);
        static readonly Vector3 PosUp = new Vector3(0f, -0.05f, 0.48f);
        static readonly Vector3 RotLow = new Vector3(-112f, 0f, -8f);   // 아직 눕혀 든 상태
        static readonly Vector3 RotUp = new Vector3(-79f, 0f, -1.5f);   // 거의 정면 — 아래 설명

        // ── 왜 -79도인가 (2026-08-23 재조정) ────────────────
        // 앞면 법선은 로컬 +Y다. 눈 기준 X 회전 θ에서 법선 = (0, cosθ, sinθ) 이므로
        // **눈을 정확히 마주 보는 각**은 지도가 눈보다 조금 아래(y −0.05, z 0.48)에 있는 만큼
        // −90도가 아니라 **−84.6도**다. 거기서 6도쯤 되젖힌 −79도로 잡았다:
        //   · 완전히 마주 보게(−84.6) 두면 손에 든 종이가 아니라 눈앞에 붙은 게시판처럼 보인다
        //   · 처음 값 −68도는 16.6도나 누워 별빛 길이 위쪽으로 찌그러져 읽혔다 (사용자 지적)
        // Z −1.5도는 손으로 든 티. 거리도 0.60 → 0.48로 당겨 화면에서 약 1.25배 커졌다
        // (가로 0.62m가 시야 66도를 덮는다 — 돔은 위·양옆으로 여전히 보인다).

        // ── 밝기 (0.5m 앞 점광원이라 값이 작다 — 위 ⚠️ 참고) ──
        // ⚠️ 지도를 당기면 등불까지의 거리도 0.540 → 0.448m로 줄어 **1.45배 밝아진다.**
        //    거리를 고칠 때는 세기도 같이 나눠야 종이가 다시 허옇게 뜨지 않는다.
        const float LampLift = 0.110f;   // 지도를 들었을 때: 그림이 읽히는 밝기
        const float LampPath = 0.038f;   // 별빛이 오를 때: 종이가 물러난다
        const float PaperDim = 0.45f;    // 별빛이 다 올랐을 때 종이에 먹이는 배수

        /// <summary>연출이 도는 동안 참. 중복 실행을 막는다.</summary>
        public static bool Running { get; private set; }

        // ───────────────────────────────────────────────────
        /// <summary>
        /// <see cref="SecretMapUse"/>가 **관측실이라고 판단했을 때만** 부른다.
        /// 맡았으면 true (안내든 연출이든 여기서 끝낸다).
        /// </summary>
        public static bool Use(InventoryItem item, System.Action closeBoard, HonsangController hon)
        {
            if (item == null || hon == null) return false;

            if (Running) { DebugToast.Show(MsgBusy, 2.5f); return true; }

            // 혼상 연출이 도는 중이면 끼어들지 않는다
            if (hon.SequenceRunning) { DebugToast.Show(MsgBusy, 2.5f); return true; }

            // 관측실이되 불이 없다 — 구멍만으로는 아무것도 아니다
            if (!hon.IsLit && !GyeonuWorld.DebugIgnoreConditions)
            {
                DebugToast.Show(MsgNoLight, 3.5f);
                return true;
            }

            if (item.modelPrefab == null)
            {
                Debug.LogError("[비밀지도] 모델 프리팹이 비어 있다 — " +
                               "Tools ▸ 이문록 ▸ 소지품 ▸ 비밀지도 만들기 를 먼저 눌러라");
                return true;
            }

            closeBoard?.Invoke();   // 화면을 차지하는 연출이다 — 판은 물러난다
            DebugToast.HidePinned(); // "지도를 펼쳐 볼 때다" 안내(HonsangController)는 할 일을 다했다 (2026-09-09)

            var go = new GameObject("비밀지도_연출");
            go.AddComponent<SecretMapReveal>().Begin(item, hon);
            return true;
        }

        // ───────────────────────────────────────────────────
        InventoryItem item;
        HonsangController hon;

        void Begin(InventoryItem it, HonsangController h)
        {
            item = it; hon = h;
            StartCoroutine(Seq());
        }

        static float S01(float x) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(x));

        IEnumerator Seq()
        {
            Running = true;

            // ── 조작 잠금 ──
            var walk = FindFirstObjectByType<DebugWalkController>(FindObjectsInactive.Exclude);
            var rig = FindFirstObjectByType<DebugFocusRig>(FindObjectsInactive.Exclude);
            if (rig != null) rig.ExitFocus();               // 무언가 들여다보는 중이었다면 물러난다

            Transform eye = walk != null && walk.eye != null ? walk.eye
                          : (Camera.main != null ? Camera.main.transform : null);
            if (eye == null)
            {
                // ⚠️ 여기서 그냥 빠져나가면 Running이 참인 채로 남아 지도를 **다시는** 못 쓴다.
                Debug.LogWarning("[비밀지도] 눈(카메라)을 못 찾았다 — 연출을 건너뛴다");
                Running = false;
                Destroy(gameObject);
                yield break;
            }

            var interactor = eye.GetComponent<DebugInteractor>();
            if (interactor != null) interactor.enabled = false;
            if (walk != null) walk.enabled = false;

            // ── 지도를 손에 ──
            var map = Instantiate(item.modelPrefab, eye);
            map.name = "펼친_비밀지도";
            foreach (var c in map.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (var p in map.GetComponentsInChildren<ItemPickup>(true)) Destroy(p);
            map.transform.localPosition = PosLow;
            map.transform.localEulerAngles = RotLow;

            var path = map.GetComponentInChildren<SecretMapStarPath>(true);
            if (path != null)
            {
                // 프리팹의 값은 **밝은 소지품 미리보기 무대** 기준으로 올려 둔 것이다.
                // 여기는 반대로 거의 암흑이라 그대로 쓰면 길이 하얗게 뭉친다 — 제 세기로 되돌린다.
                path.brightness = 1f;
                // 종이 물리기도 끈다. 이 연출은 등불을 거두며 **스스로** 종이를 다루는데,
                // 둘이 같은 MPB를 놓고 매 프레임 다투면 밝기가 튄다.
                path.paperDimWhenLit = 0f;
                path.SetProgress(0f);                       // 아직은 종이일 뿐
            }
            else Debug.LogWarning("[비밀지도] 프리팹에 SecretMapStarPath가 없다 — 별빛 길이 뜨지 않는다");

            // 어둠 속에서 종이가 보이도록 지도에만 닿는 작은 빛을 딸려 보낸다.
            // 사거리를 짧게(1.2m) 잡아 방까지 밝히지 않는다 — 별밤의 어둠은 그대로 둔다.
            // ⚠️ 세기가 작아 보여도 0.5m 앞이다. 점광원은 거리 제곱으로 떨어지므로 여기서는
            //    1/0.29 ≈ 3.4배로 들어온다 — 1.35로 잡았더니 종이가 허옇게 타서 그림도,
            //    그 위의 별빛도 아무것도 안 보였다 (2026-08-23 실측).
            var lamp = new GameObject("지도_비추는빛").transform;
            lamp.SetParent(eye, false);
            lamp.localPosition = new Vector3(0.16f, 0.22f, 0.16f);
            var lampLight = lamp.gameObject.AddComponent<Light>();
            lampLight.type = LightType.Point;
            lampLight.color = new Color(0.80f, 0.86f, 1.00f);   // 별빛에 가까운 찬 빛
            lampLight.range = 1.2f;
            lampLight.intensity = 0f;
            lampLight.shadows = LightShadows.None;

            // 별빛이 오르는 동안 종이를 물린다 — 가산 셰이더라 바탕이 밝으면 빛이 묻힌다.
            // 재질을 건드리면 소지품 상세의 지도까지 어두워지므로 **이 개체에만** MPB로 먹인다.
            var paper = map.GetComponent<Renderer>();
            var paperMpb = new MaterialPropertyBlock();

            // ── 카메라 목표값은 혼상 쪽 값을 물려받는다 ──
            float pitch0 = walk != null ? walk.Pitch : 0f;
            var cam = walk != null ? walk.GetComponentInChildren<Camera>() : Camera.main;
            float fov0 = cam != null ? cam.fieldOfView : 60f;
            float fovGaze = hon != null ? hon.gazeFov : fov0;
            float pitchGaze = hon != null ? hon.gazePitch : pitch0;

            float t = 0f;
            while (t < TEnd)
            {
                t += Time.deltaTime;

                // ① 들어 올린다 (내릴 때는 되짚어 간다)
                float kUp = S01((t - TLift0) / (TLift1 - TLift0));
                float kDown = S01((t - TDrop0) / (TDrop1 - TDrop0));
                float k = kUp * (1f - kDown);
                map.transform.localPosition = Vector3.Lerp(PosLow, PosUp, k);
                map.transform.localEulerAngles = Vector3.Lerp(RotLow, RotUp, k);

                // ③이 진행될수록 등불을 거두고 종이를 물려 별빛만 남긴다
                float kp = S01((t - TPath0) / (TPath1 - TPath0));
                lampLight.intensity = Mathf.Lerp(LampLift, LampPath, kp) * k;
                if (paper != null)
                {
                    float dim = Mathf.Lerp(1f, PaperDim, kp);
                    paper.GetPropertyBlock(paperMpb);
                    paperMpb.SetColor("_BaseColor", new Color(dim, dim, dim, 1f));
                    paperMpb.SetColor("_EmissionColor", new Color(0.13f * dim, 0.13f * dim, 0.13f * dim, 1f));
                    paper.SetPropertyBlock(paperMpb);
                }

                // ② 시선이 천장으로 — 데스크톱 워커가 있을 때만.
                //    VR에서는 고개를 빼앗지 않는다. 어두운 방에서 유일하게 밝아지는 것이 손안의
                //    지도와 천장이라, 시선은 저절로 그리로 간다.
                float kg = S01((t - TGaze0) / (TGaze1 - TGaze0));
                if (walk != null) walk.Pitch = Mathf.Lerp(pitch0, pitchGaze, kg);
                if (cam != null) cam.fieldOfView = Mathf.Lerp(fov0, fovGaze, kg);

                // ③ 구멍마다 별빛이 배어 나온다 (내릴 때 함께 스러진다)
                if (path != null) path.SetProgress(kp * (1f - kDown));

                yield return null;
            }

            Cleanup(map, lamp != null ? lamp.gameObject : null);

            // ── 얻은 것 ──
            GyeonuWorld.Set(GyeonuWorld.F_타공지도_길밝힘);
            Journal.Instance.AddClue(CaseId.Case3_Gyeonu, "star_path_lit",
                "타공 비밀지도 — 혼상의 빛 아래에서 구멍이 이어져 길 하나를 그렸다.");
            DebugToast.Show(MsgDone, 4.5f);
            Debug.Log("[비밀지도] 별빛 길을 밝혔다 — 견우마을로 갈 수 있다");

            // 조작 복귀
            if (walk != null) walk.enabled = true;
            if (interactor != null) interactor.enabled = true;
            Running = false;
            Destroy(gameObject);
        }

        /// <summary>연출 뒷정리 — 중간에 튕겨도 손에 든 것이 남지 않게 한곳으로 모았다.</summary>
        void Cleanup(GameObject map, GameObject lamp)
        {
            if (map != null) Destroy(map);
            if (lamp != null) Destroy(lamp);
        }

        void OnDisable()
        {
            // 씬이 바뀌는 등으로 연출이 끊겼을 때 잠금이 남지 않게 한다
            if (!Running) return;
            Running = false;
            var walk = FindFirstObjectByType<DebugWalkController>(FindObjectsInactive.Include);
            if (walk != null) walk.enabled = true;
        }
    }
}
