namespace LaserCollisionIn3DObjects.Domain.Scene;

/// <summary>
/// Owns collision viewport refresh lifecycle. Requests made before initialization are
/// intentionally ignored; re-entrant requests are coalesced into one final refresh.
/// </summary>
public sealed class CollisionViewportRefreshCoordinator
{
    private readonly Action<bool> _refresh;
    private bool _isInitialized;
    private bool _isRefreshing;
    private bool _hasPendingRefresh;
    private bool _pendingRunCollision;

    public CollisionViewportRefreshCoordinator(Action<bool> refresh) =>
        _refresh = refresh ?? throw new ArgumentNullException(nameof(refresh));

    public bool IsInitialized => _isInitialized;

    public void Initialize()
    {
        if (_isInitialized) return;
        _isInitialized = true;
        Request(runCollision: false);
    }

    public void Request(bool runCollision)
    {
        if (!_isInitialized) return;
        if (_isRefreshing)
        {
            _hasPendingRefresh = true;
            _pendingRunCollision |= runCollision;
            return;
        }

        var nextRunCollision = runCollision;
        do
        {
            _hasPendingRefresh = false;
            _pendingRunCollision = false;
            _isRefreshing = true;
            try { _refresh(nextRunCollision); }
            finally { _isRefreshing = false; }
            nextRunCollision = _pendingRunCollision;
        }
        while (_hasPendingRefresh);
    }
}
