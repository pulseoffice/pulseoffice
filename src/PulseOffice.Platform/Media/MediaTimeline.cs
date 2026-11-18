namespace PulseOffice.Platform.Media;

public sealed class MediaTimeline
{
    public static readonly TimeSpan StartGrace = TimeSpan.FromMilliseconds(500);

    private readonly Lock _gate = new();
    private TimeSpan _origin;
    private TimeSpan? _anchorPlayed;
    private TimeSpan _anchorWall;
    private TimeSpan _last;

    public void Restart(TimeSpan origin)
    {
        lock (_gate)
        {
            _origin = origin;
            _anchorPlayed = null;
            _anchorWall = TimeSpan.Zero;
            _last = origin;
        }
    }

    public TimeSpan Position(TimeSpan wall, TimeSpan? played)
    {
        lock (_gate)
        {
            if (played is not { } heard)
                return Settle(_anchorPlayed is { } kept ? _origin + kept + (wall - _anchorWall) : _origin + wall);
            if (heard > (_anchorPlayed ?? TimeSpan.Zero))
            {
                _anchorPlayed = heard;
                _anchorWall = wall;
            }

            var position = _anchorPlayed is { } anchor
                ? _origin + anchor + (wall - _anchorWall)
                : _origin + (wall > StartGrace ? wall - StartGrace : TimeSpan.Zero);
            return Settle(position);
        }
    }

    private TimeSpan Settle(TimeSpan position)
    {
        if (position > _last) _last = position;
        return _last;
    }
}
