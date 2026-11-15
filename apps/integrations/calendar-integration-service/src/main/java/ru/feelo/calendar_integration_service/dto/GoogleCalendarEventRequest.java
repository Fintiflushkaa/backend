package ru.feelo.calendar_integration_service.dto;

public record GoogleCalendarEventRequest(
        String summary,
        String description,
        String location,
        GoogleCalendarEventDateTime start,
        GoogleCalendarEventDateTime end
) {
}