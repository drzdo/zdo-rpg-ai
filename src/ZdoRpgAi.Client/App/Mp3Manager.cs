using ZdoRpgAi.Core;

namespace ZdoRpgAi.Client.App;

public class Mp3Manager {
    private static readonly ILog Log = Logger.Get<Mp3Manager>();

    private readonly string _voiceDir;
    private readonly int _maxFiles;
    private int _currentIndex;

    public Mp3Manager(string dataDir, int maxFiles) {
        _voiceDir = Path.Combine(dataDir, "Sound", "zdorpgai_mp3");
        _maxFiles = maxFiles;
        Directory.CreateDirectory(_voiceDir);
        PreCreateSlots();
    }

    private void PreCreateSlots() {
        for (var i = 1; i <= _maxFiles; i++) {
            var path = Path.Combine(_voiceDir, $"voice_{i:D3}.mp3");
            if (!File.Exists(path)) {
                File.WriteAllBytes(path, SilentMp3Frame);
                Log.Debug("Pre-created MP3 slot: voice_{Index:D3}.mp3", i);
            }
        }
    }

    // Minimal valid MP3 frame (MPEG1 Layer3, 128kbps, 44100Hz, ~0ms silence)
    private static readonly byte[] SilentMp3Frame = [
        0xFF, 0xFB, 0x90, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x54, 0x41, 0x47, 0x00,
    ];

    public string SaveMp3(byte[] data) {
        _currentIndex = (_currentIndex % _maxFiles) + 1;
        var fileName = $"voice_{_currentIndex:D3}.mp3";
        var fullPath = Path.Combine(_voiceDir, fileName);
        using (var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None)) {
            fs.Write(data);
            fs.Flush(flushToDisk: true);
        }
        Log.Debug("Saved MP3: {FileName} ({Size} bytes, fsynced)", fileName, data.Length);
        return fileName;
    }
}
