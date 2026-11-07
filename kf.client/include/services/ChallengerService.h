//
// Created by Бесхмельницкий Антон on 19.10.2026.
//

#ifndef KF_CLIENT_CHALLENGERSERVICE_H
#define KF_CLIENT_CHALLENGERSERVICE_H

#include "cfg/AppConfig.h"
#include "crypto/IRsaEncryptor.h"
#include "http/IHttpClient.h"
#include "models/ChallengerRequest.h"
#include <expected>
#include <memory>
#include <string>

namespace services {

    // Builds the challenger payload, encrypts it, posts it, returns the server JSON.
    class ChallengerService {
    public:
        ChallengerService(std::shared_ptr<http::IHttpClient> http,
                          std::shared_ptr<crypto::IRsaEncryptor> encryptor,
                          cfg::AppConfig cfg);

        [[nodiscard]] auto createChallenger(const models::ChallengerRequest& req)
            -> std::expected<std::string, std::string>;

        /// Reads the id of a newly created challenger from the create response.
        [[nodiscard]] static auto parseChallengerId(std::string_view responseBody)
            -> std::expected<long, std::string>;

    private:
        std::shared_ptr<http::IHttpClient> http_;
        std::shared_ptr<crypto::IRsaEncryptor> encryptor_;
        cfg::AppConfig cfg_;
    };

}

#endif //KF_CLIENT_CHALLENGERSERVICE_H
