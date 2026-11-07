//
// Created by Бесхмельницкий Антон on 19.10.2026.
//
#ifndef KF_CLIENT_CPRHTTPCLIENT_H
#define KF_CLIENT_CPRHTTPCLIENT_H

#include "IHttpClient.h"

namespace http {

    class CprHttpClient final : public IHttpClient {
    public:
        [[nodiscard]] auto get(std::string_view url)
            -> std::expected<HttpResponse, std::string> override;

        [[nodiscard]] auto post(std::string_view url, std::string body,
                                std::string_view contentType)
            -> std::expected<HttpResponse, std::string> override;

        [[nodiscard]] auto postMultipart(std::string_view url, const MultipartFile& file,
                                        std::string_view bearerToken)
            -> std::expected<HttpResponse, std::string> override;
    };

}

#endif //KF_CLIENT_CPRHTTPCLIENT_H
