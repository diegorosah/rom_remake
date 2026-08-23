using System;
using UnityEngine;

namespace RetroRPG.Runtime
{
    /// <summary>
    /// A game-agnostic grid NPC. It owns no dialogue, script, ROM, or importer data;
    /// those systems may safely call <see cref="Face"/> later without moving the NPC.
    /// </summary>
    public sealed class NpcController : MonoBehaviour
    {
        [SerializeField] private string npcId;
        [SerializeField] private GridCollisionMap collisionMap;
        [SerializeField] private MapCellOccupancy occupancy;
        [SerializeField] private DirectionalSpriteAnimator spriteAnimator;
        [SerializeField, Min(0.01f)] private float cellsPerSecond = 4f;
        [SerializeField] private Vector2Int startingCell;
        [SerializeField] private byte startingElevation;
        [SerializeField] private Vector2Int minimumMovementCell;
        [SerializeField] private Vector2Int maximumMovementCell;
        [SerializeField] private bool hasConfiguredMovementBounds;
        [SerializeField] private bool isVisible = true;

        private Vector2Int currentCell;
        private Vector2Int targetCell;
        private byte elevation;
        private byte targetElevation;
        private GridDirection facing = GridDirection.Down;
        private bool isMoving;
        private bool isConfigured;
        private bool isMapActive = true;
        private float moveProgress;
        private int simulationTick;
        private bool hasSimulationTick;
        private INpcMovementPattern movementPattern = new FixedFacingNpcMovementPattern();
        private INpcTickSource tickSource;

        public string NpcId => npcId;
        public GridCollisionMap CollisionMap => collisionMap;
        public MapCellOccupancy Occupancy => occupancy;
        public DirectionalSpriteAnimator SpriteAnimator => spriteAnimator;
        public Vector2Int CurrentCell => currentCell;
        public Vector2Int ReservedCell => isMoving ? targetCell : currentCell;
        public byte Elevation => elevation;
        public GridDirection Facing => facing;
        public bool IsMoving => isMoving;
        public bool IsVisible => isVisible;
        public bool IsMapActive => isMapActive;
        public int SimulationTick => simulationTick;
        public Vector2Int MinimumMovementCell => minimumMovementCell;
        public Vector2Int MaximumMovementCell => maximumMovementCell;

        public void Configure(
            string configuredNpcId,
            GridCollisionMap configuredMap,
            Vector2Int initialCell,
            byte initialElevation,
            DirectionalSpriteAnimator configuredAnimator = null,
            MapCellOccupancy configuredOccupancy = null,
            float configuredCellsPerSecond = 4f)
        {
            if (string.IsNullOrWhiteSpace(configuredNpcId))
            {
                throw new ArgumentException("NPC ID is required.", nameof(configuredNpcId));
            }

            if (configuredMap == null)
            {
                throw new ArgumentNullException(nameof(configuredMap));
            }

            if (!configuredMap.IsInBounds(initialCell) || configuredMap.GetCollision(initialCell) != 0)
            {
                throw new ArgumentException("NPC initial cell must be in bounds and passable.", nameof(initialCell));
            }

            if (configuredCellsPerSecond <= 0f || float.IsNaN(configuredCellsPerSecond) || float.IsInfinity(configuredCellsPerSecond))
            {
                throw new ArgumentOutOfRangeException(nameof(configuredCellsPerSecond));
            }

            byte mapElevation = configuredMap.GetElevation(initialCell);
            if (mapElevation != 0 && mapElevation != 15 && initialElevation != mapElevation)
            {
                throw new ArgumentException("NPC initial elevation must match its non-pass-through cell.", nameof(initialElevation));
            }

            ValidateOccupancyMap(configuredMap, configuredOccupancy);
            CancelPendingMove();
            ReleaseOccupancy();
            npcId = configuredNpcId;
            collisionMap = configuredMap;
            occupancy = configuredOccupancy;
            spriteAnimator = configuredAnimator == null ? GetComponent<DirectionalSpriteAnimator>() : configuredAnimator;
            cellsPerSecond = configuredCellsPerSecond;
            startingCell = initialCell;
            startingElevation = initialElevation;
            minimumMovementCell = initialCell;
            maximumMovementCell = initialCell;
            hasConfiguredMovementBounds = false;
            currentCell = initialCell;
            targetCell = initialCell;
            elevation = initialElevation;
            targetElevation = initialElevation;
            isMoving = false;
            moveProgress = 0f;
            simulationTick = 0;
            hasSimulationTick = false;
            isConfigured = true;
            transform.position = collisionMap.CellCenter(currentCell);
            RegisterOccupancyOrThrow();
            SynchronizeAnimator();
        }

