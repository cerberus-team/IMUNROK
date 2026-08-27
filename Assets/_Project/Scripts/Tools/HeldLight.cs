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
    /// <b>닿는 데를 좁게 잡는 것이 요령이다.</b> 0.8m 면 손에 든 것에는 닿고 바닥
    /// (1.6m 아래)에도 벽에도 안 닿는다. 그림자를 굽지 않으므로 값도 거의 안 든다.
    ///
    /// 씬에 놓을 것이 없다. 게임이 시작될 때 저 혼자 카메라 밑으로 들어간다.
    /// </summary>
    public class HeldLight : MonoBehaviour
    {
        /// <summary>닿는 거리(m). 손에 든 것까지만 간다.</summary>
        private const float Reach = 0.80f;

        /// <summary>눈가에서 살짝 오른쪽 위 — 도구가 오른손에 들리므로 그쪽이 밝아야 한다.</summary>
        private static readonly Vector3 At = new Vector3(0.10f, 0.12f, 0.10f);

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
            // 카메라는 늦게 바뀔 수 있다(VR 몸을 지으면서 옮겨 간다). 매 칸 확인한다.
            var cam = Camera.main;
            if (cam == null) return;
            if (_eye != cam.transform) Attach(cam.transform);

            // 맨손일 때는 끈다. 아무것도 안 들었는데 눈앞이 밝으면
            // <b>어디서 오는지 알 수 없는 빛</b>이 되어 방의 어둠이 거짓말이 된다.
            bool holding = !string.IsNullOrEmpty(ToolbeltHud.SelectedToolId);
            if (_lamp != null && _lamp.enabled != holding) _lamp.enabled = holding;
        }

        private void Attach(Transform eye)
        {
            _eye = eye;
            if (_lamp == null)
            {
                var go = new GameObject("손빛");
                _lamp = go.AddComponent<Light>();
                _lamp.type = LightType.Point;
                _lamp.color = new Color(1f, 0.96f, 0.90f);
                _lamp.shadows = LightShadows.None;
                _lamp.renderMode = LightRenderMode.ForcePixel;   // 손에 든 것이라 늘 또렷해야 한다
            }
            _lamp.range = Reach;
            _lamp.intensity = 2.6f;
            _lamp.transform.SetParent(eye, false);
            _lamp.transform.localPosition = At;
            _lamp.transform.localRotation = Quaternion.identity;
        }
    }
}
