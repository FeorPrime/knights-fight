//
// Created by Бесхмельницкий Антон on 19.10.2026.
//

#include "http/CprHttpClient.h"
#include <cpr/cpr.h>
#include <format>

namespace http {

    auto CprHttpClient::get(std::string_view url)
        -> std::expected<HttpResponse, std::string>
    {
        auto r = cpr::Get(cpr::Url{std::string{url}});
        if (r.error.code != cpr::ErrorCode::OK) {
            return std::unexpected(std::format("GET {} failed: {}", url, r.error.message));
        }
        return HttpResponse{static_cast<int>(r.status_code), std::move(r.text)};
    }

    auto CprHttpClient::post(std::string_view url, std::string body,
                             std::string_view contentType)
        -> std::expected<HttpResponse, std::string>
    {
        auto r = cpr::Post(cpr::Url{std::string{url}},
                           cpr::Body{std::move(body)},
                           cpr::Header{{"Content-Type", std::string{contentType}}});
        if (r.error.code != cpr::ErrorCode::OK) {
            return std::unexpected(std::format("POST {} failed: {}", url, r.error.message));
        }
        return HttpResponse{static_cast<int>(r.status_code), std::move(r.text)};
    }

    auto CprHttpClient::postMultipart(std::string_view url, const MultipartFile& file,
                                      std::string_view bearerToken)
        -> std::expected<HttpResponse, std::string>
    {
        // cpr::Buffer takes a pair of iterators, not a pointer and a length.
        auto buffer = cpr::Buffer{
            file.data.begin(),
            file.data.end(),
            file.fileName.c_str()
        };

        auto multipart = cpr::Multipart{
            cpr::Part{file.fieldName, buffer, file.contentType}
        };

        auto r = cpr::Post(cpr::Url{std::string{url}},
                           multipart,
                           cpr::Header{{"Authorization", "Bearer " + std::string{bearerToken}}});
        if (r.error.code != cpr::ErrorCode::OK) {
            return std::unexpected(std::format("POST {} failed: {}", url, r.error.message));
        }
        return HttpResponse{static_cast<int>(r.status_code), std::move(r.text)};
    }

}