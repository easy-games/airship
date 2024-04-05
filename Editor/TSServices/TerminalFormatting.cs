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

    public static Stack<FormatTag> formatTags = new();

    public static string Linkify(string packageDirectory, string input) {
        if (Regex.IsMatch(input, @"(?:(src\\.+[\\][^\\]+\.ts)|(src/.+[\/][^\/]+\.ts))")) {
            return Regex.Replace(input, @"(?:(src\\.+[\\][^\\]+\.ts)|(src/.+[\/][^\/]+\.ts))", (match) => {
                var link = $"{packageDirectory}{Path.DirectorySeparatorChar}{match.Value}".Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                //Debug.Log($"file:///{Application.dataPath}{Path.AltDirectorySeparatorChar}../{link}");
                // return $"<a href='file:///{Application.dataPath}{Path.AltDirectorySeparatorChar}../{link}'>{link}</a>";
                return link;
            });
        }

        return input;
    }
    
    public static string TerminalToUnity(string input) {
        return Regex.Replace(input,@"\x1B\[(\d+)m", (match) => {
            if (int.TryParse(match.Groups[1].Value, out int ansiCode)) {
                switch ((ANSICode) ansiCode) {
                    case ANSICode.Reset:
                        // TODO: Pop appropriate tags

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
                        
                        return resultString;
                    case ANSICode.Reverse:
                    case ANSICode.Bold:
                        formatTags.Push(FormatTag.Bold);
                        return "<b>";
                    case ANSICode.ForegroundDarkGrey:
                        formatTags.Push(FormatTag.Color);
                        return "<color=#aeaeae>";
                    case ANSICode.ForegroundLightYellow:
                        formatTags.Push(FormatTag.Color);
                        return "<color=#e5a03b>";
                    case ANSICode.ForegroundLightRed:
                        formatTags.Push(FormatTag.Color);
                        return "<color=#e05f67>";
                    case ANSICode.ForegroundLightCyan:
                        formatTags.Push(FormatTag.Color);
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