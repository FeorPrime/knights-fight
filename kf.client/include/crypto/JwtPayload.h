//
// Created by Бесхмельницкий Антон on 07.11.2026.
//

#ifndef KF_CLIENT_JWTPAYLOAD_H
#define KF_CLIENT_JWTPAYLOAD_H

#include <expected>
#include <string>
#include <string_view>

namespace crypto {

    /// Reads the "sub" claim of a JWT - the id of the challenger it was issued for.
    [[nodiscard]] auto extractSubject(std::string_view jwt) -> std::expected<long, std::string>;

}

#endif //KF_CLIENT_JWTPAYLOAD_H
