"""
Unified Diff 파일 편집 유틸리티

unidiff를 사용한 패치 파싱 및 적용
"""

import tempfile
from pathlib import Path
from typing import Optional, Union

import patch_ng
from unidiff import PatchSet


class PatchApplyError(Exception):
    """패치 적용 실패 예외"""

    pass


class PatchUtil:
    """Unified Diff 패치 적용 유틸리티"""

    @staticmethod
    def apply_patch(
        original_content: str, patch_content: str, strict: bool = True
    ) -> str:
        """
        Unified diff 패치를 적용

        Args:
            original_content: 원본 파일 내용
            patch_content: Unified diff 형식의 패치
            strict: 엄격 모드 (실패시 예외 발생)

        Returns:
            패치 적용된 내용

        Raises:
            PatchApplyError: 패치 적용 실패시
        """
        try:
            # unidiff로 파싱
            patchset = PatchSet(patch_content)

            if not patchset:
                raise PatchApplyError("Invalid or empty patch")

            # 원본 라인 분리
            original_lines = original_content.splitlines(keepends=True)

            # 각 파일 패치 처리 (단일 파일 가정)
            for patched_file in patchset:
                for hunk in patched_file:
                    # Hunk 적용
                    original_lines = PatchUtil._apply_hunk(
                        original_lines, hunk, strict
                    )

            # 결과 반환
            return "".join(original_lines)

        except Exception as e:
            raise PatchApplyError(f"Failed to apply patch: {e}") from e

    @staticmethod
    def _apply_hunk(
        lines: list[str], hunk, strict: bool = True
    ) -> list[str]:
        """
        단일 hunk를 라인 리스트에 적용

        Args:
            lines: 원본 라인 리스트
            hunk: Hunk 객체
            strict: 엄격 모드

        Returns:
            패치 적용된 라인 리스트
        """
        # hunk는 1-based, Python list는 0-based
        start_line = hunk.source_start - 1

        # 새 라인 리스트 생성
        new_lines = lines[:start_line]

        # Hunk 적용
        original_line_idx = start_line
        for line in hunk:
            if line.is_context:
                # 컨텍스트 라인: 그대로 유지
                if original_line_idx < len(lines):
                    new_lines.append(lines[original_line_idx])
                    original_line_idx += 1
            elif line.is_added:
                # 추가된 라인
                new_lines.append(line.value)
            elif line.is_removed:
                # 제거된 라인: 스킵
                original_line_idx += 1

        # 나머지 라인 추가
        new_lines.extend(lines[original_line_idx:])

        return new_lines

    @staticmethod
    def parse_patch(patch_content: str) -> PatchSet:
        """
        Unified diff 패치 파싱 및 메타데이터 추출

        Args:
            patch_content: Unified diff 형식의 패치

        Returns:
            PatchSet 객체 (파일 목록, 변경 통계 등)
        """
        try:
            return PatchSet(patch_content)
        except Exception as e:
            raise PatchApplyError(f"Failed to parse patch: {e}") from e

    @staticmethod
    def get_patch_stats(patch_content: str) -> dict:
        """
        패치 통계 정보 추출

        Args:
            patch_content: Unified diff 형식의 패치

        Returns:
            패치 통계 딕셔너리
            {
                "files_changed": int,
                "additions": int,
                "deletions": int,
                "files": [{"path": str, "additions": int, "deletions": int}]
            }
        """
        patchset = PatchUtil.parse_patch(patch_content)

        total_additions = 0
        total_deletions = 0
        files = []

        for patched_file in patchset:
            additions = patched_file.added
            deletions = patched_file.removed

            total_additions += additions
            total_deletions += deletions

            files.append(
                {
                    "path": patched_file.path,
                    "source_path": patched_file.source_file,
                    "target_path": patched_file.target_file,
                    "additions": additions,
                    "deletions": deletions,
                    "is_added_file": patched_file.is_added_file,
                    "is_removed_file": patched_file.is_removed_file,
                    "is_modified_file": patched_file.is_modified_file,
                }
            )

        return {
            "files_changed": len(patchset),
            "additions": total_additions,
            "deletions": total_deletions,
            "files": files,
        }

    @staticmethod
    def validate_patch(patch_content: str) -> tuple[bool, Optional[str]]:
        """
        패치 유효성 검증

        Args:
            patch_content: Unified diff 형식의 패치

        Returns:
            (유효 여부, 에러 메시지)
        """
        try:
            patchset = PatchUtil.parse_patch(patch_content)
            # PatchSet은 빈 리스트처럼 동작, len()으로 확인
            if len(patchset) == 0:
                return False, "Empty or invalid patch"
            return True, None
        except Exception as e:
            return False, str(e)


