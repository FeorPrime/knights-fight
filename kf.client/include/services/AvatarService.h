//
// Created by Бесхмельницкий Антон on 07.11.2026.
//

#ifndef KF_CLIENT_AVATARSERVICE_H
#define KF_CLIENT_AVATARSERVICE_H

#include "cfg/AppConfig.h"
#include "http/IHttpClient.h"
#include <expected>
#include <memory>
#include <string>

namespace services {

    // Uploads an avatar image for a challenger.
    class AvatarService {
    public:
        AvatarService(std::shared_ptr<http::IHttpClient> http, cfg::AppConfig cfg);

        [[nodiscard]] auto uploadAvatar(long challengerId, const std::string& imagePath,
                                        const std::string& jwt)
            -> std::expected<std::string, std::string>;

    private:
        std::shared_ptr<http::IHttpClient> http_;
        cfg::AppConfig cfg_;
    };

}

#endif //KF_CLIENT_AVATARSERVICE_H
