using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Models;

namespace CodeBeaker.Core.Sessions;

/// <summary>
/// Session과 SessionData 간 변환 유틸리티
/// IExecutionEnvironment는 직렬화되지 않고 런타임에 재구성됨
/// </summary>
public static class SessionMapper
{
    /// <summary>
    /// Session → SessionData (직렬화 가능한 데이터)
    /// </summary>
    public static SessionData ToSessionData(Session session)
    {
        return new SessionData
        {
            SessionId = session.SessionId,
            ContainerId = session.ContainerId,
            EnvironmentId = session.EnvironmentId,
            RuntimeType = session.RuntimeType,
            Language = session.Language,
            CreatedAt = session.CreatedAt,
            LastActivity = session.LastActivity,
            State = session.State.ToString(),
            Config = ToConfigData(session.Config),
            Metadata = ConvertMetadata(session.Metadata),
            ExecutionCount = session.ExecutionCount
        };
    }

    /// <summary>
    /// SessionData → Session (Environment는 null로 설정, 이후 재구성 필요)
    /// </summary>
    public static Session FromSessionData(SessionData data)
    {
        return new Session
        {
            SessionId = data.SessionId,
            ContainerId = data.ContainerId,
            EnvironmentId = data.EnvironmentId,
            RuntimeType = data.RuntimeType,
            Environment = null, // 나중에 재구성
            Language = data.Language,
            CreatedAt = data.CreatedAt,
            LastActivity = data.LastActivity,
            State = Enum.Parse<SessionState>(data.State),
            Config = FromConfigData(data.Config),
            Metadata = ConvertMetadataBack(data.Metadata),
            ExecutionCount = data.ExecutionCount
        };
    }

    private static SessionConfigData ToConfigData(SessionConfig config)
    {
        return new SessionConfigData
        {
            Language = config.Language,
            RuntimePreference = config.RuntimePreference?.ToString(),
            RuntimeType = config.RuntimeType?.ToString(),
            DockerImage = config.DockerImage,
            IdleTimeoutMinutes = config.IdleTimeoutMinutes,
            MaxLifetimeMinutes = config.MaxLifetimeMinutes,
            PersistFilesystem = config.PersistFilesystem,
            MemoryLimitMB = config.MemoryLimitMB,
            CpuShares = config.CpuShares
        };
    }

    private static SessionConfig FromConfigData(SessionConfigData data)
    {
        return new SessionConfig
        {
            Language = data.Language,
            RuntimePreference = string.IsNullOrEmpty(data.RuntimePreference)
                ? null
                : Enum.Parse<Runtime.RuntimePreference>(data.RuntimePreference),
            RuntimeType = string.IsNullOrEmpty(data.RuntimeType)
                ? null
                : Enum.Parse<RuntimeType>(data.RuntimeType),
            DockerImage = data.DockerImage,
            IdleTimeoutMinutes = data.IdleTimeoutMinutes,
            MaxLifetimeMinutes = data.MaxLifetimeMinutes,
            PersistFilesystem = data.PersistFilesystem,
            MemoryLimitMB = data.MemoryLimitMB,
            CpuShares = data.CpuShares
        };
    }

    /// <summary>
    /// Metadata 변환 (object → string)
    /// </summary>
    private static Dictionary<string, string> ConvertMetadata(Dictionary<string, object> metadata)
    {
        return metadata.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value?.ToString() ?? string.Empty
        );
    }

    /// <summary>
    /// Metadata 역변환 (string → object)
    /// </summary>
    private static Dictionary<string, object> ConvertMetadataBack(Dictionary<string, string> metadata)
    {
        return metadata.ToDictionary(
            kvp => kvp.Key,
            kvp => (object)kvp.Value
        );
    }
}
