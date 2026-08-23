using RetroRPG.IR;

namespace RetroRPG.Importers.GBA.PokemonFireRed
{
    public static class FireRedMapEncoding
    {
        public static MapCellDefinition DecodeMapCell(ushort value)
        {
            return new MapCellDefinition(value & 0x03FF, (value >> 10) & 0x3, (value >> 12) & 0xF);
        }

        public static SubtileDefinition DecodeSubtile(ushort value)
        {
            return new SubtileDefinition(value & 0x03FF, (value >> 12) & 0xF, (value & 0x0400) != 0, (value & 0x0800) != 0);
        }

        public static MetatileLayerRoute DecodeLayerRoute(uint attributes)
        {
            switch ((attributes >> 29) & 0x3)
            {
                case 0: return new MetatileLayerRoute(RenderLayer.Middle, RenderLayer.Top);
                case 1: return new MetatileLayerRoute(RenderLayer.Bottom, RenderLayer.Middle);
                case 2: return new MetatileLayerRoute(RenderLayer.Bottom, RenderLayer.Top);
                default: return new MetatileLayerRoute(RenderLayer.Invalid, RenderLayer.Invalid);
            }
        }
    }
}
