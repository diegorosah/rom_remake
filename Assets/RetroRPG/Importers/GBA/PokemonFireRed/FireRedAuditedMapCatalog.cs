using System;
using System.Collections.Generic;
using RetroRPG.IR;

namespace RetroRPG.Importers.GBA.PokemonFireRed
{
    /// <summary>Maps whose formats are already audited; this is not a scanner for arbitrary ROM maps.</summary>
    public static class FireRedAuditedMapCatalog
    {
        private static readonly MapCatalogDefinition Catalog = new MapCatalogDefinition(new[]
        {
            new MapImportDescriptorDefinition(
                FireRedRomLayoutRev1.PalletTownMapId,
                "Pallet Town",
                FireRedRomLayoutRev1.PalletTownWidth,
                FireRedRomLayoutRev1.PalletTownHeight,
                false,
                new[] { FireRedRomLayoutRev1.PlayersHouse1FMapId, FireRedRomLayoutRev1.RivalsHouseMapId },
                new[] { FireRedRomLayoutRev1.OakLabMapId }),
            new MapImportDescriptorDefinition(
                FireRedRomLayoutRev1.PlayersHouse1FMapId,
                "Player's House 1F",
                FireRedRomLayoutRev1.PlayersHouse1FWidth,
                FireRedRomLayoutRev1.PlayersHouse1FHeight,
                true,
                new[] { FireRedRomLayoutRev1.PalletTownMapId, FireRedRomLayoutRev1.PlayersHouse2FMapId },
                new string[0]),
            new MapImportDescriptorDefinition(
                FireRedRomLayoutRev1.PlayersHouse2FMapId,
                "Player's House 2F",
                FireRedRomLayoutRev1.PlayersHouse2FWidth,
                FireRedRomLayoutRev1.PlayersHouse2FHeight,
                true,
                new[] { FireRedRomLayoutRev1.PlayersHouse1FMapId },
                new string[0]),
            new MapImportDescriptorDefinition(
                FireRedRomLayoutRev1.RivalsHouseMapId,
                "Rival's House",
                FireRedRomLayoutRev1.RivalsHouseWidth,
                FireRedRomLayoutRev1.RivalsHouseHeight,
                true,
                new[] { FireRedRomLayoutRev1.PalletTownMapId },
                new string[0]),
            new MapImportDescriptorDefinition(
                FireRedRomLayoutRev1.Route1MapId,
                "Route 1",
                FireRedRomLayoutRev1.Route1Width,
                FireRedRomLayoutRev1.Route1Height,
                false,
                new string[0],
                new string[0])
        });

        public static MapCatalogDefinition Definition => Catalog;

        internal static IReadOnlyList<FireRedMapSpec> ResolveSpecs(IList<MapImportDescriptorDefinition> descriptors)
        {
            if (descriptors == null || descriptors.Count == 0) throw new ArgumentException("At least one audited map descriptor is required.", nameof(descriptors));
            var specs = new List<FireRedMapSpec>(descriptors.Count);
            for (var index = 0; index < descriptors.Count; index++)
            {
                var descriptor = descriptors[index] ?? throw new ArgumentException("Map descriptors cannot contain null.", nameof(descriptors));
                var spec = FindSpec(descriptor.Id);
                if (spec == null || spec.Name != descriptor.Name || spec.Width != descriptor.Width || spec.Height != descriptor.Height)
                {
                    throw new InvalidOperationException("Audited map descriptor no longer matches its bounded FireRed map specification: " + descriptor.Id);
                }

                specs.Add(spec);
            }

            return specs;
        }

        private static FireRedMapSpec FindSpec(string id)
        {
            for (var index = 0; index < FireRedRomLayoutRev1.AuditedMapSpecs.Count; index++)
            {
                var spec = FireRedRomLayoutRev1.AuditedMapSpecs[index];
                if (string.Equals(spec.Id, id, StringComparison.Ordinal)) return spec;
            }

            return null;
        }
    }
}
