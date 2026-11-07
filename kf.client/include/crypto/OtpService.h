//
// Created for KnightFight client OTP support.
//

#ifndef KF_CLIENT_OTPSERVICE_H
#define KF_CLIENT_OTPSERVICE_H

#include <cstdint>
#include <expected>
#include <optional>
#include <span>
#include <string>
#include <string_view>
#include <vector>

namespace crypto {

    enum class OtpHashAlgorithm {
        Sha1,
        Sha256,
        Sha512
    };

    class OtpService {
    public:
        static constexpr int DEFAULT_DIGITS = 6;
        static constexpr int DEFAULT_STEP_SECONDS = 30;

        [[nodiscard]] static auto resolveSeedBytes(std::string_view seed)
            -> std::vector<std::byte>;

        [[nodiscard]] static auto generateHotp(
            std::span<const std::byte> seedBytes,
            uint64_t counter,
            int digits = DEFAULT_DIGITS,
            OtpHashAlgorithm algorithm = OtpHashAlgorithm::Sha1)
            -> std::expected<std::string, std::string>;

        [[nodiscard]] static auto generateHotp(
            std::string_view seed,
            uint64_t counter,
            int digits = DEFAULT_DIGITS,
            OtpHashAlgorithm algorithm = OtpHashAlgorithm::Sha1)
            -> std::expected<std::string, std::string>;

        [[nodiscard]] static auto generateTotp(
            std::span<const std::byte> seedBytes,
            std::optional<int64_t> unixSeconds = std::nullopt,
            int stepSeconds = DEFAULT_STEP_SECONDS,
            int digits = DEFAULT_DIGITS,
            OtpHashAlgorithm algorithm = OtpHashAlgorithm::Sha1)
            -> std::expected<std::string, std::string>;

        [[nodiscard]] static auto generateTotp(
            std::string_view seed,
            std::optional<int64_t> unixSeconds = std::nullopt,
            int stepSeconds = DEFAULT_STEP_SECONDS,
            int digits = DEFAULT_DIGITS,
            OtpHashAlgorithm algorithm = OtpHashAlgorithm::Sha1)
            -> std::expected<std::string, std::string>;
    };

}

#endif //KF_CLIENT_OTPSERVICE_H
