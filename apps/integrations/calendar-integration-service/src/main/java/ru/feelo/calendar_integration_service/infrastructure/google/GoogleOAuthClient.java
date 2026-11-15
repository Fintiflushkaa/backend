package ru.feelo.calendar_integration_service.infrastructure.google;

import org.springframework.beans.factory.annotation.Value;
import org.springframework.http.MediaType;
import org.springframework.stereotype.Component;
import org.springframework.util.LinkedMultiValueMap;
import org.springframework.util.MultiValueMap;
import org.springframework.web.client.RestClient;

import ru.feelo.calendar_integration_service.dto.GoogleTokenResponse;

@Component
public class GoogleOAuthClient {

    private final RestClient restClient;

    @Value("${google.oauth.client-id}")
    private String clientId;

    @Value("${google.oauth.client-secret}")
    private String clientSecret;

    @Value("${google.oauth.redirect-uri}")
    private String redirectUri;

    public GoogleOAuthClient(RestClient googleOAuthRestClient) {
        this.restClient = googleOAuthRestClient;
    }

    public GoogleTokenResponse exchangeCodeForTokens(String code) {

        MultiValueMap<String, String> request =
                new LinkedMultiValueMap<>();

        request.add("code", code);
        request.add("client_id", clientId);
        request.add("client_secret", clientSecret);
        request.add("redirect_uri", redirectUri);
        request.add("grant_type", "authorization_code");

        return restClient.post()
                .uri("/token")
                .contentType(MediaType.APPLICATION_FORM_URLENCODED)
                .body(request)
                .retrieve()
                .body(GoogleTokenResponse.class);
    }
    public GoogleTokenResponse refreshAccessToken(String refreshToken) {
        MultiValueMap<String, String> request = new LinkedMultiValueMap<>();

        request.add("client_id", clientId);
        request.add("client_secret", clientSecret);
        request.add("refresh_token", refreshToken);
        request.add("grant_type", "refresh_token");

        return restClient.post()
                .uri("/token")
                .contentType(MediaType.APPLICATION_FORM_URLENCODED)
                .body(request)
                .retrieve()
                .body(GoogleTokenResponse.class);
    }
    public void revokeToken(String token) {
        MultiValueMap<String, String> request = new LinkedMultiValueMap<>();
        request.add("token", token);

        RestClient.create()
                .post()
                .uri("https://oauth2.googleapis.com/revoke")
                .contentType(MediaType.APPLICATION_FORM_URLENCODED)
                .body(request)
                .retrieve()
                .toBodilessEntity();
    }
}