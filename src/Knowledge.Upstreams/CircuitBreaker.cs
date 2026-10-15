namespace Knowledge.Upstreams;

/// <summary>Stops calling an upstream that keeps failing, then lets one probe through after a pause.</summary>
public sealed class CircuitBreaker(TimeProvider clock, int failureThreshold, TimeSpan openFor)
{
    private readonly object _gate = new();
    private int _failures;
    private DateTimeOffset? _openedAt;
    private bool _probing;

    public bool IsOpen
    {
        get
        {
            lock (_gate)
            {
                return _openedAt is not null && clock.GetUtcNow() - _openedAt < openFor;
            }
        }
    }

    /// <summary>True when a call may go ahead. After the pause, exactly one probe call is allowed.</summary>
    public bool TryAcquire()
    {
        lock (_gate)
        {
            if (_openedAt is null)
            {
                return true;
            }

            if (clock.GetUtcNow() - _openedAt < openFor || _probing)
            {
                return false;
            }

            _probing = true;
            return true;
        }
    }

    public void RecordSuccess()
    {
        lock (_gate)
        {
            _failures = 0;
            _openedAt = null;
            _probing = false;
        }
    }

    public void RecordFailure()
    {
        lock (_gate)
        {
            _probing = false;
            _failures++;
            if (_failures >= failureThreshold)
            {
                _openedAt = clock.GetUtcNow();
            }
        }
    }
}
