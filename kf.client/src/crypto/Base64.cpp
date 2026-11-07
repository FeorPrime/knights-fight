//
// Created by Бесхмельницкий Антон on 19.10.2026.
//

#include "crypto/Base64.h"
#include <cstddef>
#include <span>
#include <openssl/bio.h>
#include <openssl/buffer.h>
#include <openssl/evp.h>
#include <format>

namespace crypto {

    auto base64Encode(std::span<const std::byte> in) -> std::string {
        if (in.empty()) return {};
        BIO* b64 = BIO_new(BIO_f_base64());
        BIO* mem  = BIO_new(BIO_s_mem());
        b64 = BIO_push(b64, mem);
        BIO_set_flags(b64, BIO_FLAGS_BASE64_NO_NL);
        BIO_write(b64, in.data(), static_cast<int>(in.size()));
        BIO_flush(b64);

        BUF_MEM* bptr{};
        BIO_get_mem_ptr(mem, &bptr);
        std::string out(bptr->data, bptr->length);
        BIO_free_all(b64);
        return out;
    }

    auto base64Encode(std::string_view in) -> std::string {
        return base64Encode(std::as_bytes(std::span{in.data(), in.size()}));
    }

    auto base64Decode(std::string_view in)
        -> std::expected<std::vector<std::byte>, std::string>
    {
        if (in.empty()) return std::vector<std::byte>{};
        BIO* b64 = BIO_new(BIO_f_base64());
        BIO* mem  = BIO_new_mem_buf(in.data(), static_cast<int>(in.size()));
        mem = BIO_push(b64, mem);
        BIO_set_flags(mem, BIO_FLAGS_BASE64_NO_NL);

        std::vector<std::byte> out(in.size());
        int n = BIO_read(mem, out.data(), static_cast<int>(out.size()));
        BIO_free_all(b64);
        if (n < 0) return std::unexpected<std::string>("base64 decode failed");
        out.resize(static_cast<std::size_t>(n));
        return out;
    }

    auto base64UrlDecode(std::string_view in) -> std::expected<std::string, std::string> {
        std::string translated;
        translated.reserve(in.size() + 4);
        for (const char c : in) {
            if (c == '-') translated += '+';
            else if (c == '_') translated += '/';
            else translated += c;
        }
        while (translated.size() % 4 != 0) {
            translated += '=';
        }

        const auto decoded = base64Decode(translated);
        if (!decoded) return std::unexpected(decoded.error());

        const auto& bytes = *decoded;
        return std::string(reinterpret_cast<const char*>(bytes.data()), bytes.size());
    }

}