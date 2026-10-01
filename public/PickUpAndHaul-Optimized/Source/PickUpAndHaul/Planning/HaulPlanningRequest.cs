namespace PickUpAndHaul.Planning;

/// <summary>
/// Everything one normal zPUAH planning run is asked: who hauls, which thing the work giver was offered, and whether the
/// player forced it. Deliberately minimal; later phases extend it (purpose, route constraints, ...) when they need to.
/// </summary>
internal readonly struct HaulPlanningRequest
{
    public readonly Pawn Pawn;
    public readonly Thing InitialThing;
    public readonly bool Forced;

    public HaulPlanningRequest(Pawn pawn, Thing initialThing, bool forced)
    {
        Pawn = pawn;
        InitialThing = initialThing;
        Forced = forced;
    }
}
