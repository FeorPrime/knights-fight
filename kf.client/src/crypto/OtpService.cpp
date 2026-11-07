//
// Created for KnightFight client OTP support.
//

#include "crypto/OtpService.h"
#include "crypto/Base32.h"
#include "crypto/Base64.h"

#include <array>
#include <chrono>
#include <cmath>
#include <format>
#include <openssl/evp.h>
#include <openssl/hmac.h>

namespace crypto {

    auto OtpService::resolveSeedBytes(std::string_view seed) -> std::vector<std::byte> {
        if (seed.empty()) return {};

        // 1. Try Base32 (default format for OTP seeds in the system)
        if (auto b32 = base32Decode(seed); b32.has_value() && !b32->empty()) {
            return std::move(*b32);
        }

        // 2. Try Base64
        if (auto b64 = base64Decode(seed); b64.has_value() && !b64->empty()) {
            return std::move(*b64);
        }

        // 3. Fallback to raw string bytes
        auto span = std::as_bytes(std::span{seed.data(), seed.size()});
        return std::vector<std::byte>(span.begin(), span.end());
    }

    auto OtpService::generateHotp(
        std::span<const std::byte> seedBytes,
        uint64_t counter,
        int digits,
        OtpHashAlgorithm algorithm)
        -> std::expected<std::string, std::string>
    {
        if (seedBytes.empty()) {
            return std::unexpected("Seed bytes cannot be empty");
        }
        if (digits < 1 || digits > 9) {
            return std::unexpected("Digits must be between 1 and 9");
        }

        const EVP_MD* md = nullptr;
        switch (algorithm) {
            case OtpHashAlgorithm::Sha1:   md = EVP_sha1(); break;
            case OtpHashAlgorithm::Sha256: md = EVP_sha256(); break;
            case OtpHashAlgorithm::Sha512: md = EVP_sha512(); break;
            default:
                return std::unexpected("Unsupported hash algorithm");
        }

        std::array<unsigned char, 8> counterBytes{};
        uint64_t c = counter;
        for (int i = 7; i >= 0; --i) {
            counterBytes[i] = static_cast<unsigned char>(c & 0xFF);
            c >>= 8;
        }

        unsigned char hash[EVP_MAX_MD_SIZE];
        unsigned int hashLen = 0;

        if (!HMAC(md,
                  seedBytes.data(), static_cast<int>(seedBytes.size()),
                  counterBytes.data(), counterBytes.size(),
                  hash, &hashLen)) {
            return std::unexpected("HMAC computation failed");
        }

        if (hashLen == 0) {
            return std::unexpected("Empty HMAC result");
        }

        int offset = hash[hashLen - 1] & 0x0F;
        if (offset + 4 > static_cast<int>(hashLen)) {
            return std::unexpected("Invalid HMAC offset");
        }

        uint32_t binary = ((static_cast<uint32_t>(hash[offset]) & 0x7F) << 24)
                        | ((static_cast<uint32_t>(hash[offset + 1]) & 0xFF) << 16)
                        | ((static_cast<uint32_t>(hash[offset + 2]) & 0xFF) << 8)
                        | (static_cast<uint32_t>(hash[offset + 3]) & 0xFF);

        uint32_t modulo = 1;
        for (int i = 0; i < digits; ++i) {
            modulo *= 10;
        }

        uint32_t otp = binary % modulo;
        return std::format("{:0{}}", otp, digits);
    }

    auto OtpService::generateHotp(
        std::string_view seed,
        uint64_t counter,
        int digits,
        OtpHashAlgorithm algorithm)
        -> std::expected<std::string, std::string>
    {
        auto seedBytes = resolveSeedBytes(seed);
        if (seedBytes.empty()) {
            return std::unexpected("Seed cannot be empty or invalid");
        }
        return generateHotp(seedBytes, counter, digits, algorithm);
    }

    auto OtpService::generateTotp(
        std::span<const std::byte> seedBytes,
        std::optional<int64_t> unixSeconds,
        int stepSeconds,
        int digits,
        OtpHashAlgorithm algorithm)
        -> std::expected<std::string, std::string>
    {
        if (stepSeconds <= 0) {
            return std::unexpected("Step seconds must be greater than zero");
        }

        int64_t timestamp = unixSeconds.has_value()
            ? *unixSeconds
            : std::chrono::duration_cast<std::chrono::seconds>(
                  std::chrono::system_clock::now().time_since_epoch()).count();

        if (timestamp < 0) {
            return std::unexpected("Timestamp cannot be negative");
        }

        uint64_t counter = static_cast<uint64_t>(timestamp / stepSeconds);
        return generateHotp(seedBytes, counter, digits, algorithm);
    }

    auto OtpService::generateTotp(
        std::string_view seed,
        std::optional<int64_t> unixSeconds,
        int stepSeconds,
        int digits,
        OtpHashAlgorithm algorithm)
        -> std::expected<std::string, std::string>
    {
        auto seedBytes = resolveSeedBytes(seed);
        if (seedBytes.empty()) {
            return std::unexpected("Seed cannot be empty or invalid");
        }
        return generateTotp(seedBytes, unixSeconds, stepSeconds, digits, algorithm);
    }

}
