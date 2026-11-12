# Almost Delivered 데이터베이스 연결

## 현재 상태

- Hostinger Game Panel에 `Almost Delivered MySQL` 인스턴스가 생성되어 실행 중이다.
- MySQL 8.4.13의 AMP 콘솔에서 `SELECT 1;` 결과가 `1`로 반환되었다.
- `almost_delivered` 데이터베이스가 생성되었고 목록에서 확인되었다.
- `almost_admin`과 `almost_api` 계정이 생성되었고, DBeaver에서 `almost_admin`으로 SSH 터널을 통한 접속을 확인했다.
- `AlmostDelivered.Api`는 MySQL 연결 검사용 ASP.NET Core 서버다. VPS 내부에서 `almost_api`로 `/health/db`를 호출해 HTTP 200과 `{"status":"ok","database":"mysql"}`을 확인했다. Unity 클라이언트는 아직 API를 호출하지 않는다.

## 연결 구조

Unity 클라이언트는 MySQL에 직접 접속하지 않는다. 계정 정보와 저장 데이터는 게임 서버 API가 검증하고 MySQL에 저장한다. 현재 API의 `/health`는 서버 프로세스 상태를, `/health/db`는 MySQL에서 `SELECT 1` 실행 여부를 검사한다.

## 이후 연결 절차

1. VPS에서는 root만 읽을 수 있는 접속 정보 파일을 만들고, systemd `LoadCredential=db-connection:...`으로 API에 전달한다. API는 `CREDENTIALS_DIRECTORY/db-connection`을 읽는다. 현재 서비스는 전용 `almost-api` Linux 계정으로 `127.0.0.1:15001`에서 실행 중이다. 로컬 개발에서는 환경 변수 `Database__ConnectionString`도 사용할 수 있지만 비밀번호를 명령 인수나 프로젝트 파일에 쓰지 않는다.
2. VPS 내부에서 `/health/db`가 HTTP 200과 `{"status":"ok","database":"mysql"}`을 반환하는 것을 확인했다. 접속 정보가 없거나 접속에 실패하면 HTTP 503을 반환한다.
3. 계정 인증과 플레이어별 저장 API를 추가한 뒤 Unity를 API에 연결한다. 공개 클라이언트에 DB 비밀번호를 넣지 않는다.

배포용 서비스 정의와 비밀번호 숨김 입력 스크립트는 `Server/deploy/`에 있다. 서비스는 현재 실행 중이며 부팅 시 자동 시작은 아직 설정하지 않았다.

코드 흐름과 현재 연동 범위는 [학습 노트](STUDY.md)에 정리했다.

Game Panel 경로: `Instances` → `Almost Delivered MySQL` → `Console`.
