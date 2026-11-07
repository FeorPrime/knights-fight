//
// Created by Бесхмельницкий Антон on 19.10.2026.
//

#ifndef KF_CLIENT_CHALLENGERREQUEST_H
#define KF_CLIENT_CHALLENGERREQUEST_H

#include <nlohmann/json.hpp>
#include <string>

namespace models {

    struct ChallengerRequest {
        std::string name;
        std::string secret;
    };

    inline void to_json(nlohmann::json& j, const ChallengerRequest& r) {
        j = nlohmann::json{{"ChallengerName", r.name}, {"Secret", r.secret}};
    }

}

#endif //KF_CLIENT_CHALLENGERREQUEST_H
