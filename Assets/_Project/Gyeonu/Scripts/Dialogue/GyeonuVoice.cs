using System;
using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 견우의 받아쓰기 — 녹음한 소리를 <b>Gemini</b>에게 보내 글로 받는다 (2026-08-26).
    ///
    /// ■ 왜 껍데기를 씌웠나
    ///   대화창이 <c>GyeonuGeminiResponder</c> 를 직접 부르고 있었다. 그러면 화면만 가져다 쓰려는
    ///   사람이 우리 LLM 연동까지 끌고 가야 한다. 화면은 이제 <see cref="IVoiceTranscriber"/> 만 알고,
    ///   그 자리에 우리를 꽂는 것이 이 파일이다.
    ///
    /// ■ 안 꽂으면
    ///   대화창이 <b>음성 단추와 녹음 막대를 통째로 감춘다</b>. 글쓰기만으로 멀쩡히 돌아간다 —
    ///   다른 사건 담당자는 아무것도 안 해도 되고, 쓰고 싶으면 이 파일을 본떠 자기 것을 만들면 된다.
    ///
    /// ⚠️ 도메인 리로드가 꺼진 프로젝트라 <see cref="UiDialogue"/> 가 세션 시작에 자리를 비운다.
    ///    여기서 세션마다 다시 꽂지 않으면 두 번째 Play 부터 음성 단추가 사라진다.
    /// </summary>
    public class GyeonuVoice : IVoiceTranscriber
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install() { UiDialogue.Voice = new GyeonuVoice(); }

        public void Transcribe(MonoBehaviour host, byte[] wav, Action<string> onText, Action<string> onError)
        {
            GyeonuGeminiResponder.Transcribe(host, wav, onText, onError);
        }
    }
}
