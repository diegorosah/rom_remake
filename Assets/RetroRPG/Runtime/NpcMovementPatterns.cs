using System;
using UnityEngine;

namespace RetroRPG.Runtime
{
    /// <summary>Supplies a deterministic integer stream for NPC initialization and wander decisions.</summary>
    public interface INpcRandomSource
    {
        int NextInt(int exclusiveUpperBound);
    }

    /// <summary>Optional externally-owned deterministic simulation clock.</summary>
    public interface INpcTickSource
    {
        int CurrentTick { get; }
    }

    /// <summary>Chooses a movement command without depending on Unity time or random APIs.</summary>
    public interface INpcMovementPattern
    {
        bool TryGetNextDirection(NpcController npc, int simulationTick, out GridDirection direction);
    }

    /// <summary>Pattern for a stationary NPC whose direction is controlled explicitly through Face.</summary>
    public sealed class FixedFacingNpcMovementPattern : INpcMovementPattern
    {
        public bool TryGetNextDirection(NpcController npc, int simulationTick, out GridDirection direction)
        {
            direction = GridDirection.None;
            return false;
        }
    }

    /// <summary>Deterministic, tick-spaced cardinal wander decisions.</summary>
    public sealed class DeterministicWanderNpcMovementPattern : INpcMovementPattern
    {
        private readonly int intervalTicks;
        private readonly INpcRandomSource random;

        public DeterministicWanderNpcMovementPattern(int configuredIntervalTicks, INpcRandomSource configuredRandom)
        {
            if (configuredIntervalTicks <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(configuredIntervalTicks));
            }

            intervalTicks = configuredIntervalTicks;
            random = configuredRandom ?? throw new ArgumentNullException(nameof(configuredRandom));
        }

        public bool TryGetNextDirection(NpcController npc, int simulationTick, out GridDirection direction)
        {
            direction = GridDirection.None;
            if (npc == null || simulationTick < 0 || simulationTick % intervalTicks != 0)
            {
                return false;
            }

            int index = random.NextInt(4);
            switch (index)
            {
                case 0: direction = GridDirection.Down; return true;
                case 1: direction = GridDirection.Up; return true;
                case 2: direction = GridDirection.Left; return true;
                case 3: direction = GridDirection.Right; return true;
                default: throw new InvalidOperationException("NPC random source returned a value outside [0, 4).");
            }
        }
    }

    /// <summary>Small platform-independent xorshift source for stable generated NPC behavior.</summary>
    public sealed class DeterministicNpcRandomSource : INpcRandomSource
    {
        private uint state;

        public DeterministicNpcRandomSource(uint seed)
        {
            state = seed == 0 ? 0x6D2B79F5u : seed;
        }

        public int NextInt(int exclusiveUpperBound)
        {
            if (exclusiveUpperBound <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(exclusiveUpperBound));
            }

            uint value = state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            state = value;
            return (int)(value % (uint)exclusiveUpperBound);
        }
    }
}
