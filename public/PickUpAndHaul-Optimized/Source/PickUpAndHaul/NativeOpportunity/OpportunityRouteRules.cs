// Pure logic: no RimWorld/Unity type, so the unit-test project can link and run it (see PickUpAndHaul.Tests.csproj).
using System;

namespace PickUpAndHaul.NativeOpportunity;

/// <summary>
/// "Is this still a reasonably on-the-way detour?" for ONE opportunity item, as plain float arithmetic over the four distances of
/// the trip:
/// <code>
/// originalTrip = start -> originalJob                          (what the pawn was going to walk anyway)
/// newLegs      = start -> thing  +  store -> originalJob       (the legs the detour adds)
/// totalDetour  = start -> thing  +  thing -> store  +  store -> originalJob
/// </code>
/// The limits are the Phase 1 defaults (they equal what vanilla's own opportunistic hauling uses); the settings UI does not expose
/// them. Every comparison is "reject if strictly greater", so a value exactly ON a limit is accepted and one float step beyond it is
/// rejected; the limits themselves are computed as <c>originalTrip * ratio</c> in <c>float</c>, in this order, with no tolerance.
/// </summary>
/// <remarks>
/// Nothing here divides: ratios are applied by multiplication, so a zero or tiny original trip cannot turn into an infinite or NaN
/// ratio that accepts everything. Trips shorter than <see cref="MinOriginalTrip"/> are never worth a detour, and non-finite or
/// negative distances are rejected outright.
/// </remarks>
internal static class OpportunityRouteRules
{
    /// <summary>Largest absolute start -> thing distance, in cells.</summary>
    public const float MaxStartToThing = 30f;

    /// <summary>Largest start -> thing distance as a fraction of the original trip.</summary>
    public const float MaxStartToThingRatio = 0.50f;

    /// <summary>Largest absolute store -> original job distance, in cells.</summary>
    public const float MaxStoreToJob = 50f;

    /// <summary>Largest store -> original job distance as a fraction of the original trip.</summary>
    public const float MaxStoreToJobRatio = 0.60f;

    /// <summary>Largest (start -> thing + store -> job) as a fraction of the original trip.</summary>
    public const float MaxNewLegsRatio = 1.00f;

    /// <summary>Largest (start -> thing -> store -> job) as a fraction of the original trip.</summary>
    public const float MaxTotalTripRatio = 1.70f;

    /// <summary>An original trip shorter than this (the pawn is practically there already) is never worth a detour.</summary>
    public const float MinOriginalTrip = 3f;

    private static bool IsUsable(float distance) => !float.IsNaN(distance) && !float.IsInfinity(distance) && distance >= 0f;

    /// <summary>Is the pawn far enough from where it is going for a detour to make sense at all?</summary>
    public static bool OriginalTripWorthConsidering(float originalTrip) => IsUsable(originalTrip) && originalTrip >= MinOriginalTrip;

    /// <summary>The start -> thing leg alone: absolute cap and fraction of the original trip.</summary>
    public static bool ThingLegAcceptable(float originalTrip, float startToThing)
        => OriginalTripWorthConsidering(originalTrip)
        && IsUsable(startToThing)
        && startToThing <= MaxStartToThing
        && startToThing <= originalTrip * MaxStartToThingRatio;

    /// <summary>
    /// A cheap necessary condition that needs no storage lookup: even if the store were ON the straight line from the thing to the
    /// job, start -> thing -> job would already have to fit the total-trip limit. (Triangle inequality: thing -> store -> job is never
    /// shorter than thing -> job, so this never rejects a route that <see cref="RouteAcceptable"/> accepts.)
    /// </summary>
    public static bool ThingCouldStillFit(float originalTrip, float startToThing, float thingToJob)
        => OriginalTripWorthConsidering(originalTrip)
        && IsUsable(startToThing)
        && IsUsable(thingToJob)
        && startToThing + thingToJob <= originalTrip * MaxTotalTripRatio;

    /// <summary>The store -> original job leg alone: absolute cap and fraction of the original trip.</summary>
    public static bool StoreLegAcceptable(float originalTrip, float storeToJob)
        => OriginalTripWorthConsidering(originalTrip)
        && IsUsable(storeToJob)
        && storeToJob <= MaxStoreToJob
        && storeToJob <= originalTrip * MaxStoreToJobRatio;

    /// <summary>The whole route start -> thing -> store -> original destination against every limit.</summary>
    public static bool RouteAcceptable(float originalTrip, float startToThing, float thingToStore, float storeToJob)
        => ThingLegAcceptable(originalTrip, startToThing)
        && StoreLegAcceptable(originalTrip, storeToJob)
        && IsUsable(thingToStore)
        && startToThing + storeToJob <= originalTrip * MaxNewLegsRatio
        && startToThing + thingToStore + storeToJob <= originalTrip * MaxTotalTripRatio;
}
