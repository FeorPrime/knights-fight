#include "crypto/Base32.h"
#include "crypto/OtpService.h"
#include <cassert>
#include <iostream>
#include <string>

void test_base32() {
    std::string text = "Hello World!";
    auto encoded = crypto::base32Encode(text);
    auto decoded = crypto::base32Decode(encoded);
    assert(decoded.has_value());
    std::string decodedStr(reinterpret_cast<const char*>(decoded->data()), decoded->size());
    assert(decodedStr == text);
    std::cout << "[✓] Base32 encode/decode test passed.\n";
}

void test_rfc4226() {
    // RFC 4226 test secret: "12345678901234567890" (ASCII bytes)
    std::string secret = "12345678901234567890";
    auto secretBytes = std::as_bytes(std::span{secret.data(), secret.size()});

    const std::pair<uint64_t, std::string> expected[] = {
        {0, "755224"},
        {1, "287082"},
        {2, "359152"},
        {3, "969429"},
        {4, "338314"},
        {5, "254676"},
        {6, "287922"},
        {7, "162583"},
        {8, "399871"},
        {9, "520489"}
    };

    for (const auto& [counter, exp] : expected) {
        auto otp = crypto::OtpService::generateHotp(secretBytes, counter, 6, crypto::OtpHashAlgorithm::Sha1);
        assert(otp.has_value());
        assert(*otp == exp);
    }
    std::cout << "[✓] RFC 4226 HOTP test vectors passed.\n";
}

void test_rfc6238() {
    // RFC 6238 SHA-1 test key: 20 bytes ASCII "12345678901234567890"
    std::string secretSha1 = "12345678901234567890";
    auto secretSha1Bytes = std::as_bytes(std::span{secretSha1.data(), secretSha1.size()});

    struct Vector {
        int64_t time;
        std::string exp8;
        std::string exp6;
    };

    const Vector vectors[] = {
        {59, "94287082", "287082"},
        {1111111109, "07081804", "081804"},
        {1111111111, "14050471", "050471"},
        {1234567890, "89005924", "005924"},
        {2000000000, "69279037", "279037"},
        {20000000000, "65353130", "353130"}
    };

    for (const auto& v : vectors) {
        auto otp8 = crypto::OtpService::generateTotp(secretSha1Bytes, v.time, 30, 8, crypto::OtpHashAlgorithm::Sha1);
        assert(otp8.has_value());
        assert(*otp8 == v.exp8);

        auto otp6 = crypto::OtpService::generateTotp(secretSha1Bytes, v.time, 30, 6, crypto::OtpHashAlgorithm::Sha1);
        assert(otp6.has_value());
        assert(*otp6 == v.exp6);
    }
    std::cout << "[✓] RFC 6238 TOTP SHA-1 test vectors passed.\n";

    // RFC 6238 SHA-256 test key: 32 bytes ASCII "12345678901234567890123456789012"
    std::string secretSha256 = "12345678901234567890123456789012";
    auto secretSha256Bytes = std::as_bytes(std::span{secretSha256.data(), secretSha256.size()});

    const std::pair<int64_t, std::string> sha256Vectors[] = {
        {59, "46119246"},
        {1111111109, "68084774"},
        {1111111111, "67062674"},
        {1234567890, "91819424"},
        {2000000000, "90698825"},
        {20000000000, "77737706"}
    };

    for (const auto& [time, exp8] : sha256Vectors) {
        auto otp = crypto::OtpService::generateTotp(secretSha256Bytes, time, 30, 8, crypto::OtpHashAlgorithm::Sha256);
        assert(otp.has_value());
        assert(*otp == exp8);
    }
    std::cout << "[✓] RFC 6238 TOTP SHA-256 test vectors passed.\n";

    // RFC 6238 SHA-512 test key: 64 bytes ASCII "1234567890123456789012345678901234567890123456789012345678901234"
    std::string secretSha512 = "1234567890123456789012345678901234567890123456789012345678901234";
    auto secretSha512Bytes = std::as_bytes(std::span{secretSha512.data(), secretSha512.size()});

    const std::pair<int64_t, std::string> sha512Vectors[] = {
        {59, "90693936"},
        {1111111109, "25091201"},
        {1111111111, "99943326"},
        {1234567890, "93441116"},
        {2000000000, "38618901"},
        {20000000000, "47863826"}
    };

    for (const auto& [time, exp8] : sha512Vectors) {
        auto otp = crypto::OtpService::generateTotp(secretSha512Bytes, time, 30, 8, crypto::OtpHashAlgorithm::Sha512);
        assert(otp.has_value());
        assert(*otp == exp8);
    }
    std::cout << "[✓] RFC 6238 TOTP SHA-512 test vectors passed.\n";
}

int main() {
    test_base32();
    test_rfc4226();
    test_rfc6238();
    std::cout << "All OTP tests passed!\n";
    return 0;
}
