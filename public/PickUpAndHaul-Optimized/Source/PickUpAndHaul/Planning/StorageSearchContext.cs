namespace PickUpAndHaul.Planning;

/// <summary>
/// The "already spoken for" memory of ONE planning operation. While a plan is built, every storage lookup asks for a place
/// that no earlier pickup of the same plan has been given; the skip sets are how a lookup remembers that.
/// </summary>
/// <remarks>
/// This replaces the former <c>public static HashSet&lt;IntVec3&gt; skipCells</c> / <c>skipThings</c> fields of the work giver.
/// Lifetime and ownership: created by <see cref="HaulJobPlanner"/> for one request and passed explicitly to every lookup; it is
/// never shared between pawns or jobs and nothing keeps it alive afterwards, so there is nothing to clean up (and nothing that
/// an exception could leave behind). Do not make it static or thread-local: planning may become nested.
/// </remarks>
internal sealed class StorageSearchContext
{
    public HashSet<IntVec3> SkipCells { get; } = new();
    public HashSet<Thing> SkipThings { get; } = new();

    /// <summary>Marks a target that the plan already uses so that later lookups do not hand it out again.</summary>
    public void MarkUsed(StoreTarget target)
    {
        if (target.container != null)
        {
            SkipThings.Add(target.container);
        }
        else
        {
            SkipCells.Add(target.cell);
        }
    }
}
