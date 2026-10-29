package ru.feelo.calendar_integration_service.application;

import ru.feelo.calendar_integration_service.domain.model.SyncJob;

import java.util.UUID;

public interface CalendarSyncService {

    UUID startExport(
            UUID connectionId,
            UUID scheduleId
    );

    SyncJob getJobStatus(UUID jobId);

    void retryFailedJob(UUID jobId);
}