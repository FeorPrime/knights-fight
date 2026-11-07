//
// Created by Бесхмельницкий Антон on 07.11.2026.
//

#include "crypto/JwtPayload.h"
#include "crypto/Base64.h"
#include <nlohmann/json.hpp>

namespace crypto {

    auto extractSubject(std::string_view jwt) -> std::expected<long, std::string> {
        const auto firstDot = jwt.find('.');
        if (firstDot == std::string_view::npos) return std::unexpected("JWT has no payload part");

        const auto secondDot = jwt.find('.', firstDot + 1);
        if (secondDot == std::string_view::npos) return std::unexpected("JWT payload is not terminated");

        const auto payload = base64UrlDecode(jwt.substr(firstDot + 1, secondDot - firstDot - 1));
        if (!payload) return std::unexpected(payload.error());

        const auto json = nlohmann::json::parse(*payload, nullptr, false);
        if (json.is_discarded()) return std::unexpected("JWT payload is not valid JSON");

        if (!json.contains("sub")) return std::unexpected("JWT payload has no 'sub' claim");

        // AuthService issues "sub" as a string, but a numeric one is accepted too.
        const auto& subject = json["sub"];
        if (subject.is_string()) {
            try {
                return std::stoll(subject.get<std::string>());
            } catch (const std::exception&) {
                return std::unexpected("JWT 'sub' claim is not a number");
            }
        }
        if (subject.is_number_integer()) {
            return subject.get<long>();
        }
        return std::unexpected("JWT 'sub' claim is not a number");
    }

}
