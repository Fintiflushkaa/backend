package ru.feelo.calendar_integration_service.application;

import ru.feelo.calendar_integration_service.domain.model.CalendarConnection;

import java.util.Optional;
import java.util.UUID;

public interface CalendarConnectionService {

    Optional<CalendarConnection> getConnection(UUID userId);

    String getAuthorizationUrl(UUID userId);

    CalendarConnection completeAuthorization(
            UUID userId,
            String authorizationCode,
            String state
    );

    void disconnect(UUID userId);
}