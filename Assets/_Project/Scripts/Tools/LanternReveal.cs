using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>종이를 불 앞에 든다</b> — 겹 사이에 숨은 것이 비쳐 나온다.
    ///
    /// <b>단추가 없다.</b> 눌러서 하는 일이 아니라, <b>불과 종이와 눈이 한 줄에 서면</b>
    /// 되는 일이다. 조선 문서에서 숨길 데는 종이 <b>사이</b>다 — 배접(褙接)은 종이를
    /// 여러 겹 붙여 두껍게 만드는 일이고, 그 겹에 한 장을 끼워 넣으면 겉으로는
    /// 아무것도 없다. 등잔 뒤에 대면 그때만 그림자가 비친다.
    ///
    /// 그러므로 이것은 <b>등불의 재주가 아니라 불의 재주</b>다(<see cref="Firelight"/>).
    /// 방에 놓인 촛불이든 손에 든 등불이든 똑같이 비춘다 — 손에 든 등불은 그저
    /// <b>들고 다닐 수 있는 불</b>일 뿐이고, 종이 뒤로 넘겨 두면 제 몫을 한다.
    ///
    /// <b>대고 있는 만큼 읽힌다.</b> 잠깐 스치면 무언가 비친다는 것만 알고, 오래
    /// 대고 있어야 글자가 잡히고, 끝까지 대고 있어야 뜻까지 새겨진다. 급히 지나가며
    /// 다 알아내는 조사는 없다.
    ///
    /// 씬에 놓을 것이 없다. 게임이 시작될 때 저 혼자 하나 선다.
    /// </summary>
    public class LanternReveal : MonoBehaviour
    {
        /// <summary>불을 <b>똑바로</b> 마주 대고 있을 때 다 비치기까지 걸리는 시간(초).</summary>
        private const float Seconds = 2.5f;

        /// <summary>비스듬히 대면 그만큼 느리다. 이보다 어긋나면 아예 안 센다(도).</summary>
        private const float WideAngle = 30f;
        /// <summary>이 안으로 들어오면 똑바로 댄 것으로 친다(도).</summary>
        private const float TrueAngle = 7f;

        /// <summary>종이에서 떼면 이 빠르기로 식는다(초당). 댈 때보다 느리다.</summary>
        private const float Cool = 0.35f;

        /// <summary>손에 든 종이가 눈에서 떨어진 거리(m). 불은 이보다 뒤에 있어야 한다.</summary>
        private const float PageAt = 0.62f;

        private float _t;
        private LanternController _lamp;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindFirstObjectByType<LanternReveal>() != null) return;
            var go = new GameObject("_불빛에비추기");
            go.AddComponent<LanternReveal>();
            DontDestroyOnLoad(go);
        }

        private void Update()
        {
            if (!DocumentView.IsOpen) { _t = 0f; Lower(); return; }

            // 종이에 불로 볼 것이 없으면 아무 일도 안 한다. 그냥 옛 문서를 들고
            // 촛불 앞에 선 것뿐인데 종이가 달아오르면, 뭔가 있는 줄 알고 한참을 선다.
            if (!DocumentView.HasBacklight) { _t = 0f; Lower(); return; }

            var cam = Camera.main;
            if (cam == null) return;

            // 손에 든 등불은 <b>종이 뒤로 넘겨 둔다</b>. 그러면 제가 그 불이 된다 —
            // 따로 세는 것이 아니라, 뒤에 가 있으므로 아래 셈에 저절로 걸린다.
            bool carrying = ToolbeltHud.SelectedToolId == "lantern";
            if (_lamp == null) _lamp = FindFirstObjectByType<LanternController>();
            if (_lamp != null) _lamp.SetRaise(carrying ? 1f : 0f);

            float q = Quality(cam);

            if (q > 0.01f) _t += q * Time.deltaTime / Seconds;
            else { _t -= Cool * Time.deltaTime; if (_t <= 0f) { _t = 0f; return; } }

            _t = Mathf.Clamp01(_t);
            DocumentView.Lighting(_t);
        }

        private void Lower() { if (_lamp != null) _lamp.SetRaise(0f); }

        /// <summary>
        /// 지금 종이가 불을 <b>얼마나 잘 받고 있나</b>(0~1).
        ///
        /// 불이 종이 <b>뒤</b>에 있어야 하고(눈보다 멀어야 한다), 눈에서 본 방향이
        /// 종이와 겹쳐야 한다. 똑바로 마주 댈수록, 가까울수록 빨리 비친다.
        /// 불이 여럿이면 그중 가장 잘 받는 것 하나를 쓴다 — 촛불 둘을 겹쳐 든다고
        /// 두 배로 비치지는 않는다.
        /// </summary>
        private float Quality(Camera cam)
        {
            Vector3 eye = cam.transform.position;
            Vector3 look = cam.transform.forward;
            float best = 0f;

            foreach (var f in Firelight.All)
            {
                if (f == null || !f.Lit) continue;

                Vector3 to = f.Where - eye;
                float dist = to.magnitude;
                if (dist > f.Reach) continue;
                if (dist < PageAt + 0.04f) continue;      // 종이보다 앞이면 뒤가 아니다

                float ang = Vector3.Angle(to, look);
                float aim = Mathf.InverseLerp(WideAngle, TrueAngle, ang);
                if (aim <= 0f) continue;

                // 가까울수록 세다. 아주 멀어도 아주 못 쓰지는 않게 바닥을 둔다.
                float near = Mathf.Lerp(0.4f, 1f, Mathf.InverseLerp(f.Reach, f.Reach * 0.3f, dist));
                float q = aim * near * f.Strength;
                if (q > best) best = q;
            }
            return best;
        }
    }
}
