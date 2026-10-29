package ru.feelo.calendar_integration_service.domain.model;

import java.util.UUID;

public record SyncJob(
        UUID id,
        UUID connectionId,
        SyncJobStatus status,
        int totalItems,
        int processedItems,
        String errorMessage
) {
}