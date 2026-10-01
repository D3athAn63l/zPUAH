// Pure logic: no RimWorld/Unity type, so the unit-test project can link and run it (see PickUpAndHaul.Tests.csproj).
using System;

namespace PickUpAndHaul.NativeOpportunity;

/// <summary>Whether the native Phase 1 opportunity feature may act right now.</summary>
internal enum NativeOpportunityStatus
{
    /// <summary>The (default-off) setting is not enabled.</summary>
    Off,

    /// <summary>The setting is enabled but a standalone While You're Up is loaded and owns the opportunity decision.</summary>
    InactiveStandaloneWyuActive,

    /// <summary>Enabled, and nothing else claims the opportunity decision.</summary>
    Active,
}

/// <summary>
/// The coexistence rule: native opportunistic hauling must never run next to a standalone While You're Up implementation, because
/// both would try to replace the very same decision (what vanilla's opportunistic prefix hauls). Detection is by RimWorld
/// package id only; nothing of those mods is called, patched or referenced.
/// </summary>
internal static class NativeOpportunityActivation
{
    /// <summary>Package ids of the standalone While You're Up implementations that this mod defers to.</summary>
    public static readonly string[] StandaloneWyuPackageIds =
    {
        "D3athAn63l.zWYU",
        "CodeOptimist.JobsOfOpportunity",
        "hoodie.whileyoureup",
        "kevlou127.WhileHYouOreHUpHQ1V0S",
    };

    /// <param name="isPackageActive">Package id -> is a mod with that id active? (Production: RimWorld's active-mod metadata.)</param>
    public static bool AnyStandaloneWyuActive(Func<string, bool> isPackageActive)
    {
        if (isPackageActive == null)
        {
            throw new ArgumentNullException(nameof(isPackageActive));
        }

        for (var i = 0; i < StandaloneWyuPackageIds.Length; i++)
        {
            if (isPackageActive(StandaloneWyuPackageIds[i]))
            {
                return true;
            }
        }

        return false;
    }

    public static NativeOpportunityStatus Evaluate(bool settingEnabled, Func<string, bool> isPackageActive)
    {
        if (!settingEnabled)
        {
            return NativeOpportunityStatus.Off;
        }

        return AnyStandaloneWyuActive(isPackageActive)
            ? NativeOpportunityStatus.InactiveStandaloneWyuActive
            : NativeOpportunityStatus.Active;
    }
}
