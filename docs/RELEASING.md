# 릴리스 절차

CodeBeaker는 세 개의 NuGet 패키지(`CodeBeaker.Core`·`CodeBeaker.Commands`·`CodeBeaker.Runtimes`)와
API 서버(GitHub Release의 tarball + GHCR 런타임 이미지)를 함께 릴리스한다. 릴리스는 **`vX.Y.Z` 태그 push 한 번**으로
시작되고, 그 뒤는 `.github/workflows/ci.yml`이 한다.

## 버전의 단일 출처

버전은 리포 루트 `Directory.Build.props`의 `<Version>` 요소 하나다 — 세 패키지는 항상 같은 버전으로 함께 나간다.
태그는 트리거일 뿐이며, 발행 잡은 태그(`vX.Y.Z`)와 이 값이 같은지 **발행 전에** 확인하고 다르면 실패한다. 태그에서 버전을
뽑아 쓰지 않는 이유는 로컬 `dotnet pack`(props 기준)과 CI 발행본(태그 기준)의 버전이 갈라지는 것을 막기 위해서다.
패키지 메타데이터(저자·라이선스·리포 URL)도 같은 파일에 있고, 패키지로 나가는 프로젝트만 csproj에서 `IsPackable=true`로 opt-in 한다.

## 절차

1. **버전 상승** — `Directory.Build.props`의 `<Version>`을 올리고, `CHANGELOG.md`에 `## [X.Y.Z]` 절을 채운다.
   `0.x` 단계에서는 깨는 변경이 마이너 릴리스에 들어가므로 `### Removed`/`### Changed`를 특히 정확히 적는다.
2. **커밋·push 후 CI 그린 확인** — 태그를 밀기 전에 `main`의 CI가 통과하는지 본다. 릴리스 잡들은 빌드·테스트·
   품질·보안 잡을 `needs`로 물고 있어 그중 하나라도 실패하면 아무것도 게시되지 않는다.
3. **태그 push**

   ```bash
   git tag vX.Y.Z
   git push origin vX.Y.Z
   ```

4. **CI가 하는 것** (전부 태그 push에서만 실행된다)
   - `prepare-release` — API 서버 tarball을 GitHub Release에 첨부(릴리스 노트 자동 생성)
   - `publish-runtime-images` — 런타임 이미지 4종을 GHCR에 `:latest`와 `:vX.Y.Z`로 push
   - `publish-nuget` — 패키지 3종을 pack 해 nuget.org에 push. 같은 버전이 이미 있으면 건너뛴다(`--skip-duplicate`)라
     실패한 실행을 다시 돌려도 안전하다.
5. **확인** — nuget.org에서 세 패키지의 새 버전이 보이는지, GitHub Release와 GHCR 태그가 생겼는지 본다.

태그와 `<Version>`이 어긋나 발행 잡이 실패했다면: 태그를 지우고(`git push --delete origin vX.Y.Z`, `git tag -d vX.Y.Z`),
`Directory.Build.props`를 고쳐 커밋한 뒤 같은 태그를 다시 민다. 단, `prepare-release`·`publish-runtime-images`는 발행 잡과 독립이라
이미 게시됐을 수 있다 — 그 경우 GitHub Release는 삭제하고 다시 만든다.

## 1회 설정 — nuget.org Trusted Publishing

발행 잡은 장기 API 키를 쓰지 않는다. GitHub Actions의 OIDC 토큰을 nuget.org가 1시간짜리 임시 API 키로 바꿔 준다
([Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing), [NuGet/login](https://github.com/NuGet/login)).
처음 한 번만 다음을 해 둔다.

1. nuget.org에 로그인 → 사용자 메뉴 → **Trusted Publishing** → 정책 추가:
   - Repository Owner: `iyulab`
   - Repository: `code-beaker`
   - Workflow File: `ci.yml` (파일명만, 경로 없이)
   - Environment: 비움 (워크플로가 GitHub 환경을 쓰지 않는다)
   - 정책 소유자: 패키지를 소유한 계정/조직과 같아야 한다. 이미 발행된 패키지에 새 버전을 올리는 것이므로
     정책 범위에 "기존 패키지의 새 버전 발행"이 포함돼야 한다.
2. 리포 **Settings → Secrets and variables → Actions**에 `NUGET_USER` 시크릿을 만든다. 값은 nuget.org **프로필명**이다
   (이메일 아님). 정책 소유자와 이 값은 기존 `CodeBeaker.*` 패키지를 소유한 계정(또는 그 조직)이어야 한다 — 다른 계정의
   정책으로는 기존 패키지에 새 버전을 올릴 수 없다.
3. 정책은 첫 발행이 성공할 때까지 "임시 활성(7일)"일 수 있다. 7일 안에 발행이 없으면 비활성이 되니 등록 직후
   릴리스를 한 번 하거나, 필요할 때 nuget.org에서 7일 창을 다시 연다.

## 로컬에서 발행본을 미리 확인하기

```bash
for proj in src/CodeBeaker.Core/CodeBeaker.Core.csproj src/CodeBeaker.Commands/CodeBeaker.Commands.csproj src/CodeBeaker.Runtimes/CodeBeaker.Runtimes.csproj; do
  dotnet pack "$proj" --configuration Release --output ./artifacts/nuget
done
```

CI와 같은 명령이다. CI에서는 `Directory.Build.props`가 `GITHUB_ACTIONS` 환경변수를 보고 `ContinuousIntegrationBuild`를 켜므로
로컬 팩과 CI 팩은 소스 경로 정규화만 다르다. `--no-build`는 쓰지 않는다 — 증분 판정이 일부 패키지만 다시 만들어 발행본의 출처가 섞일 수 있다.
