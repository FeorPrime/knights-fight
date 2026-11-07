//
// Created by Бесхмельницкий Антон on 20.10.2026.
//
#include "services/AuthService.h"
#include <nlohmann/json.hpp>
#include <format>
#include <iostream>
#include <span>

#include <iomanip>
#include <sstream>

namespace services {

    namespace {
        constexpr std::string_view jwtPrefix = "JWT:";
        constexpr std::string_view otpSeparator = "__OTP:";

        auto extractAuthLoginResult(const std::string& loginPayload)
            -> std::expected<models::AuthLoginResult, std::string>
        {
            if (!loginPayload.starts_with(jwtPrefix)) {
                return std::unexpected("Login payload does not start with JWT:");
            }

            const auto otpPos = loginPayload.find(otpSeparator);
            if (otpPos == std::string::npos) {
                return std::unexpected("Login payload does not contain __OTP:");
            }

            auto jwt = loginPayload.substr(jwtPrefix.size(), otpPos - jwtPrefix.size());
            auto otp = loginPayload.substr(otpPos + otpSeparator.size());

            if (jwt.empty()) {
                return std::unexpected("JWT part is empty");
            }

            if (otp.empty()) {
                return std::unexpected("OTP part is empty");
            }

            return models::AuthLoginResult{
                .jwt = std::move(jwt),
                .otp = std::move(otp)
            };
        }
    }

// Helper to print bytes as hex for debugging
std::string bytesToHex(std::span<const std::byte> bytes) {
    std::ostringstream oss;
    for (auto b : bytes) {
        oss << std::hex << std::setw(2) << std::setfill('0') << static_cast<int>(b);
    }
    return oss.str();
}

AuthService::AuthService(std::shared_ptr<http::IHttpClient> http,
                         std::shared_ptr<crypto::IRsaEncryptor> serverEncryptor,
                         std::shared_ptr<crypto::ClientKeyManager> clientKeyManager,
                         std::shared_ptr<services::KeyService> keyService,
                         cfg::AppConfig cfg)
    : http_(std::move(http)),
      serverEncryptor_(std::move(serverEncryptor)),
      clientKeyManager_(std::move(clientKeyManager)),
      keyService_(std::move(keyService)),
      cfg_(std::move(cfg)) {}

auto AuthService::login(const std::string& challengerName, const std::string& secret)
    -> std::expected<models::AuthLoginResult, std::string>
{
    // 1. Ensure we have the server's public key
    if (auto r = keyService_->ensureLoaded(); !r) return std::unexpected(r.error());

    // 2. Build the inner plaintext request
    models::AuthTokenRequest req{challengerName, secret };
    std::string innerJson = nlohmann::json(req).dump();

    // 3. Encrypt inner request with SERVER's public key (RSA-OAEP-SHA256)
    auto innerBytes = std::as_bytes(std::span{innerJson.data(), innerJson.size()});
    auto encryptedInner = serverEncryptor_->encrypt(innerBytes);
    if (!encryptedInner) return std::unexpected(encryptedInner.error());

    // 4. Wrap in Envelope
    nlohmann::json envelope;
    envelope["Algorithm"] = "RSA-OAEP-SHA256";
    envelope["Version"] = "1.0";
    envelope["Payload"] = crypto::base64Encode(
        std::as_bytes(std::span{encryptedInner->data(), encryptedInner->size()}));
    envelope["OpenPayload"] = clientKeyManager_->getPublicKeyPem();

    // 5. POST to /api/Auth/login
    auto resp = http_->post(cfg_.loginUrl, envelope.dump(), "application/json");
    if (!resp) return std::unexpected(resp.error());
    if (resp->status == 403) return std::unexpected("Authentication failed (403)");
    if (resp->status < 200 || resp->status >= 300)
        return std::unexpected(std::format("HTTP {}", resp->status));

    // 6. Parse response
    auto j = nlohmann::json::parse(resp->body, nullptr, false);
    if (j.is_discarded()) return std::unexpected("Invalid JSON response from server");

    auto resultValue = j["resultValue"];
    if (!resultValue.contains("encryptedKey") || !resultValue.contains("nonce") ||
        !resultValue.contains("ciphertext") || !resultValue.contains("tag")) {
        return std::unexpected(std::format(
            "Server response missing expected fields. Raw body: {}", resp->body));
        }

    models::AuthTokenResponse authResp = resultValue.get<models::AuthTokenResponse>();

//    models::AuthTokenResponse authResp = j.get<models::AuthTokenResponse>();

    // 7. Decode Base64 fields
    auto encKeyBytes = crypto::base64Decode(authResp.encryptedKey);
    auto nonceBytes = crypto::base64Decode(authResp.nonce);
    auto cipherBytes = crypto::base64Decode(authResp.ciphertext);
    auto tagBytes = crypto::base64Decode(authResp.tag);

    auto val = encKeyBytes.value();

    if (!encKeyBytes || !nonceBytes || !cipherBytes || !tagBytes)
        return std::unexpected("Failed to decode base64 response fields");

    // DEBUG: Print sizes
    std::cout << std::format("[DEBUG] Encrypted AES key size: {} bytes\n", encKeyBytes->size());
    std::cout << std::format("[DEBUG] Nonce size: {} bytes\n", nonceBytes->size());
    std::cout << std::format("[DEBUG] Ciphertext size: {} bytes\n", cipherBytes->size());
    std::cout << std::format("[DEBUG] Tag size: {} bytes\n", tagBytes->size());

    // 8. Decrypt the AES key using CLIENT's private key
    auto aesKey = clientKeyManager_->decrypt(*encKeyBytes);
    if (!aesKey) return std::unexpected("Failed to decrypt AES key: " + aesKey.error());

#if NDEBUG
    // DEBUG: Print decrypted AES key
    std::cout << std::format("[DEBUG] Decrypted AES key size: {} bytes\n", aesKey->size());
    std::cout << std::format("[DEBUG] Decrypted AES key (hex): {}\n", bytesToHex(std::as_bytes(std::span{aesKey->data(), aesKey->size()})));
#endif

    // 9. Decrypt the JWT payload using AES-256-GCM
    auto jwtPlaintext = crypto::AesGcmDecryptor::decrypt(
        std::as_bytes(std::span{aesKey->data(), aesKey->size()}),
        std::as_bytes(std::span{nonceBytes->data(), nonceBytes->size()}),
        std::as_bytes(std::span{cipherBytes->data(), cipherBytes->size()}),
        std::as_bytes(std::span{tagBytes->data(), tagBytes->size()})
    );

    if (!jwtPlaintext) return std::unexpected(jwtPlaintext.error());

        auto loginResult = extractAuthLoginResult(*jwtPlaintext);
        if (!loginResult) {
            return std::unexpected(loginResult.error());
        }

        jwtToken_ = loginResult->jwt;

        std::cout << loginResult->jwt << std::endl;
        std::cout << loginResult->otp << std::endl;

        return *loginResult;
}

auto AuthService::getToken() const -> std::optional<std::string> {
    return jwtToken_;
}

}
