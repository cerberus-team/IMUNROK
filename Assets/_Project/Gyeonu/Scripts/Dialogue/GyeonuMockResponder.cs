using System;
using System.Collections.Generic;
using UnityEngine;
using IMUNROK.Common;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// API 키가 없을 때 쓰는 대타 대답기 (2026-08-25).
    ///
    /// ■ 왜 공통 <see cref="MockNpcResponder"/> 를 쓰지 않는가
    ///   공통 목업은 등급 표식([등급:…])을 붙이지 않는다. 그러면 응답이 늘 중립으로 읽혀
    ///   <b>신뢰도 경로가 통째로 안 돌아간다</b> — 키가 없는 사람은 대화 시스템의 절반을
    ///   시험할 수 없게 된다. 그래서 낱말로나마 등급을 흉내 내는 대타를 사건 쪽에 둔다.
    ///   공통 목업은 그대로 두었다(읽기만).
    ///
    /// ⚠️ 이것은 <b>배선 확인용</b>이지 대사 품질을 위한 것이 아니다. 키를 넣으면
    ///    <see cref="GeminiNpcResponder"/> 가 자동으로 대신 들어간다
    ///    (<see cref="DialogueSession.HasApiKey"/>).
    /// </summary>
    public class GyeonuMockResponder : INpcResponder
    {
        readonly NpcProfile _profile;
        int _turn;

        public GyeonuMockResponder(NpcProfile profile) { _profile = profile; }

        // 낱말만 보고 가르는 아주 거친 판정. 진짜 판정은 AI가 문맥으로 한다.
        static readonly string[] InsultWords = { "범인", "죽였", "해쳤", "거짓말", "너 때문", "네가 그랬", "살인", "끌고 갔" };
        static readonly string[] PressureWords = { "왜 말", "어디 있었", "숨기", "그날 밤", "말해", "대답해", "안 하", "못 하" };
        static readonly string[] FavorWords = { "믿", "걱정", "돕", "도와", "괜찮", "미안", "고맙", "찾아 드리", "찾아드리" };
        static readonly string[] SongWords = { "노래", "불러", "부르", "들려" };

        static bool IsChild(NpcProfile p) =>
            p != null && (p.npcId == NpcId.Child01 || p.npcId == NpcId.Child02 || p.npcId == NpcId.Child03);

        public void GetResponse(MonoBehaviour host, NpcRequest req, Action<string> onReply, Action<string> onError)
        {
            string input = req.playerInput ?? "";

            // 아이들과 노래 — 키 없이도 노래 배선을 시험할 수 있게 (2026-09-09, 09-10 좁힘).
            // 불러 달라고 청했을 때만 [노래]. 노래에 대해 묻기만 하면 말로만 답한다 (진짜 판정은 DialogueGrantValidator).
            if (!req.isEvidence && IsChild(_profile) && Contains(input, SongWords))
            {
                if (!DialogueGrantValidator.IsSongRequest(input))
                    onReply?.Invoke("[등급:중립] 은하수 건너 오작교 노래예요. 어른들한테 들어서 다 알아요.");
                else
                    onReply?.Invoke(ChildrenSong.Singing
                        ? "[등급:호의] 지금 부르고 있잖아요, 들어 보세요."
                        : "[등급:호의] 좋아요, 들어 보세요.\n[노래]");
                return;
            }
            string tone = Grade(input);

            string body;
            if (req.isEvidence)
            {
                body = !string.IsNullOrEmpty(req.justRevealedInfo)
                    ? "…그것을, 어디서."
                    : "…그건 저와 상관없는 일입니다.";
                // 제시에는 등급이 붙지 않는다 — 표식 없이 대사만 돌려준다
                onReply?.Invoke(body);
                return;
            }

            switch (tone)
            {
                case "모욕": body = "…그런 말씀은, 마십시오."; break;
                case "압박": body = "…드릴 말씀이 없습니다."; break;
                case "호의": body = "…고맙습니다. 저는, 괜찮습니다."; break;
                default:
                    var deflect = Deflections;
                    body = deflect[_turn++ % deflect.Length];
                    break;
            }
            onReply?.Invoke("[등급:" + tone + "] " + body);
        }

        string[] Deflections => new[]
        {
            "…예.",
            "…모르는 일입니다.",
            "…그건, 잘 모릅니다.",
            "…물어보실 것이 더 있으십니까.",
        };

        static string Grade(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "중립";
            if (Contains(s, InsultWords)) return "모욕";
            if (Contains(s, PressureWords)) return "압박";
            if (Contains(s, FavorWords)) return "호의";
            return "중립";
        }

        static bool Contains(string s, IEnumerable<string> words)
        {
            foreach (var w in words) if (s.Contains(w)) return true;
            return false;
        }
    }
}
