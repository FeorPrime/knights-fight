//
// Created by Бесхмельницкий Антон on 19.10.2026.
//

#ifndef KF_CLIENT_IRSAENCRYPTOR_H
#define KF_CLIENT_IRSAENCRYPTOR_H

#include <cstddef>
#include <expected>
#include <span>
#include <string>
#include <string_view>
#include <vector>

namespace crypto {

    class IRsaEncryptor {
    public:
        virtual ~IRsaEncryptor() = default;

        // Load a PEM-encoded public key. Lifetime: session.
        virtual auto loadPublicKeyPem(std::string_view pem)
            -> std::expected<void, std::string> = 0;

        // RSA-OAEP with SHA-256 (both OAEP hash and MGF1).
        [[nodiscard]] virtual auto encrypt(std::span<const std::byte> plaintext)
            -> std::expected<std::vector<std::byte>, std::string> = 0;
    };

}

#endif //KF_CLIENT_IRSAENCRYPTOR_H
