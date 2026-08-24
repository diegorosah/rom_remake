using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RetroRPG.Runtime
{
    public sealed class DialogueCatalog : MonoBehaviour
    {
        [SerializeField] private List<DialogueCatalogEntry> entries = new List<DialogueCatalogEntry>();
        private readonly Dictionary<string, DialogueDefinition> definitions = new Dictionary<string, DialogueDefinition>(StringComparer.Ordinal);

        public void Configure(IList<DialogueDefinition> configuredDefinitions)
        {
            if (configuredDefinitions == null)
            {
                throw new ArgumentNullException(nameof(configuredDefinitions));
            }

            definitions.Clear();
            entries = new List<DialogueCatalogEntry>(configuredDefinitions.Count);
            for (int index = 0; index < configuredDefinitions.Count; index++)
            {
                DialogueDefinition definition = configuredDefinitions[index] ?? throw new ArgumentException("Dialogue definitions cannot contain null.", nameof(configuredDefinitions));
                if (definitions.ContainsKey(definition.InteractionKey))
                {
                    throw new ArgumentException("Dialogue interaction keys must be unique.", nameof(configuredDefinitions));
                }

                definitions.Add(definition.InteractionKey, definition);
                var entry = new DialogueCatalogEntry();
                entry.Configure(definition);
                entries.Add(entry);
            }
        }

        public bool TryResolve(string interactionKey, out DialogueDefinition definition)
        {
            definition = null;

            if (string.IsNullOrWhiteSpace(interactionKey))
            {
                return false;
            }

            return definitions.TryGetValue(interactionKey, out definition);
        }

        private void Awake()
        {
            definitions.Clear();
            for (int index = 0; index < entries.Count; index++)
            {
                DialogueDefinition definition = entries[index].ToDefinition();
                if (definitions.ContainsKey(definition.InteractionKey))
                {
                    throw new InvalidOperationException("Serialized dialogue interaction keys must be unique.");
                }

                definitions.Add(definition.InteractionKey, definition);
            }
        }

        private void OnValidate()
        {
            if (entries == null)
            {
                entries = new List<DialogueCatalogEntry>();
            }
        }
    }
}
