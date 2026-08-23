using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

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
            int? length = null,
            string stage = null)
        {
            Category = category ?? throw new ArgumentNullException(nameof(category));
            Severity = severity;
            Message = message ?? throw new ArgumentNullException(nameof(message));
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
            Offset = offset;
            Length = length;
            Stage = stage;
        }

        public string Category { get; }

        public DiagnosticSeverity Severity { get; }

        public string Message { get; }

        public string Stage { get; private set; }

        public long? Offset { get; }

        public int? Length { get; }

        public long? EndOffsetExclusive => Offset.HasValue && Length.HasValue
            ? checked(Offset.Value + Length.Value)
            : null;

        internal void AssignStageIfUnset(string stage)
        {
            if (string.IsNullOrEmpty(Stage))
            {
                Stage = stage;
            }
        }

        public override string ToString()
        {
            var location = Offset.HasValue ? $" @ 0x{Offset.Value:X}" : string.Empty;
            var stage = string.IsNullOrEmpty(Stage) ? string.Empty : $"[{Stage}] ";
            return $"{stage}[{Category}] {Severity}: {Message}{location}";
        }
    }

    [Serializable]
    public sealed class ImportReport
    {
        private readonly List<ParseDiagnostic> diagnostics = new List<ParseDiagnostic>();
        private readonly ReadOnlyCollection<ParseDiagnostic> readOnlyDiagnostics;

        public ImportReport(string stage)
        {
            Stage = stage ?? throw new ArgumentNullException(nameof(stage));
            readOnlyDiagnostics = diagnostics.AsReadOnly();
        }

        public string Stage { get; }

        public IReadOnlyList<ParseDiagnostic> Diagnostics => readOnlyDiagnostics;

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
            if (diagnostic == null)
            {
                throw new ArgumentNullException(nameof(diagnostic));
            }

            diagnostic.AssignStageIfUnset(Stage);
            diagnostics.Add(diagnostic);
        }
    }
}
