using HarmonyLib;

namespace PickUpAndHaul.NativeOpportunity;

/// <summary>
/// The only Harmony patches of the native opportunity feature (Phase 1), two methods in all. Each exists for one reason:
/// <list type="bullet">
/// <item><c>Pawn_JobTracker.TryOpportunisticJob</c> — PREFIX opens a short-lived invocation scope (when the feature is active) and
/// stores it in <c>__state</c>; FINALIZER closes exactly that scope however the method ended, hands vanilla the selected job
/// if vanilla itself found none, and re-throws any exception unchanged. This is the owner of the whole opportunity decision, so the
/// scope is bounded by it. Vanilla is what queues the original job again (it enqueues it in front of its queue when this method
/// returns a job); nothing here touches the job queue.</item>
/// <item><c>ListerHaulables.ThingsPotentiallyNeedingHauling</c> — PREFIX: while an invocation is open and has not searched yet, the
/// first haulables query on its pawn's map is the point where vanilla has passed all of its own opportunistic preconditions and
/// starts looking for something to haul. That query is answered with "nothing" (so vanilla's own loop does not also pick a haul)
/// and the native search runs once instead. Every other caller, and every query with no open invocation, passes straight through to
/// the original method; the hot path is one null check.</item>
/// </list>
/// No transpiler and no instruction offsets: both hooks are semantic (a method and its return value). If installing them fails, or
/// anything in them throws, vanilla's behavior is what remains.
/// </summary>
internal static class OpportunityPatches
{
    /// <summary>Installs both patches on <paramref name="harmony"/>. Never throws: on failure the feature simply stays inert.</summary>
    internal static void Install(Harmony harmony)
    {
        try
        {
            harmony.Patch(
                original: AccessTools.Method(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.TryOpportunisticJob)),
                prefix: new HarmonyMethod(typeof(OpportunityPatches), nameof(TryOpportunisticJob_Prefix)),
                finalizer: new HarmonyMethod(typeof(OpportunityPatches), nameof(TryOpportunisticJob_Finalizer)));

            harmony.Patch(
                original: AccessTools.Method(typeof(ListerHaulables), nameof(ListerHaulables.ThingsPotentiallyNeedingHauling)),
                prefix: new HarmonyMethod(typeof(OpportunityPatches), nameof(ThingsPotentiallyNeedingHauling_Prefix)));
        }
        catch (Exception exception)
        {
            Verse.Log.Error("[zPUAH Opportunity] could not install the native opportunity hooks; the feature stays inactive and normal Pick Up And Haul is unaffected. " + exception);
        }
    }

    private static void TryOpportunisticJob_Prefix(Pawn_JobTracker __instance, Job job, out OpportunityInvocation __state)
    {
        __state = null;
        try
        {
            if (job == null || !NativeOpportunityGate.IsActive)
            {
                return;
            }

            var pawn = __instance.pawn;
            if (pawn == null)
            {
                return;
            }

            var invocation = new OpportunityInvocation(__instance, pawn, job);
            invocation.Scope = OpportunityInvocation.Scopes.Open(invocation);
            __state = invocation;
        }
        catch (Exception exception)
        {
            __state = null;
            OpportunityLog.Failure("opening the opportunity scope", exception);
        }
    }

    private static Exception TryOpportunisticJob_Finalizer(Exception __exception, ref Job __result, OpportunityInvocation __state)
    {
        if (__state != null)
        {
            try
            {
                OpportunityInvocation.Scopes.Close(__state.Scope);

                // Only fill in a result when vanilla itself found none, and never after vanilla threw.
                if (__exception == null && __result == null && __state.SelectedJob != null)
                {
                    __result = __state.SelectedJob;
                    __state.OnSelectedJobApplied();
                }
            }
            catch (Exception exception)
            {
                OpportunityLog.Failure("closing the opportunity scope", exception);
            }
        }

        // Whatever vanilla threw (or nothing) goes on unchanged.
        return __exception;
    }

    private static bool ThingsPotentiallyNeedingHauling_Prefix(ListerHaulables __instance, ref ICollection<Thing> __result)
    {
        var scopes = OpportunityInvocation.Scopes;
        if (!scopes.AnyOpen)
        {
            return true;
        }

        try
        {
            var scope = scopes.TryClaimSearch(__instance, static (invocation, lister) => invocation.OwnsQuery(lister));
            return scope == null || scope.Context.RunNativeSearch(ref __result);
        }
        catch (Exception exception)
        {
            OpportunityLog.Failure("the haulables hook", exception);
            return true;
        }
    }
}
