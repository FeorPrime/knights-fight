//
// Created by Бесхмельницкий Антон on 20.10.2026.
//

#ifndef KF_CLIENT_AUTHTOKENRESPONSE_H
#define KF_CLIENT_AUTHTOKENRESPONSE_H

#include <nlohmann/json.hpp>
#include <string>

namespace models {

    struct AuthTokenResponse {
        std::string encryptedKey;
        std::string nonce;
        std::string ciphertext;
        std::string tag;
    };

    struct AuthLoginResult {
        std::string jwt;
        std::string otp;
    };

    inline void from_json(const nlohmann::json& j, AuthTokenResponse& r) {
        j.at("encryptedKey").get_to(r.encryptedKey);
        j.at("nonce").get_to(r.nonce);
        j.at("ciphertext").get_to(r.ciphertext);
        j.at("tag").get_to(r.tag);
    }

}

#endif //KF_CLIENT_AUTHTOKENRESPONSE_H
