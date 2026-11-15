package ru.feelo.calendar_integration_service.dto;

public record GoogleCalendarEventDateTime(
        String dateTime,
        String date,
        String timeZone
) {
}