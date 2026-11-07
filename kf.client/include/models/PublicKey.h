//
// Created by Бесхмельницкий Антон on 19.10.2026.
//

#ifndef KF_CLIENT_PUBLICKEY_H
#define KF_CLIENT_PUBLICKEY_H

#include <string>

namespace models {

    // Value object — PEM text lives here for the whole session.
    struct PublicKey {
        std::string pem;
    };

}

#endif //KF_CLIENT_PUBLICKEY_H
