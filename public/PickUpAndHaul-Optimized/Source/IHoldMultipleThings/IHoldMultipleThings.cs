using RimWorld;
using Verse;

namespace IHoldMultipleThings
{
    /// <summary>
    /// Interface for mods that want to provide custom capacity/stacking behavior
    /// for items stored in their containers. Reference this assembly to interact
    /// with Pick Up And Haul's inventory system.
    /// </summary>
    public interface IHoldMultipleThings
    {
        /// <summary>
        /// Returns the capacity of the storage at the given cell for the given thing.
        /// </summary>
        bool CapacityAt(Thing thing, IntVec3 storeCell, Map map, out int capacity);

        /// <summary>
        /// Returns whether the thing can be stacked at the given cell.
        /// </summary>
        bool StackableAt(Thing thing, IntVec3 storeCell, Map map);
    }
}
