using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>손에 든 것을 비춘다.</b> 눈가에 걸어 두는 아주 작은 불 하나.
    ///
    /// <b>왜 필요한가</b>: 어두운 서고에서 돋보기를 들면 <b>아무것도 안 보였다</b>.
    /// 안 그려진 것이 아니다 — 소품의 놋쇠 테는 빛을 받아야 놋빛이 나는데 그 방에는
    /// 빛이 없다. 그래서 테가 시커멓고, 유리에 비치는 것은 뒤에 있는 어두운 책장이니,
    /// 결국 <b>가장자리가 없는 어두운 그림</b> 하나가 손에 들려 있는 꼴이 된다.
    ///
    /// <b>한 번 잘못 고쳤다.</b> 알 둘레에 빛과 상관없는 놋빛 고리를 그려 둘렀더니
    /// 대번에 보이기는 했다. 그런데 그것은 <b>소품 대신 그린 가짜</b>다 — 만든 사람이
    /// 빚어 넣은 테는 그대로 캄캄한 채 그 위에 다른 고리가 하나 더 떠 있는 것이고,
    /// 물건을 고친 것이 아니라 물건 앞에 그림을 붙인 것이다. 걷어냈다.
    ///
    /// <b>고칠 데는 물건이 아니라 빛이다.</b> 손에 든 것은 늘 눈에서 한 뼘 앞에 있으므로,
    /// 눈가에 <b>닿는 데가 한 뼘뿐인</b> 불을 하나 두면 그 물건만 밝아진다. 방은
    /// 어두운 채로 두고 — 어두워야 등불이 값을 한다 — 손에 쥔 것만 제 빛깔을 되찾는다.
    /// 실제로 사람이 물건을 살필 때 하는 짓이 이것이다. 어두운 데서는 <b>눈앞으로
    /// 가져온다</b>.
    ///
    /// <b>닿는 데를 좁게 잡는 것이 요령이다.</b> 다만 0.8m 는 좁은 것이 아니었다 —
    /// 눈가에 걸어 두었더니 앞의 벽까지 밝혀 <b>머리에서 빛이 나는</b> 꼴이 됐다.
    /// 불은 <b>손</b>에 두고 0.42m 에서 끊는다. 그림자를 굽지 않으므로 값도 거의 안 든다.
    ///
    /// 씬에 놓을 것이 없다. 게임이 시작될 때 저 혼자 카메라 밑으로 들어간다.
    /// </summary>
    public class HeldLight : MonoBehaviour
    {
        /// <summary>
        /// 닿는 거리(m). <b>손에 든 것에서 끝나야 한다.</b>
        ///
        /// 0.80m 로 두었더니 <b>머리에서 빛이 났다</b> — 불이 눈가(0.10, 0.12, 0.10)에
        /// 걸려 있어서 앞의 벽이며 문짝이며가 통째로 하얘졌고, 화면으로 보면 사람
        /// 정수리에 등을 얹은 꼴이었다. 어둠을 지키려고 둔 불이 어둠을 지웠다.
        ///
        /// 고칠 데는 세기가 아니라 <b>자리와 거리</b>다. 불을 눈이 아니라 <b>손</b>에
        /// 두고(HeldRig 의 도구 자리가 대략 (0.2, −0.17, 0.3)), 닿는 데를 물건 한 뼘
        /// 너머에서 끊는다. 벽은 아무리 가까워도 0.5m 밖이라 이제 안 닿는다.
        /// </summary>
        private const float Reach = 0.30f;

        /// <summary>불이 걸리는 자리 — <b>눈가가 아니라 손</b>이다.</summary>
        private static readonly Vector3 At = new Vector3(0.17f, -0.16f, 0.30f);

        /// <summary>
        /// <b>동그란 불을 좁은 갓으로 바꾼다.</b>
        ///
        /// 0.42m 짜리 점불을 손에 걸어 두어도 <b>여전히 「머리에서 빛이 난다」</b>는 말을
        /// 들었다. 당연하다 — 점불은 <b>사방</b>으로 간다. 눈에서 0.3m 앞에 두고 0.42m를
        /// 뻗으면 얼굴 둘레 0.7m 안의 모든 것이 밝아지고, 어두운 방에서 벽이나 문갑에
        /// 다가서는 순간 그 자리에 <b>둥근 빛무리</b>가 앉는다. 밤을 지키려고 둔 불이
        /// 밤을 지우는 것은 고치지 못한 채였다.
        ///
        /// 그래서 <b>갓을 씌워 손만 겨눈다</b>(Spot). 눈과 도구 사이에 두고 도구를 향해
        /// 겨누면, 빛이 가는 길에 도구가 서 있으므로 그 너머로는 거의 안 넘어간다.
        /// 닿는 데도 도구 <b>한 뼘 뒤</b>에서 끊는다.
        /// </summary>
        private const float ConeAngle = 46f;

        /// <summary>
        /// <b>등불을 든 손에는 안 켠다.</b> 등불은 제 빛을 가진 물건이라, 여기서 하나를
        /// 더 얹으면 손잡이만 두 배로 밝아진다.
        /// </summary>
        private const string OwnLight = "lantern";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var go = new GameObject("_손빛");
            go.AddComponent<HeldLight>();
            DontDestroyOnLoad(go);
        }

        private Light _lamp;
        private Transform _eye;

        private void Update()
        {
            // 카메라는 늦게 바뀔 수 있다(씬이 갈리면서 다른 것이 된다). 매 칸 확인한다.
            var cam = Camera.main;
            if (cam == null) return;
            if (_eye != cam.transform) Attach(cam.transform);

            // 맨손일 때는 끈다. 아무것도 안 들었는데 눈앞이 밝으면
            // <b>어디서 오는지 알 수 없는 빛</b>이 되어 방의 어둠이 거짓말이 된다.
            string tool = ToolbeltHud.SelectedToolId;
            bool holding = !string.IsNullOrEmpty(tool) && tool != OwnLight;

            // <b>종이를 펴 든 동안에는 끈다.</b> 그때 눈이 보는 것은 판(Canvas)이고
            // 판에는 빛이 안 닿는다 — 이 불이 하는 일은 <b>종이 뒤의 방</b>을 밝히는 것뿐,
            // 곧 읽는 데 방해만 된다. 게다가 종이를 볼 때 사람은 가장 가까이 다가선다.
            if (holding && DocumentView.IsOpen) holding = false;

            if (_lamp == null) return;
            if (_lamp.enabled != holding) _lamp.enabled = holding;
            if (!holding) return;

            // <b>자리는 도구가 정한다.</b> 못 박아 둔 (0.17, −0.16, 0.30) 은 돋보기의
            // 자리였다 — 유척·마패는 다른 데 들리므로 불이 엉뚱한 곳을 비췄다.
            HeldRig.Pose pose;
            Vector3 aim = HeldRig.TryGet(tool, out pose) ? pose.position : At;
            // 눈과 도구 사이 — 도구를 향해 겨눈다.
            _lamp.transform.localPosition = aim * 0.45f;
            _lamp.transform.localRotation = Quaternion.LookRotation((aim - aim * 0.45f).normalized, Vector3.up);
            _lamp.range = Mathf.Max(0.12f, aim.magnitude * 0.55f + Reach * 0.5f);
        }

        private void Attach(Transform eye)
        {
            _eye = eye;
            if (_lamp == null)
            {
                var go = new GameObject("손빛");
                _lamp = go.AddComponent<Light>();
                _lamp.type = LightType.Spot;
                _lamp.spotAngle = ConeAngle;
                _lamp.innerSpotAngle = ConeAngle * 0.45f;
                _lamp.color = new Color(1f, 0.96f, 0.90f);
                _lamp.shadows = LightShadows.None;
                _lamp.renderMode = LightRenderMode.ForcePixel;   // 손에 든 것이라 늘 또렷해야 한다
            }
            _lamp.range = Reach;
            // 갓을 좁히면 같은 세기가 더 밝게 모인다 — 점불 때의 1.5 는 눈이 부시다.
            _lamp.intensity = 0.9f;
            _lamp.transform.SetParent(eye, false);
            _lamp.transform.localPosition = At * 0.45f;
            _lamp.transform.localRotation = Quaternion.identity;
        }
    }
}
