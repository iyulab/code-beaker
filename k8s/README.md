# CodeBeaker Kubernetes Deployment Guide

CodeBeaker 인프라 프레임워크의 Kubernetes 배포 가이드

---

## 📋 목차

1. [아키텍처 개요](#아키텍처-개요)
2. [사전 요구사항](#사전-요구사항)
3. [배포 방법](#배포-방법)
4. [설정 옵션](#설정-옵션)
5. [모니터링 및 관찰성](#모니터링-및-관찰성)
6. [트러블슈팅](#트러블슈팅)

---

## 아키텍처 개요

### 컴포넌트 구성

```
┌─────────────────────────────────────────────┐
│           Kubernetes Cluster                │
│                                             │
│  ┌──────────────┐      ┌──────────────┐   │
│  │  API Pods    │      │ Worker Pods  │   │
│  │  (Replicas)  │      │  (Replicas)  │   │
│  └──────┬───────┘      └──────┬───────┘   │
│         │                     │            │
│         └──────┬──────────────┘            │
│                │                           │
│         ┌──────▼───────┐                   │
│         │  FileQueue   │ (PVC: ReadWriteMany)
│         │  FileStorage │                   │
│         └──────────────┘                   │
│                                             │
│  ┌──────────────────────────────────────┐  │
│  │  Docker Runtime Images (PVC)        │  │
│  └──────────────────────────────────────┘  │
└─────────────────────────────────────────────┘
```

### 주요 특징

- **API 컴포넌트**: REST API 서버 (HPA: 2-10 replicas)
- **Worker 컴포넌트**: 백그라운드 작업 처리 (HPA: 3-20 replicas)
- **공유 스토리지**: ReadWriteMany PVC로 FileQueue/FileStorage 공유
- **자동 스케일링**: CPU/메모리 기반 HPA
- **Health Checks**: Liveness/Readiness probes

---

## 사전 요구사항

### 필수 소프트웨어

- **Kubernetes** ≥ 1.24
- **kubectl** ≥ 1.24
- **Helm** ≥ 3.10 (Helm 배포시)
- **Docker** (이미지 빌드)

### 클러스터 요구사항

- **Storage Class**: ReadWriteMany 지원 (NFS, CephFS, AWS EFS 등)
- **Metrics Server**: HPA를 위해 필수
- **최소 노드 리소스**:
  - CPU: 4 cores
  - Memory: 8GB
  - Storage: 20GB

---

## 배포 방법

### 방법 1: Kustomize 사용 (권장)

**기본 배포**:
```bash
# Base 매니페스트 적용
kubectl apply -k k8s/base

# 배포 확인
kubectl get all -n codebeaker
```

**개발 환경 배포**:
```bash
# Dev overlay 적용 (리소스 제한 완화)
kubectl apply -k k8s/overlays/dev
```

**프로덕션 환경 배포**:
```bash
# Prod overlay 적용 (리소스 제한 강화, 고가용성)
kubectl apply -k k8s/overlays/prod
```

### 방법 2: Helm 사용

**Helm 차트 설치**:
```bash
# 기본 설치
helm install codebeaker ./helm/codebeaker

# 사용자 정의 값 파일 사용
helm install codebeaker ./helm/codebeaker -f custom-values.yaml

# 특정 네임스페이스에 설치
helm install codebeaker ./helm/codebeaker --namespace codebeaker --create-namespace
```

**업그레이드**:
```bash
helm upgrade codebeaker ./helm/codebeaker
```

**제거**:
```bash
helm uninstall codebeaker
```

### 방법 3: 직접 매니페스트 적용

```bash
# 1. Namespace 생성
kubectl apply -f k8s/base/namespace.yaml

# 2. ConfigMap 생성
kubectl apply -f k8s/base/configmap.yaml

# 3. PVC 생성
kubectl apply -f k8s/base/pvc.yaml

# 4. Deployment 생성
kubectl apply -f k8s/base/api-deployment.yaml
kubectl apply -f k8s/base/worker-deployment.yaml

# 5. Service 생성
kubectl apply -f k8s/base/service.yaml

# 6. HPA 생성 (선택)
kubectl apply -f k8s/base/hpa.yaml
```

---

## 설정 옵션

### ConfigMap 설정

`k8s/base/configmap.yaml`에서 다음 항목 조정:

```yaml
# 워커 동시성 (FileQueue 제약: 3-10 권장)
WORKER_CONCURRENCY: "10"

# 실행 제한
EXECUTION_TIMEOUT_SECONDS: "30"
EXECUTION_MEMORY_LIMIT_MB: "512"

# 큐/스토리지 경로
QUEUE_BASE_DIR: "/data/queue"
STORAGE_BASE_DIR: "/data/storage"
```

### 리소스 조정

**API 리소스** (낮은 부하):
```yaml
resources:
  requests:
    memory: "128Mi"
    cpu: "100m"
  limits:
    memory: "256Mi"
    cpu: "500m"
```

**Worker 리소스** (코드 실행 부하):
```yaml
resources:
  requests:
    memory: "256Mi"
    cpu: "200m"
  limits:
    memory: "1Gi"
    cpu: "1000m"
```

### HPA 조정

**API HPA** (트래픽 기반):
```yaml
minReplicas: 2
maxReplicas: 10
targetCPUUtilizationPercentage: 70
targetMemoryUtilizationPercentage: 80
```

**Worker HPA** (작업 부하 기반):
```yaml
minReplicas: 3
maxReplicas: 20
targetCPUUtilizationPercentage: 75
targetMemoryUtilizationPercentage: 85
```

---

## 스토리지 구성

### PVC 설정

**FileQueue/FileStorage PVC** (ReadWriteMany 필수):
```yaml
spec:
  accessModes:
    - ReadWriteMany  # 여러 Pod 동시 접근
  resources:
    requests:
      storage: 10Gi
  storageClassName: nfs-client  # 환경에 맞게 조정
```

### Storage Class 예제

**NFS Storage Class** (권장):
```yaml
apiVersion: storage.k8s.io/v1
kind: StorageClass
metadata:
  name: nfs-client
provisioner: example.com/nfs
parameters:
  archiveOnDelete: "false"
```

**AWS EFS**:
```yaml
storageClassName: efs-sc
```

**Azure Files**:
```yaml
storageClassName: azurefile
```

---

## 접근 방법

### ClusterIP (기본)

클러스터 내부에서만 접근:
```bash
kubectl port-forward svc/codebeaker-api -n codebeaker 8080:80
# http://localhost:8080
```

### LoadBalancer

외부 접근 (클라우드 환경):
```yaml
# k8s/base/service.yaml
spec:
  type: LoadBalancer
```

```bash
kubectl get svc codebeaker-api-loadbalancer -n codebeaker
# EXTERNAL-IP 확인
```

### Ingress

Ingress Controller를 통한 접근:
```yaml
# Helm values.yaml
ingress:
  enabled: true
  className: "nginx"
  hosts:
    - host: codebeaker.example.com
      paths:
        - path: /
          pathType: Prefix
```

---

## 모니터링 및 관찰성

### Health Checks

**API Health Check**:
```bash
kubectl exec -it <api-pod> -n codebeaker -- curl http://localhost:5039/health
```

**로그 확인**:
```bash
# API 로그
kubectl logs -f deployment/codebeaker-api -n codebeaker

# Worker 로그
kubectl logs -f deployment/codebeaker-worker -n codebeaker

# 모든 Pod 로그
kubectl logs -f -l app.kubernetes.io/name=codebeaker -n codebeaker
```

### Metrics

**Pod 리소스 사용량**:
```bash
kubectl top pods -n codebeaker
```

**HPA 상태**:
```bash
kubectl get hpa -n codebeaker
```

---

## 트러블슈팅

### Pod가 시작되지 않음

**PVC Pending**:
```bash
kubectl describe pvc codebeaker-data -n codebeaker

# 해결: Storage Class 확인 및 설정
```

**ImagePullBackOff**:
```bash
# Docker 이미지 빌드 및 푸시 필요
docker build -t codebeaker/api:latest -f src/CodeBeaker.API/Dockerfile .
docker build -t codebeaker/worker:latest -f src/CodeBeaker.Worker/Dockerfile .
```

### Worker가 작업을 처리하지 않음

**Docker Socket 접근 문제**:
```bash
# Worker Pod 로그 확인
kubectl logs -f deployment/codebeaker-worker -n codebeaker

# hostPath 권한 확인
```

**FileQueue 동시성 제한**:
- Worker replica 수를 3-10으로 제한 (FileQueue 특성)
- 높은 처리량 필요시 Redis Queue 구현 권장

### HPA가 스케일링하지 않음

**Metrics Server 미설치**:
```bash
kubectl apply -f https://github.com/kubernetes-sigs/metrics-server/releases/latest/download/components.yaml
```

**리소스 요청 미설정**:
- Deployment에 `resources.requests` 필수

---

## 프로덕션 권장사항

### 고가용성

1. **Multi-AZ 배포**: 여러 가용 영역에 Pod 분산
2. **PodDisruptionBudget**: 최소 가용 Pod 수 보장
3. **Node Affinity**: API/Worker를 적절한 노드에 배치

### 보안

1. **Network Policies**: Pod 간 통신 제한
2. **RBAC**: 최소 권한 원칙
3. **Secret Management**: ConfigMap 대신 Secret 사용 (민감 정보)

### 성능

1. **ReadWriteMany PVC 대안**: 고처리량 필요시 Redis Queue
2. **Worker 노드 전용**: Worker를 전용 노드에 배치
3. **리소스 모니터링**: Prometheus + Grafana 통합

---

## 다음 단계

1. **Docker 이미지 빌드**: `docker build` 명령으로 이미지 생성
2. **Registry 푸시**: Docker Hub 또는 private registry에 푸시
3. **배포 테스트**: Dev 환경에서 먼저 테스트
4. **모니터링 설정**: Prometheus 메트릭 통합

---

**마지막 업데이트**: 2025-10-27
**버전**: 1.0.0
