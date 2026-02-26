using ZdoRpgAi.Core;

namespace ZdoRpgAi.Client.App;

public class Mp3Manager {
    private static readonly ILog Log = Logger.Get<Mp3Manager>();

    private readonly string _voiceDir;
    private readonly int _maxFiles;
    private int _currentIndex;

    public Mp3Manager(string dataDir, int maxFiles) {
        _voiceDir = Path.Combine(dataDir, "zdorpgai_mp3");
        _maxFiles = maxFiles;
    }

    public string SaveMp3(byte[] data) {
        _currentIndex = (_currentIndex % _maxFiles) + 1;
        var fileName = $"voice_{_currentIndex:D3}.mp3";
        var fullPath = Path.Combine(_voiceDir, fileName);
        File.WriteAllBytes(fullPath, data);
        Log.Debug("Saved MP3: {FileName} ({Size} bytes)", fileName, data.Length);
        return fileName;
    }
}
