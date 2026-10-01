// Pure logic: no RimWorld/Unity type, so the unit-test project can link and run it (see PickUpAndHaul.Tests.csproj).
using System;

namespace PickUpAndHaul.NativeOpportunity;

/// <summary>
/// The bookkeeping of "one invocation of the owner method is in progress": a short-lived scope is opened when the owner starts,
/// closed (in a finalizer) when it ends however it ends, and at most ONE search may be claimed from it.
/// </summary>
/// <remarks>
/// <para>Scopes form a chain, so a nested or re-entrant owner invocation opens its own scope on top: queries made while the inner
/// scope is open can only ever claim the INNER scope, and the outer scope's one search is untouched and available again once the
/// inner one has closed. A scope that is already claimed or closed is never claimed again, so the search fires at most once per
/// owner invocation, even if the code it triggers asks the same question again (re-entrant queries just pass through).</para>
/// <para>Closing is idempotent and self-healing: closing a scope also drops any inner scopes that were (wrongly) left open, so a
/// leaked inner scope cannot outlive the invocation it was nested in. Nothing is kept between invocations.</para>
/// </remarks>
internal sealed class InvocationScopes<TContext> where TContext : class
{
    internal sealed class Scope
    {
        internal Scope(TContext context, Scope previous)
        {
            Context = context;
            Previous = previous;
        }

        public TContext Context { get; }
        internal Scope Previous { get; }
        public bool SearchClaimed { get; internal set; }
        public bool Closed { get; internal set; }
    }

    private Scope _innermost;

    /// <summary>The cheap test for hot paths: is any invocation open at all?</summary>
    public bool AnyOpen => _innermost != null;

    /// <summary>How many scopes are open (0 when nothing is in progress).</summary>
    public int Depth
    {
        get
        {
            var depth = 0;
            for (var scope = _innermost; scope != null; scope = scope.Previous)
            {
                depth++;
            }

            return depth;
        }
    }

    /// <summary>Opens a scope for a new owner invocation; it becomes the innermost one.</summary>
    public Scope Open(TContext context)
    {
        var scope = new Scope(context, _innermost);
        _innermost = scope;
        return scope;
    }

    /// <summary>
    /// Claims the one search of the INNERMOST open scope, if that scope is not claimed or closed and its context owns the query
    /// (<paramref name="ownsQuery"/>). Returns the scope, or null when the query is not ours to answer.
    /// </summary>
    public Scope TryClaimSearch<TArg>(TArg argument, Func<TContext, TArg, bool> ownsQuery)
    {
        var scope = _innermost;
        if (scope == null || scope.Closed || scope.SearchClaimed || !ownsQuery(scope.Context, argument))
        {
            return null;
        }

        scope.SearchClaimed = true;
        return scope;
    }

    /// <summary>Ends the invocation: restores the enclosing scope. Safe to call twice and with a scope that is no longer open.</summary>
    public void Close(Scope scope)
    {
        if (scope == null)
        {
            return;
        }

        scope.Closed = true;

        var inChain = false;
        for (var current = _innermost; current != null; current = current.Previous)
        {
            if (current == scope)
            {
                inChain = true;
                break;
            }
        }

        if (!inChain)
        {
            // An outer close already dropped this scope: nothing to restore.
            return;
        }

        // Any scope still open above this one was nested in this invocation and ends with it.
        for (var current = _innermost; current != scope; current = current.Previous)
        {
            current.Closed = true;
        }

        _innermost = scope.Previous;
    }
}
