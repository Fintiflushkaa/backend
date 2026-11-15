
package ru.feelo.calendar_integration_service.infrastructure.persistence;

import org.springframework.data.jpa.repository.JpaRepository;

import java.util.Optional;
import java.util.UUID;

public interface GoogleOAuthCredentialRepository
        extends JpaRepository<GoogleOAuthCredentialEntity, UUID> {

    Optional<GoogleOAuthCredentialEntity> findByUserId(UUID userId);

    void deleteByUserId(UUID userId);
}
