#include "udp/UdpClient.h"

#include <cstring>
#include <format>

#ifdef _WIN32
#include <winsock2.h>
#include <ws2tcpip.h>
#pragma comment(lib, "ws2_32.lib")
#else
#include <arpa/inet.h>
#include <netdb.h>
#include <netinet/in.h>
#include <sys/socket.h>
#include <sys/types.h>
#include <unistd.h>
#endif

namespace udp {

    namespace {
#ifdef _WIN32
        class WinsockInit {
        public:
            WinsockInit() {
                WSADATA wsaData;
                WSAStartup(MAKEWORD(2, 2), &wsaData);
            }
            ~WinsockInit() {
                WSACleanup();
            }
        };

        void ensureWinsock() {
            static WinsockInit init;
        }

        void closeSocket(SOCKET s) {
            closesocket(s);
        }
#else
        void ensureWinsock() {}

        void closeSocket(int s) {
            close(s);
        }
#endif
    } // namespace

    UdpClient::UdpClient() {
        ensureWinsock();
    }

    UdpClient::~UdpClient() = default;

    auto UdpClient::send(
        const std::string& host,
        int port,
        std::span<const std::byte> data
    ) -> std::expected<size_t, std::string> {
        if (data.empty()) {
            return std::unexpected("Cannot send empty UDP packet");
        }

        struct addrinfo hints{};
        hints.ai_family = AF_UNSPEC;
        hints.ai_socktype = SOCK_DGRAM;
        hints.ai_protocol = IPPROTO_UDP;

        struct addrinfo* res = nullptr;
        std::string portStr = std::to_string(port);
        int err = getaddrinfo(host.c_str(), portStr.c_str(), &hints, &res);
        if (err != 0) {
            return std::unexpected(std::format("getaddrinfo failed for {}:{}: {}", host, port, gai_strerror(err)));
        }

#ifdef _WIN32
        SOCKET sockfd = socket(res->ai_family, res->ai_socktype, res->ai_protocol);
        if (sockfd == INVALID_SOCKET) {
            freeaddrinfo(res);
            return std::unexpected(std::format("Failed to create UDP socket: WSAGetLastError={}", WSAGetLastError()));
        }
#else
        int sockfd = socket(res->ai_family, res->ai_socktype, res->ai_protocol);
        if (sockfd < 0) {
            freeaddrinfo(res);
            return std::unexpected(std::format("Failed to create UDP socket: {}", strerror(errno)));
        }
#endif

        auto sent = sendto(
            sockfd,
            reinterpret_cast<const char*>(data.data()),
            static_cast<int>(data.size()),
            0,
            res->ai_addr,
            static_cast<socklen_t>(res->ai_addrlen)
        );

        freeaddrinfo(res);
        closeSocket(sockfd);

        if (sent < 0) {
#ifdef _WIN32
            return std::unexpected(std::format("sendto failed: WSAGetLastError={}", WSAGetLastError()));
#else
            return std::unexpected(std::format("sendto failed: {}", strerror(errno)));
#endif
        }

        return static_cast<size_t>(sent);
    }

    auto UdpClient::send(
        const std::string& host,
        int port,
        std::string_view message
    ) -> std::expected<size_t, std::string> {
        return send(
            host,
            port,
            std::as_bytes(std::span{message.data(), message.size()})
        );
    }

} // namespace udp
