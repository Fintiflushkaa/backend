package ru.feelo.calendar_integration_service.config;

import org.springframework.beans.factory.annotation.Value;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.web.client.RestClient;

@Configuration
public class GoogleOAuthConfig {

    @Bean
    public RestClient googleOAuthRestClient() {
        return RestClient.builder()
                .baseUrl("https://oauth2.googleapis.com")
                .build();
    }
}