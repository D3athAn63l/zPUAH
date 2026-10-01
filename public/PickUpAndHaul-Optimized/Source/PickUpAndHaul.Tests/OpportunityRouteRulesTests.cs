using System;
using Xunit;
using PickUpAndHaul.NativeOpportunity;

namespace PickUpAndHaul.Tests;

/// <summary>
/// The Phase 1 route math at its exact boundaries. A value ON a limit is accepted and one float step beyond is rejected; there is no
/// tolerance (the limits are computed as <c>originalTrip * ratio</c> in float, in the same order as the code under test).
/// </summary>
public class OpportunityRouteRulesTests
{
    private static float Above(float x) => MathF.BitIncrement(x);

    // A comfortably acceptable baseline: trip 100; thing 10 away; store 20 from the thing and 40 from the job.
    private const float Trip = 100f;

    [Fact]
    public void TheDefaultsAreTheDocumentedPhase1Limits()
    {
        Assert.Equal(30f, OpportunityRouteRules.MaxStartToThing);
        Assert.Equal(0.50f, OpportunityRouteRules.MaxStartToThingRatio);
        Assert.Equal(50f, OpportunityRouteRules.MaxStoreToJob);
        Assert.Equal(0.60f, OpportunityRouteRules.MaxStoreToJobRatio);
        Assert.Equal(1.00f, OpportunityRouteRules.MaxNewLegsRatio);
        Assert.Equal(1.70f, OpportunityRouteRules.MaxTotalTripRatio);
        Assert.True(OpportunityRouteRules.RouteAcceptable(Trip, 10f, 20f, 40f));
    }

    [Fact]
    public void StartToThingAbsoluteLimitIs30()
    {
        // trip 100 (ratio limit 50): only the absolute limit binds
        Assert.True(OpportunityRouteRules.ThingLegAcceptable(Trip, 30f));
        Assert.False(OpportunityRouteRules.ThingLegAcceptable(Trip, Above(30f)));
        Assert.True(OpportunityRouteRules.RouteAcceptable(Trip, 30f, 10f, 40f));
        Assert.False(OpportunityRouteRules.RouteAcceptable(Trip, Above(30f), 10f, 40f));
    }

    [Fact]
    public void StartToThingRatioLimitIsHalfTheOriginalTrip()
    {
        // trip 40: ratio limit 20 (the absolute limit does not bind); 0.5 is exactly representable
        Assert.True(OpportunityRouteRules.ThingLegAcceptable(40f, 20f));
        Assert.False(OpportunityRouteRules.ThingLegAcceptable(40f, Above(20f)));
        // trip 70: ratio limit 35, so the absolute 30 binds first
        Assert.True(OpportunityRouteRules.ThingLegAcceptable(70f, 30f));
        Assert.False(OpportunityRouteRules.ThingLegAcceptable(70f, Above(30f)));
    }

    [Fact]
    public void StoreToJobAbsoluteLimitIs50()
    {
        // trip 200 (ratio limit 120): only the absolute limit binds
        Assert.True(OpportunityRouteRules.StoreLegAcceptable(200f, 50f));
        Assert.False(OpportunityRouteRules.StoreLegAcceptable(200f, Above(50f)));
    }

    [Fact]
    public void StoreToJobRatioLimitIs0Point6OfTheOriginalTrip()
    {
        // trip 50: ratio limit 50 * 0.6f (the absolute limit does not bind)
        var limit = 50f * OpportunityRouteRules.MaxStoreToJobRatio;
        Assert.True(OpportunityRouteRules.StoreLegAcceptable(50f, limit));
        Assert.False(OpportunityRouteRules.StoreLegAcceptable(50f, Above(limit)));
        // and in whole numbers: 0.6 of 100 is 60, so 59 passes and 61 does not (the absolute 50 binds there; use trip 50: 30 / 31)
        Assert.True(OpportunityRouteRules.StoreLegAcceptable(50f, 29.9f));
        Assert.False(OpportunityRouteRules.StoreLegAcceptable(50f, 30.1f));
    }

    [Fact]
    public void NewLegsMayNotExceedTheOriginalTrip()
    {
        // trip 60: thing <= 30 and store <= 36, so 30 + 30 = 60 is exactly the new-legs limit (all values exactly representable)
        Assert.True(OpportunityRouteRules.RouteAcceptable(60f, 30f, 10f, 30f));
        Assert.False(OpportunityRouteRules.RouteAcceptable(60f, 30f, 10f, 30f + 1f / 128f));
        Assert.False(OpportunityRouteRules.RouteAcceptable(60f, 30f + 1f / 128f, 10f, 30f)); // (also over the 30 thing-leg limit)
    }

    [Fact]
    public void TheWholeDetourMayNotExceed1Point7TimesTheOriginalTrip()
    {
        // trip 100: thing 30 + store->job 50 = 80 new legs; the thing->store leg may then be at most 170 - 80 = 90
        Assert.Equal(170f, 100f * OpportunityRouteRules.MaxTotalTripRatio);
        Assert.True(OpportunityRouteRules.RouteAcceptable(Trip, 30f, 90f, 50f));
        Assert.False(OpportunityRouteRules.RouteAcceptable(Trip, 30f, 90f + 1f / 64f, 50f));
    }

