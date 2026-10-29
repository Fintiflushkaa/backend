package ru.feelo.calendar_integration_service.domain.provider;

import ru.feelo.calendar_integration_service.domain.model.CalendarConnection;
import ru.feelo.calendar_integration_service.domain.model.CalendarEvent;

public interface CalendarProvider {

    String createEvent(
            CalendarConnection connection,
            CalendarEvent event
    );

    void updateEvent(
            CalendarConnection connection,
            String externalEventId,
            CalendarEvent event
    );

    void deleteEvent(
            CalendarConnection connection,
            String externalEventId
    );
}