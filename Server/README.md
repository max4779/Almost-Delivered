# Almost Delivered 데이터베이스 연결

## 현재 상태

- Hostinger Game Panel에 `Almost Delivered MySQL` 인스턴스가 생성되어 실행 중이다.
- MySQL 8.4.13의 AMP 콘솔에서 `SELECT 1;` 결과가 `1`로 반환되었다.
- `almost_delivered` 데이터베이스가 생성되었고 목록에서 확인되었다.
- `almost_admin`과 `almost_api` 계정이 생성되었고, DBeaver에서 `almost_admin`으로 SSH 터널을 통한 접속을 확인했다.
- `AlmostDelivered.Api`는 MySQL 연결 검사, 게스트 저장, 계정 가입·로그인을 처리한다. Unity에서 게스트 저장을 확인했고, 임시 계정으로 가입→프로필 조회→로그아웃을 확인한 뒤 임시 데이터를 삭제했다.

## 연결 구조

Unity 클라이언트는 MySQL에 직접 접속하지 않는다. 현재 개발 PC의 Unity는 로컬 SSH 터널을 통해 VPS 내부 API에 요청한다. API는 게스트 토큰 또는 계정 세션 토큰의 해시로 프로필을 찾고 특성 규칙을 검사한 뒤 MySQL에 저장한다. 비밀번호는 ASP.NET Core Identity의 해시 형식으로만 저장한다. `/health`는 서버 프로세스 상태를, `/health/db`는 MySQL 연결을 검사한다.

## 이후 연결 절차

1. VPS에서는 root만 읽을 수 있는 접속 정보 파일을 만들고, systemd `LoadCredential=db-connection:...`으로 API에 전달한다. API는 `CREDENTIALS_DIRECTORY/db-connection`을 읽는다. 현재 서비스는 전용 `almost-api` Linux 계정으로 `127.0.0.1:15001`에서 실행 중이다. 로컬 개발에서는 환경 변수 `Database__ConnectionString`도 사용할 수 있지만 비밀번호를 명령 인수나 프로젝트 파일에 쓰지 않는다.
2. VPS 내부에서 `/health/db`가 HTTP 200과 `{"status":"ok","database":"mysql"}`을 반환하는 것을 확인했다. 접속 정보가 없거나 접속에 실패하면 HTTP 503을 반환한다.
3. `Server/deploy/001_player_profiles.sql`과 `002_accounts.sql`을 적용했다. 게임용 DB 계정에는 SELECT·INSERT·UPDATE·DELETE 권한만 유지했다.
4. Unity 시작 화면에서 계정 로그인·회원가입 또는 게스트 시작을 선택한다. 회원가입 시 기존 서버 게스트 진행도를 새 계정으로 복사한다. 개발 PC에서는 SSH 터널이 필요하다.

현재 계정 기능은 로컬 개발용이다. 세션은 30일 후 만료되며 비밀번호 재설정·이메일 인증·로그인 시도 제한은 아직 없다. Unity는 세션 토큰을 개발 PC의 `PlayerPrefs`에 보관한다. 다른 PC에 배포하기 전에 공개 HTTPS, 안전한 토큰 보관, 계정 복구, 멀티플레이 서버의 계정 검증을 추가해야 한다.

개발 PC에서 Unity를 실행하기 전 PowerShell에서 다음 명령을 실행하고 창을 열어 둔다. `<VPS_IP>`는 본인의 VPS 주소로 바꾼다. 연결되면 기지 특성 창에 `서버 연결됨 · 저장 완료`가 표시된다.

```powershell
ssh -i "$env:USERPROFILE\.ssh\almost_delivered_vps_ed25519" -N -L 15001:127.0.0.1:15001 root@<VPS_IP>
```

배포용 서비스 정의와 비밀번호 숨김 입력 스크립트는 `Server/deploy/`에 있다. 서비스는 현재 실행 중이며 VPS 부팅 시 자동 시작하도록 설정했다. 개발 PC의 SSH 터널은 별도로 다시 열어야 한다.

코드 흐름과 현재 연동 범위는 [학습 노트](STUDY.md)에 정리했다.

Game Panel 경로: `Instances` → `Almost Delivered MySQL` → `Console`.