    [Fact]
    public void TheCheapPrefilterUsesTheSameTotalTripLimit()
    {
        Assert.True(OpportunityRouteRules.ThingCouldStillFit(Trip, 30f, 140f));
        Assert.False(OpportunityRouteRules.ThingCouldStillFit(Trip, 30f, 140f + 1f / 64f));
    }

    [Fact]
    public void ZeroAndVeryShortOriginalTripsNeverAcceptAnything()
    {
        foreach (var trip in new[] { 0f, float.Epsilon, 1f, 2.999f, MathF.BitDecrement(OpportunityRouteRules.MinOriginalTrip) })
        {
            Assert.False(OpportunityRouteRules.OriginalTripWorthConsidering(trip));
            Assert.False(OpportunityRouteRules.ThingLegAcceptable(trip, 0f));
            Assert.False(OpportunityRouteRules.ThingCouldStillFit(trip, 0f, 0f));
            Assert.False(OpportunityRouteRules.StoreLegAcceptable(trip, 0f));
            Assert.False(OpportunityRouteRules.RouteAcceptable(trip, 0f, 0f, 0f)); // not even a free detour is "on the way" when there is no way
        }

        Assert.True(OpportunityRouteRules.OriginalTripWorthConsidering(3f));
        Assert.True(OpportunityRouteRules.RouteAcceptable(3f, 1f, 1f, 1f));
    }

    [Fact]
    public void NonFiniteOrNegativeDistancesAreRejected()
    {
        foreach (var bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1f })
        {
            Assert.False(OpportunityRouteRules.OriginalTripWorthConsidering(bad));
            Assert.False(OpportunityRouteRules.RouteAcceptable(bad, 10f, 20f, 40f));
            Assert.False(OpportunityRouteRules.RouteAcceptable(Trip, bad, 20f, 40f));
            Assert.False(OpportunityRouteRules.RouteAcceptable(Trip, 10f, bad, 40f));
            Assert.False(OpportunityRouteRules.RouteAcceptable(Trip, 10f, 20f, bad));
            Assert.False(OpportunityRouteRules.ThingCouldStillFit(Trip, bad, 20f));
            Assert.False(OpportunityRouteRules.ThingCouldStillFit(Trip, 10f, bad));
        }
    }

    /// <summary>The same numbers vanilla's own opportunistic hauling uses, written out as plain arithmetic (1.6 decompile).</summary>
    private static bool VanillaAccepts(float num, float num2, float itemToJob, float itemToStore, float num3)
    {
        if (num < 3f)
        {
            return false;
        }

        if (num2 > 30f || num2 > num * 0.5f || num2 + itemToJob > num * 1.7f)
        {
            return false;
        }

        return !(num3 > 50f) && !(num3 > num * 0.6f) && !(num2 + itemToStore + num3 > num * 1.7f) && !(num2 + num3 > num);
    }

    private static float Dist(int ax, int az, int bx, int bz)
    {
        var dx = ax - bx;
        var dz = az - bz;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    [Fact]
    public void TheRulesAgreeWithVanillasOwnOpportunisticNumbersOnRandomMaps()
    {
        var rng = new Random(20260601);
        var accepted = 0;
        for (var n = 0; n < 200_000; n++)
        {
            int P() => rng.Next(0, 120);
            int sx = P(), sz = P(), jx = P(), jz = P(), tx = P(), tz = P(), cx = P(), cz = P();
            var trip = Dist(sx, sz, jx, jz);
            var startToThing = Dist(sx, sz, tx, tz);
            var thingToJob = Dist(tx, tz, jx, jz);
            var thingToStore = Dist(tx, tz, cx, cz);
            var storeToJob = Dist(cx, cz, jx, jz);

            var ours = OpportunityRouteRules.ThingLegAcceptable(trip, startToThing)
                && OpportunityRouteRules.ThingCouldStillFit(trip, startToThing, thingToJob)
                && OpportunityRouteRules.RouteAcceptable(trip, startToThing, thingToStore, storeToJob);

            Assert.Equal(VanillaAccepts(trip, startToThing, thingToJob, thingToStore, storeToJob), ours);
            if (ours)
            {
                accepted++;
            }
        }

        Assert.True(accepted > 500, "the random maps must actually produce accepted routes, not only rejections (" + accepted + ")");
    }

    [Fact]
    public void AnAcceptedRouteAlwaysPassesTheCheapPrefilter()
    {
        var rng = new Random(77);
        for (var n = 0; n < 200_000; n++)
        {
            int P() => rng.Next(0, 80);
            int sx = P(), sz = P(), jx = P(), jz = P(), tx = P(), tz = P(), cx = P(), cz = P();
            var trip = Dist(sx, sz, jx, jz);
            var startToThing = Dist(sx, sz, tx, tz);
            if (OpportunityRouteRules.RouteAcceptable(trip, startToThing, Dist(tx, tz, cx, cz), Dist(cx, cz, jx, jz)))
            {
                Assert.True(OpportunityRouteRules.ThingCouldStillFit(trip, startToThing, Dist(tx, tz, jx, jz)),
                    $"prefilter rejected an acceptable route: start({sx},{sz}) job({jx},{jz}) thing({tx},{tz}) store({cx},{cz})");
            }
        }
    }
}
