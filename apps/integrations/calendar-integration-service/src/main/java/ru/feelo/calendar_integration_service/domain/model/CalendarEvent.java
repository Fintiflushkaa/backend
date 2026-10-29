package ru.feelo.calendar_integration_service.domain.model;

import java.time.Instant;
import java.util.UUID;

public record CalendarEvent(
        UUID taskId,
        String title,
        String description,
        Instant startAt,
        Instant endAt,
        String timeZone
) {
}