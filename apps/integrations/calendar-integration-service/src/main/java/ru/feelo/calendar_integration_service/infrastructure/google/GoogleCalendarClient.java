package ru.feelo.calendar_integration_service.infrastructure.google;

import org.springframework.stereotype.Component;
import org.springframework.web.client.RestClient;

import ru.feelo.calendar_integration_service.dto.GoogleCalendarEventsResponse;
import org.springframework.http.MediaType;
import ru.feelo.calendar_integration_service.dto.GoogleCalendarEvent;
import ru.feelo.calendar_integration_service.dto.GoogleCalendarEventRequest;

import java.time.Instant;

@Component
public class GoogleCalendarClient {

    private final RestClient restClient;

    public GoogleCalendarClient() {
        this.restClient = RestClient.builder()
                .baseUrl("https://www.googleapis.com/calendar/v3")
                .build();
    }

    public GoogleCalendarEventsResponse listEvents(
            String accessToken,
            Instant timeMin,
            Instant timeMax
    ) {
        if (timeMin == null || timeMax == null || !timeMax.isAfter(timeMin)) {
            throw new IllegalArgumentException("Invalid event time range");
        }

        return restClient.get()
                .uri(uriBuilder -> uriBuilder
                        .path("/calendars/primary/events")
                        .queryParam("timeMin", timeMin.toString())
                        .queryParam("timeMax", timeMax.toString())
                        .queryParam("singleEvents", true)
                        .queryParam("orderBy", "startTime")
                        .queryParam("maxResults", 100)
                        .build())
                .headers(headers -> headers.setBearerAuth(accessToken))
                .retrieve()
                .body(GoogleCalendarEventsResponse.class);
    }
    public GoogleCalendarEvent createEvent(
            String accessToken,
            GoogleCalendarEventRequest event
    ) {
        return restClient.post()
                .uri("/calendars/primary/events")
                .contentType(MediaType.APPLICATION_JSON)
                .headers(headers -> headers.setBearerAuth(accessToken))
                .body(event)
                .retrieve()
                .body(GoogleCalendarEvent.class);
    }
    public GoogleCalendarEvent updateEvent(
            String accessToken,
            String eventId,
            GoogleCalendarEventRequest event
    ) {
        return restClient.put()
                .uri("/calendars/primary/events/{eventId}", eventId)
                .contentType(MediaType.APPLICATION_JSON)
                .headers(headers -> headers.setBearerAuth(accessToken))
                .body(event)
                .retrieve()
                .body(GoogleCalendarEvent.class);
    }
    public void deleteEvent(
            String accessToken,
            String eventId
    ) {
        restClient.delete()
                .uri("/calendars/primary/events/{eventId}", eventId)
                .headers(headers -> headers.setBearerAuth(accessToken))
                .retrieve()
                .toBodilessEntity();
    }
}