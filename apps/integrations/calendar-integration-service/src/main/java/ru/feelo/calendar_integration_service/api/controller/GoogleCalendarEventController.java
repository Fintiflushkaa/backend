package ru.feelo.calendar_integration_service.api.controller;

import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;
import org.springframework.http.HttpStatus;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.ResponseStatus;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.PathVariable;

import ru.feelo.calendar_integration_service.dto.GoogleCalendarEvent;
import ru.feelo.calendar_integration_service.dto.GoogleCalendarEventRequest;
import ru.feelo.calendar_integration_service.application.GoogleCalendarEventService;
import ru.feelo.calendar_integration_service.dto.GoogleCalendarEventsResponse;

import java.time.Instant;
import java.util.UUID;

@RestController
@RequestMapping("/api/google/calendar")
public class GoogleCalendarEventController {

    private final GoogleCalendarEventService eventService;

    public GoogleCalendarEventController(
            GoogleCalendarEventService eventService
    ) {
        this.eventService = eventService;
    }

    @GetMapping("/events")
    public GoogleCalendarEventsResponse getEvents(
            @RequestParam UUID userId,
            @RequestParam Instant timeMin,
            @RequestParam Instant timeMax
    ) {
        return eventService.getEvents(
                userId,
                timeMin,
                timeMax
        );
    }
    @PostMapping("/events")
    @ResponseStatus(HttpStatus.CREATED)
    public GoogleCalendarEvent createEvent(
            @RequestParam UUID userId,
            @RequestBody GoogleCalendarEventRequest event
    ) {
        return eventService.createEvent(userId, event);
    }
    @PutMapping("/events/{eventId}")
    public GoogleCalendarEvent updateEvent(
            @RequestParam UUID userId,
            @PathVariable String eventId,
            @RequestBody GoogleCalendarEventRequest event
    ) {
        return eventService.updateEvent(
                userId,
                eventId,
                event
        );
    }
    @DeleteMapping("/events/{eventId}")
    public ResponseEntity<Void> deleteEvent(
            @RequestParam UUID userId,
            @PathVariable String eventId
    ) {
        eventService.deleteEvent(userId, eventId);

        return ResponseEntity.noContent().build();
    }
}