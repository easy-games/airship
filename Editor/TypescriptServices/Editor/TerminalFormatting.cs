using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Airship.Editor;
using UnityEngine;

public static class ConsoleFormatting {
    public static string LinkWithLineAndColumn(string link, string text, int line, int column) {
        if (string.IsNullOrEmpty(text)) {
            return "unknown";
        }
        
        var resultingString = $"<a href='#' file='{link}' line='{line}' col='{column}'>{text}</a>";

        if (line != -1 && column != -1 && line != 0 && column != 0) {
            resultingString += ":" + Number(line) + ":" + Number(column);
        }
        
        return resultingString;
    }

    private static string Number(int value) {
        return $"<color=#e5a03b>{value}</color>";
    }
    
    private static string Red(string value) {
        return $"<color=#e05f67>{value}</color>";
    }
    
    private static string ErrorCode(int value) {
        return $"<color=#8e8e8e>TS{value}</color>";
    }

    public static string GetProblemItemString(TypescriptFileDiagnosticItem item, bool pretty = true) {
        if (pretty) {
            var link = $"{item.Project.Directory}{Path.DirectorySeparatorChar}{item.FileLocation}".Replace(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            
            var message = LinkWithLineAndColumn(link, item.FileLocation, item.LineAndColumn.Line, item.LineAndColumn.Column);

            if (item.ProblemType == TypescriptProblemType.Error) {
                if (item.ErrorCode > 0) {
                    message += " - " + Red("Error") + " " + ErrorCode(item.ErrorCode);
                }
                else {
                    message += " - " + Red("Compiler Error");
                }
            } else if (item.ProblemType == TypescriptProblemType.Message) {
                if (item.ErrorCode > 0) {
                    message += " - " + ErrorCode(item.ErrorCode);
                }
                else {
                    message += " - ";
                }
            }


            message += ": " + item.Message;
            
            return message;
        }
        else {
            return item.ToString();
        }
    }
}

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

    private static Regex simpleFileLinkRegex = new(@"(src\\.+[\\][^\\]+\.ts|src/.+[\/][^\/]+\.ts)");
    private static Regex fileLinkRegex = new(@"(src\\.+[\\][^\\]+\.ts|src/.+[\/][^\/]+\.ts)(?::(\d+):(\d+))");

    public struct FileLink {
        public string FilePath;
        public int Line;
        public int Column;

        public static FileLink? Parse(string input) {
            input = StripANSI(input);
            if (fileLinkRegex.IsMatch(input)) {
                FileLink link;
                var values = fileLinkRegex.Match(input);
                link.FilePath = values.Groups[1].Value;
                
                int.TryParse(values.Groups[2].Value, out link.Line);
                int.TryParse(values.Groups[3].Value, out link.Column);
                return link;
            }

            return null;
        }
    }
    
    public static string Linkify(string packageDirectory, string input, FileLink? linkedFile) {
        if (simpleFileLinkRegex.IsMatch(input)) {
            return simpleFileLinkRegex.Replace(input, (match) => {
                var fileLink = linkedFile?.FilePath ?? match.Groups[1].Value;
                var line = linkedFile?.Line ?? -1;
                var col = linkedFile?.Column ?? -1;
                
                var link = $"{packageDirectory}{Path.DirectorySeparatorChar}{fileLink}".Replace(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                if (line != -1 && col != -1) {
                    return $"<a href='#' file='{link}' line='{line}' col='{col}'>{match.Value}</a>";
                }
                else {
                    return $"<a href='#' file='{link}'>{match.Value}</a>";
                }
            });
        }

        return input;
    }

    public static string StripANSI(string input) {
        return Regex.Replace(input, @"\x1B\[(\d+)m", "");
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