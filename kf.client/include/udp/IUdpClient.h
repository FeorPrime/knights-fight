#ifndef KF_CLIENT_IUDPCLIENT_H
#define KF_CLIENT_IUDPCLIENT_H

#include <expected>
#include <span>
#include <string>
#include <string_view>

namespace udp {

    class IUdpClient {
    public:
        virtual ~IUdpClient() = default;

        [[nodiscard]] virtual auto send(
            const std::string& host,
            int port,
            std::span<const std::byte> data
        ) -> std::expected<size_t, std::string> = 0;

        [[nodiscard]] virtual auto send(
            const std::string& host,
            int port,
            std::string_view message
        ) -> std::expected<size_t, std::string> = 0;
    };

} // namespace udp

#endif // KF_CLIENT_IUDPCLIENT_H
