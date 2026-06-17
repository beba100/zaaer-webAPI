-- Simplify reservation_event_profiles.event_status to: unconfirmed | confirmed | closed
-- Align with reservations.status (checked_in / checked_out).

SET NOCOUNT ON;

UPDATE rep
SET rep.event_status = CASE
    WHEN r.status IN ('checked_out', 'CheckedOut', 'checked-out') THEN 'closed'
    WHEN r.status IN ('checked_in', 'CheckedIn', 'checked-in', 'checkin') THEN 'confirmed'
    WHEN rep.event_status IN ('closed', 'completed', 'cancelled') THEN 'closed'
    WHEN rep.event_status IN ('confirmed', 'event_today', 'event_running', 'deposit_paid') THEN 'confirmed'
    ELSE 'unconfirmed'
END,
rep.updated_at = GETDATE()
FROM dbo.reservation_event_profiles rep
INNER JOIN dbo.reservations r
    ON r.zaaer_id = rep.reservation_id
    OR r.reservation_id = rep.reservation_id;

PRINT 'Hall event statuses normalized to unconfirmed / confirmed / closed.';
