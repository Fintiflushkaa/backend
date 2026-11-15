package ru.feelo.calendar_integration_service.dto;

public record GoogleCalendarEvent(
        String id,
        String summary,
        String description,
        String location,
        String htmlLink,
        String status,
        GoogleCalendarEventDateTime start,
        GoogleCalendarEventDateTime end
) {
}