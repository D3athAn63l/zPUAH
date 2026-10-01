// Pure logic: no RimWorld/Unity types, so the unit-test project can link and run it (see AllocationLedger.cs).
using System;
using System.Collections.Generic;

namespace PickUpAndHaul.Planning;

/// <summary>What the pickup loop needs from the pawn/map. The production implementation is <see cref="PickupPolicy"/>.</summary>
internal interface IPickupPolicy<TItem> where TItem : class
{
    /// <summary>Encumbrance this item adds to the pawn if it is carried whole.</summary>
    float AddedEncumbrance(TItem item);

    /// <summary>How many of <paramref name="item"/> may still be taken when the running encumbrance has passed 1.0.</summary>
    int CountPastCapacity(TItem item, float encumbrance);

    /// <summary>
    /// Takes the next candidate to consider (nearest to <paramref name="last"/>, removed from the pool), or null when there is none.
    /// </summary>
    TItem NextCandidateAfter(TItem last);
}

/// <summary>
/// The pickup loop of the original <c>JobOnThing</c>: allocate the first thing, then keep taking the next nearest candidate
/// until the pawn's inventory is full or no candidate is left.
/// </summary>
internal static class PickupSequencer
{
    /// <param name="ledger">Already holds the initial storage anchor.</param>
    /// <param name="first">The thing the work giver was asked about; always considered first.</param>
    /// <param name="startingEncumbrance"><c>MassUtility.EncumbrancePercent(pawn)</c> before anything is picked up.</param>
    /// <param name="overweight">Combat Extended says the pawn is overweight already (currently always false, see CompatHelper).</param>
    public static void Run<TTarget, TItem>(AllocationLedger<TTarget, TItem> ledger, TItem first, float startingEncumbrance,
        bool overweight, IPickupPolicy<TItem> policy) where TItem : class
    {
        var encumbrance = startingEncumbrance;
        var nextThing = first;
        var lastThing = first;

        do
        {
            if (ledger.Allocate(nextThing))
            {
                lastThing = nextThing;
                encumbrance += policy.AddedEncumbrance(nextThing);

                if (encumbrance > 1 || overweight)
                {
                    // can't CountToPickUpUntilOverEncumbered here, pawn doesn't actually hold these things yet
                    var nextThingLeftOverCount = policy.CountPastCapacity(nextThing, encumbrance);
                    ledger.ReplaceLastCount(nextThingLeftOverCount);

                    // We are now out of inventory space - and should bail right away.
                    break;
                }
            }
        }
        while ((nextThing = policy.NextCandidateAfter(lastThing)) != null);
    }
}
