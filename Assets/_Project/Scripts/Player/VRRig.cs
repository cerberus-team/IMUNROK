using System.Collections;
using UnityEngine;
using UnityEngine.XR;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>헤드셋을 쓰면 저 혼자 몸을 짓는다</b> — 머리 하나와 손 둘.
    ///
    /// 여태 이 게임의 카메라는 <b>책상 앞 사람</b>이었다. 마우스로 돌리고 WASD 로 걷는
    /// 카메라 하나가 곧 눈이었고, 짚는 광선도 돋보기가 보는 곳도 등불이 종이 뒤에
    /// 갔는지도 전부 그 카메라 하나를 기준으로 쟀다. 헤드셋을 꽂아도 그림만 두 눈으로
    /// 갈릴 뿐, <b>게임이 아는 눈</b>은 여전히 책상 앞에 있었다 — 고개를 돌려 장롱을
    /// 보고 있어도 광선은 엉뚱한 데를 짚었다.
    ///
    /// 그래서 헤드셋이 붙어 있을 때만 이렇게 갈아 끼운다:
    ///
    /// <code>
    ///   VR_몸        ← 스틱으로 옮겨 다니는 것(VRLocomotion). 발이 바닥을 딛는다
    ///     ├ 카메라   ← 헤드셋이 움직인다(XRPose · Head). 게임이 아는 '눈'이 여기가 된다
    ///     ├ 왼손     ← 컨트롤러(XRPose · LeftHand) + 광선(VRRaySelector)
    ///     └ 오른손   ← 컨트롤러(XRPose · RightHand) + 광선(VRRaySelector)
    /// </code>
    ///
    /// 카메라를 <b>새로 만들지 않고</b> 있던 것을 몸 밑으로 옮긴다. 그 카메라에는 이미
    /// 도구·수첩·자막이 매달려 있고 <c>Camera.main</c> 으로 서로를 찾고 있어서, 새 카메라를
    /// 세우면 그 줄이 전부 끊긴다. 옮기기만 하면 <b>고친 데 없이</b> 다 따라온다 —
    /// 돋보기가 보는 곳도, 종이가 걸리는 자리도, 불빛이 종이를 비추는 셈도.
    ///
    /// 마우스로 짚던 부품(<see cref="MouseRaySelector"/>·<see cref="MouseInspector"/>)과
    /// 걷던 부품(<see cref="DebugFlyCamera"/>)은 그동안 꺼 둔다. 둘이 같은 트랜스폼을
    /// 두고 다투면 머리가 떨린다.
    ///
    /// <b>헤드셋이 없으면 아무 일도 안 한다.</b> 책상에서 여는 그대로다.
    /// </summary>
    public class VRRig : MonoBehaviour
    {
        /// <summary>지금 VR 로 서 있나. 다른 곳에서 손짓 안내를 고를 때 본다.</summary>
        public static bool Active { get; private set; }

        /// <summary>몸(스틱으로 옮겨 다니는 것). 순간이동 같은 것이 자리를 옮길 때 쓴다.</summary>
        public static Transform Body { get; private set; }

        public static Transform LeftHand { get; private set; }
        public static Transform RightHand { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            // 아예 서지 않는 두 자리 — 두드림 한 번이 5초씩 화면을 붙잡는 까닭이다.
            //   · 찍는 중(ShotMode)      — 이 장면만 조용하면 된다
            //   · 헤드셋 안 씀(VrPref)   — 당분간 책상에서만 만진다
            if (ShotMode.On || VrPref.HeadsetOff) return;

            var go = new GameObject("_VR_몸짓기");
            go.AddComponent<VRRig>();
            DontDestroyOnLoad(go);
        }

        private void Start()
        {
            // 씬마다 다시 본다. 어전은 고정 화면이라 몸을 안 짓는데, 그 다음 씬으로
            // 넘어가면 지어야 한다 — 이 부품은 씬을 건너 살아남으므로 Start 한 번으로는
            // 그 자리를 잡을 수가 없다.
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnScene;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnScene;
            StartCoroutine(WaitForHeadset());
        }

        private void OnDestroy()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnScene;
        }

        private void OnScene(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
        {
            if (!XRSettings.isDeviceActive) return;
            Build();
        }

        /// <summary>
        /// 헤드셋은 <b>늦게 붙을 수 있다</b>. 링크를 나중에 켜는 일이 흔하므로 몇 초 동안
        /// 기다려 본다. 그 안에 안 붙으면 책상 앞 사람 그대로 두고 물러난다.
        /// </summary>
        private IEnumerator WaitForHeadset()
        {
            // <b>재생을 누른 뒤에 링크를 켜도 붙는다.</b>
            //
            // 유니티는 게임이 뜨는 그 한 번만 XR 을 올려 본다. 그때 링크가 안 켜져 있으면
            // "Unable to start Oculus XR Plugin" 한 줄을 남기고 그 판은 끝까지 <b>평면</b>이다 —
            // 헤드셋을 뒤늦게 켜도 소용이 없어서, 링크 → 재생 순서를 지키지 못하면
            // 매번 재생을 껐다 켜야 했다. 그래서 여기서 <b>몇 번 더 두드려 본다</b>.
            // <b>두드림은 공짜가 아니다.</b> 링크가 꺼져 있으면 오큘러스 런타임은
            // 실패를 돌려주기까지 <b>5초 넘게 주 실을 붙잡는다</b>. 3초마다 45초를
            // 두드리던 때에는 8초마다 5초씩 화면이 통째로 얼어붙어서, 인트로에서
            // 봉서가 굴러오다 멈춰 섰다 — 재 보니 멈춘 횟수와 "Unable to start
            // Oculus XR Plugin" 줄 수가 정확히 같았다.
            //
            // 그러니 <b>몇 번만</b> 두드리고, 그 사이도 벌린다. 늦게 켜도 붙는 창은
            // 그만큼 좁아지지만, 안 켤 사람에게 45초를 얼려 두는 것보다는 낫다.
            // 그 창을 놓쳤으면 재생을 껐다 켜면 된다 — 그렇게 적어 준다.
            float waited = 0f, knock = FirstKnock;
            int knocks = 0;
            while (waited < WaitSeconds && knocks < MaxKnocks)
            {
                if (XRSettings.isDeviceActive && InputDevices.GetDeviceAtXRNode(XRNode.Head).isValid) break;

                knock -= Time.unscaledDeltaTime;
                if (knock <= 0f)
                {
                    knocks++;
                    knock = FirstKnock + knocks * 4f;     // 두 번째는 더 오래 기다린다
                    yield return TryStartXR();
                }
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            if (!XRSettings.isDeviceActive && knocks >= MaxKnocks)
                Debug.Log("[VR] 링크를 못 잡았다 — 이 판은 평면으로 간다. 헤드셋으로 하려면 "
                          + "링크를 켠 뒤 재생을 껐다 다시 누르십시오.");
            if (!XRSettings.isDeviceActive) yield break;   // 책상 앞 그대로

            // 바닥을 원점으로 삼는다 — 그래야 헤드셋이 주는 높이가 곧 <b>키</b>가 된다.
            var subsystems = new System.Collections.Generic.List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            foreach (var s in subsystems) s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);

            Build();
        }

        /// <summary>기다리는 시간(초). 이 안에 링크가 켜지면 붙는다.</summary>
        private const float WaitSeconds = 45f;

        /// <summary>첫 두드림까지(초).</summary>
        private const float FirstKnock = 2f;

        /// <summary>
        /// 몇 번까지 두드리나. <b>한 번이 5초짜리 멈춤</b>이라 인색해야 한다.
        /// </summary>
        private const int MaxKnocks = 2;

        /// <summary>
        /// XR 을 한 번 올려 본다. 이미 올라와 있으면 아무 일도 안 한다.
        /// 링크가 아직 안 켜져 있으면 조용히 실패하고, 다음 두드림 때 다시 해 본다.
        /// </summary>
        private static IEnumerator TryStartXR()
        {
            var settings = UnityEngine.XR.Management.XRGeneralSettings.Instance;
            var mgr = settings != null ? settings.Manager : null;
            if (mgr == null || mgr.activeLoader != null) yield break;

            yield return mgr.InitializeLoader();
            if (mgr.activeLoader == null) yield break;

            mgr.StartSubsystems();
            Debug.Log("[VR] 링크를 잡았다 — 헤드셋으로 넘어간다.");
        }

        /// <summary>
        /// 몸을 짓는다. <see cref="FixedView"/> 가 있는 씬에서는 <b>머리를 못 박고
        /// 손만 준다</b> — 어전 같은 연출 화면은 카메라가 어디를 보는지까지 짜 둔
        /// 그림이라 목에 넘길 수 없고, 그렇다고 손을 뺏으면 헤드셋에는 자판도
        /// 마우스도 없어 단추 하나 못 누른다.
        /// </summary>
        private void Build()
        {
            var cam = Camera.main;
            if (cam == null) { Debug.LogWarning("[VR] 카메라를 못 찾아 몸을 못 짓는다."); return; }
            if (Body != null) return;

            bool fixedHead = FixedView.Here;
            Transform camT = cam.transform;

            // ① 몸 — 카메라가 서 있던 자리의 <b>발밑</b>에 세운다
            float floorY = camT.position.y - 1.7f;
            if (Physics.Raycast(camT.position + Vector3.up * 0.2f, Vector3.down,
                                out RaycastHit floor, 6f, ~0, QueryTriggerInteraction.Ignore))
                floorY = floor.point.y;

            var body = new GameObject("VR_몸").transform;
            body.position = new Vector3(camT.position.x, floorY, camT.position.z);
            body.rotation = Quaternion.Euler(0f, camT.eulerAngles.y, 0f);
            Body = body;

            // ② 책상 앞 사람의 손발을 잠깐 거둔다
            Disable(camT, "DebugFlyCamera");
            Disable(camT, "MouseRaySelector");
            Disable(camT, "MouseInspector");

            // ③ 카메라를 몸 밑으로. 자세는 헤드셋이 넣는다.
            //    <b>고정 화면에서는 손대지 않는다</b> — 옮기지도, XRPose 를 붙이지도
            //    않는다. 씬에 짜 둔 자리와 각 그대로 두 눈에만 갈려 보인다.
            if (!fixedHead)
            {
                camT.SetParent(body, true);
                camT.localPosition = new Vector3(0f, 1.6f, 0f);
                camT.localRotation = Quaternion.identity;
                var head = camT.gameObject.AddComponent<XRPose>();
                SetNode(head, XRNode.Head);
            }

            // ④ 손 둘
            LeftHand = MakeHand(body, "왼손", XRNode.LeftHand, new Vector3(-0.2f, 1.0f, 0.25f));
            RightHand = MakeHand(body, "오른손", XRNode.RightHand, new Vector3(0.2f, 1.0f, 0.25f));

            // ⑤ 스틱으로 걷고 돌아선다. 고정 화면에서는 안 준다 —
            //    몸이 움직여도 눈은 그대로라 걷는 것이 아니라 어긋나는 것이 된다.
            if (!fixedHead)
            {
                var loco = body.gameObject.AddComponent<VRLocomotion>();
                SetField(loco, "_head", camT);
            }

            // ⑥ 단추를 잇는다. 여기까지 안 하면 말하기도 수첩도 도구도 못 부른다 —
            //    그것들이 죄 키보드에 매여 있는데, 헤드셋을 쓰면 자판을 누를 손이 없다.
            body.gameObject.AddComponent<VRButtons>();

            Active = true;
            Debug.Log(fixedHead
                ? "[VR] 손 둘만 지었다 — 이 씬은 고정 화면이라 머리는 못 박아 둔다(FixedView)."
                : "[VR] 몸을 지었다 — 머리 하나, 손 둘. 바닥 y=" + floorY.ToString("F2"));
        }

        private static Transform MakeHand(Transform body, string name, XRNode node, Vector3 rest)
        {
            var go = new GameObject(name);
            go.transform.SetParent(body, false);
            go.transform.localPosition = rest;

            var pose = go.AddComponent<XRPose>();
            SetNode(pose, node);
            SetField(pose, "_restPosition", rest);

            // 광선 한 줄
            var lineGo = new GameObject("광선");
            lineGo.transform.SetParent(go.transform, false);
            var line = lineGo.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.widthMultiplier = 0.004f;
            line.numCapVertices = 2;
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            if (sh != null) line.material = new Material(sh);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;

            // 화면의 단추도 이 광선으로 누른다. 붙이는 차례가 중요하다 — 짚는 쪽이
            // 깨어날 때 이것을 찾아 물기 때문이다.
            go.AddComponent<VRUiRay>().Bind(node);

            var sel = go.AddComponent<VRRaySelector>();
            sel.Bind(node, line);

            // 손으로 직접 두드릴 수 있게 한다. 광선은 광선대로 두고, 팔을 뻗어 치는
            // 쪽도 같이 연다 — 문 앞에서는 치는 편이 훨씬 자연스럽다.
            go.AddComponent<HandKnock>().Setup(node);
            return go.transform;
        }

        private static void SetNode(XRPose pose, XRNode node) => SetField(pose, "_node", node);

        private static void SetField(object target, string field, object value)
        {
            var f = target.GetType().GetField(field,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f != null) f.SetValue(target, value);
        }

        private static void Disable(Transform on, string typeName)
        {
            foreach (var c in on.GetComponents<MonoBehaviour>())
                if (c != null && c.GetType().Name == typeName) c.enabled = false;
        }
    }
}
