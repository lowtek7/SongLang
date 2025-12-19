# Song

관계 중심 프로그래밍 언어. 모든 것은 노드이고, 관계가 동작을 정의한다.

```
Player IS Entity
Player HAS HP 100
Player HAS Name "Hero"

Enemy IS Entity
Enemy HAS HP 50

Attack IS RELATION
Attack HAS Attacker (Node)
Attack HAS Victim (Node)
Attack DO
    Victim HAS HP (Victim.HP - Attacker.Damage)
END

Player HAS Damage 25
Player Attack Enemy    // Enemy.HP가 25가 됨
```

## 철학

Song은 기존 OOP와 다른 접근을 취한다:

```
OOP:  player.Attack(enemy)     // 행동이 객체에 속함
Song: Player Attack Enemy      // 행동이 관계에 속함
```

핵심 원칙:
- **모든 것은 노드** - 데이터, 타입, 관계, 행동 전부
- **노드는 관계의 집합** - 관계들이 붙으면서 정체성이 생김
- **타입/인스턴스 구분 없음** - 프로토타입 기반, 어떤 노드든 더 구체화 가능
- **코드 = 그래프** - 모든 것은 그래프로 표현 가능

## 시작하기

### 요구사항

- .NET 10.0+

### 빌드 & 실행

```bash
# 빌드
dotnet build

# 파일 실행
dotnet run -- yourfile.song

# REPL 시작
dotnet run
```

### REPL 명령어

```
:help, :h        도움말
:help <키워드>    키워드별 상세 도움말
:graph, :g       현재 그래프 상태 출력
:clear, :c       그래프 초기화
:quit, :q        종료
```

## 문법

### 기본 관계

```
// 상속 (IS)
Player IS Entity
Dragon IS Monster
Dragon IS Flying        // 다중 상속

// 속성 (HAS)
Player HAS HP 100
Player HAS Name "Hero"
Player HAS Damage (10 + Level * 2)    // 표현식

// 속성 접근
Player.HP PRINT              // 점 표기법
(HP OF Player) PRINT         // OF 표기법

// 능력 (CAN)
Player CAN ATTACK
Bird CAN FLY

// 제거 (LOSES)
Player LOSES HP              // 속성 제거
Player LOSES FLY             // 능력 제거
Player LOSES IS Monster      // 상속 제거
```

### 제어문

```
// 조건 (WHEN)
Player WHEN (HP < 30) DO
    Player HAS Status "Critical"
END

// ELSE 사용
Player WHEN (HP > 70) DO
    Player HAS Grade "A"
ELSE WHEN (HP > 40) DO
    Player HAS Grade "B"
ELSE DO
    Player HAS Grade "C"
END

// 패턴 기반 조건
Player HAS HP 0 WHEN DO
    Player IS Dead
END

// 자식 노드 순회 (EACH)
Inventory EACH Item DO
    Item PRINT
END

// 모든 매칭 노드에 적용 (ALL)
ALL Enemy HAS Stunned true
ALL Monster PRINT
```

### 확률

```
// 랜덤 숫자 (min~max 포함)
Player HAS Damage (RANDOM 10 30)

// 확률 블록
CHANCE 30 DO
    Player HAS CriticalHit true
ELSE DO
    Player HAS CriticalHit false
END

// 동적 확률
CHANCE (Player.Luck * 2) DO
    Player HAS DoubleReward true
END
```

### 사용자 정의 관계

```
// 역할(Role)을 가진 관계 정의
Attack IS RELATION
Attack HAS Attacker (Node)    // 첫 번째 역할 = 호출자
Attack HAS Victim (Node)      // 두 번째 역할 = 대상
Attack DO
    Victim HAS HP (Victim.HP - Attacker.Damage)
END

// 사용
Player Attack Enemy

// 3개 역할 관계
Trade IS RELATION
Trade HAS Giver (Node)
Trade HAS Receiver (Node)
Trade HAS Item (Node)
Trade DO
    Giver LOSES IS Item
    Receiver IS Item
END

Player Trade Merchant Sword
```

### 쿼리 시스템

```
// 타입으로 노드 찾기
?enemies IS Enemy

// 속성으로 찾기
?wounded HAS HP WHERE ?wounded.HP < 50

// 능력으로 찾기
?flyers CAN FLY

// 쿼리 결과를 ALL과 함께 사용
?weakEnemies IS Enemy WHERE ?weakEnemies.HP < 30
ALL ?weakEnemies HAS Marked true
```

### 표현식

```
// 산술: + - * / %
Player HAS TotalDamage (BaseDamage + BonusDamage)

// 비교: == != < > <= >=
Player WHEN (HP > MaxHP / 2) DO ... END

// 논리: AND OR NOT
Player WHEN (HP > 0 AND Level > 5) DO ... END
Player WHEN (NOT IsDead) DO ... END

// 문자열 연결
Message HAS Text ("HP: " + Player.HP)
```

### 디버그

```
// 전체 그래프 출력
DEBUG GRAPH

// 출력 예시
// --- Graph State ---
// Node(Player) IS Entity { HP=100, Name="Hero" } CAN [ATTACK]
// Node(Entity)
// -------------------
```

## 프로젝트 구조

```
Song/
├── Program.cs           # 진입점, REPL
├── Tokenizer/
│   ├── Token.cs         # 토큰 타입과 값
│   ├── TokenType.cs     # 토큰 타입 enum
│   └── Tokenizer.cs     # 렉서
├── Parser/
│   ├── Statement.cs     # AST 문장 노드
│   ├── Expression.cs    # AST 표현식 노드
│   └── Parser.cs        # 파서
├── Runtime/
│   ├── Node.cs          # 노드 표현
│   ├── Graph.cs         # 노드/관계 저장소
│   ├── Interpreter.cs   # 실행 엔진
│   └── SongError.cs     # 에러 타입
└── Repl/
    └── HelpSystem.cs    # REPL 도움말
```

## 예제

더 많은 예제는 테스트 파일 참고:
- `test.song` - 기본 문법
- `test_roles.song` - 역할을 가진 사용자 정의 관계
- `test_query.song` - 쿼리 시스템
- `test_else.song` - 조건 분기
- `test_random.song` - RANDOM과 CHANCE

## 라이선스

MIT

## 작성자

Amber Song
