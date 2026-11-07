//
// Created by Бесхмельницкий Антон on 19.10.2026.
//

#include "services/KeyService.h"
#include <nlohmann/json.hpp>

namespace services {

    KeyService::KeyService(std::shared_ptr<http::IHttpClient> http,
                           std::shared_ptr<crypto::IRsaEncryptor> encryptor,
                           cfg::AppConfig cfg)
        : http_(std::move(http)),
          encryptor_(std::move(encryptor)),
          cfg_(std::move(cfg)) {}

    auto KeyService::ensureLoaded() -> std::expected<void, std::string> {
        if (cached_) return {};

        auto resp = http_->get(cfg_.publicKeyUrl);
        if (!resp) return std::unexpected(resp.error());
        if (resp->status < 200 || resp->status >= 300)
            return std::unexpected(std::string("HTTP ") + std::to_string(resp->status));

        auto j = nlohmann::json::parse(resp->body, nullptr, false);
        if (j.is_discarded() || !j.contains("result") || !j["result"].is_string())
            return std::unexpected("JSON missing string field 'result'");

        models::PublicKey pk{j["result"].get<std::string>()};
        if (auto r = encryptor_->loadPublicKeyPem(pk.pem); !r)
            return std::unexpected(r.error());

        cached_ = std::move(pk);
        return {};
    }

    auto KeyService::current() const -> std::optional<models::PublicKey> {
        return cached_;
    }

}
