using Docker.DotNet;
using Docker.DotNet.Models;

namespace CodeBeaker.Integration.Tests.TestHelpers;

/// <summary>
/// Docker 이미지 존재 여부 확인 및 테스트 스킵 헬퍼
/// </summary>
public static class DockerTestHelper
{
    private static readonly DockerClient _client = new DockerClientConfiguration().CreateClient();
    private static readonly Dictionary<string, bool> _imageCache = new();

    /// <summary>
    /// Docker 이미지가 존재하는지 확인
    /// </summary>
    public static async Task<bool> ImageExistsAsync(string imageName)
    {
        if (_imageCache.TryGetValue(imageName, out var cached))
            return cached;

        try
        {
            var images = await _client.Images.ListImagesAsync(
                new ImagesListParameters
                {
                    Filters = new Dictionary<string, IDictionary<string, bool>>
                    {
                        ["reference"] = new Dictionary<string, bool> { [imageName] = true }
                    }
                });

            var exists = images.Any();
            _imageCache[imageName] = exists;
            return exists;
        }
        catch
        {
            _imageCache[imageName] = false;
            return false;
        }
    }

    /// <summary>
    /// 필요한 Docker 이미지가 모두 존재하는지 확인
    /// </summary>
    public static async Task<bool> AllImagesExistAsync()
    {
        var requiredImages = new[]
        {
            "codebeaker-python:latest",
            "codebeaker-nodejs:latest",
            "codebeaker-golang:latest",
            "codebeaker-dotnet:latest"
        };

        foreach (var image in requiredImages)
        {
            if (!await ImageExistsAsync(image))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Docker가 실행 중인지 확인
    /// </summary>
    public static async Task<bool> IsDockerRunningAsync()
    {
        try
        {
            await _client.System.PingAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 테스트 스킵 이유 메시지 생성
    /// </summary>
    public static async Task<string?> GetSkipReasonAsync()
    {
        if (!await IsDockerRunningAsync())
            return "Docker is not running. Start Docker Desktop and try again.";

        if (!await AllImagesExistAsync())
            return "Docker runtime images are not built. Run: .\\scripts\\build-runtime-images.ps1";

        return null; // null이면 테스트 실행
    }
}
