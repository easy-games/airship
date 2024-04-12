using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

public class TerminalFormatting {
    public enum ANSICode : byte {
        Reset = 0,
        Bold = 1,
        Reverse = 7,
        
        DefaultForeground = 39,
        ForegroundBlack = 30,
        ForegroundRed = 31,
        ForegroundGreen = 32,
        ForegroundYellow = 33,
        ForegroundBlue = 34,
        ForegroundMagenta = 35,
        ForegroundCyan = 36,
        ForegroundLightGrey = 37,
        
        ForegroundDarkGrey = 90,
        ForegroundLightRed = 91,
        ForegroundLightGreen = 92,
        ForegroundLightYellow = 93,
        
        ForegroundLightCyan = 96,
    }    
    
    public enum FormatTag {
        Bold,
        Color,
    }
    
    private static Dictionary<string, string> codeToTag = new();

    static TerminalFormatting() {
        codeToTag.Add("0m", "<b>");
    }

    public static List<FormatTag> formatTags = new();

    public static string Linkify(string packageDirectory, string input) {
        if (Regex.IsMatch(input, @"(?:(src\\.+[\\][^\\]+\.ts)|(src/.+[\/][^\/]+\.ts))")) {
            return Regex.Replace(input, @"(?:(src\\.+[\\][^\\]+\.ts|src/.+[\/][^\/]+\.ts))(?::(\d+):(\d+))*", (match) => {
                var fileLink = match.Groups[1].Value;
                var lineNumber = match.Groups[2].Value;
                var colNumber = match.Groups[3].Value;
                
                Debug.Log($"Link stuff fileLink={fileLink} lineNo={lineNumber} colNo={colNumber}");
                
                var link = $"{packageDirectory}{Path.DirectorySeparatorChar}{fileLink}".Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                if (lineNumber != "" && colNumber != "") {
                    return $"<a file='{link}' line={lineNumber} col={colNumber}>{match.Value}</a>";
                }
                else {
                    return $"<a file='{link}'>{match.Value}</a>";
                }
                
                
                // return link;
            }, RegexOptions.ECMAScript);
        }

        return input;
    }
    
    public static string TerminalToUnity(string input) {
        return Regex.Replace(input,@"\x1B\[(\d+)m", (match) => {
            if (int.TryParse(match.Groups[1].Value, out int ansiCode)) {
                switch ((ANSICode) ansiCode) {
                    case ANSICode.Reset:
                        var resultString = "";
                        foreach (var tag in formatTags) {
                            switch (tag) {
                                case FormatTag.Bold:
                                    resultString += "</b>";
                                    break;
                                case FormatTag.Color:
                                    resultString += "</color>";
                                    break;
                            }
                        }
                        
                        formatTags.Clear();
                        
                        return resultString;
                    case ANSICode.Reverse:
                    case ANSICode.Bold:
                        formatTags.Add(FormatTag.Bold);
                        return "<b>";
                    case ANSICode.ForegroundDarkGrey:
                        formatTags.Add(FormatTag.Color);
                        return "<color=#8e8e8e>";
                    case ANSICode.ForegroundLightYellow:
                        formatTags.Add(FormatTag.Color);
                        return "<color=#e5a03b>";
                    case ANSICode.ForegroundLightRed:
                        formatTags.Add(FormatTag.Color);
                        return "<color=#e05f67>";
                    case ANSICode.ForegroundLightCyan:
                        formatTags.Add(FormatTag.Color);
                        return "<color=#31a7c2>";
                    default:
                        return $"!!{ansiCode}!!";
                }
            }
            
            return "";
        }, RegexOptions.Singleline);
        //return "";
    }
}