namespace ZdoRpgAi.ModEmulator.Console;

public class ConsoleInput {
    private readonly List<string> _history = new();
    private readonly string _historyFilePath;
    private int _historyIndex;

    public ConsoleInput() {
        _historyFilePath = Path.Combine(Path.GetTempPath(), "zdorpgai-emulator-history.txt");
        LoadHistory();
    }

    public string? ReadLine(string prompt = "> ") {
        System.Console.Write(prompt);
        var p = prompt.Length;
        var buffer = new List<char>();
        var cursor = 0;
        _historyIndex = _history.Count;
        string? savedInput = null;

        while (true) {
            var key = System.Console.ReadKey(intercept: true);

            // Ctrl shortcuts
            if (key.Modifiers.HasFlag(ConsoleModifiers.Control)) {
                switch (key.Key) {
                    case ConsoleKey.A:
                        if (cursor > 0) {
                            System.Console.CursorLeft = p;
                            cursor = 0;
                        }
                        continue;
                    case ConsoleKey.E:
                        if (cursor < buffer.Count) {
                            System.Console.CursorLeft = p + buffer.Count;
                            cursor = buffer.Count;
                        }
                        continue;
                    case ConsoleKey.W:
                        if (cursor > 0) {
                            var end = cursor;
                            while (cursor > 0 && buffer[cursor - 1] == ' ') cursor--;
                            while (cursor > 0 && buffer[cursor - 1] != ' ') cursor--;
                            buffer.RemoveRange(cursor, end - cursor);
                            ClearAndRedraw(buffer, cursor, end, p);
                        }
                        continue;
                    case ConsoleKey.K:
                        if (cursor < buffer.Count) {
                            var removed = buffer.Count - cursor;
                            buffer.RemoveRange(cursor, removed);
                            System.Console.Write(new string(' ', removed));
                            System.Console.CursorLeft = p + cursor;
                        }
                        continue;
                }
            }

            switch (key.Key) {
                case ConsoleKey.Enter:
                    System.Console.WriteLine();
                    var line = new string(buffer.ToArray());
                    if (line.Length > 0) {
                        if (_history.Count == 0 || _history[^1] != line) {
                            _history.Add(line);
                        }
                        SaveHistory();
                    }
                    return line;

                case ConsoleKey.UpArrow:
                    if (_historyIndex > 0) {
                        if (_historyIndex == _history.Count) {
                            savedInput = new string(buffer.ToArray());
                        }
                        _historyIndex--;
                        ReplaceBuffer(buffer, ref cursor, _history[_historyIndex], p);
                    }
                    break;

                case ConsoleKey.DownArrow:
                    if (_historyIndex < _history.Count) {
                        _historyIndex++;
                        var text = _historyIndex < _history.Count
                            ? _history[_historyIndex]
                            : savedInput ?? "";
                        ReplaceBuffer(buffer, ref cursor, text, p);
                    }
                    break;

                case ConsoleKey.LeftArrow:
                    if (cursor > 0) {
                        cursor--;
                        System.Console.CursorLeft = p + cursor;
                    }
                    break;

                case ConsoleKey.RightArrow:
                    if (cursor < buffer.Count) {
                        cursor++;
                        System.Console.CursorLeft = p + cursor;
                    }
                    break;

                case ConsoleKey.Home:
                    if (cursor > 0) {
                        System.Console.CursorLeft = p;
                        cursor = 0;
                    }
                    break;

                case ConsoleKey.End:
                    if (cursor < buffer.Count) {
                        System.Console.CursorLeft = p + buffer.Count;
                        cursor = buffer.Count;
                    }
                    break;

                case ConsoleKey.Backspace:
                    if (cursor > 0) {
                        buffer.RemoveAt(cursor - 1);
                        cursor--;
                        RedrawFromCursor(buffer, cursor, p);
                    }
                    break;

                case ConsoleKey.Delete:
                    if (cursor < buffer.Count) {
                        buffer.RemoveAt(cursor);
                        RedrawFromCursor(buffer, cursor, p);
                    }
                    break;

                default:
                    if (key.KeyChar >= ' ') {
                        buffer.Insert(cursor, key.KeyChar);
                        cursor++;
                        if (cursor == buffer.Count) {
                            System.Console.Write(key.KeyChar);
                        } else {
                            RedrawFromCursor(buffer, cursor, p);
                        }
                    }
                    break;
            }
        }
    }

    private static void ClearAndRedraw(List<char> buffer, int cursor, int oldEnd, int promptLen) {
        System.Console.CursorLeft = promptLen + cursor;
        var tail = new string(buffer.ToArray(), cursor, buffer.Count - cursor);
        var clearLen = oldEnd - cursor;
        System.Console.Write(tail + new string(' ', clearLen - tail.Length));
        System.Console.CursorLeft = promptLen + cursor;
    }

    private static void ReplaceBuffer(List<char> buffer, ref int cursor, string text, int promptLen) {
        System.Console.CursorLeft = promptLen;
        var clearLen = buffer.Count;
        buffer.Clear();
        buffer.AddRange(text);
        System.Console.Write(text);
        if (text.Length < clearLen) {
            System.Console.Write(new string(' ', clearLen - text.Length));
            System.Console.CursorLeft = promptLen + text.Length;
        }
        cursor = buffer.Count;
    }

    private static void RedrawFromCursor(List<char> buffer, int cursor, int promptLen) {
        System.Console.CursorLeft = promptLen + (cursor > 0 ? cursor - 1 : 0);
        var startPos = cursor > 0 ? cursor - 1 : 0;
        var tail = new string(buffer.ToArray(), startPos, buffer.Count - startPos);
        System.Console.Write(tail + " ");
        System.Console.CursorLeft = promptLen + cursor;
    }

    private void LoadHistory() {
        try {
            if (!File.Exists(_historyFilePath)) return;
            var lines = File.ReadAllLines(_historyFilePath);
            foreach (var line in lines) {
                if (line.Length > 0) {
                    _history.Add(line);
                }
            }
        } catch {
            // History file lost or corrupt — that's fine
        }
    }

    private void SaveHistory() {
        try {
            // Keep last 200 entries
            var start = _history.Count > 200 ? _history.Count - 200 : 0;
            var lines = _history.GetRange(start, _history.Count - start);
            File.WriteAllLines(_historyFilePath, lines);
        } catch {
            // Best effort
        }
    }
}
