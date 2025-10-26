"""
Patch Utility 테스트

Unified diff 적용 및 분석 테스트
"""

import tempfile
from pathlib import Path

import pytest

from src.common.patch_util import FilePatcher, PatchApplyError, PatchUtil


class TestPatchUtil:
    """PatchUtil 기본 기능 테스트"""

    def test_apply_simple_patch(self):
        """간단한 패치 적용"""
        original = """line 1
line 2
line 3
line 4
"""

        patch_content = """--- a/test.txt
+++ b/test.txt
@@ -1,4 +1,4 @@
 line 1
-line 2
+line 2 modified
 line 3
 line 4
"""

        result = PatchUtil.apply_patch(original, patch_content)

        assert "line 2 modified" in result
        assert "line 1" in result
        assert "line 3" in result

    def test_apply_addition_patch(self):
        """줄 추가 패치"""
        original = """line 1
line 2
line 3
"""

        patch_content = """--- a/test.txt
+++ b/test.txt
@@ -1,3 +1,4 @@
 line 1
 line 2
+new line
 line 3
"""

        result = PatchUtil.apply_patch(original, patch_content)

        assert "new line" in result
        assert result.count("\n") >= 3

    def test_apply_deletion_patch(self):
        """줄 삭제 패치"""
        original = """line 1
line 2
line 3
line 4
"""

        patch_content = """--- a/test.txt
+++ b/test.txt
@@ -1,4 +1,3 @@
 line 1
-line 2
 line 3
 line 4
"""

        result = PatchUtil.apply_patch(original, patch_content)

        assert "line 2" not in result
        assert "line 1" in result
        assert "line 3" in result

    def test_parse_patch_metadata(self):
        """패치 메타데이터 파싱"""
        patch_content = """--- a/file1.py
+++ b/file1.py
@@ -1,3 +1,3 @@
-old line
+new line
 unchanged
 another line
--- a/file2.py
+++ b/file2.py
@@ -1,2 +1,3 @@
 line 1
+added line
 line 2
"""

        patchset = PatchUtil.parse_patch(patch_content)

        assert len(patchset) == 2
        assert patchset[0].path == "file1.py"
        assert patchset[1].path == "file2.py"

    def test_get_patch_stats(self):
        """패치 통계 추출"""
        patch_content = """--- a/test.py
+++ b/test.py
@@ -1,5 +1,6 @@
 line 1
-line 2
+line 2 modified
 line 3
+new line
 line 4
 line 5
"""

        stats = PatchUtil.get_patch_stats(patch_content)

        assert stats["files_changed"] == 1
        assert stats["additions"] == 2  # 1 modified + 1 added
        assert stats["deletions"] == 1
        assert len(stats["files"]) == 1
        assert stats["files"][0]["path"] == "test.py"

    def test_validate_patch_valid(self):
        """유효한 패치 검증"""
        patch_content = """--- a/test.txt
+++ b/test.txt
@@ -1,3 +1,3 @@
 line 1
-old line
+new line
 line 3
"""

        valid, error = PatchUtil.validate_patch(patch_content)

        assert valid is True
        assert error is None

    def test_validate_patch_invalid(self):
        """잘못된 패치 검증"""
        patch_content = "not a valid patch"

        valid, error = PatchUtil.validate_patch(patch_content)

        assert valid is False
        assert error is not None

    def test_apply_patch_with_strict_mode(self):
        """엄격 모드 패치 적용 (불일치시 실패)"""
        original = """line 1
line 2
"""

        # 원본과 맞지 않는 패치
        patch_content = """--- a/test.txt
+++ b/test.txt
@@ -1,2 +1,2 @@
-line 3
+line 4
"""

        with pytest.raises(PatchApplyError):
            PatchUtil.apply_patch(original, patch_content, strict=True)


