//
// Created by Бесхмельницкий Антон on 19.10.2026.
//

#ifndef KF_CLIENT_KEYSERVICE_H
#define KF_CLIENT_KEYSERVICE_H

#include "cfg/AppConfig.h"
#include "crypto/IRsaEncryptor.h"
#include "http/IHttpClient.h"
#include "models/PublicKey.h"
#include <expected>
#include <memory>
#include <optional>
#include <string>

namespace services {

    // Fetches the public key once and caches it for the session.
    class KeyService {
    public:
        KeyService(std::shared_ptr<http::IHttpClient> http,
                   std::shared_ptr<crypto::IRsaEncryptor> encryptor,
                   cfg::AppConfig cfg);

        // Fetches + caches. Idempotent.
        auto ensureLoaded() -> std::expected<void, std::string>;

        [[nodiscard]] auto current() const -> std::optional<models::PublicKey>;

    private:
        std::shared_ptr<http::IHttpClient> http_;
        std::shared_ptr<crypto::IRsaEncryptor> encryptor_;
        cfg::AppConfig cfg_;
        std::optional<models::PublicKey> cached_;
    };

}

#endif //KF_CLIENT_KEYSERVICE_H
