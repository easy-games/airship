using System.Collections.Generic;
using System.Text.RegularExpressions;

public class TerminalFormatting {
    private static Dictionary<string, string> codeToTag = new();

    static TerminalFormatting() {
        codeToTag.Add("0m", "<b>");
    }
    
    public static string TerminalToUnity(string input) {
        return Regex.Replace(input,@"\x1B\[\d+m","");
        //return "";
    }
}