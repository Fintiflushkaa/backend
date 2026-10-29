package ru.feelo.calendar_integration_service.application;

import ru.feelo.calendar_integration_service.domain.model.CalendarEvent;

import java.util.UUID;

public interface CalendarService {

    String createEvent(
            UUID connectionId,
            CalendarEvent event
    );

    void updateEvent(
            UUID connectionId,
            CalendarEvent event
    );

    void deleteEvent(
            UUID connectionId,
            UUID taskId
    );
}