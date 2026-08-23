using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RetroRPG.Runtime
{
    /// <summary>
    /// A grid actor that performs one exact cardinal cell step per accepted move.
    /// Runtime movement is intentionally independent from ROM and importer data.
    /// </summary>
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private GridCollisionMap collisionMap;
        [SerializeField, Min(0.01f)] private float cellsPerSecond = 4f;
        [SerializeField] private bool inputEnabled = true;
        [SerializeField] private DirectionalSpriteAnimator spriteAnimator;
        [SerializeField] private MapCellOccupancy occupancy;
        [SerializeField] private Vector2Int startingCell;
        [SerializeField] private byte startingElevation;

        private Vector2Int currentCell;
        private Vector2Int targetCell;
        private byte elevation;
        private byte targetElevation;
        private GridDirection facing = GridDirection.Down;
        private bool isMoving;
        private bool isConfigured;
        private float moveProgress;
        private IGridMoveInterceptor[] moveInterceptors = Array.Empty<IGridMoveInterceptor>();

        /// <summary>Raised after an accepted grid step has reached its destination.</summary>
        public event Action<PlayerController> MovementCompleted;

        public GridCollisionMap CollisionMap => collisionMap;
        public DirectionalSpriteAnimator SpriteAnimator => spriteAnimator;
        public MapCellOccupancy Occupancy => occupancy;
        public float CellsPerSecond
        {
            get => cellsPerSecond;
            set
            {
                ValidateCellsPerSecond(value, nameof(value));
                cellsPerSecond = value;
            }
        }

        public Vector2Int CurrentCell => currentCell;
        public Vector2Int ReservedCell => isMoving ? targetCell : currentCell;
        public byte Elevation => elevation;
        public GridDirection Facing => facing;
        public bool IsMoving => isMoving;
        public bool InputEnabled
        {
            get => inputEnabled;
            set => inputEnabled = value;
        }

        /// <summary>
        /// Replaces the deterministic movement-interception chain. Interceptors run
        /// after facing is updated and before ordinary grid collision is evaluated.
        /// This deliberately accepts interfaces rather than importer or map types so
        /// runtime systems such as warps remain game-agnostic.
        /// </summary>
        public void SetMoveInterceptors(params IGridMoveInterceptor[] interceptors)
        {
            if (interceptors == null || interceptors.Length == 0)
            {
                moveInterceptors = Array.Empty<IGridMoveInterceptor>();
                return;
            }

            int validCount = 0;
            for (int index = 0; index < interceptors.Length; index++)
            {
                if (interceptors[index] != null)
                {
                    validCount++;
                }
            }

            if (validCount == 0)
            {
                moveInterceptors = Array.Empty<IGridMoveInterceptor>();
                return;
            }

            var configured = new IGridMoveInterceptor[validCount];
            int destination = 0;
            for (int index = 0; index < interceptors.Length; index++)
            {
                IGridMoveInterceptor interceptor = interceptors[index];
                if (interceptor != null)
                {
                    configured[destination++] = interceptor;
                }
            }

            moveInterceptors = configured;
        }

        public void Configure(GridCollisionMap map, Vector2Int initialCell, byte initialElevation)
        {
            Configure(map, initialCell, initialElevation, occupancy);
        }

        public void Configure(
            GridCollisionMap map,
            Vector2Int initialCell,
            byte initialElevation,
            MapCellOccupancy configuredOccupancy)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (!map.IsInBounds(initialCell))
            {
                throw new ArgumentOutOfRangeException(nameof(initialCell), initialCell, "Initial cell is outside the collision map.");
            }

            ValidateCellsPerSecond(cellsPerSecond, nameof(cellsPerSecond));

            if (map.GetCollision(initialCell) != 0)
            {
                throw new ArgumentException("Initial cell is blocked by collision data.", nameof(initialCell));
            }

            byte cellElevation = map.GetElevation(initialCell);
            if (cellElevation != 0 && cellElevation != 15 && initialElevation != cellElevation)
            {
                throw new ArgumentException(
                    "Initial elevation must match a non-pass-through starting cell elevation.",
                    nameof(initialElevation));
            }

            ValidateOccupancyMap(map, configuredOccupancy);
            CancelPendingMove();
            ReleaseOccupancy();
            collisionMap = map;
            occupancy = configuredOccupancy;
            if (spriteAnimator == null)
            {
                spriteAnimator = GetComponent<DirectionalSpriteAnimator>();
            }
            startingCell = initialCell;
            startingElevation = initialElevation;
            currentCell = initialCell;
            targetCell = initialCell;
            elevation = initialElevation;
            targetElevation = initialElevation;
            isMoving = false;
            moveProgress = 0f;
            isConfigured = true;
            transform.position = collisionMap.CellCenter(currentCell);
            RegisterOccupancyOrThrow();
            SynchronizeAnimator();
        }

        public void Configure(GridCollisionMap map, Vector2Int initialCell, byte initialElevation, float configuredCellsPerSecond)
        {
            CellsPerSecond = configuredCellsPerSecond;
            Configure(map, initialCell, initialElevation);
        }

        public void Configure(
            GridCollisionMap map,
            Vector2Int initialCell,
            byte initialElevation,
            float configuredCellsPerSecond,
            MapCellOccupancy configuredOccupancy)
        {
            CellsPerSecond = configuredCellsPerSecond;
            Configure(map, initialCell, initialElevation, configuredOccupancy);
        }

        public void SetOccupancy(MapCellOccupancy configuredOccupancy)
        {
            ValidateOccupancyMap(collisionMap, configuredOccupancy);
            if (occupancy == configuredOccupancy)
            {
                return;
            }

            CancelPendingMove();
            ReleaseOccupancy();
            occupancy = configuredOccupancy;
            if (isConfigured)
            {
                RegisterOccupancyOrThrow();
            }
        }

        public bool TryMove(GridDirection direction)
        {
            if (!GridDirections.IsCardinal(direction) || !isConfigured || isMoving)
            {
                return false;
            }

            // A failed cardinal request still changes the facing used by the idle sprite.
            facing = direction;
            if (TryInterceptMove(direction))
            {
                SynchronizeAnimator();
                return false;
            }

            if (!collisionMap.CanMove(currentCell, elevation, direction, out Vector2Int nextCell, out byte nextElevation))
            {
                SynchronizeAnimator();
                return false;
            }

            if (occupancy != null && !occupancy.TryReserveMove(this, currentCell, nextCell))
            {
                SynchronizeAnimator();
                return false;
            }

            targetCell = nextCell;
            targetElevation = nextElevation;
            moveProgress = 0f;
            isMoving = true;
            SynchronizeAnimator();
            return true;
        }

        /// <summary>
        /// Rebinds this actor after a map transition. Unlike <see cref="Configure"/>,
        /// the destination may intentionally be a collision-marked doorway cell.
        /// </summary>
        public void PlaceAfterTransition(
            GridCollisionMap map,
            Vector2Int destinationCell,
            byte destinationElevation,
            GridDirection destinationFacing)
        {
            PlaceAfterTransition(map, destinationCell, destinationElevation, destinationFacing, occupancy);
        }

        public void PlaceAfterTransition(
            GridCollisionMap map,
            Vector2Int destinationCell,
            byte destinationElevation,
            GridDirection destinationFacing,
            MapCellOccupancy destinationOccupancy)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (!map.IsInBounds(destinationCell))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(destinationCell), destinationCell, "Transition destination is outside the collision map.");
            }

            if (!GridDirections.IsCardinal(destinationFacing))
            {
                throw new ArgumentOutOfRangeException(nameof(destinationFacing), "Transition facing must be cardinal.");
            }

            ValidateOccupancyMap(map, destinationOccupancy);
            CancelPendingMove();
            ReleaseOccupancy();
            collisionMap = map;
            occupancy = destinationOccupancy;
            if (spriteAnimator == null)
            {
                spriteAnimator = GetComponent<DirectionalSpriteAnimator>();
            }

            currentCell = destinationCell;
            targetCell = destinationCell;
            elevation = destinationElevation;
            targetElevation = destinationElevation;
            facing = destinationFacing;
            isMoving = false;
            moveProgress = 0f;
            isConfigured = true;
            transform.position = collisionMap.CellCenter(currentCell);
            RegisterOccupancyOrThrow();
            SynchronizeAnimator();
        }

        public void PlaceAfterTransition(GridCollisionMap map, Vector2Int destinationCell, byte destinationElevation)
        {
            PlaceAfterTransition(map, destinationCell, destinationElevation, facing);
        }

        /// <summary>Advances one fixed 60 Hz movement tick.</summary>
        public void Tick()
        {
            Advance(1f / DirectionalSpriteAnimator.TickRate);
        }

        /// <summary>Abandons an in-flight step and releases its destination reservation.</summary>
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

        /// <summary>Advances interpolation without accepting additional input.</summary>
        public void Advance(float deltaSeconds)
        {
            if (deltaSeconds <= 0f)
            {
                return;
            }

            if (isMoving)
            {
                moveProgress = Mathf.Min(1f, moveProgress + deltaSeconds * cellsPerSecond);
                Vector3 from = collisionMap.CellCenter(currentCell);
                Vector3 to = collisionMap.CellCenter(targetCell);
                transform.position = Vector3.LerpUnclamped(from, to, moveProgress);

                if (moveProgress >= 1f)
                {
                    currentCell = targetCell;
                    elevation = targetElevation;
                    occupancy?.CommitMove(this, currentCell);
                    isMoving = false;
                    moveProgress = 0f;
                    transform.position = collisionMap.CellCenter(currentCell);
                    SynchronizeAnimator();
                    MovementCompleted?.Invoke(this);
                }
            }

            if (spriteAnimator != null)
            {
                spriteAnimator.Advance(deltaSeconds);
            }
        }

        private void Awake()
        {
            if (collisionMap != null)
            {
                Configure(collisionMap, startingCell, startingElevation);
            }
        }

        private void OnDestroy()
        {
            ReleaseOccupancy();
        }

        private void Update()
        {
            if (inputEnabled && !isMoving)
            {
                TryReadKeyboardMove();
            }

            Advance(Time.deltaTime);
        }

        private void TryReadKeyboardMove()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            // Priority produces one cardinal command even while two keys are held.
            if (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame)
            {
                TryMove(GridDirection.Up);
            }
            else if (keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame)
            {
                TryMove(GridDirection.Down);
            }
            else if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame)
            {
                TryMove(GridDirection.Left);
            }
            else if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame)
            {
                TryMove(GridDirection.Right);
            }
        }

        private void SynchronizeAnimator()
        {
            if (spriteAnimator != null)
            {
                spriteAnimator.SetState(facing, isMoving);
            }
        }

        private bool TryInterceptMove(GridDirection direction)
        {
            for (int index = 0; index < moveInterceptors.Length; index++)
            {
                IGridMoveInterceptor interceptor = moveInterceptors[index];
                if (interceptor != null && interceptor.TryInterceptMove(this, direction))
                {
                    return true;
                }
            }

            return false;
        }

        private void RegisterOccupancyOrThrow()
        {
            if (occupancy != null && !occupancy.TryRegister(this, currentCell))
            {
                throw new InvalidOperationException("Player starting cell is already occupied or its map is inactive.");
            }
        }

        private void ReleaseOccupancy()
        {
            if (occupancy != null)
            {
                occupancy.Unregister(this);
            }
        }

        private static void ValidateOccupancyMap(GridCollisionMap map, MapCellOccupancy configuredOccupancy)
        {
            if (configuredOccupancy != null && map != null && configuredOccupancy.CollisionMap != null &&
                configuredOccupancy.CollisionMap != map)
            {
                throw new ArgumentException("Occupancy must belong to the same collision map.", nameof(configuredOccupancy));
            }
        }

        private static void ValidateCellsPerSecond(float value, string parameterName)
        {
            if (value <= 0f || float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, "Cells per second must be finite and positive.");
            }
        }
    }
}
