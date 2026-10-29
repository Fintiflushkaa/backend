package ru.feelo.calendar_integration_service.domain.model;

public enum SyncJobStatus {
    PENDING,
    IN_PROGRESS,
    COMPLETED,
    PARTIALLY_COMPLETED,
    FAILED
}