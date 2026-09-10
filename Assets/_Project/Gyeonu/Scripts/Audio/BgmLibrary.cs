using System;
using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>제3사건 음악 15곡의 식별자. 번호는 음원 파일 이름과 같다.</summary>
    public enum BgmId
    {
        None = 0,
        S01_마을_낮 = 1,
        S02_마을_밤 = 2,
        S03_은하담_낮 = 3,
        S04_은하담_밤맑음 = 4,
        S05_은하담_밤비 = 5,
        S06_관아_낮 = 6,
        S07_관아_밤 = 7,
        S08_집무실 = 8,
        S09_관측실 = 9,
        S10_서고 = 10,
        S11_견우마을 = 11,
        S12_선아의방 = 12,
        E01_밤하늘재현 = 13,
        E02_최초의두사람 = 14,
        V01_아이들노래 = 15,
    }

    /// <summary>
    /// 제3사건 음악 목록 (2026-09-09). 곡마다 <b>루프 지점</b>과 크기를 들고 있다.
    ///
    /// ■ 왜 루프 지점이 필요한가
    ///   수노(Suno)가 만든 곡은 앞뒤에 페이드가 박혀 있다. 그대로 <c>AudioSource.loop</c> 를 켜면
    ///   끝에서 잦아들었다가 처음에서 다시 차오르는 것이 한 바퀴마다 들린다. 그래서 페이드가 끝난
    ///   자리(<see cref="Track.loopStart"/>)와 페이드가 시작되기 전 자리(<see cref="Track.loopEnd"/>)를
    ///   적어 두고, <see cref="LoopDeck"/> 이 그 사이를 크로스페이드로 잇는다.
    ///
    /// ■ 원본은 코드에 (<c>Editor/BgmLibraryBuilder</c>)
    ///   프로필과 같은 이유다 — 에셋 diff 는 사람이 읽을 수 없다. 루프 지점을 고치면 빌더를 고치고
    ///   Tools ▸ 이문록 ▸ 음악 ▸ 「BGM 임포트 설정 + 라이브러리 굽기」로 다시 굽는다.
    ///   에셋은 <c>Resources/BgmLibrary</c> 에 두어 <see cref="MusicDirector"/> 가 씬에 아무것도
    ///   두지 않고도 찾을 수 있게 한다.
    /// </summary>
    [CreateAssetMenu(menuName = "이문록/견우/BGM 라이브러리", fileName = "BgmLibrary")]
    public class BgmLibrary : ScriptableObject
    {
        public const string ResourcePath = "BgmLibrary";

        [Serializable]
        public class Track
        {
            public BgmId id;
            public AudioClip clip;

            [Range(0f, 1f)]
            [Tooltip("이 곡만의 배율. 전체 크기(masterVolume)에 곱한다")]
            public float volume = 1f;

            [Tooltip("끝나면 loopStart 로 이어 붙인다. 한 번만 트는 곡(E02·V01)은 끈다")]
            public bool loop = true;

            [Tooltip("페이드인이 끝난 자리(초). 두 바퀴째부터 여기서 시작한다")]
            public float loopStart = 0f;

            [Tooltip("페이드아웃이 시작되기 전 자리(초). 음수면 클립 끝")]
            public float loopEnd = -1f;

            [Tooltip("이음매를 겹치는 길이(초). 등파워 크로스페이드")]
            public float loopCrossfade = 4f;

            public float LoopEndOrLength => loopEnd > 0f && clip != null ? Mathf.Min(loopEnd, clip.length) : (clip != null ? clip.length : 0f);
        }

        [Header("전체")]
        [Range(0f, 1f)]
        [Tooltip("배경 음악 전체 크기. 말소리·효과음을 덮지 않게 0.4 안팎")]
        public float masterVolume = 0.42f;

        [Tooltip("씬이 바뀔 때 곡을 섞는 길이(초)")]
        public float sceneCrossfade = 2.5f;

        [Tooltip("같은 씬에서 시간대·날씨·층이 바뀔 때 곡을 섞는 길이(초)")]
        public float timeCrossfade = 3f;

        [Header("곡")]
        public Track[] tracks = Array.Empty<Track>();

        public Track Find(BgmId id)
        {
            if (id == BgmId.None) return null;
            foreach (var t in tracks) if (t != null && t.id == id) return t;
            return null;
        }

        public static BgmLibrary Load() => Resources.Load<BgmLibrary>(ResourcePath);
    }
}
