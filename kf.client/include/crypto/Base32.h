//
// Created for KnightFight client OTP support.
//

#ifndef KF_CLIENT_BASE32_H
#define KF_CLIENT_BASE32_H

#include <expected>
#include <span>
#include <string>
#include <string_view>
#include <vector>

namespace crypto {

    [[nodiscard]] auto base32Encode(std::span<const std::byte> in, bool pad = false) -> std::string;
    [[nodiscard]] auto base32Encode(std::string_view in, bool pad = false) -> std::string;
    [[nodiscard]] auto base32Decode(std::string_view in)
        -> std::expected<std::vector<std::byte>, std::string>;

}

#endif //KF_CLIENT_BASE32_H
