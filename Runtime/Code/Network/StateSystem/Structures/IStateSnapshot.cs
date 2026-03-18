using Code.Player.Character.Net;

namespace Code.Network.StateSystem.Structures
{
    /**
     * Interface for state snapshots when using a networked state system.
     * Allows snapshot implementations to be either classes or structs.
     */
    public interface IStateSnapshot {
        int lastProcessedCommand { get; set; }
        double time { get; set; }
        int tick { get; set; }
        
        bool Compare<TSystem, TState, TDiff, TInput>(
            NetworkedStateSystem<TSystem, TState, TDiff, TInput> system, TState snapshot)
            where TState : struct, IStateSnapshot
            where TDiff : StateDiff
            where TInput : InputCommand
            where TSystem : NetworkedStateSystem<TSystem, TState, TDiff, TInput>;
        
        StateDiff CreateDiff<TState>(TState snapshot) where TState : IStateSnapshot;
        
        /// <summary>
        /// Applies a diff to this snapshot and returns the new resulting state.
        /// Returns null if the diff cannot be correctly applied (e.g. CRC failure).
        /// </summary>
        IStateSnapshot ApplyDiff(StateDiff diff);
        
        object Clone();
    }
}