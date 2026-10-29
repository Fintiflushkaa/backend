package ru.feelo.calendar_integration_service.domain.model;

import java.util.UUID;

public record CalendarConnection(
        UUID id,
        UUID userId,
        CalendarProviderType provider,
        String externalCalendarId,
        ConnectionStatus status
) {
}