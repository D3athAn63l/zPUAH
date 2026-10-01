namespace PickUpAndHaul.Planning;

/// <summary>
/// Which additional things the pawn tries to take along, and how its carrying capacity limits that: the candidate loop's
/// RimWorld-facing half. One instance per planning operation, owning that operation's working copy of the haulables.
/// </summary>
/// <remarks>
/// Candidate order and eligibility are unchanged: nearest first (squared horizontal distance from the previous pickup),
/// within <see cref="DistanceToSearchMore"/>, reachable, and passing <see cref="HaulCandidates.GoodThingToHaul"/> plus
/// <c>PawnCanAutomaticallyHaulFast</c> (not forced) plus, when AllowTool is active and the first thing was urgently designated,
/// the same urgent designation.
/// </remarks>
internal sealed class PickupPolicy : IPickupPolicy<Thing>
{
    //Thanks to AlexTD for the more dynamic search range
    //And queueing
    //And optimizing
    private const float SEARCH_FOR_OTHERS_RANGE_FRACTION = 0.5f;

    private readonly Pawn _pawn;
    private readonly Map _map;
    private readonly List<Thing> _haulables;
    private readonly bool _isUrgent;
    private readonly DesignationManager _designationManager;
    private readonly DesignationDef _haulUrgentlyDesignation;
    private readonly Predicate<Thing> _validator;

    public float DistanceToSearchMore { get; }

    public PickupPolicy(Pawn pawn, Thing initialThing, StoreTarget storeTarget)
    {
        _pawn = pawn;
        _map = pawn.Map;
        _designationManager = _map.designationManager;

        var distanceToHaul = (storeTarget.Position - initialThing.Position).LengthHorizontal * SEARCH_FOR_OTHERS_RANGE_FRACTION;
        DistanceToSearchMore = Math.Max(12f, distanceToHaul);

        //Find extra things than can be hauled to inventory, queue to reserve them
        _haulUrgentlyDesignation = PickUpAndHaulDesignationDefOf.haulUrgently;
        _isUrgent = ModCompatibilityCheck.AllowToolIsActive && _designationManager.DesignationOn(initialThing)?.def == _haulUrgentlyDesignation;

        // Work on a private copy of the shared per-tick cache: it is sorted and consumed.
        _haulables = new List<Thing>(HaulablesCache.Get(_map));
        HaulCandidates.SortByDistanceTo(_haulables, initialThing.Position);
        _haulables.Remove(initialThing);

        _validator = Validator;
    }

    private bool Validator(Thing t)
        => (!_isUrgent || _designationManager.DesignationOn(t)?.def == _haulUrgentlyDesignation)
        && HaulCandidates.GoodThingToHaul(t, _pawn) && HaulAIUtility.PawnCanAutomaticallyHaulFast(_pawn, t, false); //forced is false, may differ from first thing

    public Thing NextCandidateAfter(Thing last)
        => HaulCandidates.GetClosestAndRemove(last.Position, _map, _haulables, PathEndMode.ClosestTouch,
            TraverseParms.For(_pawn), DistanceToSearchMore, _validator);

    public float AddedEncumbrance(Thing item)
        => CapacityMath.AddedEncumbrance(item.stackCount, item.GetStatValue(StatDefOf.Mass), MassUtility.Capacity(_pawn));

    public int CountPastCapacity(Thing item, float encumbrance)
        => CapacityMath.CountPastCapacity(encumbrance, MassUtility.Capacity(_pawn), item.GetStatValue(StatDefOf.Mass));
}
