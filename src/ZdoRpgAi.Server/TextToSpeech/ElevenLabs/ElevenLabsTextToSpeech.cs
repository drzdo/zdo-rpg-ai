using System.Text;
using System.Text.Json.Nodes;
using ZdoRpgAi.Core;

namespace ZdoRpgAi.Server.TextToSpeech.ElevenLabs;

public class ElevenLabsTextToSpeech : ITextToSpeech {
    private static readonly ILog Log = Logger.Get<ElevenLabsTextToSpeech>();

    private readonly HttpClient _http;
    private readonly string _model;
    private readonly double _stability;
    private readonly double _similarityBoost;
    private readonly double _style;
    private readonly bool _useSpeakerBoost;
    private readonly string _fallbackVoiceId;
    private readonly Dictionary<string, string> _voiceIdByRaceSex;

    public ElevenLabsTextToSpeech(ElevenLabsConfig config) {
        ArgumentNullException.ThrowIfNull(config.VoiceIdMapping.Fallback, "ElevenLabs.VoiceIdMapping.Fallback");
        ArgumentNullException.ThrowIfNull(config.VoiceIdMapping.ByRaceSex, "ElevenLabs.VoiceIdMapping.ByRaceSex");
        _model = config.Model;
        _stability = config.Stability;
        _similarityBoost = config.SimilarityBoost;
        _style = config.Style;
        _useSpeakerBoost = config.UseSpeakerBoost;
        _fallbackVoiceId = config.VoiceIdMapping.Fallback;
        _voiceIdByRaceSex = config.VoiceIdMapping.ByRaceSex;
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("xi-api-key", config.ApiKey);
    }

    public async Task<ITextToSpeechOutput> GenerateAsync(ITextToSpeechInput input) {
        var voiceId = ResolveVoiceId(input.npcRace, input.npcSex);

        var url = $"https://api.elevenlabs.io/v1/text-to-speech/{voiceId}";

        var body = new JsonObject {
            ["text"] = input.text,
            ["model_id"] = _model,
            ["voice_settings"] = new JsonObject {
                ["stability"] = _stability,
                ["similarity_boost"] = _similarityBoost,
                ["style"] = _style,
                ["use_speaker_boost"] = _useSpeakerBoost,
            },
        };

        var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        Log.Debug("Synthesizing {Length} chars with voice {VoiceId} for NPC {NpcId}", input.text.Length, voiceId, input.npcId);

        var resp = await _http.PostAsync(url, content);

        if (!resp.IsSuccessStatusCode) {
            var error = await resp.Content.ReadAsStringAsync();
            Log.Error("API error {StatusCode}: {Response}", resp.StatusCode, error);
            throw new Exception($"ElevenLabs API error {resp.StatusCode}: {error}");
        }

        var audio = await resp.Content.ReadAsByteArrayAsync();
        Log.Debug("Received {Size} bytes of audio", audio.Length);
        return new ITextToSpeechOutput { Mp3Bytes = audio };
    }

    private string ResolveVoiceId(string race, string sex) {
        var key = $"{char.ToLowerInvariant(race[0])}{char.ToLowerInvariant(sex[0])}";

        if (_voiceIdByRaceSex.TryGetValue(key, out var voiceId)) {
            return voiceId;
        }

        Log.Warn("No voice mapping for key '{Key}' (race={Race}, sex={Sex}), using fallback", key, race, sex);
        return _fallbackVoiceId;
    }
}
