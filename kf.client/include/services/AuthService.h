//
// Created by Бесхмельницкий Антон on 20.10.2026.
//

#ifndef KF_CLIENT_AUTHSERVICE_H
#define KF_CLIENT_AUTHSERVICE_H

#include "cfg/AppConfig.h"
#include "crypto/AesGcmDecryptor.h"
#include "crypto/Base64.h"
#include "crypto/ClientKeyManager.h"
#include "crypto/IRsaEncryptor.h"
#include "http/IHttpClient.h"
#include "models/AuthTokenRequest.h"
#include "models/AuthTokenResponse.h"
#include "services/KeyService.h"
#include <expected>
#include <memory>
#include <string>

namespace services {

    class AuthService {
    public:
        AuthService(std::shared_ptr<http::IHttpClient> http,
                    std::shared_ptr<crypto::IRsaEncryptor> serverEncryptor,
                    std::shared_ptr<crypto::ClientKeyManager> clientKeyManager,
                    std::shared_ptr<services::KeyService> keyService,
                    cfg::AppConfig cfg);

        // Performs the full login handshake and returns the decrypted JWT token
        auto login(const std::string& challengerName, const std::string& secret)
            -> std::expected<models::AuthLoginResult, std::string>;

        // Returns the stored JWT token for subsequent requests
        [[nodiscard]] auto getToken() const -> std::optional<std::string>;

    private:
        std::shared_ptr<http::IHttpClient> http_;
        std::shared_ptr<crypto::IRsaEncryptor> serverEncryptor_;
        std::shared_ptr<crypto::ClientKeyManager> clientKeyManager_;
        std::shared_ptr<services::KeyService> keyService_;
        cfg::AppConfig cfg_;
        std::optional<std::string> jwtToken_;
    };

}

#endif //KF_CLIENT_AUTHSERVICE_H
