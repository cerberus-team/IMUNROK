using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 에디터에서 Play 없이도 안개 파티클이 보이고 흐르게 한다.
    ///
    /// 기본적으로 ParticleSystem은 Play 모드에서만 시뮬레이션되고, 에디터에서는
    /// 하이어라키에서 선택했을 때만 씬 뷰 오버레이로 미리보기된다. 배경 연출은
    /// 선택 여부와 무관하게 항상 보여야 배치·강도를 판단할 수 있으므로
    /// [ExecuteAlways] 로 매 에디터 틱마다 Simulate 를 강제 호출한다.
    ///
    /// Play 중에는 아무것도 하지 않는다 — 정상 시뮬레이션에 맡긴다.
    /// 시뮬레이션 상태는 직렬화되지 않으므로 씬을 더럽히지 않는다.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("이문록/안개 파티클 에디터 재생 (FogParticleAutoPlay)")]
    [RequireComponent(typeof(ParticleSystem))]
    public class FogParticleAutoPlay : MonoBehaviour
    {
        [Tooltip("에디터에서 미리보기할 최대 프레임 간격 — 창이 오래 멎어 있다가 돌아와도 튀지 않게 한다")]
        public float maxStep = 0.05f;

        ParticleSystem _ps;
        float _last;

        void OnEnable()
        {
            _ps = GetComponent<ParticleSystem>();
            _last = Time.realtimeSinceStartup;
            if (_ps == null) return;

            if (Application.isPlaying)
            {
                // ★★ Simulate() 는 시스템을 **정지 상태로 남긴다**(isPlaying=false).
                //   편집 모드 프리뷰로 그 상태가 된 채 Play 에 들어가면 입자가 하나도 방출되지 않는다.
                //   실제로 하산 안개가 Play 에서 통째로 안 보였고(입자 0개), 알파·밀도를 아무리
                //   올려도 소용이 없었다 — 원인이 이것이었다. Play 시작 때 명시적으로 다시 튼다.
                //   prewarm=true 라 Play(true) 만으로 첫 프레임부터 자욱하다.
                _ps.Play(true);
                return;
            }

            // 시작하자마자 자욱하게 — 빈 화면에서 서서히 차오르면 배치 판단이 안 된다
            _ps.Simulate(_ps.main.startLifetime.constantMax, true, true, false);
        }

        void Update()
        {
            if (Application.isPlaying || _ps == null) return;
            float now = Time.realtimeSinceStartup;
            float dt = Mathf.Clamp(now - _last, 0f, maxStep);
            _last = now;
            if (dt <= 0f) return;
            _ps.Simulate(dt, true, false, false);   // restart=false → 이어서 진행
        }
    }
}
