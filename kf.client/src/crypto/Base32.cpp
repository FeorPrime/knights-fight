//
// Created for KnightFight client OTP support.
//

#include "crypto/Base32.h"
#include <cctype>

namespace crypto {

    namespace {
        constexpr std::string_view alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    }

    auto base32Encode(std::span<const std::byte> in, bool pad) -> std::string {
        if (in.empty()) return {};

        std::string result;
        result.reserve((in.size() * 8 + 4) / 5);

        int buffer = 0;
        int bitsLeft = 0;

        for (const auto b : in) {
            buffer = (buffer << 8) | static_cast<unsigned char>(b);
            bitsLeft += 8;
            while (bitsLeft >= 5) {
                bitsLeft -= 5;
                const int index = (buffer >> bitsLeft) & 0x1F;
                result.push_back(alphabet[index]);
            }
        }

        if (bitsLeft > 0) {
            const int index = (buffer << (5 - bitsLeft)) & 0x1F;
            result.push_back(alphabet[index]);
        }

        if (pad) {
            while (result.size() % 8 != 0) {
                result.push_back('=');
            }
        }

        return result;
    }

    auto base32Encode(std::string_view in, bool pad) -> std::string {
        return base32Encode(std::as_bytes(std::span{in.data(), in.size()}), pad);
    }

    auto base32Decode(std::string_view in)
        -> std::expected<std::vector<std::byte>, std::string>
    {
        if (in.empty()) return std::vector<std::byte>{};

        std::vector<std::byte> output;
        output.reserve(in.size() * 5 / 8);

        int buffer = 0;
        int bitsLeft = 0;

        for (const char c : in) {
            if (c == '=' || c == ' ' || c == '-' || c == '\r' || c == '\n' || c == '\t') {
                continue;
            }

            int val = 0;
            if (c >= 'A' && c <= 'Z') {
                val = c - 'A';
            } else if (c >= 'a' && c <= 'z') {
                val = c - 'a';
            } else if (c >= '2' && c <= '7') {
                val = c - '2' + 26;
            } else {
                return std::unexpected(std::string("Invalid character in Base32 string: ") + c);
            }

            buffer = (buffer << 5) | val;
            bitsLeft += 5;

            if (bitsLeft >= 8) {
                bitsLeft -= 8;
                output.push_back(static_cast<std::byte>((buffer >> bitsLeft) & 0xFF));
            }
        }

        return output;
    }

}
