//
// Created by Бесхмельницкий Антон on 19.10.2026.
//

#include "crypto/OpensslRsaEncryptor.h"
#include <cstddef>
#include <memory>
#include <openssl/evp.h>
#include <openssl/pem.h>
#include <openssl/err.h>
#include <openssl/bio.h>
#include <openssl/rsa.h>
#include <format>
#include <span>

namespace crypto {

void OpensslRsaEncryptor::PkeyDeleter::operator()(EVP_PKEY* p) const {
    EVP_PKEY_free(p);
}

OpensslRsaEncryptor::~OpensslRsaEncryptor() = default;

namespace {
std::string lastOpenSslError() {
    unsigned long e = ERR_get_error();
    if (e == 0) return "unknown OpenSSL error";
    char buf[256];
    ERR_error_string_n(e, buf, sizeof(buf));
    return buf;
}
} // namespace

auto OpensslRsaEncryptor::loadPublicKeyPem(std::string_view pem)
    -> std::expected<void, std::string>
{
    BIO* bio = BIO_new_mem_buf(pem.data(), static_cast<int>(pem.size()));
    if (!bio) return std::unexpected(lastOpenSslError());

    EVP_PKEY* raw = PEM_read_bio_PUBKEY(bio, nullptr, nullptr, nullptr);
    BIO_free(bio);
    if (!raw) return std::unexpected("invalid PEM public key: " + lastOpenSslError());

    key_.reset(raw);
    return {};
}

auto OpensslRsaEncryptor::encrypt(std::span<const std::byte> plaintext)
    -> std::expected<std::vector<std::byte>, std::string>
{
    if (!key_) return std::unexpected<std::string>("public key not loaded");

    EVP_PKEY_CTX* ctx = EVP_PKEY_CTX_new(key_.get(), nullptr);    if (!ctx) return std::unexpected(lastOpenSslError());
    std::unique_ptr<EVP_PKEY_CTX, decltype(&EVP_PKEY_CTX_free)> guard{
        ctx, &EVP_PKEY_CTX_free};

    if (EVP_PKEY_encrypt_init(ctx) <= 0)
        return std::unexpected("encrypt_init: " + lastOpenSslError());
    if (EVP_PKEY_CTX_set_rsa_padding(ctx, RSA_PKCS1_OAEP_PADDING) <= 0)
        return std::unexpected("set_padding: " + lastOpenSslError());
    if (EVP_PKEY_CTX_set_rsa_oaep_md(ctx, EVP_sha256()) <= 0)
        return std::unexpected("set_oaep_md: " + lastOpenSslError());
    if (EVP_PKEY_CTX_set_rsa_mgf1_md(ctx, EVP_sha256()) <= 0)
        return std::unexpected("set_mgf1_md: " + lastOpenSslError());

    std::size_t outLen = 0;
    if (EVP_PKEY_encrypt(ctx, nullptr, &outLen,
                         reinterpret_cast<const unsigned char*>(plaintext.data()),
                         plaintext.size()) <= 0) {
        return std::unexpected("size query: " + lastOpenSslError());
    }

    std::vector<std::byte> out(outLen);
    if (EVP_PKEY_encrypt(ctx,
                         reinterpret_cast<unsigned char*>(out.data()), &outLen,
                         reinterpret_cast<const unsigned char*>(plaintext.data()),
                         plaintext.size()) <= 0) {
        return std::unexpected("encrypt: " + lastOpenSslError());
    }
    out.resize(outLen);
    return out;
}

}
