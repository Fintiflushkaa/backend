package ru.feelo.calendar_integration_service.api.controller;

import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.*;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import ru.feelo.calendar_integration_service.application.GoogleOAuthService;

import java.util.UUID;

@RestController
@RequestMapping("/api/oauth/google")
public class GoogleOAuthController {

    private static final Logger log =
            LoggerFactory.getLogger(GoogleOAuthController.class);

    private final GoogleOAuthService googleOAuthService;

    public GoogleOAuthController(GoogleOAuthService googleOAuthService) {
        this.googleOAuthService = googleOAuthService;
    }

    @GetMapping("/start")
    public ResponseEntity<Void> startAuthorization(
            @RequestParam UUID userId
    ) {
        String authorizationUrl =
                googleOAuthService.generateAuthorizationUrl(userId);

        return ResponseEntity
                .status(302)
                .header("Location", authorizationUrl)
                .build();
    }

    @GetMapping("/callback")
    public ResponseEntity<String> callback(
            @RequestParam(required = false) String code,
            @RequestParam(required = false) String state,
            @RequestParam(required = false) String error
    ) {
        if (error != null) {
            return ResponseEntity.badRequest()
                    .body("Google authorization failed: " + error);
        }

        if (code == null || state == null) {
            return ResponseEntity.badRequest()
                    .body("Missing authorization code or state");
        }

        try {
            googleOAuthService.completeAuthorization(code, state);

            return ResponseEntity.ok(
                    "Google Calendar successfully connected!"
            );

        } catch (IllegalArgumentException e) {
            return ResponseEntity.badRequest()
                    .body("Invalid OAuth state");
        } catch (Exception e) {
            log.error("Failed to complete Google OAuth authorization", e);

            return ResponseEntity.internalServerError()
                    .body("Failed to complete Google authorization");
        }
    }
    @GetMapping("/status")
    public ResponseEntity<String> getStatus(@RequestParam UUID userId) {
        boolean connected = googleOAuthService.isConnected(userId);

        return ResponseEntity.ok(
                connected ? "Google Calendar is connected"
                        : "Google Calendar is not connected"
        );
    }

    @DeleteMapping("/disconnect")
    public ResponseEntity<String> disconnect(@RequestParam UUID userId) {
        googleOAuthService.disconnect(userId);

        return ResponseEntity.ok("Google Calendar disconnected successfully");
    }
}