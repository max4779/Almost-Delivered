# 게임과 MySQL 연동 코드 학습 노트

## 현재 실제로 연결된 부분

| 구간 | 상태 | 확인 근거 |
| --- | --- | --- |
| DBeaver → VPS MySQL | 연결됨 | SSH 키와 `almost_admin`으로 데이터베이스 목록 확인 |
| ASP.NET Core API → MySQL | 연결됨 | VPS 내부 `/health/db`에서 HTTP 200과 `database: mysql` 확인 |
| Unity → API | 개발 PC에서 연결됨 | SSH 터널을 통해 게스트 프로필 생성·조회·저장 |
| Unity 특성 저장 | MySQL + 기기 캐시 | 기지에서 특성 1단계 선택 후 DB 저장과 Unity 재실행 확인 |
| Unity 멀티플레이 | 별도 프로토타입 | `NetworkSession`이 Unity Netcode 호스트에 연결 |
| 계정 가입·로그인 API | 개발 서버에 연결됨 | 임시 계정 가입→프로필 조회→로그아웃 후 정리 확인 |

현재 Unity의 게스트 특성은 API를 거쳐 MySQL에 저장된다. 네트워크가 끊기면 기기에 먼저 저장하고 연결을 재시도한다. Unity Netcode 연결은 계정 저장용 API와 별개다.

## `Program.cs`: API의 DB 연결 검사

파일: `Server/AlmostDelivered.Api/Program.cs`

1. `WebApplication.CreateBuilder(args)`는 서버 설정을 읽고 웹 서버를 준비한다. `builder.Build()`가 실제 서버 객체를 만든다.
2. `MapGet("/health", ...)`는 API 프로세스가 실행 중인지 확인한다. DB 상태는 검사하지 않는다.
3. VPS에서 실행하면 systemd가 제공하는 `CREDENTIALS_DIRECTORY/db-connection` 파일을 먼저 읽어 설정에 넣는다. `MapGet("/health/db", ...)`는 `Database:ConnectionString` 설정을 읽고, 값이 없으면 HTTP 503을 반환한다.
4. `new MySqlConnection(connectionString)`은 연결 객체를 만들고, `OpenAsync`가 MySQL에 실제로 접속한다.
5. `MySqlCommand("SELECT 1", connection)`은 읽기 전용 검사용 쿼리다. 결과가 `1`이면 HTTP 200과 `database: mysql`을 반환한다.
6. MySQL 오류나 시간 초과가 나면 로그를 남기고 HTTP 503을 반환한다. `await using`은 연결과 명령 객체를 작업 후 정리한다.
7. `app.Run()`이 API를 실행한다.

로컬 개발 환경에서 환경 변수 이름 `Database__ConnectionString`의 이중 밑줄은 .NET 설정 키 `Database:ConnectionString`으로 바뀐다. VPS에서는 systemd `LoadCredential`을 사용해 root 전용 비밀 파일을 서비스에 전달한다. `appsettings.json`에 비밀번호를 넣지 않는 이유는 이 파일이 프로젝트에 포함되기 때문이다. MySQL 접속 정보는 서버에서만 사용해야 한다.

`POST /v1/profiles/guest`는 서버가 임의 게스트 ID와 접근 토큰을 발급한다. 토큰 원문은 Unity에 한 번만 전달하고 DB에는 SHA-256 해시를 저장한다. `GET /v1/profile`은 해당 토큰으로 프로필을 조회하고, `PUT /v1/profile`은 특성 규칙을 검사한 뒤 DB에 저장한다. 테이블 생성 SQL은 `Server/deploy/001_player_profiles.sql`에 있고, 게임용 `almost_api` 계정에는 테이블 생성 권한을 주지 않았다.

`POST /v1/auth/register`는 이메일과 비밀번호를 검사하고 계정·프로필·30일 세션을 한 DB 트랜잭션에서 만든다. 게스트 토큰을 보내면 서버에 저장된 게스트 특성을 새 계정으로 복사한다. 비밀번호는 `PasswordHasher<string>`으로 해시해 `accounts`에 보관하고, 세션 토큰은 해시만 `account_sessions`에 저장한다. `POST /v1/auth/login`은 저장된 해시와 비밀번호를 비교하고 새 세션 토큰을 발급한다. `POST /v1/auth/logout`은 해당 세션을 지운다. 스키마는 `Server/deploy/002_accounts.sql`이다.

