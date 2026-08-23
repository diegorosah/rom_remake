using System;
using System.Collections.Generic;

namespace RetroRPG.Core
{
    public enum DiagnosticSeverity
    {
        Info,
        Warning,
        Error
    }

    [Serializable]
    public sealed class ParseDiagnostic
    {
        public ParseDiagnostic(
            string category,
            DiagnosticSeverity severity,
            string message,
            long? offset = null,
            int? length = null)
        {
            Category = category ?? throw new ArgumentNullException(nameof(category));
            Severity = severity;
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Offset = offset;
            Length = length;
        }

        public string Category { get; }

        public DiagnosticSeverity Severity { get; }

        public string Message { get; }

        public long? Offset { get; }

        public int? Length { get; }

        public override string ToString()
        {
            var location = Offset.HasValue ? $" @ 0x{Offset.Value:X}" : string.Empty;
            return $"[{Category}] {Severity}: {Message}{location}";
        }
    }

    [Serializable]
    public sealed class ImportReport
    {
        private readonly List<ParseDiagnostic> diagnostics = new List<ParseDiagnostic>();

        public ImportReport(string stage)
        {
            Stage = stage ?? throw new ArgumentNullException(nameof(stage));
        }

        public string Stage { get; }

        public IReadOnlyList<ParseDiagnostic> Diagnostics => diagnostics;

        public bool HasErrors
        {
            get
            {
                for (var i = 0; i < diagnostics.Count; i++)
                {
                    if (diagnostics[i].Severity == DiagnosticSeverity.Error)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public void Add(ParseDiagnostic diagnostic)
        {
            diagnostics.Add(diagnostic ?? throw new ArgumentNullException(nameof(diagnostic)));
        }
    }
}

