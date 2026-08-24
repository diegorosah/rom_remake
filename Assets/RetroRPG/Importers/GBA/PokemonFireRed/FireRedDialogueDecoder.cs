using System;
using System.Collections.Generic;
using RetroRPG.Core;
using RetroRPG.Importers.GBA.Common;
using RetroRPG.IR;

namespace RetroRPG.Importers.GBA.PokemonFireRed
{
    /// <summary>Strict declarative decoder for the two audited static MVP 5 dialogue circuits.</summary>
    internal static class FireRedDialogueDecoder
    {
        public static DialogueCatalogDefinition Decode(RomReader reader, ImportReport report)
        {
            return Decode(reader, report, null);
        }

        /// <summary>Decodes only circuits whose target-event map prefix is in the selected bundle.</summary>
        public static DialogueCatalogDefinition Decode(RomReader reader, ImportReport report, IList<string> selectedMapIds)
        {
            if (reader == null || report == null) throw new ArgumentNullException();
            var includePalletTown = IncludesMap(selectedMapIds, FireRedRomLayoutRev1.PalletTownMapId);
            var includePlayersHouse = IncludesMap(selectedMapIds, FireRedRomLayoutRev1.PlayersHouse1FMapId);
            var includeRivalsHouse = IncludesMap(selectedMapIds, FireRedRomLayoutRev1.RivalsHouseMapId);
            var dialogues = new List<DialogueDefinition>();
            if (includePalletTown)
            {
                dialogues.Add(DecodeCircuit(reader, "dialogue_pallet_fat_man", FireRedRomLayoutRev1.PalletTownMapId + ":object:2", FireRedRomLayoutRev1.FatManDialogueScript, FireRedRomLayoutRev1.FatManDialogueText, FireRedRomLayoutRev1.FatManDialogueTextLength, DialoguePresentation.Npc, true));
                report.Add(new ParseDiagnostic("Dialogue", DiagnosticSeverity.Warning, "No state profile is declared for Woman; her dialogue remains unsupported."));
                report.Add(new ParseDiagnostic("Dialogue", DiagnosticSeverity.Warning, "Professor Oak has no supported dialogue in the preview profile."));
            }

            if (includePlayersHouse) report.Add(new ParseDiagnostic("Dialogue", DiagnosticSeverity.Warning, "Mom requires a state profile; her dialogue remains unsupported."));
            if (includeRivalsHouse)
            {
                dialogues.Add(DecodeCircuit(reader, "dialogue_rivals_house_town_map", FireRedRomLayoutRev1.RivalsHouseMapId + ":object:2", FireRedRomLayoutRev1.TownMapDialogueScript, FireRedRomLayoutRev1.TownMapDialogueText, FireRedRomLayoutRev1.TownMapDialogueTextLength, DialoguePresentation.Neutral, false));
                report.Add(new ParseDiagnostic("Dialogue", DiagnosticSeverity.Warning, "Daisy has stateful specials and remains unsupported by the bounded dialogue decoder."));
            }

            report.Add(new ParseDiagnostic("Dialogue", DiagnosticSeverity.Info, "Parsed " + dialogues.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " whitelisted static dialogue circuits for selected maps without executing ROM scripts."));
            return new DialogueCatalogDefinition(dialogues);
        }

        private static bool IncludesMap(IList<string> selectedMapIds, string mapId)
        {
            if (selectedMapIds == null) return true;
            for (var index = 0; index < selectedMapIds.Count; index++)
            {
                if (string.Equals(selectedMapIds[index], mapId, StringComparison.Ordinal)) return true;
            }

            return false;
        }

        private static DialogueDefinition DecodeCircuit(RomReader reader, string id, string targetId, int scriptOffset, int expectedTextOffset, int expectedTextLength, DialoguePresentation presentation, bool facePlayer)
        {
            reader.EnsureRange(scriptOffset, 9, "Whitelisted dialogue script is outside ROM bounds.");
            Expect(reader, reader.ReadByte(scriptOffset), FireRedRomLayoutRev1.ScriptOpcodeLoadWord, "Dialogue script must load a text pointer", scriptOffset);
            Expect(reader, reader.ReadByte(checked(scriptOffset + 1)), FireRedRomLayoutRev1.ScriptDataSlotZero, "Dialogue script must load slot zero", checked(scriptOffset + 1));
            var textOffset = reader.ConvertGbaPointer(reader.ReadUInt32(checked(scriptOffset + 2)), 1);
            if (textOffset != expectedTextOffset) throw new RomReadException("Dialogue text pointer does not match the audited circuit.", checked(scriptOffset + 2), 4, reader.Length);
            Expect(reader, reader.ReadByte(checked(scriptOffset + 6)), FireRedRomLayoutRev1.ScriptOpcodeCallStd, "Dialogue script must invoke a standard message box", checked(scriptOffset + 6));
            Expect(reader, reader.ReadByte(checked(scriptOffset + 7)), FireRedRomLayoutRev1.ScriptStandardMessageBoxNpc, "Dialogue script must invoke the standard NPC message box", checked(scriptOffset + 7));
            Expect(reader, reader.ReadByte(checked(scriptOffset + 8)), FireRedRomLayoutRev1.ScriptOpcodeEnd, "Dialogue script must terminate", checked(scriptOffset + 8));
            var tokens = DecodeText(reader, textOffset, expectedTextLength);
            return new DialogueDefinition(id, targetId, presentation, facePlayer, new[] { new DialoguePageDefinition(tokens) });
        }

