#ifndef KF_CLIENT_UDPCLIENT_H
#define KF_CLIENT_UDPCLIENT_H

#include "udp/IUdpClient.h"

namespace udp {

    class UdpClient : public IUdpClient {
    public:
        UdpClient();
        ~UdpClient() override;

        [[nodiscard]] auto send(
            const std::string& host,
            int port,
            std::span<const std::byte> data
        ) -> std::expected<size_t, std::string> override;

        [[nodiscard]] auto send(
            const std::string& host,
            int port,
            std::string_view message
        ) -> std::expected<size_t, std::string> override;
    };

} // namespace udp

#endif // KF_CLIENT_UDPCLIENT_H
