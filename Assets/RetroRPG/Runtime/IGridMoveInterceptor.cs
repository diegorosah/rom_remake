namespace RetroRPG.Runtime
{
    /// <summary>
    /// Optional runtime seam for a cardinal movement command. Returning true consumes
    /// the command; returning false lets <see cref="PlayerController"/> use normal
    /// collision and movement. Implementations must not read ROM or importer data.
    /// </summary>
    public interface IGridMoveInterceptor
    {
        bool TryInterceptMove(PlayerController player, GridDirection direction);
    }
}
