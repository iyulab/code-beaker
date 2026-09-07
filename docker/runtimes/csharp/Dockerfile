# C# 런타임 Docker 이미지
FROM mcr.microsoft.com/dotnet/sdk:10.0

# 이 이미지에는 10.0 런타임 하나만 들어 있다. 사용자 코드가 그보다 낮은 세대를
# 타깃하면(net8.0 등) 호스트가 요청한 런타임을 못 찾고 실패하므로, 상위 메이저로
# 넘어가도록 열어 둔다. 8.0 런타임을 나란히 깔지 않는 이유는 그것이 2026-11 에
# 지원을 마쳐 무패치 런타임을 계속 배포하게 되기 때문이다.
ENV DOTNET_ROLL_FORWARD=Major

# 보안: 비root 사용자 생성.
# .NET 10 SDK 이미지는 Ubuntu 기반이라 UID 1000 을 stock `ubuntu` 계정이 이미 쓰고 있다.
# 이 리포의 런타임 이미지 네 개와 helm securityContext(`runAsUser: 1000`)가 모두 UID 1000 을
# 전제하므로, 그 자리를 비우고 coderunner 에게 준다.
RUN if id -u ubuntu >/dev/null 2>&1; then userdel -r ubuntu; fi \
    && useradd -m -u 1000 -s /bin/bash coderunner

# 작업 디렉토리 설정 및 권한 부여
WORKDIR /workspace
RUN chown coderunner:coderunner /workspace

# 사용자 전환
USER coderunner

# 기본 명령어
CMD ["dotnet", "run", "--project", "/workspace"]
