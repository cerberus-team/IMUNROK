using UnityEngine;
using UnityEngine.InputSystem;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 포커스 조작 입력 (마우스 임시 — VR 컨트롤러 리그로 교체 예정, 2026-08-14).
    /// 워커 카메라에 필요 시 자동 부착된다. 흐름:
    ///   진입: 시점 저장 → 걷기·조준 비활성 → transitionTime 보간으로 대상 정면 → 고정
    ///   조작: 좌클릭 드래그 → FocusInteractable.HandleDrag
    ///   이탈: Esc 또는 우클릭 → 보간 복귀 → 걷기·조준 재활성
    /// 포커스 중에는 화면 가장자리 비네트(절차 텍스처)로 주변을 어둡게 해 대상에 집중시킨다.
    /// 카메라가 이동하는 쪽을 택했다 — 대상을 당겨오면 혼상 내부광원의 구멍 빛 투영이
    /// 방을 따라 움직이고, 복귀 시 원위치 오차·콜라이더 문제가 생긴다.
    /// </summary>
    public class DebugFocusRig : MonoBehaviour
    {
        enum Phase { Idle, Enter, Hold, Exit }

        Phase phase = Phase.Idle;
        FocusInteractable target;
        DebugWalkController walk;
        DebugInteractor interactor;
        Vector3 fromPos, toPos, savedPos;
        Quaternion fromRot, toRot, savedRot;
        float t;       // 보간 진행 0~1
        float dim;     // 현재 비네트 강도
        Renderer vignetteQuad;   // 카메라 앞 쿼드 — OnGUI가 아니라 3D 렌더 (VR·캡처에서도 보인다)
        MaterialPropertyBlock vignetteMpb;
        // ⚠️ static 캐시 금지 (2026-08-14 실측): 도메인 리로드가 꺼진 프로젝트에서 static 참조가
        //    플레이 세션을 넘겨 살아남는데 텍스처 내용은 죽어, 다음 세션 비네트가 흰 화면이 된다
        Texture2D vignetteTex;

        /// <summary>입력 측 진입점 — actor(워커 카메라)에 리그를 붙이고 포커스를 연다.</summary>
        public static void Begin(FocusInteractable it, GameObject actor)
        {
            var rig = actor.GetComponent<DebugFocusRig>();
            if (rig == null) rig = actor.AddComponent<DebugFocusRig>();
            rig.EnterFocus(it);
        }

        public bool IsFocusing => phase != Phase.Idle;

        void EnterFocus(FocusInteractable it)
        {
            if (phase != Phase.Idle || it == null) return;
            target = it;
            walk = GetComponentInParent<DebugWalkController>();
            interactor = GetComponent<DebugInteractor>();
            if (walk != null) walk.enabled = false;          // 이동·시선 잠금
            if (interactor != null) interactor.enabled = false; // 조준점·중복 클릭 잠금
            savedPos = transform.position;
            savedRot = transform.rotation;
            target.GetFocusPose(savedPos, out toPos, out toRot);
            fromPos = savedPos; fromRot = savedRot;
            t = 0f;
            phase = Phase.Enter;
            EnsureVignette();
            target.OnFocusChanged(true);
        }

        /// <summary>복귀 시작 (Esc/우클릭, 테스트 코드에서도 호출 가능).</summary>
        public void ExitFocus()
        {
            if (phase != Phase.Hold) return;
            fromPos = transform.position; fromRot = transform.rotation;
            toPos = savedPos; toRot = savedRot;
            t = 0f;
            phase = Phase.Exit;
            target.OnFocusChanged(false);
        }

        void Update()
        {
            if (phase == Phase.Idle || target == null) return;

            if (phase == Phase.Enter || phase == Phase.Exit)
            {
                t += Time.deltaTime / Mathf.Max(0.05f, target.transitionTime);
                float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                transform.SetPositionAndRotation(Vector3.LerpUnclamped(fromPos, toPos, s),
                    Quaternion.SlerpUnclamped(fromRot, toRot, s));
                dim = target.dimStrength * (phase == Phase.Enter ? s : 1f - s);
                if (t >= 1f)
                {
                    if (phase == Phase.Enter) phase = Phase.Hold;
                    else
                    {
                        if (walk != null) walk.enabled = true;
                        if (interactor != null) interactor.enabled = true;
                        target = null;
                        phase = Phase.Idle;
                        dim = 0f;
                    }
                }
                ApplyVignette();
                return;
            }

            // Hold — 드래그 조작 + 이탈 입력
            dim = target.dimStrength;
            ApplyVignette();
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            if (mouse == null || kb == null) return;
            if ((kb.escapeKey.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame) && target.CanExitFocus)
            {
                ExitFocus();
                return;
            }
            // 누르는 대상(암문 자물쇠) — 커서가 가리킨 곳으로 레이를 쏴 준다.
            // 포커스 중에는 걷기 컨트롤러가 꺼져 커서 잠금이 풀려 있으므로 마우스로 직접 가리킬 수 있다.
            if (mouse.leftButton.wasPressedThisFrame)
            {
                var cam = GetComponent<Camera>();
                if (cam != null) target.HandleClick(cam.ScreenPointToRay(mouse.position.ReadValue()));
            }
            if (mouse.leftButton.isPressed)
            {
                Vector2 d = mouse.delta.ReadValue();
                if (d.sqrMagnitude > 0.0001f) target.HandleDrag(d);
            }
            float sc = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(sc) > 0.01f) target.HandleScroll(sc);
        }

        void OnGUI()
        {
            if (phase != Phase.Hold || target == null) return;
            var hint = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            hint.normal.textColor = new Color(1f, 1f, 1f, 0.55f);
            string status = target.FocusStatus;
            if (!string.IsNullOrEmpty(status))
            {
                var stat = new GUIStyle(hint) { fontSize = 16 };
                stat.normal.textColor = new Color(1f, 0.92f, 0.7f, 0.9f);
                GUI.Label(new Rect(0, Screen.height - 64f, Screen.width, 22f), status, stat);
            }
            // 하단 고정 안내(DebugToast.ShowPinned)가 뜨면 조작 힌트는 그 자리를 내준다
            if (!DebugToast.PinnedActive && !string.IsNullOrEmpty(target.FocusHint))
                GUI.Label(new Rect(0, Screen.height - 40f, Screen.width, 22f), target.FocusHint, hint);
        }

        /// <summary>비네트 쿼드 준비 — 카메라 앞 0.4m, 화면을 덮는 크기. Sprites/Default(알파 블렌드)
        /// 위에 방사형 어둠 텍스처. 알파는 ApplyVignette가 매 프레임 조절한다.</summary>
        void EnsureVignette()
        {
            if (vignetteQuad != null) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "포커스_비네트";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 0.4f);
            go.transform.localRotation = Quaternion.identity;
            var cam = GetComponent<Camera>();
            float fov = cam != null ? cam.fieldOfView : 60f;
            float hgt = 2f * 0.4f * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * 1.25f;   // 여유 25%
            float asp = cam != null ? cam.aspect : 1.78f;
            go.transform.localScale = new Vector3(hgt * asp, hgt, 1f);
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.mainTexture = VignetteTex();
            mat.renderQueue = 3900;   // 투명 뒤 — 항상 마지막에 덮인다
            vignetteQuad = go.GetComponent<Renderer>();
            vignetteQuad.sharedMaterial = mat;
            vignetteQuad.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            vignetteMpb = new MaterialPropertyBlock();
            ApplyVignette();
        }

        void ApplyVignette()
        {
            if (vignetteQuad == null) return;
            if (dim <= 0.003f)
            {
                if (phase == Phase.Idle)
                {
                    Destroy(vignetteQuad.gameObject);
                    vignetteQuad = null;
                    if (vignetteTex != null) { Destroy(vignetteTex); vignetteTex = null; }
                    return;
                }
                vignetteQuad.enabled = false;
                return;
            }
            vignetteQuad.enabled = true;
            vignetteMpb.SetColor("_Color", new Color(1f, 1f, 1f, dim));
            vignetteQuad.SetPropertyBlock(vignetteMpb);
        }

        /// <summary>가장자리로 갈수록 어두워지는 비네트 — 중앙(대상)은 그대로 둔다.</summary>
        Texture2D VignetteTex()
        {
            if (vignetteTex != null) return vignetteTex;
            const int res = 256;
            vignetteTex = new Texture2D(res, res, TextureFormat.RGBA32, false) { name = "포커스_비네트텍스처", hideFlags = HideFlags.HideAndDontSave };
            float c = (res - 1) * 0.5f;
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    float a = Mathf.Pow(Mathf.Clamp01((d - 0.34f) / 0.66f), 1.5f);
                    vignetteTex.SetPixel(x, y, new Color(0f, 0f, 0f, a));
                }
            vignetteTex.Apply();
            return vignetteTex;
        }
    }
}
