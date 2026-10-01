namespace PickUpAndHaul.Planning;

/// <summary>What kind of trip a planning request is for.</summary>
internal enum HaulRequestKind
{
    /// <summary>
    /// The ordinary zPUAH trip: the work giver offered a Thing, storage is looked up from scratch, further things are collected
    /// (multi-pickup) and the existing "haul more within 12 cells" chaining applies. Phase 0 behavior, unchanged.
    /// </summary>
    Normal,

    /// <summary>
    /// A native opportunity trip (Phase 1): the pawn is already walking to some other job, one worthwhile item was selected and a
    /// storage destination for it was already chosen and route-validated. The planner uses exactly that destination, plans exactly
    /// that one pickup, and never scans for more.
    /// </summary>
    Opportunity,
}

/// <summary>
/// Everything one planning run is asked: who hauls, which thing, and (for an opportunity trip) the destination it was approved for.
/// Build one through the public constructor / <see cref="Normal"/> (a normal trip) or <see cref="Opportunity"/>; the full constructor is
/// private so a request that mixes the two (an opportunity without a destination, a normal trip with a preselected one, an
/// opportunity that may collect more) cannot be written.
/// </summary>
internal readonly struct HaulPlanningRequest
{
    public readonly Pawn Pawn;
    public readonly Thing InitialThing;
    public readonly bool Forced;

    /// <summary>Normal or Opportunity. Decides which planner path runs.</summary>
    public readonly HaulRequestKind Kind;

    /// <summary>
    /// The storage target an opportunity trip must use (the one its route was approved for). Meaningful only when
    /// <see cref="Kind"/> is <see cref="HaulRequestKind.Opportunity"/>; <c>default</c> otherwise.
    /// </summary>
    public readonly StoreTarget RequiredAnchor;

    /// <summary>A normal request. (Kept as the plain constructor so the work giver's call is unchanged from Phase 0.)</summary>
    public HaulPlanningRequest(Pawn pawn, Thing initialThing, bool forced)
        : this(pawn, initialThing, forced, HaulRequestKind.Normal, default)
    {
    }

    private HaulPlanningRequest(Pawn pawn, Thing initialThing, bool forced, HaulRequestKind kind, StoreTarget requiredAnchor)
    {
        Pawn = pawn;
        InitialThing = initialThing;
        Forced = forced;
        Kind = kind;
        RequiredAnchor = requiredAnchor;
    }

    /// <summary>A normal zPUAH request (same as the public constructor).</summary>
    public static HaulPlanningRequest Normal(Pawn pawn, Thing initialThing, bool forced)
        => new(pawn, initialThing, forced, HaulRequestKind.Normal, default);

    /// <summary>
    /// An opportunity request for <paramref name="thing"/> to <paramref name="plannedDestination"/>. There is deliberately no
    /// "allow more pickups" and no "forced" argument: an opportunity trip is one item, never player-forced.
    /// </summary>
    public static HaulPlanningRequest Opportunity(Pawn pawn, Thing thing, StoreTarget plannedDestination)
        => new(pawn, thing, false, HaulRequestKind.Opportunity, plannedDestination);

    /// <summary>True only for normal requests. An opportunity request can never collect additional pickups.</summary>
    public bool AllowAdditionalPickups => Kind == HaulRequestKind.Normal;
}
