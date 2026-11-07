cmake_minimum_required(VERSION 3.28)
project(kf_client LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

find_package(OpenSSL REQUIRED)

include(cmake/FetchDeps.cmake)

add_executable(kf_client main.cpp
        src/cli/CliApp.cpp
        src/crypto/AesGcmDecryptor.cpp
        src/crypto/Base32.cpp
        src/crypto/Base64.cpp
        src/crypto/ClientKeyManager.cpp
        src/crypto/JwtPayload.cpp
        src/crypto/OpensslRsaEncryptor.cpp
        src/crypto/OtpService.cpp
        src/services/AuthService.cpp
        src/services/AvatarService.cpp
        src/services/ChallengerService.cpp
        src/services/KeyService.cpp
        src/services/UdpCommandService.cpp
        src/http/CprHttpClient.cpp
        src/udp/UdpClient.cpp
)

target_include_directories(kf_client PRIVATE
        "${CMAKE_CURRENT_SOURCE_DIR}/include"
        "${CMAKE_CURRENT_SOURCE_DIR}/src/"
)

target_link_libraries(kf_client PRIVATE
        OpenSSL::SSL
        OpenSSL::Crypto
        cpr::cpr
        nlohmann_json::nlohmann_json
)

add_executable(test_otp
        tests/test_otp.cpp
        src/crypto/Base32.cpp
        src/crypto/Base64.cpp
        src/crypto/OtpService.cpp
)

target_include_directories(test_otp PRIVATE
        "${CMAKE_CURRENT_SOURCE_DIR}/include"
        "${CMAKE_CURRENT_SOURCE_DIR}/src/"
)

target_link_libraries(test_otp PRIVATE
        OpenSSL::SSL
        OpenSSL::Crypto
)

add_executable(test_udp
        tests/test_udp.cpp
        src/crypto/Base32.cpp
        src/crypto/Base64.cpp
        src/crypto/OtpService.cpp
        src/services/UdpCommandService.cpp
        src/udp/UdpClient.cpp
)

target_include_directories(test_udp PRIVATE
        "${CMAKE_CURRENT_SOURCE_DIR}/include"
        "${CMAKE_CURRENT_SOURCE_DIR}/src/"
)

target_link_libraries(test_udp PRIVATE
        OpenSSL::SSL
        OpenSSL::Crypto
        nlohmann_json::nlohmann_json
)

# Platform-specific niceties
if(MSVC)
    target_compile_options(kf_client PRIVATE /W4 /utf-8)
    target_compile_options(test_otp PRIVATE /W4 /utf-8)
    target_compile_options(test_udp PRIVATE /W4 /utf-8)
else()
    target_compile_options(kf_client PRIVATE -Wall -Wextra -Wpedantic)
    target_compile_options(test_otp PRIVATE -Wall -Wextra -Wpedantic)
    target_compile_options(test_udp PRIVATE -Wall -Wextra -Wpedantic)
endif()