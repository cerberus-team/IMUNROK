using System;
using System.Collections.Generic;
using UnityEngine;

namespace IMUNROK.Ui
{
    /// <summary>
    /// 대화창이 <b>말 상대에게 묻는 것 전부</b> (2026-08-26).
    ///
    /// ■ 왜 인터페이스인가
    ///   대화창 1,000여 줄은 원래 견우의 <c>DialogueSession</c>(Gemini 연동 + 점수 매김)에
    ///   직접 붙어 있었다. 실측해 보니 <b>화면이 실제로 쓰는 것은 아래 일곱 가지뿐</b>이다.
    ///   점수(<c>GyeonuCase</c>)는 <b>화면이 아예 모른다</b> — 세션 안에서 매겨진다.
    ///
    /// ■ 아무것도 구현 안 하면
    ///   <see cref="UiDialogue.Dummy"/> 가 답한다. 화면은 뜨고, 물으면 정해진 대꾸가 돌아온다.
    /// </summary>
    public interface IDialogueBackend
    {
        /// <summary>이름패에 뜰 이름.</summary>
        string SpeakerName { get; }

        /// <summary>지금 화면에 보일 상대의 말.</summary>
        string CurrentLine { get; }

        /// <summary>대답을 기다리는 중인가 — 참이면 화면이 「…」를 띄우고 입력을 막는다.</summary>
        bool Busy { get; }

        /// <summary>내용이 달라졌다 (화면이 다시 그린다).</summary>
        event Action Changed;

        /// <summary>묻는다.</summary>
        void Ask(string text);

        /// <summary>증거를 내민다. 받아들일 수 없는 것이면 false — 화면이 안내를 띄운다.</summary>
        bool Present(IUiItem item);

        /// <summary>지금 내밀 수 있는 것들 — 소지품 판이 이 목록으로 열린다.</summary>
        IReadOnlyList<IUiItem> Presentables();
    }

    /// <summary>
    /// 대화창이 <b>말 거는 대상(NPC)에게 묻는 것</b>. 자리와 배치안만 알면 된다.
    /// </summary>
    public interface IDialogueSpeaker
    {
        /// <summary>판 배치안 (A 좌측판 / B 하단띠 / C 말풍선).</summary>
        DialogueLayout Layout { get; }

        /// <summary>말이 끝나면 저절로 보낼지 (음성 입력).</summary>
        bool VoiceAutoSend { get; }

        /// <summary>얼굴 언저리 — 말풍선과 판이 이 점을 기준으로 앉는다.</summary>
        Vector3 FocusPoint { get; }
    }

    /// <summary>
    /// <b>녹음한 소리를 글로 바꾸는 곳.</b> 견우는 Gemini에게 맡긴다.
    ///
    /// 안 꽂으면 대화창이 <b>음성 단추와 녹음 막대를 통째로 감춘다</b> — 글쓰기만 남는다.
    /// 그래서 이걸 구현하지 않아도 화면은 멀쩡히 돌아간다.
    /// </summary>
    public interface IVoiceTranscriber
    {
        /// <param name="host">코루틴을 돌릴 주인 (대화창 자신이 넘어온다).</param>
        /// <param name="wav">16비트 PCM 모노 WAV.</param>
        void Transcribe(MonoBehaviour host, byte[] wav, Action<string> onText, Action<string> onError);
    }

    /// <summary>
    /// <b>대화 상대와 받아쓰기를 꽂는 자리.</b>
    ///
    /// ⚠️ 도메인 리로드가 꺼진 프로젝트다 — 꽂는 쪽은
    ///    <see cref="RuntimeInitializeOnLoadMethod"/> 로 <b>세션마다 다시 꽂을 것.</b>
    /// </summary>
    public static class UiDialogue
    {
        static IVoiceTranscriber _voice;

        /// <summary>받아쓰기. <b>null 이면 음성 단추가 숨는다</b> (기본값이 null이다).</summary>
        public static IVoiceTranscriber Voice
        {
            get { return _voice; }
            set { _voice = value; }
        }

        /// <summary>받아쓸 수 있는가 — 대화창이 음성 단추를 낼지 이걸로 정한다.</summary>
        public static bool CanTranscribe { get { return _voice != null; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _voice = null; }

        /// <summary>
        /// 아무 백엔드도 없을 때 쓰는 <b>더미 상대</b>. 팀원이 아무것도 구현하지 않아도
        /// 대화창이 뜨고 조작되는지 보려고 만들었다 — 예제 씬이 이걸 쓴다.
        /// </summary>
        public class Dummy : IDialogueBackend
        {
            readonly string[] replies =
            {
                "…무슨 말씀이신지.",
                "그날 밤 일은 잘 기억나지 않습니다.",
                "그건 제가 답할 수 있는 것이 아닙니다.",
                "…어찌 그것을 아십니까.",
            };
            int turn;

            public string SpeakerName { get; set; }
            public string CurrentLine { get; private set; }
            public bool Busy { get; private set; }
            public event Action Changed;

            public Dummy(string name = "이름 없는 이")
            {
                SpeakerName = name;
                CurrentLine = "…누구십니까.";
            }

            public void Ask(string text)
            {
                CurrentLine = replies[turn++ % replies.Length];
                if (Changed != null) Changed();
            }

            public bool Present(IUiItem item)
            {
                if (item == null) return false;
                CurrentLine = "「" + item.DisplayName + "」… 이것을 어디서 얻으셨습니까.";
                if (Changed != null) Changed();
                return true;
            }

            public IReadOnlyList<IUiItem> Presentables() { return UiItems.Source.Items; }
        }
    }
}
