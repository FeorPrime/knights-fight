//
// Created by Бесхмельницкий Антон on 19.10.2026.
//

#ifndef KF_CLIENT_BASE64_H
#define KF_CLIENT_BASE64_H

#include <expected>
#include <span>
#include <string>
#include <string_view>
#include <vector>

namespace crypto {

    [[nodiscard]] auto base64Encode(std::span<const std::byte> in) -> std::string;
    [[nodiscard]] auto base64Encode(std::string_view in) -> std::string;
    [[nodiscard]] auto base64Decode(std::string_view in)
        -> std::expected<std::vector<std::byte>, std::string>;
    /// Decodes base64url (the JWT flavour): '-' and '_' stand in for '+' and '/'.
    [[nodiscard]] auto base64UrlDecode(std::string_view in)
        -> std::expected<std::string, std::string>;

}

#endif //KF_CLIENT_BASE64_H
