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

        public void GetResponse(MonoBehaviour host, NpcRequest req, Action<string> onReply, Action<string> onError)
        {
            string input = req.playerInput ?? "";
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
