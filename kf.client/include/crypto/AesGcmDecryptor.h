//
// Created by Бесхмельницкий Антон on 20.10.2026.
//

#ifndef KF_CLIENT_AESGCMDECRYPTOR_H
#define KF_CLIENT_AESGCMDECRYPTOR_H

#include <expected>
#include <span>
#include <string>
#include <vector>

namespace crypto {

    class AesGcmDecryptor {
    public:
        // Decrypts AES-256-GCM ciphertext.
        // key must be 32 bytes, nonce 12 bytes, tag 16 bytes.
        [[nodiscard]] static auto decrypt(std::span<const std::byte> key,
                                          std::span<const std::byte> nonce,
                                          std::span<const std::byte> ciphertext,
                                          std::span<const std::byte> tag)
            -> std::expected<std::string, std::string>;
    };

}

#endif //KF_CLIENT_AESGCMDECRYPTOR_H
