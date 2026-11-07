//
// Created by Бесхмельницкий Антон on 20.10.2026.
//

#ifndef KF_CLIENT_AUTHTOKENREQUEST_H
#define KF_CLIENT_AUTHTOKENREQUEST_H

#include <nlohmann/json.hpp>
#include <string>

namespace models {

    struct AuthTokenRequest {
        std::string challengerName;
        std::string secret;
    };

    inline void to_json(nlohmann::json& j, const AuthTokenRequest& r) {
        j = nlohmann::json{
            {"ChallengerName", r.challengerName},
            {"Secret", r.secret}
        };
    }

}

#endif //KF_CLIENT_AUTHTOKENREQUEST_H
