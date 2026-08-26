using System.Collections;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>봉서 세 통을 굴린다 — 대사 없이.</b>
    ///
    /// <b>굴리는 일은 새로 짓지 않는다.</b> 처음엔 굴리는 셈(거리 = 반지름 × 각)을
    /// 여기에 다시 썼는데, 재 보니 <see cref="IntroDocument.PlaySlideIn"/> 이 이미
    /// 그 일을 하고 있었다 — 같은 셈으로, 같은 잦아듦으로, "왕 쪽에서 떼구르르
    /// 굴러와 제자리에 멎는다"고 적어 두고서. 두 벌을 두면 어느 날 한쪽만 고치게 된다.
    ///
    /// 그러니 여기서 하는 일은 <b>부르는 것</b>뿐이다. 왜 그럴 것이 따로 필요한가 하면,
    /// 그 굴림이 <see cref="IntroController"/> 안에 묶여 있어 <b>왕의 다섯 마디가
    /// 다 끝나야</b> 오기 때문이다. 찍고 싶은 것이 그림뿐일 때는 스무 초를 기다렸다
    /// 스무 초를 잘라내야 한다.
    ///
    /// 붙이는 곳: 문서 셋을 담은 부모(Documents).
    /// 채비는 [이문록 ▸ 영상 ▸ 인트로: 봉서 굴러오는 장면 채비] 가 대신 해 준다.
    /// </summary>
    public class ScrollRollIn : MonoBehaviour
    {
        [Tooltip("왕 쪽(뒤)에서 이만큼 떨어진 데서 굴러온다(m). " +
                 "1.2 면 화면 위 언저리, 1.8 이면 화면 밖에서 들어온다")]
        [SerializeField] private float _fromDistance = 1.6f;

        [Tooltip("한 통이 굴러오는 데 걸리는 시간(초). 어명에서는 0.6 인데, " +
                 "그건 대사 끝의 곁가지라 빠른 것이고 이쪽은 <b>그림이 알맹이</b>라 느리다")]
        [SerializeField] private float _seconds = 1.4f;

        [Tooltip("다음 통이 뜨기까지(초). 셋이 한꺼번에 오면 굴러오는 것이 아니라 쏟아진다")]
        [SerializeField] private float _stagger = 0.5f;

        [Tooltip("굴리기 전에 이만큼(초) 기다린다 — 녹화를 켤 틈")]
        [SerializeField] private float _delay = 0.8f;

        [Tooltip("켜면 씬이 열리자마자 굴린다. 영상만 찍을 때 쓴다")]
        [SerializeField] private bool _playOnStart;

        private bool _rolling;

        private void Start()
        {
            if (_playOnStart) Play();
        }

        /// <summary>굴린다. 이미 구르는 중이면 아무 일도 안 한다.</summary>
        [ContextMenu("굴려 보기")]
        public void Play()
        {
            if (_rolling) return;
            _rolling = true;
            StartCoroutine(Roll());
        }

        /// <summary>다시 굴릴 수 있게 되돌린다(같은 재생 안에서 여러 번 찍을 때).</summary>
        [ContextMenu("다시 굴릴 채비")]
        public void Rearm() { _rolling = false; }

        private IEnumerator Roll()
        {
            if (_delay > 0f) yield return new WaitForSeconds(_delay);

            var docs = GetComponentsInChildren<IntroDocument>(true);
            for (int i = 0; i < docs.Length; i++)
                docs[i].PlaySlideIn(i * _stagger, _seconds, _fromDistance);
        }
    }
}
