INSERT INTO audit_events (audit_event_id, user_id, operation, resource_type, resource_id, result, occurred_at_utc)
VALUES (@AuditEventId, @UserId, @Operation, @ResourceType, @ResourceId, @Result, @OccurredAtUtc);
