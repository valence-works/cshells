namespace CShells.Nuplane;

/// <summary>Controls which eligible Nuplane reconciliation completions refresh the CShells feature catalog.</summary>
public enum NuplaneRefreshTrigger
{
    /// <summary>Refresh on every delivered eligible package-reconciliation completion.</summary>
    EveryEligibleCompletion,

    /// <summary>Refresh when package changes or previously requested catalog freshness remains pending.</summary>
    ChangedOrPending
}
