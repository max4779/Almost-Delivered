# 게임과 MySQL 연동 코드 학습 노트

## 현재 실제로 연결된 부분

| 구간 | 상태 | 확인 근거 |
| --- | --- | --- |
| DBeaver → VPS MySQL | 연결됨 | SSH 키와 `almost_admin`으로 데이터베이스 목록 확인 |
| ASP.NET Core API → MySQL | 연결됨 | VPS 내부 `/health/db`에서 HTTP 200과 `database: mysql` 확인 |
| Unity → API | 미구현 | Unity 런타임에 API 요청 코드가 없음 |
| Unity 특성 저장 | PC 내부 저장 | `LocalPlayerTraitStore`가 `PlayerPrefs` 사용 |
| Unity 멀티플레이 | 별도 프로토타입 | `NetworkSession`이 Unity Netcode 호스트에 연결 |

따라서 DBeaver에서 접속한 사실만으로 게임 데이터가 MySQL에 저장되지는 않는다. 현재 게임의 특성은 PC별 로컬 데이터다. 멀티플레이에서 사용하는 Unity Netcode 연결도 계정 저장용 API 연결과 별개다.

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

`/health/db`는 DB에 연결할 수 있는지만 확인한다. 계정 생성, 로그인, 특성 저장 기능은 아직 없다.

## `PlayerTraitStore.cs`: 현재 특성이 저장되는 곳

파일: `Assets/AlmostDelivered/Runtime/Frontend/PlayerTraitStore.cs`

- `IPlayerTraitStore`는 계정 ID로 특성을 불러오고 저장하는 인터페이스다.
- `LocalPlayerTraitStore`는 현재 구현체다. `PlayerPrefs.GetString`으로 읽고 `PlayerPrefs.SetString`으로 저장한다.
- `Key(accountId)`는 계정 ID를 SHA-256으로 변환해 로컬 저장 키를 만든다. 이것은 사용자 인증이나 서버 저장이 아니다.
- `LocalPlayerAccount.CurrentAccountId`는 로그인하지 않은 경우 임시 `guest:` ID를 만든다.
- `UseAuthenticatedAccount`는 나중에 인증된 ID를 전달할 수 있는 연결 지점이지만, 지금은 인증 요청을 보내지 않는다.

`PlayerTraitProfile.cs`의 `BindLocalAccount`는 기본적으로 `LocalPlayerTraitStore`를 선택한다. 특성 포인트를 사용하면 `Commit()`이 해당 저장소에 기록한다. 이 흐름을 따라가면 현재 저장 위치를 확인할 수 있다.

## `NetworkSession.cs`: 멀티플레이 연결과 DB 연결의 차이

파일: `Assets/AlmostDelivered/Runtime/Networking/NetworkSession.cs`

`StartHost`와 `Join`은 Unity Transport로 게임 호스트와 연결한다. 기본 포트는 UDP 7777이다. 장면 이동과 아바타 표시를 위한 연결이며, HTTP 요청이나 MySQL 접속은 하지 않는다. 게임 서버에 접속한 사실만으로 계정이 인증되거나 특성이 DB에 저장되지는 않는다.

## 실제 연동을 위한 다음 순서

1. 서버에 계정 인증과 계정별 특성 조회·저장 API를 만든다. 레벨과 포인트 사용 규칙은 서버에서도 검증한다.
2. Unity에 HTTP 클라이언트를 추가해 로그인 후 자신의 특성을 가져오고 변경 요청을 보낸다.
3. 멀티플레이 호스트가 각 플레이어의 인증된 계정과 특성 상태를 연결한다. 클라이언트가 보내는 레벨·보상 값을 그대로 신뢰하지 않는다.
4. 두 계정과 두 PC에서 저장, 재접속, 권한 분리를 확인한다.

Unity에 MySQL 비밀번호를 넣으면 게임 배포본에서 노출될 수 있다. 접속 정보는 서버에만 둔다.

## 직접 따라 해볼 연습

1. `PlayerTraitProfile.TryInvest()`에서 `Commit()`까지 따라가며 현재 특성이 어느 저장소에 기록되는지 찾는다.
2. `Program.cs`에서 `/health`와 `/health/db`가 각각 무엇을 확인하는지 비교한다.
3. `NetworkSession.Join()`에서 접속하는 주소와 포트를 찾고, 이 연결이 왜 DB 연결이 아닌지 설명해 본다.
