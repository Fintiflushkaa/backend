package ru.feelo.calendar_integration_service.dto;

import java.util.List;

public record GoogleCalendarEventsResponse(
        String nextPageToken,
        List<GoogleCalendarEvent> items
) {
}