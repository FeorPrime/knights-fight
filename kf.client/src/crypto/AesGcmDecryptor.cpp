//
// Created by Бесхмельницкий Антон on 20.10.2026.
//
#include "crypto/AesGcmDecryptor.h"

#include <expected>
#include <openssl/evp.h>
#include <openssl/err.h>
#include <format>
#include <memory>

namespace crypto {
    namespace {
        std::string lastOpenSslError() {
            unsigned long e = ERR_get_error();
            if (e == 0) return "unknown OpenSSL error";
            char buf[256];
            ERR_error_string_n(e, buf, sizeof(buf));
            return buf;
        }
    } // namespace

    auto AesGcmDecryptor::decrypt(std::span<const std::byte> key,
                              std::span<const std::byte> nonce,
                              std::span<const std::byte> ciphertext,
                              std::span<const std::byte> tag)
    -> std::expected<std::string, std::string>
{
    if (key.size() != 32) return std::unexpected("AES key must be 32 bytes");
    if (nonce.size() != 12) return std::unexpected("GCM nonce must be 12 bytes");
    if (tag.size() != 16) return std::unexpected("GCM tag must be 16 bytes");

    EVP_CIPHER_CTX* ctx = EVP_CIPHER_CTX_new();
    if (!ctx) return std::unexpected(lastOpenSslError());
    std::unique_ptr<EVP_CIPHER_CTX, decltype(&EVP_CIPHER_CTX_free)> guard{ctx, &EVP_CIPHER_CTX_free};

    // 1. Initialize cipher
    if (EVP_DecryptInit_ex(ctx, EVP_aes_256_gcm(), nullptr, nullptr, nullptr) <= 0)
        return std::unexpected("init: " + lastOpenSslError());

    // 2. Set IV length (12 bytes for GCM)
    if (EVP_CIPHER_CTX_ctrl(ctx, EVP_CTRL_GCM_SET_IVLEN, 12, nullptr) <= 0)
        return std::unexpected("set_ivlen: " + lastOpenSslError());

    // 3. Set key and nonce
    if (EVP_DecryptInit_ex(ctx, nullptr, nullptr,
                           reinterpret_cast<const unsigned char*>(key.data()),
                           reinterpret_cast<const unsigned char*>(nonce.data())) <= 0)
        return std::unexpected("set_key_nonce: " + lastOpenSslError());

    // 4. Decrypt the ciphertext directly (NO nullptr call for GCM!)
    std::vector<std::byte> out(ciphertext.size());
    int outLen = 0;
    if (EVP_DecryptUpdate(ctx, reinterpret_cast<unsigned char*>(out.data()), &outLen,
                          reinterpret_cast<const unsigned char*>(ciphertext.data()),
                          static_cast<int>(ciphertext.size())) <= 0)
        return std::unexpected("decrypt_update: " + lastOpenSslError());

    // 5. Set the expected tag BEFORE finalizing
    if (EVP_CIPHER_CTX_ctrl(ctx, EVP_CTRL_GCM_SET_TAG, 16,
                            const_cast<unsigned char*>(reinterpret_cast<const unsigned char*>(tag.data()))) <= 0)
        return std::unexpected("set_tag: " + lastOpenSslError());

    // 6. Finalize and verify the tag
    int finalLen = 0;
    if (EVP_DecryptFinal_ex(ctx, reinterpret_cast<unsigned char*>(out.data()) + outLen, &finalLen) <= 0)
        return std::unexpected("GCM tag verification failed (invalid ciphertext or key)");

    out.resize(static_cast<size_t>(outLen + finalLen));
    return std::string(reinterpret_cast<const char*>(out.data()), out.size());
}
}
