using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 곡 하나를 <b>이음매 없이</b> 무한히 트는 데크 (2026-09-09).
    ///
    /// ■ 왜 AudioSource.loop 를 쓰지 않는가
    ///   수노 결과물은 앞뒤에 페이드가 있다. 그냥 루프를 걸면 한 바퀴마다 '잦아들었다 차오르는'
    ///   구간이 들린다. 그래서 소스 두 개를 번갈아 쓴다 — 지금 소스가 <c>loopEnd - crossfade</c>
    ///   에 닿으면 다른 소스를 <c>loopStart</c> 에서 띄우고, 둘을 등파워(cos/sin)로 섞은 뒤 앞 것을
    ///   멈춘다. 4초쯤 겹치면 박자가 조금 어긋나도 배경 음악 수준에서는 이음매를 알아채기 어렵다.
    ///
    /// ■ 크기는 밖에서 준다
    ///   <see cref="Gain"/> 하나로 받는다 (전체 크기 × 곡 배율 × 곡 전환 페이드 × 덕킹).
    ///   데크는 그 값을 두 소스에 루프 크로스페이드 비율대로 나눠 줄 뿐이다.
    ///
    /// ■ Streaming 클립과의 궁합
    ///   <c>AudioSource.time</c> 대입으로 중간 지점부터 틀 수 있고, 소스마다 따로 스트리밍하므로
    ///   같은 클립을 두 소스가 겹쳐 틀어도 문제없다.
    /// </summary>
    public class LoopDeck
    {
        readonly AudioSource[] src = new AudioSource[2];
        int cur;                 // 지금 주로 울리는 소스
        int next;                // 루프 크로스페이드 상대
        bool crossing;
        float crossT;            // 0→1
        BgmLibrary.Track track;

        public string Name { get; }
        public BgmLibrary.Track Track => track;
        public bool IsPlaying { get; private set; }

        /// <summary>바깥에서 정하는 최종 크기 (0~1).</summary>
        public float Gain { get; set; } = 1f;

        /// <summary>한 번만 트는 곡이 끝까지 갔는가.</summary>
        public bool Finished => IsPlaying && track != null && !track.loop && !src[cur].isPlaying;

        public LoopDeck(GameObject host, string name)
        {
            Name = name;
            for (int i = 0; i < 2; i++)
            {
                var s = host.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.loop = false;
                s.spatialBlend = 0f;             // 배경 음악은 사방에서
                s.priority = 0;                  // 효과음이 몰려도 음악 채널은 안 뺏긴다
                s.volume = 0f;
                s.dopplerLevel = 0f;
                src[i] = s;
            }
        }

        public void Play(BgmLibrary.Track t)
        {
            track = t;
            src[0].Stop(); src[1].Stop();
            cur = 0; crossing = false; crossT = 0f;
            var s = src[cur];
            s.clip = t.clip;
            s.time = Mathf.Clamp(t.loopStart, 0f, Mathf.Max(0f, t.clip.length - 0.1f));
            s.Play();
            IsPlaying = true;
            Apply();
        }

        public void Stop()
        {
            src[0].Stop(); src[1].Stop();
            IsPlaying = false;
            crossing = false;
        }

        public void Update(float dt)
        {
            if (!IsPlaying || track == null) return;

            if (crossing)
            {
                crossT += dt / Mathf.Max(0.05f, track.loopCrossfade);
                if (crossT >= 1f)
                {
                    src[cur].Stop();
                    cur = next;
                    crossing = false;
                    crossT = 0f;
                }
            }
            else if (track.loop)
            {
                float end = track.LoopEndOrLength;
                float xf = Mathf.Min(track.loopCrossfade, Mathf.Max(0.1f, (end - track.loopStart) * 0.4f));
                if (src[cur].time >= end - xf || !src[cur].isPlaying)
                {
                    next = 1 - cur;
                    var s = src[next];
                    s.clip = track.clip;
                    s.time = Mathf.Clamp(track.loopStart, 0f, Mathf.Max(0f, track.clip.length - 0.1f));
                    s.Play();
                    crossing = true;
                    crossT = 0f;
                }
            }

            Apply();
        }

        void Apply()
        {
            float g = Mathf.Clamp01(Gain);
            if (crossing)
            {
                float a = Mathf.Cos(crossT * Mathf.PI * 0.5f);
                float b = Mathf.Sin(crossT * Mathf.PI * 0.5f);
                src[cur].volume = g * a;
                src[next].volume = g * b;
            }
            else
            {
                src[cur].volume = g;
                src[1 - cur].volume = 0f;
            }
        }
    }
}
