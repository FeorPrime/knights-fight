#include "services/UdpCommandService.h"
#include "crypto/OtpService.h"

#include <algorithm>
#include <chrono>
#include <format>
#include <regex>

namespace services {

    namespace {
        bool isSixDigits(std::string_view str) {
            return str.size() == 6 && std::all_of(str.begin(), str.end(), [](char c) {
                return c >= '0' && c <= '9';
            });
        }
    } // namespace

    UdpCommandService::UdpCommandService(
        std::shared_ptr<udp::IUdpClient> udpClient,
        cfg::AppConfig cfg
    )
        : udpClient_(std::move(udpClient)),
          cfg_(std::move(cfg)) {}

    auto UdpCommandService::createPacket(
        const std::string& challengerName,
        const std::string& otpSeedOrCode,
        const std::string& commandText,
        uint32_t sequenceNumber,
        std::optional<int64_t> timestampMs
    ) -> std::expected<models::UdpPacket, std::string> {
        if (challengerName.empty()) {
            return std::unexpected("Challenger name cannot be empty");
        }
        if (commandText.empty()) {
            return std::unexpected("Command text cannot be empty");
        }
        if (otpSeedOrCode.empty()) {
            return std::unexpected("OTP seed or code cannot be empty");
        }

        std::string otpCode;
        if (isSixDigits(otpSeedOrCode)) {
            otpCode = otpSeedOrCode;
        } else {
            auto otpResult = crypto::OtpService::generateTotp(otpSeedOrCode);
            if (!otpResult) {
                return std::unexpected(std::format("Failed to generate TOTP from seed: {}", otpResult.error()));
            }
            otpCode = *otpResult;
        }

        int64_t ts = timestampMs.value_or(
            std::chrono::duration_cast<std::chrono::milliseconds>(
                std::chrono::system_clock::now().time_since_epoch()
            ).count()
        );

        models::UdpPacket packet{
            .protocolVersion = 1,
            .sequenceNumber = sequenceNumber,
            .timestamp = ts,
            .challengerName = challengerName,
            .otpCode = std::move(otpCode),
            .commandText = commandText
        };

        return packet;
    }

    auto UdpCommandService::sendPacket(
        const models::UdpPacket& packet,
        std::optional<std::string> host,
        std::optional<int> port
    ) -> std::expected<size_t, std::string> {
        if (!udpClient_) {
            return std::unexpected("UDP client is not initialized");
        }

        std::string targetHost = host.value_or(cfg_.udpHost);
        int targetPort = port.value_or(cfg_.udpPort);

        std::string jsonPayload = packet.toJson();
        return udpClient_->send(targetHost, targetPort, jsonPayload);
    }

    auto UdpCommandService::sendCommand(
        const std::string& challengerName,
        const std::string& otpSeedOrCode,
        const std::string& commandText,
        uint32_t sequenceNumber,
        std::optional<std::string> host,
        std::optional<int> port
    ) -> std::expected<models::UdpPacket, std::string> {
        auto packet = createPacket(challengerName, otpSeedOrCode, commandText, sequenceNumber);
        if (!packet) {
            return std::unexpected(packet.error());
        }

        auto sendResult = sendPacket(*packet, std::move(host), port);
        if (!sendResult) {
            return std::unexpected(sendResult.error());
        }

        return *packet;
    }

    bool UdpCommandService::isStandardGameCommand(std::string_view commandText) {
        static const std::regex movePattern(R"(^\s*move\s+(up|down|left|right)\s*$)", std::regex::icase);
        static const std::regex handPattern(R"(^\s*use\s+hand_(left|right)\s*$)", std::regex::icase);
        static const std::regex turnPattern(R"(^\s*turn\s+(left|right)\s*$)", std::regex::icase);

        std::string text(commandText);
        return std::regex_match(text, movePattern) ||
               std::regex_match(text, handPattern) ||
               std::regex_match(text, turnPattern);
    }

} // namespace services