        private static List<DialogueToken> DecodeText(RomReader reader, int offset, int expectedLength)
        {
            if (expectedLength <= 0 || expectedLength > FireRedRomLayoutRev1.DialogueMaxTextBytes) throw new InvalidOperationException("Dialogue text length is outside the configured safety bound.");
            reader.EnsureRange(offset, expectedLength, "Dialogue text range is outside ROM bounds.");
            var tokens = new List<DialogueToken>();
            var index = 0;
            while (index < expectedLength)
            {
                reader.EnsureRange(checked(offset + index), 1, "Dialogue text is truncated before its terminator.");
                var value = reader.ReadByte(checked(offset + index++));
                if (value == FireRedRomLayoutRev1.FireRedTextEnd)
                {
                    if (index != expectedLength) throw new RomReadException("Dialogue terminator length does not match the audited circuit.", offset, index, reader.Length);
                    return tokens;
                }

                if (value == FireRedRomLayoutRev1.FireRedTextNewline) { tokens.Add(new DialogueToken(DialogueTokenKind.Newline)); continue; }
                if (value == FireRedRomLayoutRev1.FireRedTextPromptScroll) { tokens.Add(new DialogueToken(DialogueTokenKind.PromptScroll)); continue; }
                if (value == FireRedRomLayoutRev1.FireRedTextPromptClear) { tokens.Add(new DialogueToken(DialogueTokenKind.PromptClear)); continue; }
                try
                {
                    if (value == FireRedRomLayoutRev1.FireRedTextPlaceholder) { tokens.Add(new DialogueToken(DialogueTokenKind.Placeholder, PlaceholderName(ReadOperand(reader, offset, ref index, expectedLength, "placeholder")))); continue; }
                    if (value == FireRedRomLayoutRev1.FireRedTextExtendedControl) { tokens.Add(DecodeExtendedControl(reader, offset, ref index, expectedLength)); continue; }
                    tokens.Add(new DialogueToken(DialogueTokenKind.Glyph, Glyph(value)));
                }
                catch (InvalidOperationException exception)
                {
                    throw new RomReadException(exception.Message, checked(offset + index - 1), 1, reader.Length);
                }
            }

            throw new RomReadException("Dialogue text reached its audited length without an end marker.", offset, expectedLength, reader.Length);
        }

        private static DialogueToken DecodeExtendedControl(RomReader reader, int offset, ref int index, int expectedLength)
        {
            var command = ReadOperand(reader, offset, ref index, expectedLength, "extended text control");
            var operandCount = ExtendedOperandCount(command);
            var parameters = new int[operandCount];
            for (var i = 0; i < operandCount; i++) parameters[i] = ReadOperand(reader, offset, ref index, expectedLength, "extended text control operand");
            return new DialogueToken(DialogueTokenKind.ExtendedControl, ExtendedControlName(command), parameters);
        }

        private static byte ReadOperand(RomReader reader, int offset, ref int index, int expectedLength, string description)
        {
            if (index >= expectedLength) throw new RomReadException(description + " exceeds the audited dialogue range.", offset, index, reader.Length);
            reader.EnsureRange(checked(offset + index), 1, description + " is truncated.");
            return reader.ReadByte(checked(offset + index++));
        }

        private static string Glyph(byte value)
        {
            if (value == 0) return " ";
            if (value >= 0xA1 && value <= 0xAA) return ((char)('0' + (value - 0xA1))).ToString();
            if (value >= 0xBB && value <= 0xD4) return ((char)('A' + (value - 0xBB))).ToString();
            if (value >= 0xD5 && value <= 0xEE) return ((char)('a' + (value - 0xD5))).ToString();
            switch (value)
            {
                case 0x1B: return "é";
                case 0xAB: return "!"; case 0xAC: return "?"; case 0xAD: return "."; case 0xAE: return "-"; case 0xB4: return "'"; case 0xB7: return "$"; case 0xB8: return ","; case 0xBA: return "/"; case 0xF0: return ":";
                default: throw new InvalidOperationException("Dialogue contains a glyph outside the audited FireRed character whitelist.");
            }
        }

        private static string PlaceholderName(byte value)
        {
            switch (value)
            {
                case 0: return "Unknown"; case 1: return "Player"; case 2: return "StringVar1"; case 3: return "StringVar2"; case 4: return "StringVar3"; case 5: return "Kun"; case 6: return "Rival"; case 7: return "Version"; case 8: return "Magma"; case 9: return "Aqua"; case 10: return "Maxie"; case 11: return "Archie"; case 12: return "Groudon"; case 13: return "Kyogre";
                default: throw new InvalidOperationException("Dialogue contains a placeholder outside the audited whitelist.");
            }
        }

        private static int ExtendedOperandCount(byte command)
        {
            switch (command)
            {
                case 1: case 2: case 3: case 5: case 6: case 8: case 13: case 14: case 15: case 19: case 20: return 1;
                case 4: return 3;
                case 7: case 9: case 12: case 17: case 21: case 22: case 23: case 24: return 0;
                case 10: case 11: case 16: return 2;
                default: throw new InvalidOperationException("Dialogue contains an extended control outside the audited whitelist.");
            }
        }

        private static string ExtendedControlName(byte command) { return "Control" + command.ToString(System.Globalization.CultureInfo.InvariantCulture); }
        private static void Expect(RomReader reader, byte actual, int expected, string description, int offset) { if (actual != expected) throw new RomReadException(description + " does not match the verified rev1 circuit.", offset, 1, reader.Length); }
    }
}
