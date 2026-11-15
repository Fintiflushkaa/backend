
package ru.feelo.calendar_integration_service.infrastructure.google;

import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

import javax.crypto.Cipher;
import javax.crypto.spec.GCMParameterSpec;
import javax.crypto.spec.SecretKeySpec;
import java.security.SecureRandom;
import java.util.Base64;

@Component
public class GoogleTokenEncryptor {

    private static final int IV_LENGTH = 12;
    private static final int TAG_LENGTH = 128;

    private final SecretKeySpec secretKey;
    private final SecureRandom secureRandom = new SecureRandom();

    public GoogleTokenEncryptor(
            @Value("${google.oauth.encryption-key}") String encodedKey
    ) {
        byte[] key = Base64.getDecoder().decode(encodedKey);

        if (key.length != 32) {
            throw new IllegalArgumentException(
                    "Encryption key must contain exactly 32 bytes"
            );
        }

        this.secretKey = new SecretKeySpec(key, "AES");
    }

    public String encrypt(String value) {
        if (value == null || value.isBlank()) {
            return value;
        }

        try {
            byte[] iv = new byte[IV_LENGTH];
            secureRandom.nextBytes(iv);

            Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
            cipher.init(
                    Cipher.ENCRYPT_MODE,
                    secretKey,
                    new GCMParameterSpec(TAG_LENGTH, iv)
            );

            byte[] encrypted = cipher.doFinal(
                    value.getBytes(java.nio.charset.StandardCharsets.UTF_8)
            );

            byte[] result = new byte[iv.length + encrypted.length];
            System.arraycopy(iv, 0, result, 0, iv.length);
            System.arraycopy(encrypted, 0, result, iv.length, encrypted.length);

            return Base64.getEncoder().encodeToString(result);

        } catch (Exception e) {
            throw new IllegalStateException("Failed to encrypt OAuth token", e);
        }
    }

    public String decrypt(String value) {
        if (value == null || value.isBlank()) {
            return value;
        }

        try {
            byte[] data = Base64.getDecoder().decode(value);

            byte[] iv = new byte[IV_LENGTH];
            System.arraycopy(data, 0, iv, 0, IV_LENGTH);

            byte[] encrypted = new byte[data.length - IV_LENGTH];
            System.arraycopy(data, IV_LENGTH, encrypted, 0, encrypted.length);

            Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
            cipher.init(
                    Cipher.DECRYPT_MODE,
                    secretKey,
                    new GCMParameterSpec(TAG_LENGTH, iv)
            );

            byte[] decrypted = cipher.doFinal(encrypted);

            return new String(
                    decrypted,
                    java.nio.charset.StandardCharsets.UTF_8
            );

        } catch (Exception e) {
            throw new IllegalStateException("Failed to decrypt OAuth token", e);
        }
    }
}
