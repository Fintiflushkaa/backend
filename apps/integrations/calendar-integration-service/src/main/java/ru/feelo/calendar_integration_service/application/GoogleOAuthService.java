
package ru.feelo.calendar_integration_service.application;

import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import org.springframework.web.util.UriComponentsBuilder;
import org.springframework.web.client.RestClientResponseException;

import ru.feelo.calendar_integration_service.dto.GoogleTokenResponse;
import ru.feelo.calendar_integration_service.infrastructure.google.GoogleOAuthClient;
import ru.feelo.calendar_integration_service.infrastructure.google.GoogleTokenEncryptor;
import ru.feelo.calendar_integration_service.infrastructure.persistence.GoogleOAuthCredentialEntity;
import ru.feelo.calendar_integration_service.infrastructure.persistence.GoogleOAuthCredentialRepository;

import java.security.SecureRandom;
import java.time.Instant;
import java.util.Base64;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;

@Service
public class GoogleOAuthService {

    private final GoogleOAuthClient googleOAuthClient;
    private final GoogleOAuthCredentialRepository credentialRepository;

    @Value("${google.oauth.client-id}")
    private String clientId;

    @Value("${google.oauth.redirect-uri}")
    private String redirectUri;

    @Value("${google.oauth.scope}")
    private String scope;

    private final GoogleTokenEncryptor tokenEncryptor;

    private final SecureRandom secureRandom = new SecureRandom();

    private final Map<String, UUID> authorizationStates = new ConcurrentHashMap<>();

    public GoogleOAuthService(
            GoogleOAuthClient googleOAuthClient,
            GoogleOAuthCredentialRepository credentialRepository,
            GoogleTokenEncryptor tokenEncryptor) {

        this.googleOAuthClient = googleOAuthClient;
        this.credentialRepository = credentialRepository;
        this.tokenEncryptor = tokenEncryptor;
    }

    public String generateAuthorizationUrl(UUID userId) {
        byte[] randomBytes = new byte[32];
        secureRandom.nextBytes(randomBytes);

        String state = Base64.getUrlEncoder()
                .withoutPadding()
                .encodeToString(randomBytes);

        authorizationStates.put(state, userId);

        return UriComponentsBuilder
                .fromUriString("https://accounts.google.com/o/oauth2/v2/auth")
                .queryParam("client_id", clientId)
                .queryParam("redirect_uri", redirectUri)
                .queryParam("response_type", "code")
                .queryParam("scope", scope)
                .queryParam("access_type", "offline")
                .queryParam("include_granted_scopes", "true")
                .queryParam("state", state)
                .build()
                .encode()
                .toUriString();
    }

    public void completeAuthorization(String code, String state) {
        UUID userId = authorizationStates.remove(state);

        if (userId == null) {
            throw new IllegalArgumentException("Invalid or expired OAuth state");
        }

        GoogleTokenResponse tokens =
                googleOAuthClient.exchangeCodeForTokens(code);

        if (tokens == null || tokens.accessToken() == null) {
            throw new IllegalStateException(
                    "Google did not return an access token"
            );
        }

        GoogleOAuthCredentialEntity credential =
                credentialRepository.findByUserId(userId)
                        .orElseGet(GoogleOAuthCredentialEntity::new);

        credential.setUserId(userId);
        credential.setAccessToken(
                tokenEncryptor.encrypt(tokens.accessToken())
        );

        if (tokens.refreshToken() != null
                && !tokens.refreshToken().isBlank()) {
            credential.setRefreshToken(
                    tokenEncryptor.encrypt(tokens.refreshToken())
            );
        }

        if (tokens.expiresIn() != null) {
            credential.setExpiresAt(
                    Instant.now().plusSeconds(tokens.expiresIn())
            );
        }

        credential.setScope(tokens.scope());

        credentialRepository.save(credential);
    }

    public String getValidAccessToken(UUID userId) {
        GoogleOAuthCredentialEntity credential =
                credentialRepository.findByUserId(userId)
                        .orElseThrow(() -> new IllegalStateException(
                                "Google Calendar is not connected"
                        ));

        Instant now = Instant.now();

        // Если токен ещё действует больше минуты, расшифровываем и возвращаем его.
        if (credential.getExpiresAt() != null
                && credential.getExpiresAt().isAfter(now.plusSeconds(60))) {
            return tokenEncryptor.decrypt(credential.getAccessToken());
        }

        // Расшифровываем refresh token перед отправкой в Google.
        String encryptedRefreshToken = credential.getRefreshToken();

        if (encryptedRefreshToken == null || encryptedRefreshToken.isBlank()) {
            throw new IllegalStateException(
                    "Google refresh token is missing. Reconnect your calendar."
            );
        }

        String refreshToken = tokenEncryptor.decrypt(encryptedRefreshToken);

        GoogleTokenResponse tokens;

        try {
            tokens = googleOAuthClient.refreshAccessToken(refreshToken);

        } catch (RestClientResponseException e) {
            String responseBody = e.getResponseBodyAsString();

            if (responseBody.contains("invalid_grant")) {
                credentialRepository.deleteByUserId(userId);

                throw new IllegalStateException(
                        "Google authorization has expired. Please reconnect your calendar."
                );
            }

            throw e;
        }

        if (tokens == null || tokens.accessToken() == null) {
            throw new IllegalStateException(
                    "Google did not return a refreshed access token"
            );
        }

        // Сохраняем новый access token в зашифрованном виде.
        credential.setAccessToken(
                tokenEncryptor.encrypt(tokens.accessToken())
        );

        if (tokens.expiresIn() != null) {
            credential.setExpiresAt(
                    Instant.now().plusSeconds(tokens.expiresIn())
            );
        }

        // Если Google вернул новый refresh token, тоже шифруем его.
        if (tokens.refreshToken() != null
                && !tokens.refreshToken().isBlank()) {
            credential.setRefreshToken(
                    tokenEncryptor.encrypt(tokens.refreshToken())
            );
        }

        credentialRepository.save(credential);

        // Возвращаем незашифрованный токен для обращения к Google API.
        return tokens.accessToken();
    }

    public boolean isConnected(UUID userId) {
        return credentialRepository.findByUserId(userId).isPresent();
    }

    @Transactional
    public void disconnect(UUID userId) {
        GoogleOAuthCredentialEntity credential =
                credentialRepository.findByUserId(userId)
                        .orElse(null);

        if (credential == null) {
            return;
        }

        String token = credential.getRefreshToken();

        if (token != null && !token.isBlank()) {
            token = tokenEncryptor.decrypt(token);
        } else {
            token = tokenEncryptor.decrypt(credential.getAccessToken());
        }

        try {
            googleOAuthClient.revokeToken(token);
        } catch (Exception e) {
            // Ошибка отзыва не должна мешать локальному отключению.
            // Не записываем токены в логи.
        } finally {
            credentialRepository.deleteByUserId(userId);
        }
    }
}
