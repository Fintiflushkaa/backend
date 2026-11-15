package ru.feelo.calendar_integration_service.application;

import org.springframework.stereotype.Service;

import ru.feelo.calendar_integration_service.dto.GoogleCalendarEventsResponse;
import ru.feelo.calendar_integration_service.infrastructure.google.GoogleCalendarClient;
import ru.feelo.calendar_integration_service.dto.GoogleCalendarEvent;
import ru.feelo.calendar_integration_service.dto.GoogleCalendarEventRequest;

import java.time.Instant;
import java.util.UUID;

@Service
public class GoogleCalendarEventService {

    private final GoogleCalendarClient googleCalendarClient;
    private final GoogleOAuthService googleOAuthService;

    public GoogleCalendarEventService(
            GoogleCalendarClient googleCalendarClient,
            GoogleOAuthService googleOAuthService
    ) {
        this.googleCalendarClient = googleCalendarClient;
        this.googleOAuthService = googleOAuthService;
    }

    public GoogleCalendarEventsResponse getEvents(
            UUID userId,
            Instant timeMin,
            Instant timeMax
    ) {
        // Получаем действующий токен пользователя
        String accessToken =
                googleOAuthService.getValidAccessToken(userId);

        // Получаем события из Google Calendar
        return googleCalendarClient.listEvents(
                accessToken,
                timeMin,
                timeMax
        );
    }
    public GoogleCalendarEvent createEvent(
            UUID userId,
            GoogleCalendarEventRequest event
    ) {
        String accessToken =
                googleOAuthService.getValidAccessToken(userId);

        return googleCalendarClient.createEvent(
                accessToken,
                event
        );
    }
    public GoogleCalendarEvent updateEvent(
            UUID userId,
            String eventId,
            GoogleCalendarEventRequest event
    ) {
        String accessToken =
                googleOAuthService.getValidAccessToken(userId);

        return googleCalendarClient.updateEvent(
                accessToken,
                eventId,
                event
        );
    }
    public void deleteEvent(
            UUID userId,
            String eventId
    ) {
        String accessToken =
                googleOAuthService.getValidAccessToken(userId);

        googleCalendarClient.deleteEvent(
                accessToken,
                eventId
        );
    }
}