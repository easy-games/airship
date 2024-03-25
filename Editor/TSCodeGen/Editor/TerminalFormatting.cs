using System.Collections.Generic;
using System.Text.RegularExpressions;

public class TerminalFormatting {
    private static Dictionary<string, string> codeToTag = new();

    static TerminalFormatting() {
        codeToTag.Add("0m", "<b>");
    }
    
    public static string TerminalToUnity(string input) {
        var expr = new Regex(@"\e\[(\d+)m", RegexOptions.Singleline);
        return expr.Replace(input, (a) => {
            var code = a.Groups[2].Value;
            if (codeToTag.TryGetValue(code, out var tag)) {
                return tag;
            }
            else {
                return $"[{a.Value}]";
            }
        });;
    }
}