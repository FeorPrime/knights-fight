//
// Created by Бесхмельницкий Антон on 19.10.2026.
//

#ifndef KF_CLIENT_IHTTPCLIENT_H
#define KF_CLIENT_IHTTPCLIENT_H
#include <cstddef>
#include <expected>
#include <string>
#include <string_view>
#include <vector>

namespace http {

    struct HttpResponse {
        int status{};
        std::string body;
    };

    /// A single file part of a multipart/form-data request.
    struct MultipartFile {
        std::string fieldName;
        std::string fileName;
        std::string contentType;
        std::vector<std::byte> data;
    };

    class IHttpClient {
    public:
        virtual ~IHttpClient() = default;

        [[nodiscard]] virtual auto get(std::string_view url)
            -> std::expected<HttpResponse, std::string> = 0;

        [[nodiscard]] virtual auto post(std::string_view url, std::string body,
                                        std::string_view contentType)
            -> std::expected<HttpResponse, std::string> = 0;

        /// Posts one file part as multipart/form-data with a bearer token.
        [[nodiscard]] virtual auto postMultipart(std::string_view url,
                                                const MultipartFile& file,
                                                std::string_view bearerToken)
            -> std::expected<HttpResponse, std::string> = 0;
    };

}
#endif //KF_CLIENT_IHTTPCLIENT_H
