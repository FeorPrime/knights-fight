//
// Created by Бесхмельницкий Антон on 19.10.2026.
//

#ifndef KF_CLIENT_OPENSSLRSAENCRYPTOR_H
#define KF_CLIENT_OPENSSLRSAENCRYPTOR_H

#include "IRsaEncryptor.h"
#include <cstddef>
#include <openssl/evp.h>
#include <memory>

namespace crypto {

    class OpensslRsaEncryptor final : public IRsaEncryptor {
    public:
        OpensslRsaEncryptor() = default;
        ~OpensslRsaEncryptor() override;

        OpensslRsaEncryptor(const OpensslRsaEncryptor&) = delete;
        OpensslRsaEncryptor& operator=(const OpensslRsaEncryptor&) = delete;

        auto loadPublicKeyPem(std::string_view pem)
            -> std::expected<void, std::string> override;

        [[nodiscard]] auto encrypt(std::span<const std::byte> plaintext)
            -> std::expected<std::vector<std::byte>, std::string> override;

    private:
        struct PkeyDeleter { void operator()(EVP_PKEY* p) const; };
        std::unique_ptr<EVP_PKEY, PkeyDeleter> key_;
    };

}

#endif //KF_CLIENT_OPENSSLRSAENCRYPTOR_H