class FilePatcher:
    """파일 기반 패치 적용 클래스"""

    def __init__(self, base_dir: Union[str, Path]):
        """
        Args:
            base_dir: 기준 디렉토리
        """
        self.base_dir = Path(base_dir)

    def apply_patch_to_file(
        self, file_path: Union[str, Path], patch_content: str, backup: bool = True
    ) -> Path:
        """
        파일에 패치 적용

        Args:
            file_path: 패치를 적용할 파일 경로 (base_dir 기준 상대 경로)
            patch_content: Unified diff 형식의 패치
            backup: 백업 파일 생성 여부

        Returns:
            패치 적용된 파일 경로

        Raises:
            PatchApplyError: 패치 적용 실패시
        """
        full_path = self.base_dir / file_path
        if not full_path.exists():
            raise PatchApplyError(f"File not found: {full_path}")

        # 원본 내용 읽기
        original_content = full_path.read_text(encoding="utf-8")

        # 백업 생성
        if backup:
            backup_path = full_path.with_suffix(full_path.suffix + ".bak")
            backup_path.write_text(original_content, encoding="utf-8")

        # 패치 적용 (문자열 기반)
        patched_content = PatchUtil.apply_patch(original_content, patch_content)

        # 파일 저장
        full_path.write_text(patched_content, encoding="utf-8")

        return full_path

    def apply_multi_file_patch(
        self, patch_content: str, dry_run: bool = False
    ) -> dict:
        """
        여러 파일에 대한 패치 적용 (patch-ng 사용)

        Args:
            patch_content: Unified diff 형식의 패치 (여러 파일 포함 가능)
            dry_run: 실제로 적용하지 않고 시뮬레이션만

        Returns:
            적용 결과 딕셔너리
            {
                "success": bool,
                "files_patched": [str],
                "errors": [{"file": str, "error": str}]
            }
        """
        try:
            # patch-ng를 사용한 파일 기반 패치 적용
            with tempfile.TemporaryDirectory() as tmpdir:
                tmpdir_path = Path(tmpdir)

                # 패치 파일 작성
                patch_file = tmpdir_path / "patch.diff"
                patch_file.write_text(patch_content, encoding="utf-8")

                # patch-ng로 파싱
                patchset = patch_ng.fromfile(str(patch_file))

                if not patchset:
                    raise PatchApplyError("Invalid patch format")

                if dry_run:
                    # Dry run: 검증만
                    files_patched = [
                        item.source if hasattr(item, "source") else str(item)
                        for item in patchset.items
                    ]
                    return {
                        "success": True,
                        "files_patched": files_patched,
                        "errors": [],
                    }

                # 실제 적용
                result = patchset.apply(root=str(self.base_dir))

                if result:
                    files_patched = []
                    for item in patchset.items:
                        # bytes를 string으로 변환하고 'a/' prefix 제거
                        source = item.source
                        if isinstance(source, bytes):
                            source = source.decode("utf-8")
                        if source.startswith("a/") or source.startswith("b/"):
                            source = source[2:]
                        files_patched.append(source)

                    return {
                        "success": True,
                        "files_patched": files_patched,
                        "errors": [],
                    }
                else:
                    return {
                        "success": False,
                        "files_patched": [],
                        "errors": [{"file": "unknown", "error": "Patch application failed"}],
                    }

        except Exception as e:
            return {
                "success": False,
                "files_patched": [],
                "errors": [{"file": "unknown", "error": str(e)}],
            }
