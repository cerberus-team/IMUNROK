using System.IO;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Gyeonu.EditorTools
{
    /// <summary>
    /// 제3사건 음악 15곡의 임포트 설정과 <see cref="BgmLibrary"/> 에셋을 굽는다 (2026-09-09).
    ///
    /// ■ 루프 지점은 여기가 원본이다
    ///   수노 곡은 앞뒤에 페이드가 있다. 0.5초 창 RMS 로 앞뒤 포락선을 재서, 앞은 본체 크기에 닿는
    ///   자리를 <c>loopStart</c>, 뒤는 잦아들기 시작하기 직전을 <c>loopEnd</c> 로 잡았다.
    ///   <see cref="LoopDeck"/> 이 <c>loopEnd - 4초</c> 에서 <c>loopStart</c> 를 겹쳐 등파워로 잇는다.
    ///   귀로 듣고 자리를 옮기고 싶으면 <see cref="Table"/> 을 고치고 다시 굽는다.
    ///
    /// ■ 임포트 설정 (요청 그대로)
    ///   Load Type Streaming · Vorbis 70% · Load In Background.
    ///   V01 만 3D 로 쓰이므로 Force To Mono — 스테레오 클립은 3D 로 두면 좌우가 뭉개진다.
    ///   (Unity 5 이후 임포터에는 '3D' 스위치가 없다. 공간감은 AudioSource.spatialBlend 로 정한다.)
    /// </summary>
    public static class BgmLibraryBuilder
    {
        const string Folder = "Assets/_Project/Gyeonu/Audio/BGM";
        public const string AssetPath = "Assets/_Project/Gyeonu/Resources/BgmLibrary.asset";

        class Def
        {
            public BgmId id; public string file; public bool loop = true;
            public float start, end = -1f, xf = 4f, vol = 1f;
        }

        // 길이 · 포락선 실측 2026-09-09 (48kHz 스테레오)
        static readonly Def[] Table =
        {
            new Def { id = BgmId.S01_마을_낮,      file = "S01_마을_낮.wav",      start = 0f,   end = 98.5f  },   // 101.1s · 끝 1.5초 페이드
            new Def { id = BgmId.S02_마을_밤,      file = "S02_마을_밤.wav",      start = 0f,   end = 40.0f  },   // 43.1s  · 끝 3초 페이드
            new Def { id = BgmId.S03_은하담_낮,    file = "S03_은하담_낮.wav",    start = 3.0f, end = 150.5f },   // 153.5s · 앞 3초 조용한 인트로
            new Def { id = BgmId.S04_은하담_밤맑음, file = "S04_은하담_밤맑음.wav", start = 0f,   end = 134.0f },   // 136.6s
            new Def { id = BgmId.S05_은하담_밤비,   file = "S05_은하담_밤비.wav",   start = 0f,   end = 159.0f },   // 163.0s · 끝 4초 긴 페이드
            new Def { id = BgmId.S06_관아_낮,      file = "S06_관아_낮.wav",      start = 0f,   end = 88.0f  },   // 91.0s  · 끝에 잦아든 뒤 한 번 튀는 구간이 있어 그 앞에서 자른다
            new Def { id = BgmId.S07_관아_밤,      file = "S07_관아_밤.wav",      start = 0f,   end = 177.0f },   // 177.8s · 페이드가 거의 없다
            new Def { id = BgmId.S08_집무실,       file = "S08_집무실.wav",       start = 0f,   end = 171.0f },   // 177.3s · 끝 6초 긴 페이드
            new Def { id = BgmId.S09_관측실,       file = "S09_관측실.wav",       start = 0f,   end = 126.0f },   // 127.9s
            new Def { id = BgmId.S10_서고,         file = "S10_서고.wav",         start = 0f,   end = 179.0f },   // 181.7s
            new Def { id = BgmId.S11_견우마을,     file = "S11_견우마을.wav",     start = 0f,   end = 104.5f },   // 106.8s
            new Def { id = BgmId.S12_선아의방,     file = "S12_선아의방.wav",     start = 0f,   end = 175.5f },   // 178.8s
            new Def { id = BgmId.E01_밤하늘재현,   file = "E01_밤하늘재현.wav",   start = 1.5f, end = 130.0f },   // 132.0s · 별밤 내내 돈다
            new Def { id = BgmId.E02_최초의두사람, file = "E02_최초의두사람.wav", start = 0f,   end = 53.0f  },   // 55.3s  · 대화를 걸 때까지 돈다 (2026-09-10, 끝 2초 꼬리 제외)
            new Def { id = BgmId.V01_아이들노래,   file = "V01_아이들노래.wav",   loop = false },                 // 62.0s  · 한 번 부르고 쉰다
        };

        [MenuItem("Tools/이문록/음악/BGM 임포트 설정 + 라이브러리 굽기")]
        public static void Build()
        {
            ApplyImportSettings();
            Bake();
        }

        /// <summary>Streaming · Vorbis 70% · 백그라운드 로드. V01 만 모노.</summary>
        public static void ApplyImportSettings()
        {
            int n = 0;
            foreach (var d in Table)
            {
                string path = Folder + "/" + d.file;
                var imp = AssetImporter.GetAtPath(path) as AudioImporter;
                if (imp == null) { Debug.LogWarning("[음악] 음원이 없다: " + path); continue; }

                var s = imp.defaultSampleSettings;
                s.loadType = AudioClipLoadType.Streaming;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.7f;
                s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                imp.defaultSampleSettings = s;
                imp.loadInBackground = true;
                imp.forceToMono = d.id == BgmId.V01_아이들노래;
                imp.SaveAndReimport();
                n++;
            }
            Debug.Log("[음악] 임포트 설정 " + n + "곡 — Streaming · Vorbis 70%.");
        }

        /// <summary>Resources/BgmLibrary.asset 을 표대로 (다시) 쓴다.</summary>
        public static void Bake()
        {
            var dir = Path.GetDirectoryName(AssetPath).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(dir).Replace('\\', '/'), Path.GetFileName(dir));

            var lib = AssetDatabase.LoadAssetAtPath<BgmLibrary>(AssetPath);
            bool fresh = lib == null;
            if (fresh)
            {
                lib = ScriptableObject.CreateInstance<BgmLibrary>();
                AssetDatabase.CreateAsset(lib, AssetPath);
            }

            var tracks = new BgmLibrary.Track[Table.Length];
            for (int i = 0; i < Table.Length; i++)
            {
                var d = Table[i];
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + "/" + d.file);
                if (clip == null) Debug.LogWarning("[음악] 음원이 없다: " + d.file);
                tracks[i] = new BgmLibrary.Track
                {
                    id = d.id, clip = clip, volume = d.vol, loop = d.loop,
                    loopStart = d.start, loopEnd = d.end, loopCrossfade = d.xf,
                };
            }
            lib.tracks = tracks;
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log("[음악] BGM 라이브러리 " + (fresh ? "생성" : "갱신") + " — " + tracks.Length + "곡 → " + AssetPath);
        }
    }
}
