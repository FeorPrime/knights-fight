//
// Created by Бесхмельницкий Антон on 19.10.2026.
//

#ifndef KF_CLIENT_APPCONFIG_H
#define KF_CLIENT_APPCONFIG_H
#include <string>

namespace cfg {
    const std::string BASE_URL {"http://localhost:8080/api"};
    struct AppConfig {
        std::string publicKeyUrl = BASE_URL + "/auth/public-key";
        std::string createChallengerUrl = BASE_URL + "/challenger";
        std::string loginUrl = BASE_URL +"/auth/login";
        std::string testUrl = BASE_URL +"/test";
        std::string testOtpUrl = BASE_URL +"/test-otp";
        std::string lobbyUrl = BASE_URL + "/lobby";
        std::string lobbyStatusUrl = BASE_URL + "/lobby/status";
        std::string challengerAvatarUrl = BASE_URL + "/challengers/{id}/avatar";
        std::string udpHost = "127.0.0.1";
        int udpPort = 5050;
        // timeouts, retries, etc. can grow here
    };

}
#endif //KF_CLIENT_APPCONFIG_H
