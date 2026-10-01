// Pure logic: no RimWorld/Unity types, so the unit-test project can link and run it (see AllocationLedger.cs).
using System;

namespace PickUpAndHaul.Planning;

/// <summary>
/// The two capacity formulas of the original work giver, with the RimWorld lookups (stack count, mass stat, pawn capacity)
/// passed in. The expressions and their evaluation order are unchanged on purpose: this is NOT a better capacity model.
/// </summary>
internal static class CapacityMath
{
    /// <summary>Encumbrance added by carrying a whole stack: <c>stackCount * mass / capacity</c>.</summary>
    public static float AddedEncumbrance(int stackCount, float massPerItem, float pawnCapacity)
        => stackCount * massPerItem / pawnCapacity;

    /// <summary>How many items of one stack are still allowed once the running encumbrance has passed 1.0.</summary>
    public static int CountPastCapacity(float encumbrance, float pawnCapacity, float massPerItem)
        => (int)Math.Ceiling((encumbrance - 1) * pawnCapacity / massPerItem);
}
