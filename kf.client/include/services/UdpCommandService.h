#ifndef KF_CLIENT_UDPCOMMANDSERVICE_H
#define KF_CLIENT_UDPCOMMANDSERVICE_H

#include "cfg/AppConfig.h"
#include "models/UdpPacket.h"
#include "udp/IUdpClient.h"

#include <chrono>
#include <expected>
#include <memory>
#include <string>

namespace services {

    class UdpCommandService {
    public:
        UdpCommandService(
            std::shared_ptr<udp::IUdpClient> udpClient,
            cfg::AppConfig cfg
        );

        // Build a UdpPacket resolving OTP seed into TOTP code if needed
        [[nodiscard]] static auto createPacket(
            const std::string& challengerName,
            const std::string& otpSeedOrCode,
            const std::string& commandText,
            uint32_t sequenceNumber = 1,
            std::optional<int64_t> timestampMs = std::nullopt
        ) -> std::expected<models::UdpPacket, std::string>;

        // Send a prepared packet to configured or custom host/port
        auto sendPacket(
            const models::UdpPacket& packet,
            std::optional<std::string> host = std::nullopt,
            std::optional<int> port = std::nullopt
        ) -> std::expected<size_t, std::string>;

        // High-level: build and send command in one call
        auto sendCommand(
            const std::string& challengerName,
            const std::string& otpSeedOrCode,
            const std::string& commandText,
            uint32_t sequenceNumber = 1,
            std::optional<std::string> host = std::nullopt,
            std::optional<int> port = std::nullopt
        ) -> std::expected<models::UdpPacket, std::string>;

        // Helper to validate whether a command conforms to game server protocol
        [[nodiscard]] static bool isStandardGameCommand(std::string_view commandText);

    private:
        std::shared_ptr<udp::IUdpClient> udpClient_;
        cfg::AppConfig cfg_;
    };

} // namespace services

#endif // KF_CLIENT_UDPCOMMANDSERVICE_H