## `PlayerTraitStore.cs`: 기기 캐시

파일: `Assets/AlmostDelivered/Runtime/Frontend/PlayerTraitStore.cs`

- `IPlayerTraitStore`는 계정 ID로 특성을 불러오고 저장하는 인터페이스다.
- `LocalPlayerTraitStore`는 기기 캐시다. `PlayerPrefs.GetString`으로 읽고 `PlayerPrefs.SetString`으로 저장한다.
- `Key(accountId)`는 계정 ID를 SHA-256으로 변환해 로컬 저장 키를 만든다. 이것은 사용자 인증이나 서버 저장이 아니다.
- `LocalPlayerAccount.CurrentAccountId`는 로그인하지 않은 경우 임시 `guest:` ID를 만든다.
- 서버가 게스트 프로필을 만들면 `UseCloudGuestAccount`가 발급된 ID를 기기에 기억한다. 게스트 토큰도 `PlayerPrefs`에 저장하므로 현재 방식은 개발용이며 정식 계정 인증을 대신하지 않는다.
- `FrontendAccountPanel.cs`는 시작 화면의 로그인·회원가입 입력과 결과 메시지를 담당한다. 비밀번호는 입력 후 지우며 기기에 저장하지 않는다.
- `FrontendAccountSession.cs`는 계정 ID와 세션 토큰을 기억한다. 현재 토큰 저장소는 `PlayerPrefs`이므로 개발용이다. `LocalPlayerAccount`는 로그인한 계정 ID를 우선 사용한다.

`PlayerTraitProfile.cs`의 `Commit()`은 우선 기기 캐시에 저장하고 `Committed` 이벤트를 보낸다. `RemoteTraitSync.cs`가 이 이벤트를 받아 UnityWebRequest로 서버에 전송한다. 접속 시에는 서버 스냅샷을 읽어 프로필에 적용한다. 실패한 저장은 보류 표시를 남기고 다시 시도한다.

개발 PC의 Unity는 `http://127.0.0.1:15001`을 호출한다. 이 주소는 SSH 터널로 VPS 내부 API에 연결된다. SSH 터널을 닫거나 PC를 재시작하면 서버 연결은 끊기고 기기 캐시만 사용한다. Unity 배포본에 MySQL 비밀번호는 없다.

## `NetworkSession.cs`: 멀티플레이 연결과 DB 연결의 차이

파일: `Assets/AlmostDelivered/Runtime/Networking/NetworkSession.cs`

`StartHost`와 `Join`은 Unity Transport로 게임 호스트와 연결한다. 기본 포트는 UDP 7777이다. 장면 이동과 아바타 표시를 위한 연결이며, HTTP 요청이나 MySQL 접속은 하지 않는다. 게임 서버에 접속한 사실만으로 계정이 인증되거나 특성이 DB에 저장되지는 않는다.

## 정식 멀티플레이 계정을 위한 다음 순서

1. 공개 HTTPS 주소, 이메일 인증·복구, 로그인 시도 제한, 안전한 세션 토큰 저장소를 추가한다. 현재 게스트·계정 토큰은 개발 PC의 `PlayerPrefs`에 저장된다.
2. 레벨·보상 지급을 서버 권한으로 옮긴다. 현재 API는 특성 구조와 포인트 한도만 검증하며 클라이언트가 보낸 레벨을 신뢰한다.
3. 멀티플레이 호스트가 각 플레이어의 인증된 계정과 특성 상태를 연결한다.
4. 두 계정과 두 PC에서 저장, 재접속, 권한 분리를 확인한다.

Unity에 MySQL 비밀번호를 넣으면 게임 배포본에서 노출될 수 있다. 접속 정보는 서버에만 둔다.

## 직접 따라 해볼 연습

1. `PlayerTraitProfile.TryInvest()` → `Commit()` → `RemoteTraitSync.OnCommitted()` → `PUT /v1/profile` 흐름을 따라간다.
2. `Program.cs`에서 `/health/db`와 `GET /v1/profile`이 각각 무엇을 확인하는지 비교한다.
3. `NetworkSession.Join()`의 UDP 연결과 `RemoteTraitSync`의 HTTP 연결을 구분해 본다.
