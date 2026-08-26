using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 혼상 발광 제어 (2026-08-10, 점등 연출 2026-08-15 개정).
    /// - SetLit(bool): 내부 광원 + 내피 이미시브만 켜고 끈다 (구멍으로 빛이 새어나오는 상태).
    /// - StarNight(bool): 즉시 일괄 처리 — 혼상 점등 + 방 등잔 소등 + 천장 별 점등 (에디터 토글용).
    /// - PlayIgnite()/PlayExtinguish(): **점등 연출 시퀀스** — 내부광이 차오르고, 방 등불과
    ///   앰비언트가 거의 암흑까지 내려가고, 카메라가 천천히 천장 정면(-80°)을 올려다본 채
    ///   별이 쏟아지듯 나타난 뒤 잠시 머무르고 조작이 돌아온다. 퍼즐 로직은 이 메서드 하나만
    ///   부르면 된다. 불을 거두면 역순 복구 + 등불(LanternPickup)이 곁상으로 되돌아간다.
    ///   카메라 틸트는 데스크톱 워커(DebugWalkController)가 있을 때만 — VR 리그에서는 생략되고
    ///   어두워진 방에서 유일하게 밝아지는 천장이 시선을 끈다.
    ///   방 조명·별 그룹은 관측실이 재생성돼도 깨지지 않게 이름으로 찾는다.
    ///   아래층 등불(선아, "아래층_등불")은 서사상 항상 켜 두므로 소등에서 제외.
    /// </summary>
    public class HonsangController : MonoBehaviour
    {
        [Header("혼상 내부")]
        public Light innerLight;
        public Renderer interiorRenderer;                      // 구_내피
        [ColorUsage(false, true)]
        public Color emissionLit = new Color(3.2f, 1.9f, 0.9f); // 켜졌을 때 내피 발광색 (웜)

        [Header("구 회전 (퍼즐용)")]
        /// <summary>구_회전 피벗 — 부모(구)가 극축 기울기를 갖고, 이 트랜스폼은 로컬 Y 회전만 한다.
        /// 퍼즐 코드는 RotateOrb/OrbAngle만 쓰면 된다.</summary>
        public Transform orb;

        [Header("점등 연출")]
        [Tooltip("점등 연출 길이(초)")]
        public float igniteLength = 8.5f;
        [Tooltip("소등 복구 길이(초)")]
        public float extinguishLength = 5.0f;
        [Tooltip("별밤 앰비언트 — 새카만 암흑 (혼상 구멍 빛과 천장 별만 남게, 2026-08-15 재하향)")]
        public Color nightAmbient = new Color(0.004f, 0.005f, 0.010f);
        public Color nightFog = new Color(0.003f, 0.004f, 0.008f);
        [Tooltip("별을 올려다볼 때 카메라 틸트(도) — -80은 천정에 코를 박는다. -42면 은하수가 대각으로 흐르고 하단에 돔 가장자리가 걸린다 (2026-08-15 실측 확정)")]
        public float gazePitch = -42f;
        [Tooltip("별밤 카메라 시야각 — 돔 전체가 한눈에 들어오게 넓힌다 (데스크톱 한정, 소등 때 복원)")]
        public float gazeFov = 74f;

        [Header("관측실 연동 (이름으로 탐색)")]
        public string roomRootName = "관측실";
        public string lightGroupName = "조명";
        public string starGroupName = "혼상별_별빛";
        public string keepLitName = "아래층_등불";              // 소등 제외 (선아의 등불)
        /// <summary>소품 등잔이 있는 루트 (관측실 재생성에 지워지지 않게 방 루트 밖에 있다).
        /// 별밤에 방 등잔과 함께 꺼진다. 비워두면 무시.</summary>
        public string propLightRootName = "관측실_소품";

        public bool IsLit { get; private set; }

        // 점등 때 저장해 두는 원래 환경값 — 소등 복구 목표 (기본값 = 쿨 강 확정값)
        Color dayAmbient = new Color(0.140f, 0.180f, 0.310f);
        Color dayFog = new Color(0.026f, 0.038f, 0.085f);
        float dayFov = 60f;

        /// <summary>극축 기준 구 회전각 (도). 구_회전은 로컬 Y 회전만 하므로 y 성분이 곧 각도다.</summary>
        public float OrbAngle
        {
            get => orb != null ? orb.localEulerAngles.y : 0f;
            set { if (orb != null) orb.localEulerAngles = new Vector3(0f, value, 0f); }
        }

        /// <summary>구를 극축 둘레로 deltaDeg만큼 돌린다 (별구멍 패턴·빛 투영이 함께 돈다).</summary>
        public void RotateOrb(float deltaDeg)
        {
            if (orb != null) orb.Rotate(0f, deltaDeg, 0f, Space.Self);
        }

        MaterialPropertyBlock mpb;

        /// <summary>혼상 자체 점등/소등 — 내부 광원 + 내피 이미시브.</summary>
        public void SetLit(bool lit)
        {
            IsLit = lit;
            if (innerLight != null) innerLight.enabled = lit;
            if (interiorRenderer != null)
            {
                if (mpb == null) mpb = new MaterialPropertyBlock();
                interiorRenderer.GetPropertyBlock(mpb);
                mpb.SetColor("_EmissionColor", lit ? emissionLit : Color.black);
                interiorRenderer.SetPropertyBlock(mpb);
            }
        }

        /// <summary>즉시 일괄 (에디터 토글용): 혼상 점등 → 방 등잔 소등 → 천장 별 점등 (off면 역순 복구).</summary>
        public void StarNight(bool on)
        {
            SetLit(on);
            var root = GameObject.Find(roomRootName);
            if (root == null) return;
            var lights = root.transform.Find(lightGroupName);
            if (lights != null)
                foreach (var l in lights.GetComponentsInChildren<Light>(true))
                {
                    if (l == innerLight) continue;
                    var t = l.transform;
                    bool keep = false;
                    for (; t != null && t != lights; t = t.parent)
                        if (t.name == keepLitName) { keep = true; break; }
                    if (!keep) l.enabled = !on;
                }
            if (!string.IsNullOrEmpty(propLightRootName))
            {
                var propRoot = GameObject.Find(propLightRootName);
                if (propRoot != null)
                    foreach (var l in propRoot.GetComponentsInChildren<Light>(true))
                    {
                        // 켜 둘 때는 원래 꺼져 있던 등잔(아버지 것)까지 살리지 않는다
                        if (on) { if (l.enabled) { l.enabled = false; l.gameObject.name = "불빛_소등됨"; } }
                        else if (l.gameObject.name == "불빛_소등됨") { l.enabled = true; l.gameObject.name = "불빛"; }
                    }
            }
            RenderSettings.ambientLight = on ? nightAmbient : dayAmbient;
            RenderSettings.fogColor = on ? nightFog : dayFog;
            var stars = root.transform.Find(starGroupName);
            if (stars != null) stars.gameObject.SetActive(on);
        }

        // ── 점등 연출 시퀀스 (2026-08-15 개정) ─────────────────
        // 타임라인 (총 8.5초):
        //   0.2~1.6  ① 내부광·내피 이미시브가 차오르고 구멍으로 빛이 샌다
        //   0.9~3.4  ② 방·소품 등불이 서서히 어두워져 꺼진다
        //   0.9~4.5  ②' 앰비언트·안개색이 거의 암흑까지 내려간다 (혼상 빛과 별만 남게)
        //   2.4~5.4  ④ 카메라가 천천히 위로 올라가 천장 정면(-80°)을 본다 (데스크톱 워커 한정)
        //   2.8~6.8  ⑤ 천장 별이 서서히 나타난다 (별 셰이더 _Intensity를 MPB로 페이드)
        //   6.8~8.5  잠시 머무른다 → ⑥ 조작 복귀
        // 시퀀스 시작 시 포커스 중이면 자동 해제(③), 진행 중 이동 잠금.

        public bool SequenceRunning { get; private set; }

        /// <summary>점등 연출 — 퍼즐 로직은 이것 하나만 부르면 된다.</summary>
        public void PlayIgnite()
        {
            if (!SequenceRunning && !IsLit) StartCoroutine(IgniteSeq());
        }

        /// <summary>소등 복구 — 역순 연출 (별 소멸 → 등불 회복 → 등불이 곁상으로 복귀).</summary>
        public void PlayExtinguish()
        {
            if (!SequenceRunning && IsLit) StartCoroutine(ExtinguishSeq());
        }

        static float S01(float x) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(x));

        void ApplyEmission(float k)
        {
            if (interiorRenderer == null) return;
            if (mpb == null) mpb = new MaterialPropertyBlock();
            interiorRenderer.GetPropertyBlock(mpb);
            mpb.SetColor("_EmissionColor", emissionLit * k);
            interiorRenderer.SetPropertyBlock(mpb);
        }

        static void SetStars(Renderer[] rs, MaterialPropertyBlock b, float k)
        {
            foreach (var r in rs)
            {
                if (r == null) continue;
                r.GetPropertyBlock(b);
                b.SetFloat("_Intensity", k);
                r.SetPropertyBlock(b);
            }
        }

        bool LampKeep(Transform t, Transform grp)
        {
            for (; t != null && t != grp; t = t.parent)
                if (t.name == keepLitName) return true;
            return false;
        }

        IEnumerator IgniteSeq()
        {
            SequenceRunning = true;
            IsLit = true;
            var rig = FindFirstObjectByType<DebugFocusRig>();
            if (rig != null) rig.ExitFocus();                        // ③ 포커스 중이었다면 자동 해제
            var walk = FindFirstObjectByType<DebugWalkController>();
            if (walk != null) walk.enabled = false;                  // 진행 중 이동 잠금

            // 꺼질 등불 수집 (켜져 있는 것만 — 원래 꺼진 등잔은 건드리지 않는다)
            var lamps = new List<Light>();
            var orig = new List<float>();
            var room = GameObject.Find(roomRootName);
            var grp = room != null ? room.transform.Find(lightGroupName) : null;
            if (grp != null)
                foreach (var l in grp.GetComponentsInChildren<Light>(false))
                {
                    if (l == innerLight || !l.enabled || LampKeep(l.transform, grp)) continue;
                    lamps.Add(l); orig.Add(l.intensity);
                }
            var propRoot = string.IsNullOrEmpty(propLightRootName) ? null : GameObject.Find(propLightRootName);
            if (propRoot != null)
                foreach (var l in propRoot.GetComponentsInChildren<Light>(false))
                {
                    if (!l.enabled) continue;
                    lamps.Add(l); orig.Add(l.intensity);
                    l.gameObject.name = "불빛_소등됨";               // StarNight(false)·소등 복구 규약 호환
                }
            var stars = room != null ? room.transform.Find(starGroupName) : null;
            var starRs = stars != null ? stars.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
            var smpb = new MaterialPropertyBlock();
            float innerMax = innerLight != null ? innerLight.intensity : 0f;
            float pitch0 = walk != null ? walk.Pitch : 0f;
            // 돔 전체가 시야에 들어오게 시야각도 함께 넓힌다 (데스크톱 한정 — VR은 HMD가 시야각을 쥔다).
            // 별밤 동안 유지되고 소등 복구 때 되돌아온다
            var cam = walk != null ? walk.GetComponentInChildren<Camera>() : null;
            if (cam != null) dayFov = cam.fieldOfView;
            dayAmbient = RenderSettings.ambientLight;                // 소등 복구 목표 저장
            dayFog = RenderSettings.fogColor;
            if (innerLight != null) { innerLight.intensity = 0f; innerLight.enabled = true; }
            bool starsOn = false;

            float t = 0f;
            while (t < igniteLength)
            {
                t += Time.deltaTime;
                float ki = S01((t - 0.2f) / 1.4f);                              // ① 구멍 빛
                if (innerLight != null) innerLight.intensity = innerMax * ki;
                ApplyEmission(ki);
                float kr = 1f - S01((t - 0.9f) / 2.5f);                         // ② 등불 소등
                for (int i = 0; i < lamps.Count; i++)
                    if (lamps[i] != null) lamps[i].intensity = orig[i] * kr;
                float ka = S01((t - 0.9f) / 3.6f);                              // ②' 암흑화
                RenderSettings.ambientLight = Color.Lerp(dayAmbient, nightAmbient, ka);
                RenderSettings.fogColor = Color.Lerp(dayFog, nightFog, ka);
                float kg = S01((t - 2.4f) / 3.0f);                              // ④ 천장으로 — 돔 전체가 담기게
                if (walk != null) walk.Pitch = Mathf.Lerp(pitch0, gazePitch, kg);
                if (cam != null) cam.fieldOfView = Mathf.Lerp(dayFov, gazeFov, kg);
                if (t >= 2.8f)                                                  // ⑤ 별이 쏟아진다
                {
                    if (!starsOn && stars != null) { stars.gameObject.SetActive(true); starsOn = true; }
                    SetStars(starRs, smpb, S01((t - 2.8f) / 4.0f));
                }
                yield return null;
            }

            // 마무리 — 세기값은 원상 복원해 두고 끈다 (즉시 토글 StarNight와 호환)
            for (int i = 0; i < lamps.Count; i++)
                if (lamps[i] != null) { lamps[i].intensity = orig[i]; lamps[i].enabled = false; }
            if (innerLight != null) innerLight.intensity = innerMax;
            ApplyEmission(1f);
            SetStars(starRs, smpb, 1f);
            RenderSettings.ambientLight = nightAmbient;
            RenderSettings.fogColor = nightFog;
            if (cam != null) cam.fieldOfView = gazeFov;
            if (walk != null) walk.enabled = true;                              // ⑥ 조작 복귀
            SequenceRunning = false;
        }

        IEnumerator ExtinguishSeq()
        {
            SequenceRunning = true;
            IsLit = false;
            var rig = FindFirstObjectByType<DebugFocusRig>();
            if (rig != null) rig.ExitFocus();
            var walk = FindFirstObjectByType<DebugWalkController>();
            if (walk != null) walk.enabled = false;

            // 되살릴 등불 수집 — 점등 때 꺼 둔 것들 (세기값은 컴포넌트에 남아 있다)
            var lamps = new List<Light>();
            var target = new List<float>();
            var room = GameObject.Find(roomRootName);
            var grp = room != null ? room.transform.Find(lightGroupName) : null;
            if (grp != null)
                foreach (var l in grp.GetComponentsInChildren<Light>(true))
                {
                    if (l == innerLight || l.enabled || LampKeep(l.transform, grp)) continue;
                    lamps.Add(l); target.Add(l.intensity);
                    l.intensity = 0f; l.enabled = true;
                }
            var propRoot = string.IsNullOrEmpty(propLightRootName) ? null : GameObject.Find(propLightRootName);
            if (propRoot != null)
                foreach (var l in propRoot.GetComponentsInChildren<Light>(true))
                {
                    if (l.gameObject.name != "불빛_소등됨") continue;
                    l.gameObject.name = "불빛";
                    lamps.Add(l); target.Add(l.intensity);
                    l.intensity = 0f; l.enabled = true;
                }
            var stars = room != null ? room.transform.Find(starGroupName) : null;
            var starRs = stars != null ? stars.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
            var smpb = new MaterialPropertyBlock();
            float innerMax = innerLight != null ? innerLight.intensity : 0f;
            var cam = walk != null ? walk.GetComponentInChildren<Camera>() : null;
            float fov0 = cam != null ? cam.fieldOfView : gazeFov;

            float t = 0f;
            while (t < extinguishLength)
            {
                t += Time.deltaTime;
                SetStars(starRs, smpb, 1f - S01(t / 1.8f));                    // 별이 스러진다
                float ki = 1f - S01((t - 0.4f) / 1.2f);                        // 구멍 빛이 잦아든다
                if (innerLight != null) innerLight.intensity = innerMax * ki;
                ApplyEmission(ki);
                float ka = S01((t - 0.8f) / 2.4f);                             // 앰비언트 회복
                RenderSettings.ambientLight = Color.Lerp(nightAmbient, dayAmbient, ka);
                RenderSettings.fogColor = Color.Lerp(nightFog, dayFog, ka);
                if (cam != null) cam.fieldOfView = Mathf.Lerp(fov0, dayFov, ka); // 시야각 복원
                float kr = S01((t - 1.4f) / 2.8f);                             // 등불이 살아난다
                for (int i = 0; i < lamps.Count; i++)
                    if (lamps[i] != null) lamps[i].intensity = target[i] * kr;
                yield return null;
            }

            if (stars != null) stars.gameObject.SetActive(false);
            SetStars(starRs, smpb, 1f);                                        // 다음 점등 대비 초기화
            for (int i = 0; i < lamps.Count; i++)
                if (lamps[i] != null) lamps[i].intensity = target[i];
            if (innerLight != null) { innerLight.intensity = innerMax; innerLight.enabled = false; }
            ApplyEmission(0f);
            RenderSettings.ambientLight = dayAmbient;
            RenderSettings.fogColor = dayFog;
            if (cam != null) cam.fieldOfView = dayFov;
            LanternPickup.RestoreConsumed();                                   // 등불이 곁상으로 돌아온다
            if (walk != null) walk.enabled = true;
            SequenceRunning = false;
        }
    }
}