        /// <summary>Samples both coordinate ranges inclusively using the supplied deterministic source.</summary>
        public void ConfigureFromInclusiveRange(
            string configuredNpcId,
            GridCollisionMap configuredMap,
            Vector2Int minimumCell,
            Vector2Int maximumCell,
            byte initialElevation,
            INpcRandomSource random,
            DirectionalSpriteAnimator configuredAnimator = null,
            MapCellOccupancy configuredOccupancy = null,
            float configuredCellsPerSecond = 4f)
        {
            Vector2Int initialCell = ChooseInclusiveCell(configuredMap, minimumCell, maximumCell, random);
            Configure(
                configuredNpcId,
                configuredMap,
                initialCell,
                initialElevation,
                configuredAnimator,
                configuredOccupancy,
                configuredCellsPerSecond);
            ConfigureMovementBounds(minimumCell, maximumCell);
        }

        /// <summary>
        /// Sets an inclusive movement rectangle. Every ordinary move, including
        /// deterministic wandering, remains inside these bounds after spawn.
        /// </summary>
        public void ConfigureMovementBounds(Vector2Int minimumCell, Vector2Int maximumCell)
        {
            if (collisionMap == null || minimumCell.x > maximumCell.x || minimumCell.y > maximumCell.y ||
                !collisionMap.IsInBounds(minimumCell) || !collisionMap.IsInBounds(maximumCell) ||
                !IsInsideInclusive(currentCell, minimumCell, maximumCell))
            {
                throw new ArgumentOutOfRangeException(nameof(maximumCell), "NPC movement bounds must be inclusive, ordered, and contain its current cell.");
            }

            minimumMovementCell = minimumCell;
            maximumMovementCell = maximumCell;
            hasConfiguredMovementBounds = true;
        }

        public void SetMovementPattern(INpcMovementPattern configuredPattern)
        {
            movementPattern = configuredPattern ?? throw new ArgumentNullException(nameof(configuredPattern));
        }

        public void SetTickSource(INpcTickSource configuredTickSource)
        {
            tickSource = configuredTickSource;
        }

        public void SetRuntimeActive(bool active)
        {
            isMapActive = active;
            if (!active)
            {
                CancelPendingMove();
            }

            RefreshOccupancy();
        }

        public void SetVisible(bool visible)
        {
            isVisible = visible;
            if (!visible)
            {
                CancelPendingMove();
            }

            if (spriteAnimator == null)
            {
                spriteAnimator = GetComponent<DirectionalSpriteAnimator>();
            }

            if (spriteAnimator != null && spriteAnimator.SpriteRenderer != null)
            {
                spriteAnimator.SpriteRenderer.enabled = visible;
            }

            RefreshOccupancy();
        }

        /// <summary>Changes an NPC's idle/walking-facing sprite without changing cells.</summary>
        public bool Face(GridDirection direction)
        {
            if (!GridDirections.IsCardinal(direction))
            {
                return false;
            }

            facing = direction;
            SynchronizeAnimator();
            return true;
        }

        public bool TryMove(GridDirection direction)
        {
            if (!GridDirections.IsCardinal(direction) || !isConfigured || !isMapActive || !isVisible || isMoving)
            {
                return false;
            }

            Face(direction);
            if (!collisionMap.CanMove(currentCell, elevation, direction, out Vector2Int nextCell, out byte nextElevation))
            {
                return false;
            }

            if (!IsWithinMovementBounds(nextCell))
            {
                return false;
            }

            if (occupancy != null && !occupancy.TryReserveMove(this, currentCell, nextCell))
            {
                return false;
            }

            targetCell = nextCell;
            targetElevation = nextElevation;
            moveProgress = 0f;
            isMoving = true;
            SynchronizeAnimator();
            return true;
        }

        /// <summary>Advances an explicit deterministic NPC tick and asks its pattern when idle.</summary>
        public void Tick()
        {
            Tick(simulationTick + 1);
        }

        public void Tick(int configuredSimulationTick)
        {
            if (configuredSimulationTick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(configuredSimulationTick));
            }

            if (hasSimulationTick && configuredSimulationTick <= simulationTick)
            {
                return;
            }

            simulationTick = configuredSimulationTick;
            hasSimulationTick = true;
            if (isConfigured && isMapActive && isVisible && !isMoving &&
                movementPattern.TryGetNextDirection(this, simulationTick, out GridDirection direction))
            {
                TryMove(direction);
            }

            Advance(1f / DirectionalSpriteAnimator.TickRate);
        }

        public void TickFromSource()
        {
            if (tickSource == null)
            {
                throw new InvalidOperationException("An NPC tick source must be configured before calling TickFromSource.");
            }

            Tick(tickSource.CurrentTick);
        }

