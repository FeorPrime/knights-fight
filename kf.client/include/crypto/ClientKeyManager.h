//
// Created by Бесхмельницкий Антон on 20.10.2026.
//

#ifndef KF_CLIENT_CLIENTKEYMANAGER_H
#define KF_CLIENT_CLIENTKEYMANAGER_H

#include <expected>
#include <span>
#include <string>
#include <vector>
#include <memory>
#include <openssl/evp.h>

namespace crypto {

    class ClientKeyManager {
    public:
        ClientKeyManager();
        ~ClientKeyManager();

        ClientKeyManager(const ClientKeyManager&) = delete;
        ClientKeyManager& operator=(const ClientKeyManager&) = delete;

        // Generates a 2048-bit (or 4096-bit) RSA key pair
        auto generate() -> std::expected<void, std::string>;

        // Returns the public key in PEM format to send to the server
        [[nodiscard]] auto getPublicKeyPem() const -> std::string;

        // Decrypts data using the client's PRIVATE key (RSA-OAEP-SHA256)
        [[nodiscard]] auto decrypt(std::span<const std::byte> ciphertext)
            -> std::expected<std::vector<std::byte>, std::string>;

    private:
        struct PkeyDeleter { void operator()(EVP_PKEY* p) const; };
        std::unique_ptr<EVP_PKEY, PkeyDeleter> keyPair_;
    };

}

#endif //KF_CLIENT_CLIENTKEYMANAGER_H
