//
// Created by Бесхмельницкий Антон on 20.10.2026.
//

#include "crypto/ClientKeyManager.h"
#include <openssl/pem.h>
#include <openssl/err.h>
#include <openssl/rsa.h>
#include <format>
#include <span>

namespace crypto {

void ClientKeyManager::PkeyDeleter::operator()(EVP_PKEY* p) const { EVP_PKEY_free(p); }
ClientKeyManager::ClientKeyManager() = default;
ClientKeyManager::~ClientKeyManager() = default;

namespace {
std::string lastOpenSslError() {
    unsigned long e = ERR_get_error();
    if (e == 0) return "unknown OpenSSL error";
    char buf[256]; ERR_error_string_n(e, buf, sizeof(buf)); return buf;
}
} // namespace

auto ClientKeyManager::generate() -> std::expected<void, std::string> {
    EVP_PKEY_CTX* ctx = EVP_PKEY_CTX_new_id(EVP_PKEY_RSA, nullptr);
    if (!ctx) return std::unexpected(lastOpenSslError());
    std::unique_ptr<EVP_PKEY_CTX, decltype(&EVP_PKEY_CTX_free)> guard{ctx, &EVP_PKEY_CTX_free};

    if (EVP_PKEY_keygen_init(ctx) <= 0) return std::unexpected("keygen_init: " + lastOpenSslError());
    if (EVP_PKEY_CTX_set_rsa_keygen_bits(ctx, 2048) <= 0) return std::unexpected("set_bits: " + lastOpenSslError());

    EVP_PKEY* pkey = nullptr;
    if (EVP_PKEY_keygen(ctx, &pkey) <= 0) return std::unexpected("keygen: " + lastOpenSslError());

    keyPair_.reset(pkey);
    return {};
}

auto ClientKeyManager::getPublicKeyPem() const -> std::string {
    if (!keyPair_) return "";
    BIO* bio = BIO_new(BIO_s_mem());
    PEM_write_bio_PUBKEY(bio, keyPair_.get());
    BUF_MEM* bptr{}; BIO_get_mem_ptr(bio, &bptr);
    std::string pem(bptr->data, bptr->length);
    BIO_free(bio);
    return pem;
}

auto ClientKeyManager::decrypt(std::span<const std::byte> ciphertext)
    -> std::expected<std::vector<std::byte>, std::string>
{
    if (!keyPair_) return std::unexpected<std::string>("client key pair not generated");

    EVP_PKEY_CTX* ctx = EVP_PKEY_CTX_new(keyPair_.get(), nullptr);
    if (!ctx) return std::unexpected(lastOpenSslError());
    std::unique_ptr<EVP_PKEY_CTX, decltype(&EVP_PKEY_CTX_free)> guard{ctx, &EVP_PKEY_CTX_free};

    if (EVP_PKEY_decrypt_init(ctx) <= 0) return std::unexpected("decrypt_init: " + lastOpenSslError());
    if (EVP_PKEY_CTX_set_rsa_padding(ctx, RSA_PKCS1_OAEP_PADDING) <= 0) return std::unexpected("set_padding: " + lastOpenSslError());
    if (EVP_PKEY_CTX_set_rsa_oaep_md(ctx, EVP_sha256()) <= 0) return std::unexpected("set_oaep_md: " + lastOpenSslError());
    if (EVP_PKEY_CTX_set_rsa_mgf1_md(ctx, EVP_sha256()) <= 0) return std::unexpected("set_mgf1_md: " + lastOpenSslError());

    size_t outLen = 0;
    if (EVP_PKEY_decrypt(ctx, nullptr, &outLen, reinterpret_cast<const unsigned char*>(ciphertext.data()), ciphertext.size()) <= 0)
        return std::unexpected("size query: " + lastOpenSslError());

    std::vector<std::byte> out(outLen);
    if (EVP_PKEY_decrypt(ctx, reinterpret_cast<unsigned char*>(out.data()), &outLen,
                         reinterpret_cast<const unsigned char*>(ciphertext.data()), ciphertext.size()) <= 0)
        return std::unexpected("decrypt: " + lastOpenSslError());

    out.resize(outLen);
    return out;
}

}