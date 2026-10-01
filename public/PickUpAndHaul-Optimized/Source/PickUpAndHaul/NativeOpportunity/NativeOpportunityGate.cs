using PickUpAndHaul.Planning;

namespace PickUpAndHaul.NativeOpportunity;

/// <summary>
/// The RimWorld-facing side of <see cref="NativeOpportunityActivation"/>: reads the (default-off) setting and RimWorld's active-mod
/// metadata. The standalone-WYU answer is computed once (the set of active mods cannot change while the game runs).
/// </summary>
internal static class NativeOpportunityGate
{
    private static bool _standaloneChecked;
    private static bool _standaloneWyuActive;

    private static bool IsPackageActive(string packageId)
        => ModLister.GetActiveModWithIdentifier(packageId, ignorePostfix: true) != null;

    /// <summary>A standalone While You're Up (any of the known package ids) is loaded.</summary>
    public static bool StandaloneWyuActive
    {
        get
        {
            if (!_standaloneChecked)
            {
                _standaloneWyuActive = NativeOpportunityActivation.AnyStandaloneWyuActive(IsPackageActive);
                _standaloneChecked = true;
            }

            return _standaloneWyuActive;
        }
    }

    public static NativeOpportunityStatus Status
        => NativeOpportunityActivation.Evaluate(Settings.NativeOpportunisticHauling, IsPackageActive);

    /// <summary>
    /// May the native feature act right now? False while the setting is off (the default), and false whenever a standalone
    /// While You're Up is loaded, in which case a single notice is logged. Cheap enough to call at every job start.
    /// </summary>
    public static bool IsActive
    {
        get
        {
            if (!Settings.NativeOpportunisticHauling)
            {
                return false;
            }

            if (StandaloneWyuActive)
            {
                OpportunityLog.StandaloneWyuNotice();
                return false;
            }

            return true;
        }
    }
}
