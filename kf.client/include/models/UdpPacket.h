#ifndef KF_CLIENT_UDPPACKET_H
#define KF_CLIENT_UDPPACKET_H

#include <cstdint>
#include <expected>
#include <nlohmann/json.hpp>
#include <string>

namespace models {

    struct UdpPacket {
        uint8_t protocolVersion = 1; // "v"
        uint32_t sequenceNumber = 0; // "seq"
        int64_t timestamp = 0;       // "ts" (unix milliseconds)
        std::string challengerName;  // "n"
        std::string otpCode;         // "otp"
        std::string commandText;     // "cmd"

        [[nodiscard]] std::string toJson() const {
            nlohmann::json j;
            to_json(j, *this);
            return j.dump();
        }

        [[nodiscard]] static auto fromJson(const std::string& jsonStr)
            -> std::expected<UdpPacket, std::string> {
            auto j = nlohmann::json::parse(jsonStr, nullptr, false);
            if (j.is_discarded()) {
                return std::unexpected("Invalid JSON for UdpPacket");
            }
            try {
                UdpPacket p;
                from_json(j, p);
                return p;
            } catch (const std::exception& e) {
                return std::unexpected(e.what());
            }
        }

        friend void to_json(nlohmann::json& j, const UdpPacket& p) {
            j = nlohmann::json{
                {"v", p.protocolVersion},
                {"seq", p.sequenceNumber},
                {"ts", p.timestamp},
                {"n", p.challengerName},
                {"otp", p.otpCode},
                {"cmd", p.commandText}
            };
        }

        friend void from_json(const nlohmann::json& j, UdpPacket& p) {
            j.at("v").get_to(p.protocolVersion);
            j.at("seq").get_to(p.sequenceNumber);
            j.at("ts").get_to(p.timestamp);
            j.at("n").get_to(p.challengerName);
            j.at("otp").get_to(p.otpCode);
            j.at("cmd").get_to(p.commandText);
        }
    };

} // namespace models

#endif // KF_CLIENT_UDPPACKET_H
