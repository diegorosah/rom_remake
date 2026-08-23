using System;
using System.Collections.Generic;
using UnityEngine;

namespace RetroRPG.Runtime
{
    /// <summary>
    /// Map-local, reservation-based occupancy for moving grid actors. A participant
    /// keeps its departure cell occupied until it commits, and reserves its arrival
    /// cell before interpolating. This prevents swaps and pass-throughs without
    /// coupling actors to a specific game, NPC format, or importer.
    /// </summary>
    public sealed class MapCellOccupancy : MonoBehaviour
    {
        [SerializeField] private GridCollisionMap collisionMap;
        [SerializeField] private bool isMapActive = true;

        private readonly Dictionary<object, OccupancyRecord> participants = new Dictionary<object, OccupancyRecord>();

        public GridCollisionMap CollisionMap => collisionMap;
        public bool IsMapActive => isMapActive;
        public int ParticipantCount => participants.Count;

        public void Configure(GridCollisionMap configuredCollisionMap)
        {
            if (configuredCollisionMap == null)
            {
                throw new ArgumentNullException(nameof(configuredCollisionMap));
            }

            if (collisionMap != null && collisionMap != configuredCollisionMap && participants.Count != 0)
            {
                throw new InvalidOperationException("Cannot change an occupancy map while actors are registered.");
            }

            collisionMap = configuredCollisionMap;
        }

        public void SetMapActive(bool active)
        {
            isMapActive = active;
            if (!active)
            {
                // Inactive maps never influence a different map's actor movement.
                participants.Clear();
            }
        }

        public bool TryRegister(object participant, Vector2Int cell)
        {
            if (participant == null || !CanUseCell(cell) || !isMapActive)
            {
                return false;
            }

            if (participants.TryGetValue(participant, out OccupancyRecord existing))
            {
                return existing.CurrentCell == cell && !existing.HasReservedTarget;
            }

            if (IsClaimedByOther(participant, cell))
            {
                return false;
            }

            participants.Add(participant, new OccupancyRecord(cell));
            return true;
        }

        public void Unregister(object participant)
        {
            if (participant != null)
            {
                participants.Remove(participant);
            }
        }

        /// <summary>
        /// Atomically reserves an adjacent target. Both other actors' current cells
        /// and their in-flight targets block the request.
        /// </summary>
        public bool TryReserveMove(object participant, Vector2Int currentCell, Vector2Int targetCell)
        {
            if (participant == null || !isMapActive || !CanUseCell(currentCell) || !CanUseCell(targetCell))
            {
                return false;
            }

            if (!participants.TryGetValue(participant, out OccupancyRecord record) ||
                record.CurrentCell != currentCell || record.HasReservedTarget)
            {
                return false;
            }

            if (IsClaimedByOther(participant, targetCell))
            {
                return false;
            }

            record.ReservedTarget = targetCell;
            record.HasReservedTarget = true;
            participants[participant] = record;
            return true;
        }

        public void CommitMove(object participant, Vector2Int destinationCell)
        {
            if (participant == null || !participants.TryGetValue(participant, out OccupancyRecord record))
            {
                return;
            }

            if (!record.HasReservedTarget || record.ReservedTarget != destinationCell)
            {
                throw new InvalidOperationException("Only a participant's reserved target can be committed.");
            }

            record.CurrentCell = destinationCell;
            record.HasReservedTarget = false;
            record.ReservedTarget = default(Vector2Int);
            participants[participant] = record;
        }

        public void CancelMove(object participant)
        {
            if (participant != null && participants.TryGetValue(participant, out OccupancyRecord record))
            {
                record.HasReservedTarget = false;
                record.ReservedTarget = default(Vector2Int);
                participants[participant] = record;
            }
        }

        public bool IsOccupied(Vector2Int cell)
        {
            if (!isMapActive)
            {
                return false;
            }

            foreach (KeyValuePair<object, OccupancyRecord> pair in participants)
            {
                OccupancyRecord record = pair.Value;
                if (record.CurrentCell == cell || (record.HasReservedTarget && record.ReservedTarget == cell))
                {
                    return true;
                }
            }

            return false;
        }

        private void OnDisable()
        {
            SetMapActive(false);
        }

        private bool CanUseCell(Vector2Int cell)
        {
            return collisionMap != null && collisionMap.IsInBounds(cell);
        }

        private bool IsClaimedByOther(object participant, Vector2Int cell)
        {
            foreach (KeyValuePair<object, OccupancyRecord> pair in participants)
            {
                if (ReferenceEquals(pair.Key, participant))
                {
                    continue;
                }

                OccupancyRecord record = pair.Value;
                if (record.CurrentCell == cell || (record.HasReservedTarget && record.ReservedTarget == cell))
                {
                    return true;
                }
            }

            return false;
        }

        private struct OccupancyRecord
        {
            public OccupancyRecord(Vector2Int currentCell)
            {
                CurrentCell = currentCell;
                ReservedTarget = default(Vector2Int);
                HasReservedTarget = false;
            }

            public Vector2Int CurrentCell;
            public Vector2Int ReservedTarget;
            public bool HasReservedTarget;
        }
    }
}