        /// <summary>Stops interpolation at the current cell and releases any target reservation.</summary>
        public void CancelPendingMove()
        {
            if (!isMoving)
            {
                return;
            }

            occupancy?.CancelMove(this);
            targetCell = currentCell;
            targetElevation = elevation;
            isMoving = false;
            moveProgress = 0f;
            if (collisionMap != null && collisionMap.IsInBounds(currentCell))
            {
                transform.position = collisionMap.CellCenter(currentCell);
            }

            SynchronizeAnimator();
        }

        public void Advance(float deltaSeconds)
        {
            if (deltaSeconds <= 0f || !isConfigured)
            {
                return;
            }

            if (isMoving)
            {
                moveProgress = Mathf.Min(1f, moveProgress + deltaSeconds * cellsPerSecond);
                transform.position = Vector3.LerpUnclamped(
                    collisionMap.CellCenter(currentCell),
                    collisionMap.CellCenter(targetCell),
                    moveProgress);

                if (moveProgress >= 1f)
                {
                    currentCell = targetCell;
                    elevation = targetElevation;
                    occupancy?.CommitMove(this, currentCell);
                    isMoving = false;
                    moveProgress = 0f;
                    transform.position = collisionMap.CellCenter(currentCell);
                    SynchronizeAnimator();
                }
            }

            if (spriteAnimator != null)
            {
                spriteAnimator.Advance(deltaSeconds);
            }
        }

        private void Awake()
        {
            if (collisionMap != null && !string.IsNullOrWhiteSpace(npcId))
            {
                Vector2Int savedMinimum = minimumMovementCell;
                Vector2Int savedMaximum = maximumMovementCell;
                bool restoreMovementBounds = hasConfiguredMovementBounds;
                Configure(npcId, collisionMap, startingCell, startingElevation, spriteAnimator, occupancy, cellsPerSecond);
                if (restoreMovementBounds)
                {
                    ConfigureMovementBounds(savedMinimum, savedMaximum);
                }
            }
        }

        private void OnEnable()
        {
            RefreshOccupancy();
        }

        private void OnDisable()
        {
            CancelPendingMove();
            ReleaseOccupancy();
        }

        private void OnDestroy()
        {
            ReleaseOccupancy();
        }

        private void SynchronizeAnimator()
        {
            if (spriteAnimator != null)
            {
                spriteAnimator.SetState(facing, isMoving);
            }
        }

        private void RefreshOccupancy()
        {
            if (!isConfigured)
            {
                return;
            }

            if (isMapActive && isVisible && isActiveAndEnabled)
            {
                RegisterOccupancyOrThrow();
            }
            else
            {
                ReleaseOccupancy();
            }
        }

        private void RegisterOccupancyOrThrow()
        {
            if (occupancy != null && isMapActive && isVisible && isActiveAndEnabled &&
                !occupancy.TryRegister(this, currentCell))
            {
                throw new InvalidOperationException("NPC starting cell is already occupied or its map is inactive.");
            }
        }

        private void ReleaseOccupancy()
        {
            if (occupancy != null)
            {
                occupancy.Unregister(this);
            }
        }

        private static Vector2Int ChooseInclusiveCell(
            GridCollisionMap map,
            Vector2Int minimumCell,
            Vector2Int maximumCell,
            INpcRandomSource random)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            if (minimumCell.x > maximumCell.x || minimumCell.y > maximumCell.y ||
                !map.IsInBounds(minimumCell) || !map.IsInBounds(maximumCell))
            {
                throw new ArgumentOutOfRangeException(nameof(maximumCell), "Inclusive NPC range must be ordered and inside the map.");
            }

            int width = checked(maximumCell.x - minimumCell.x + 1);
            int height = checked(maximumCell.y - minimumCell.y + 1);
            int xOffset = random.NextInt(width);
            int yOffset = random.NextInt(height);
            if (xOffset < 0 || xOffset >= width || yOffset < 0 || yOffset >= height)
            {
                throw new InvalidOperationException("NPC random source returned a value outside the requested inclusive range.");
            }

            return new Vector2Int(minimumCell.x + xOffset, minimumCell.y + yOffset);
        }

        private bool IsWithinMovementBounds(Vector2Int cell)
        {
            return IsInsideInclusive(cell, minimumMovementCell, maximumMovementCell);
        }

        private static bool IsInsideInclusive(Vector2Int cell, Vector2Int minimumCell, Vector2Int maximumCell)
        {
            return cell.x >= minimumCell.x && cell.x <= maximumCell.x &&
                   cell.y >= minimumCell.y && cell.y <= maximumCell.y;
        }

        private static void ValidateOccupancyMap(GridCollisionMap map, MapCellOccupancy configuredOccupancy)
        {
            if (configuredOccupancy != null && configuredOccupancy.CollisionMap != null &&
                configuredOccupancy.CollisionMap != map)
            {
                throw new ArgumentException("Occupancy must belong to the same collision map.", nameof(configuredOccupancy));
            }
        }
    }
}
