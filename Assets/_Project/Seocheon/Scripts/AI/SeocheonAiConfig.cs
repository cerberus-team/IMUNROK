// 서천 전용 AI 호출 설정입니다. 모델명·토큰 상한을 코드가 아니라 에셋에서 바꿉니다.
using UnityEngine;

namespace IMUNROK.Seocheon.AI
{
    /// <summary>
    /// Gemini 호출 파라미터. ★상수 하드코딩을 피하려고 에셋으로 뺐습니다.
    ///
    /// 실측(2026-08-26) 기준값:
    ///   - gemini-2.0-flash 는 ★폐기되어 404 를 돌려줍니다.
    ///   - maxOutputTokens 300 이면 이 세대 모델의 ★내부 추론 토큰에 다 먹혀
    ///     본문이 빈 문자열로 옵니다(finishReason=MAX_TOKENS). 2048 에서 정상 응답.
    /// </summary>
    [CreateAssetMenu(fileName = "SeocheonAiConfig", menuName = "이문록/서천/AI 설정")]
    public sealed class SeocheonAiConfig : ScriptableObject
    {
        [Header("모델")]
        [Tooltip("모델 이름. 404 가 나면 응답 본문이 대체 모델을 알려 줍니다")]
        public string model = "gemini-3.6-flash";

        [Tooltip("응답 토큰 상한. ★추론 토큰과 ★같은 예산을 나눠 씁니다. " +
                 "2048 에서는 추론이 1962 를 먹고 본문에 82 만 남아 JSON 이 중간에서 잘렸습니다")]
        public int maxOutputTokens = 4096;

        [Tooltip("추론 강도. \"low\" / \"high\" / 비우면 모델 기본값. " +
                 "실측: low 로 8.1초·추론 1156토큰, 기본값이면 13~23초·추론 1962토큰")]
        public string thinkingLevel = "low";

        [Range(0f, 2f)]
        [Tooltip("높을수록 대사가 다양해집니다")]
        public float temperature = 0.9f;

        [Header("대화 이력")]
        [Tooltip("최근 몇 턴만 보낼지(1턴 = 플레이어 1 + NPC 1). ★길수록 느려지고 잘릴 위험이 커집니다")]
        [Range(1, 20)]
        public int historyTurns = 3;

        [Tooltip("한 응답에 허용할 문장 수. 지시문에 그대로 들어갑니다")]
        [Range(1, 6)]
        public int maxSentencesPerReply = 2;

        [Header("엔드포인트")]
        [Tooltip("모델 이름 앞까지의 주소")]
        public string endpointBase = "https://generativelanguage.googleapis.com/v1beta/models/";

        [Tooltip("네트워크 제한 시간(초)")]
        public int timeoutSeconds = 45;

        [Header("API 키")]
        [Tooltip("프로젝트 루트(Assets 상위)에 두는 키 파일 이름. ★.gitignore 에 등록돼 커밋되지 않습니다")]
        public string apiKeyFileName = "gemini_api_key.txt";

        [Header("진단")]
        [Tooltip("요청·응답 요약을 콘솔에 남깁니다. ★키는 어떤 경우에도 출력하지 않습니다")]
        public bool verboseLog = true;
    }
}
