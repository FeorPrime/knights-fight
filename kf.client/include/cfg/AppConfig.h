//
// Created by Бесхмельницкий Антон on 19.10.2026.
//

#ifndef KF_CLIENT_APPCONFIG_H
#define KF_CLIENT_APPCONFIG_H
#include <string>

namespace cfg {
    // The development defaults: the local HTTP API on 8080, the UDP game
    // protocol on 5050. ConfigLoader overrides any of them from command-line
    // keys, a configuration file or KF_CLIENT_* environment variables. The
    // endpoint paths are listed a second time in cfg/ConfigLoader.cpp (the
    // keys the command line and the configuration file accept) — change both.
    const std::string BASE_URL {"http://localhost:8080/api"};
    struct AppConfig {
        std::string baseUrl = BASE_URL;
        std::string publicKeyUrl = baseUrl + "/auth/public-key";
        std::string createChallengerUrl = baseUrl + "/challenger";
        std::string loginUrl = baseUrl + "/auth/login";
        std::string testUrl = baseUrl + "/test/auth";
        std::string testOtpUrl = baseUrl + "/test/otp";
        std::string lobbyUrl = baseUrl + "/lobby";
        std::string lobbyStatusUrl = baseUrl + "/lobby/status";
        std::string challengerAvatarUrl = baseUrl + "/challenger/{id}/avatar";
        std::string udpHost = "127.0.0.1";
        int udpPort = 5050;
        // timeouts, retries, etc. can grow here
    };

} // namespace cfg
#endif //KF_CLIENT_APPCONFIG_H
