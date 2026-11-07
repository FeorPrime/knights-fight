//
// Created by Бесхмельницкий Антон on 07.11.2026.
//

#include "services/AvatarService.h"
#include <cctype>
#include <cstddef>
#include <format>
#include <fstream>
#include <ios>
#include <iterator>
#include <optional>
#include <string>
#include <vector>

namespace services {

    namespace {

        struct ImageType {
            const char* extension;
            const char* contentType;
        };

        // The extension is a convenience for the player - the server decides
        // what the file really is by looking at its magic bytes.
        std::optional<ImageType> imageTypeFor(const std::string& path)
        {
            const auto dot = path.find_last_of('.');
            if (dot == std::string::npos) return std::nullopt;

            std::string extension = path.substr(dot + 1);
            for (auto& c : extension) c = static_cast<char>(std::tolower(static_cast<unsigned char>(c)));

            if (extension == "png") return ImageType{".png", "image/png"};
            if (extension == "jpg" || extension == "jpeg") return ImageType{".jpg", "image/jpeg"};
            return std::nullopt;
        }

    }

    AvatarService::AvatarService(std::shared_ptr<http::IHttpClient> http, cfg::AppConfig cfg)
        : http_(std::move(http)),
          cfg_(std::move(cfg)) {}

    auto AvatarService::uploadAvatar(long challengerId, const std::string& imagePath,
                                     const std::string& jwt)
        -> std::expected<std::string, std::string>
    {
        const auto imageType = imageTypeFor(imagePath);
        if (!imageType) {
            return std::unexpected(std::format(
                "'{}' is not a PNG or JPG file. Use a .png, .jpg or .jpeg image.", imagePath));
        }

        std::ifstream file(imagePath, std::ios::binary);
        if (!file) return std::unexpected(std::format("Cannot open '{}'.", imagePath));

        const std::vector<char> raw((std::istreambuf_iterator<char>(file)),
                                    std::istreambuf_iterator<char>());
        if (raw.empty()) return std::unexpected(std::format("'{}' is empty.", imagePath));

        std::vector<std::byte> data(raw.size());
        for (std::size_t i = 0; i < raw.size(); ++i) {
            data[i] = static_cast<std::byte>(static_cast<unsigned char>(raw[i]));
        }

        // The template holds a literal "{id}" - std::format would read that as a
        // named argument and throw, so the placeholder is replaced by hand.
        auto url = cfg_.challengerAvatarUrl;
        const auto placeholder = url.find("{id}");
        if (placeholder == std::string::npos) {
            return std::unexpected("The avatar URL template has no '{id}' placeholder.");
        }
        url.replace(placeholder, 4, std::to_string(challengerId));

        const http::MultipartFile part{
            .fieldName = "avatar",
            .fileName = std::format("avatar{}", imageType->extension),
            .contentType = imageType->contentType,
            .data = std::move(data)
        };

        auto response = http_->postMultipart(url, part, jwt);
        if (!response) return std::unexpected(response.error());

        if (response->status < 200 || response->status >= 300) {
            return std::unexpected(std::format("Avatar upload failed (HTTP {})", response->status));
        }

        return response->body;
    }

}
