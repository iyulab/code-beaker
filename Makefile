.PHONY: help install dev-install test lint format clean docker-up docker-down

help:
	@echo "CodeBeaker - 개발 명령어"
	@echo ""
	@echo "  make install        - 프로덕션 의존성 설치"
	@echo "  make dev-install    - 개발 의존성 포함 설치"
	@echo "  make test           - 테스트 실행"
	@echo "  make lint           - 린트 검사"
	@echo "  make format         - 코드 포맷팅"
	@echo "  make clean          - 임시 파일 정리"
	@echo "  make docker-up      - Docker 서비스 시작"
	@echo "  make docker-down    - Docker 서비스 중지"

install:
	pip install -r requirements.txt

dev-install:
	pip install -r requirements-dev.txt

test:
	pytest tests/

test-cov:
	pytest tests/ --cov=src --cov-report=html

lint:
	black --check src/ tests/
	mypy src/
	pylint src/
	flake8 src/ tests/

format:
	black src/ tests/
	isort src/ tests/

clean:
	find . -type d -name __pycache__ -exec rm -rf {} +
	find . -type f -name '*.pyc' -delete
	find . -type f -name '*.pyo' -delete
	find . -type d -name '*.egg-info' -exec rm -rf {} +
	rm -rf .pytest_cache .coverage htmlcov/ dist/ build/

docker-up:
	docker-compose up -d
	@echo "Waiting for services to be ready..."
	@sleep 5
	docker-compose ps

docker-down:
	docker-compose down

docker-logs:
	docker-compose logs -f

docker-clean:
	docker-compose down -v