class TestFilePatcher:
    """FilePatcher 파일 작업 테스트"""

    def test_apply_patch_to_single_file(self):
        """단일 파일 패치 적용"""
        with tempfile.TemporaryDirectory() as tmpdir:
            tmpdir_path = Path(tmpdir)

            # 테스트 파일 생성
            test_file = tmpdir_path / "test.txt"
            test_file.write_text("line 1\nline 2\nline 3\n", encoding="utf-8")

            # 패치 생성
            patch_content = """--- a/test.txt
+++ b/test.txt
@@ -1,3 +1,3 @@
 line 1
-line 2
+line 2 modified
 line 3
"""

            # FilePatcher로 패치 적용
            patcher = FilePatcher(tmpdir_path)
            result_path = patcher.apply_patch_to_file("test.txt", patch_content)

            # 결과 검증
            modified_content = result_path.read_text(encoding="utf-8")
            assert "line 2 modified" in modified_content
            assert result_path == test_file

            # 백업 파일 확인
            backup_file = tmpdir_path / "test.txt.bak"
            assert backup_file.exists()
            backup_content = backup_file.read_text(encoding="utf-8")
            assert "line 2\n" in backup_content

    def test_apply_patch_without_backup(self):
        """백업 없이 패치 적용"""
        with tempfile.TemporaryDirectory() as tmpdir:
            tmpdir_path = Path(tmpdir)

            test_file = tmpdir_path / "test.txt"
            test_file.write_text("original\n", encoding="utf-8")

            patch_content = """--- a/test.txt
+++ b/test.txt
@@ -1 +1 @@
-original
+modified
"""

            patcher = FilePatcher(tmpdir_path)
            patcher.apply_patch_to_file("test.txt", patch_content, backup=False)

            # 백업 파일이 없어야 함
            backup_file = tmpdir_path / "test.txt.bak"
            assert not backup_file.exists()

    def test_apply_patch_to_nonexistent_file(self):
        """존재하지 않는 파일에 패치 적용 시도"""
        with tempfile.TemporaryDirectory() as tmpdir:
            tmpdir_path = Path(tmpdir)

            patch_content = """--- a/nonexistent.txt
+++ b/nonexistent.txt
@@ -1 +1 @@
-old
+new
"""

            patcher = FilePatcher(tmpdir_path)

            with pytest.raises(PatchApplyError, match="File not found"):
                patcher.apply_patch_to_file("nonexistent.txt", patch_content)

    def test_apply_multi_file_patch_success(self):
        """여러 파일 패치 적용 성공"""
        with tempfile.TemporaryDirectory() as tmpdir:
            tmpdir_path = Path(tmpdir)

            # 테스트 파일들 생성
            file1 = tmpdir_path / "file1.txt"
            file2 = tmpdir_path / "file2.txt"
            file1.write_text("file1 line1\nfile1 line2\n", encoding="utf-8")
            file2.write_text("file2 line1\nfile2 line2\n", encoding="utf-8")

            # 다중 파일 패치
            patch_content = """--- a/file1.txt
+++ b/file1.txt
@@ -1,2 +1,2 @@
-file1 line1
+file1 line1 modified
 file1 line2
--- a/file2.txt
+++ b/file2.txt
@@ -1,2 +1,2 @@
 file2 line1
-file2 line2
+file2 line2 modified
"""

            patcher = FilePatcher(tmpdir_path)
            result = patcher.apply_multi_file_patch(patch_content)

            # 결과 검증
            assert result["success"] is True
            assert len(result["files_patched"]) == 2
            assert "file1.txt" in result["files_patched"]
            assert "file2.txt" in result["files_patched"]
            assert len(result["errors"]) == 0

            # 파일 내용 확인
            assert "file1 line1 modified" in file1.read_text()
            assert "file2 line2 modified" in file2.read_text()

    def test_apply_multi_file_patch_dry_run(self):
        """다중 파일 패치 시뮬레이션 (dry run)"""
        with tempfile.TemporaryDirectory() as tmpdir:
            tmpdir_path = Path(tmpdir)

            test_file = tmpdir_path / "test.txt"
            original_content = "original\n"
            test_file.write_text(original_content, encoding="utf-8")

            patch_content = """--- a/test.txt
+++ b/test.txt
@@ -1 +1 @@
-original
+modified
"""

            patcher = FilePatcher(tmpdir_path)
            result = patcher.apply_multi_file_patch(patch_content, dry_run=True)

            # 파일이 변경되지 않아야 함
            assert test_file.read_text() == original_content
            assert result["success"] is True


class TestRealWorldPatches:
    """실제 사용 케이스 테스트"""

    def test_python_code_patch(self):
        """Python 코드 패치"""
        original = '''def hello():
    print("Hello")
    return True

def world():
    print("World")
    return False
'''

        patch_content = '''--- a/code.py
+++ b/code.py
@@ -1,7 +1,7 @@
 def hello():
-    print("Hello")
+    print("Hello, World!")
     return True

 def world():
     print("World")
-    return False
+    return True
'''

        result = PatchUtil.apply_patch(original, patch_content)

        assert 'print("Hello, World!")' in result
        assert "return True" in result
        assert result.count("return True") == 2

    def test_multiline_addition(self):
        """여러 줄 추가 패치"""
        original = """import os

def main():
    pass
"""

        patch_content = """--- a/script.py
+++ b/script.py
@@ -1,4 +1,8 @@
 import os
+import sys
+import json

 def main():
+    config = load_config()
+    process(config)
     pass
"""

        result = PatchUtil.apply_patch(original, patch_content)

        assert "import sys" in result
        assert "import json" in result
        assert "config = load_config()" in result
