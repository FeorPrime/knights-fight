//
// Created by Бесхмельницкий Антон on 19.10.2026.
//

#include "services/ChallengerService.h"
#include "crypto/Base64.h"
#include <nlohmann/json.hpp>
#include <format>
#include <span>

namespace services {

    ChallengerService::ChallengerService(std::shared_ptr<http::IHttpClient> http,
                                         std::shared_ptr<crypto::IRsaEncryptor> encryptor,
                                         cfg::AppConfig cfg)
        : http_(std::move(http)),
          encryptor_(std::move(encryptor)),
          cfg_(std::move(cfg)) {}

    auto ChallengerService::createChallenger(const models::ChallengerRequest& req)
        -> std::expected<std::string, std::string>
    {
        // 1. Serialize inner JSON
        nlohmann::json inner = req;
        std::string innerText = inner.dump();

        // 2. RSA-OAEP-SHA256 encrypt
        auto bytes = std::as_bytes(std::span{innerText.data(), innerText.size()});
        auto enc = encryptor_->encrypt(bytes);
        if (!enc) return std::unexpected(enc.error());

        // 3. Wrap encrypted bytes in an outer JSON (base64).
        //    Adjust the field name ("payload") when you show me the expected schema.
        nlohmann::json outer;
        outer["payload"] = crypto::base64Encode(
            std::as_bytes(std::span{enc->data(), enc->size()}));

        // 4. POST
        auto resp = http_->post(cfg_.createChallengerUrl, outer.dump(), "application/json");
        if (!resp) return std::unexpected(resp.error());
        if (resp->status < 200 || resp->status >= 300)
            return std::unexpected(std::format("HTTP {}", resp->status));

        return resp->body;
    }

    auto ChallengerService::parseChallengerId(std::string_view responseBody)
        -> std::expected<long, std::string>
    {
        const auto json = nlohmann::json::parse(responseBody, nullptr, false);
        if (json.is_discarded()) return std::unexpected("Create response is not valid JSON");

        if (!json.contains("resultValue")) return std::unexpected("Create response has no resultValue");

        const auto& result = json["resultValue"];
        if (!result.contains("id")) return std::unexpected("Create response has no id");

        try {
            return result["id"].get<long>();
        } catch (const nlohmann::json::exception&) {
            return std::unexpected("Create response id is not a number");
        }
    }

}
