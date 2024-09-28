using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using UnityEngine;

namespace Airship.Editor {
    public enum TypescriptProblemType {
        Suggestion,
        Message,
        Warning,
        Error,
        Fatal,
    }

    public struct TypescriptLineAndColumn : IEquatable<TypescriptLineAndColumn> {
        public int Line;
        public int Column;

        public bool Equals(TypescriptLineAndColumn other) {
            return this.Line == other.Line && this.Column == other.Column;
        }
    }

    public struct TypescriptPosition : IEquatable<TypescriptPosition> {
        public int Position;
        public int Length;
        public string Text;

        public bool Equals(TypescriptPosition other) {
            return this.Position == other.Position;
        }
    }

    public interface ITypescriptProblemItem {
        public TypescriptProject Project { get; }
        public string Message { get; }
        public int ErrorCode { get; }
        public TypescriptProblemType ProblemType { get; }

    }

    public class TypescriptCrashProblemItem : ITypescriptProblemItem {
        public TypescriptProject Project { get; }
        public string Message { get; }
        public IEnumerable<string> StandardError { get; }
        public int ErrorCode { get; }
        public TypescriptProblemType ProblemType => TypescriptProblemType.Fatal;

        public TypescriptCrashProblemItem(TypescriptProject project, IEnumerable<string> stderr, string message, int errorCode) {
            Project = project;
            Message = message;
            ErrorCode = errorCode;
            StandardError = stderr;
        }
    }

    public class TypescriptFileDiagnosticItem : IEquatable<TypescriptFileDiagnosticItem>, ITypescriptProblemItem {
        public TypescriptProject Project { get; internal set; }
        public readonly string FileLocation;
        public string Message { get; }
        public int ErrorCode { get; }
        public TypescriptProblemType ProblemType { get; }
        public TypescriptLineAndColumn LineAndColumn { get; internal set; }
        public TypescriptPosition Position { get; internal set; }

        private TypescriptFileDiagnosticItem(
            TypescriptProject project, 
            string fileLocation, 
            string message, 
            int errorCode, 
            TypescriptProblemType problemType
        ) {
            Project = project;
            FileLocation = fileLocation.Replace("\\", "/");
            Message = message;
            ErrorCode = errorCode;
            ProblemType = problemType;
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((TypescriptFileDiagnosticItem)obj);
        }

        public static bool operator ==(TypescriptFileDiagnosticItem left, TypescriptFileDiagnosticItem right) {
            return left?.GetHashCode() == right?.GetHashCode();
        }
        
        public static bool operator !=(TypescriptFileDiagnosticItem left, TypescriptFileDiagnosticItem right) {
            return left?.GetHashCode() != right?.GetHashCode();
        }

        public override string ToString() {
            return $"{FileLocation}:{LineAndColumn.Line}:{LineAndColumn.Column}: {Message}";
        }

        public string ToUnityConsolePretty() {
            var link = $"{Project.Directory}{Path.DirectorySeparatorChar}{FileLocation}".Replace(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            
            var resultingString = ConsoleFormatting.LinkWithLineAndColumn(link, FileLocation, LineAndColumn.Line, LineAndColumn.Column);

            return resultingString;
        }

        public override int GetHashCode() {
            return HashCode.Combine(Project, FileLocation, Message, ErrorCode, (int)ProblemType, LineAndColumn);
        }

        private static readonly Regex errorRegex = new(@"(src\\.+[\\][^\\]+\.ts|src/.+[\/][^\/]+\.ts)(?::(\d+):(\d+)) - error (?:TS([0-9]+)|TS unity-ts): (.*)");

        internal static TypescriptFileDiagnosticItem FromDiagnosticEvent(CompilerEditorFileDiagnosticEvent diagnosticEvent) {
            var location = new TypescriptLineAndColumn();
            if (diagnosticEvent.Line.HasValue && diagnosticEvent.Column.HasValue) {
                location.Line = diagnosticEvent.Line.Value + 1;
                location.Column = diagnosticEvent.Column.Value + 1;
            }

            var problemItem = new TypescriptFileDiagnosticItem(
                TypescriptProjectsService.Project, 
                diagnosticEvent.FilePath, 
                diagnosticEvent.Message, 
                diagnosticEvent.Code ?? -1, 
                diagnosticEvent.Category switch {
                    TypescriptDiagnosticCategory.Warning => TypescriptProblemType.Warning,
                    TypescriptDiagnosticCategory.Error => TypescriptProblemType.Error,
                    TypescriptDiagnosticCategory.Suggestion => TypescriptProblemType.Suggestion,
                    TypescriptDiagnosticCategory.Message => TypescriptProblemType.Message,
                    _ => throw new ArgumentOutOfRangeException()
                });

            problemItem.LineAndColumn = location;
            problemItem.Position = new TypescriptPosition() {
                Length = diagnosticEvent.Position ?? 0,
                Text = diagnosticEvent.Text,
                Position = diagnosticEvent.Position ?? 0,
            };
            return problemItem;
        }

        internal static TypescriptFileDiagnosticItem Crash(string message) {
            var problemItem = new TypescriptFileDiagnosticItem(TypescriptProjectsService.Project, "", message, -1,
                TypescriptProblemType.Error);
            return problemItem;
        }
        
        [CanBeNull]
        internal static TypescriptFileDiagnosticItem Parse(string input) {
            
            
            input = TerminalFormatting.StripANSI(input);

            if (!errorRegex.IsMatch(input)) {
                Debug.Log($"Is not error item {input}");
                return null;
            }

            TypescriptLineAndColumn location;
            
            var values = errorRegex.Match(input);
            var fileLocation = values.Groups[1].Value;
            
            int.TryParse(values.Groups[2].Value, out location.Line);
            int.TryParse(values.Groups[3].Value, out location.Column);
            int.TryParse(values.Groups[4].Value, out var errorCode);
            
            var message = values.Groups[5].Value;
            
            var problemItem = new TypescriptFileDiagnosticItem(null, fileLocation, message, errorCode, TypescriptProblemType.Error);
            problemItem.LineAndColumn = location;
            return problemItem;
        }

        public bool Equals(TypescriptFileDiagnosticItem other) {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(Project, other.Project) && FileLocation == other.FileLocation && Message == other.Message && ErrorCode == other.ErrorCode && ProblemType == other.ProblemType && LineAndColumn.Equals(other.LineAndColumn);
        }
    }
}