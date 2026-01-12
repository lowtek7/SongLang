namespace Song.Repl;

/// <summary>
/// Song 언어 REPL 도움말 시스템
/// </summary>
public static class HelpSystem
{
    private static readonly Dictionary<string, HelpTopic> Topics = new(StringComparer.OrdinalIgnoreCase)
    {
        ["IS"] = new HelpTopic(
            Name: "IS",
            Category: "관계 (Relation)",
            Brief: "노드 간 상속/타입 관계 정의",
            Description: """
                IS는 노드 간의 상속 관계를 정의합니다.
                프로토타입 기반 상속으로, 자식 노드는 부모의 속성을 상속받습니다.
                """,
            Syntax: "Subject IS Parent",
            Examples: """
                // 기본 상속
                Player IS Entity
                Enemy IS Entity

                // 다중 상속
                Dragon IS Monster
                Dragon IS Flying

                // 상속된 속성 확인
                Entity HAS HP 100
                Player IS Entity
                Player PRINT    // Player는 Entity의 HP를 상속받음
                """
        ),

        ["CONTAINS"] = new HelpTopic(
            Name: "CONTAINS",
            Category: "관계 (Relation)",
            Brief: "노드에 자식 포함 (컬렉션)",
            Description: """
                CONTAINS는 노드 간의 포함(소속) 관계를 정의합니다.
                IS(상속)와 달리 컬렉션 소속을 나타냅니다.
                EACH로 순회할 때 Children(CONTAINS 대상)을 순회합니다.
                """,
            Syntax: "Container CONTAINS Item",
            Examples: """
                // 인벤토리에 아이템 추가
                Inventory CONTAINS Sword
                Inventory CONTAINS Shield
                Inventory CONTAINS Potion

                // EACH로 순회
                Inventory EACH Item DO
                    Item PRINT
                END
                // 출력: Sword, Shield, Potion의 Name

                // 쿼리 결과도 CONTAINS로 저장됨
                ?enemies IS Monster
                enemies EACH e DO
                    e HAS Marked true
                END
                """
        ),

        ["IN"] = new HelpTopic(
            Name: "IN",
            Category: "관계 (Relation)",
            Brief: "CONTAINS의 역방향",
            Description: """
                IN은 CONTAINS의 역방향 관계입니다.
                "A IN B"는 "B CONTAINS A"와 동일합니다.
                더 자연스러운 표현이 필요할 때 사용합니다.
                """,
            Syntax: "Item IN Container",
            Examples: """
                // 아이템을 인벤토리에 추가 (역방향)
                Sword IN Inventory
                Shield IN Inventory

                // 위 코드는 아래와 동일
                // Inventory CONTAINS Sword
                // Inventory CONTAINS Shield
                """
        ),

        ["HAS"] = new HelpTopic(
            Name: "HAS",
            Category: "관계 (Relation)",
            Brief: "노드에 속성 부여",
            Description: """
                HAS는 노드에 속성(Property)과 값을 부여합니다.
                숫자, 문자열, 식별자, 또는 표현식을 값으로 사용할 수 있습니다.
                """,
            Syntax: """
                Subject HAS Property Value
                Subject HAS Property (Expression)
                """,
            Examples: """
                // 숫자 속성
                Player HAS HP 100
                Player HAS Level 1

                // 문자열 속성
                Player HAS Name "Hero"
                Sword HAS Description "A legendary blade"

                // 표현식 사용
                Target HAS HP (Target.HP - Damage)
                Player HAS Score (Player.Score + 10)
                """
        ),

        ["DO"] = new HelpTopic(
            Name: "DO",
            Category: "블록 (Block)",
            Brief: "실행 블록 정의",
            Description: """
                DO는 실행 블록을 정의합니다.
                관계(RELATION)와 함께 사용하면 해당 관계 호출 시 실행됩니다.
                WHEN, EACH와 결합하여 조건부/반복 실행에 사용됩니다.
                END로 블록을 닫습니다.

                관계의 역할은 HAS RoleName (Node)로 정의합니다.
                DO 블록 내에서 역할 이름으로 노드를 참조합니다.
                """,
            Syntax: """
                Subject DO
                    statements...
                END
                """,
            Examples: """
                // 사용자 정의 관계 정의
                Attack IS RELATION
                Attack HAS Attacker (Node)
                Attack HAS Victim (Node)
                Attack DO
                    Victim HAS HP (Victim.HP - Attacker.Damage)
                END

                // 관계 실행
                Player HAS Damage 25
                Enemy HAS HP 100
                Player Attack Enemy    // Enemy HP: 75

                // WHEN과 함께 사용
                Player HAS HP 0 WHEN DO
                    Player IS Dead
                END
                """
        ),

        ["WHEN"] = new HelpTopic(
            Name: "WHEN",
            Category: "제어 (Control)",
            Brief: "조건부 실행",
            Description: """
                WHEN은 조건이 참일 때만 블록을 실행합니다.
                조건문(HAS, IS, CAN) 뒤에 WHEN DO ... END 형식으로 사용합니다.
                조건이 거짓이면 블록을 건너뜁니다.
                """,
            Syntax: """
                Condition WHEN DO
                    statements...
                END
                """,
            Examples: """
                // HAS 조건 (값 비교)
                Player HAS HP 0 WHEN DO
                    Player IS Dead
                    Player LOSES MOVE
                END

                // HAS 조건 (속성 존재 확인)
                Item HAS Enchanted WHEN DO
                    Item HAS Damage (Item.Damage * 2)
                END

                // IS 조건
                Target IS Enemy WHEN DO
                    Target HAS Hostile true
                END
                """
        ),

        ["ALL"] = new HelpTopic(
            Name: "ALL",
            Category: "제어 (Control)",
            Brief: "모든 매칭 노드에 액션 적용",
            Description: """
                ALL은 특정 타입의 모든 노드를 찾아 액션을 적용합니다.
                Is() 관계를 통해 해당 타입이거나 상속받은 모든 노드가 대상입니다.
                쿼리 변수(?var)와 함께 사용하면 쿼리 결과에 액션을 적용합니다.
                """,
            Syntax: """
                ALL TypeName Action
                ALL ?queryVar Action
                """,
            Examples: """
                // 모든 Enemy 출력
                ALL Enemy PRINT

                // 모든 Entity에 속성 부여
                ALL Entity HAS Visible true

                // 모든 Monster에 데미지
                ALL Monster HAS HP 0

                // 사용 예시
                Goblin IS Enemy
                Orc IS Enemy
                Dragon IS Enemy
                ALL Enemy PRINT    // Goblin, Orc, Dragon 모두 출력

                // 쿼리 결과와 결합
                ?strong IS Monster WHERE ?strong.HP > 50
                ALL ?strong HAS Elite true    // HP > 50인 몬스터에만 적용
                """
        ),

        ["EACH"] = new HelpTopic(
            Name: "EACH",
            Category: "제어 (Control)",
            Brief: "컬렉션 반복",
            Description: """
                EACH는 컬렉션의 Children(CONTAINS 대상)을 순회합니다.
                각 자식 노드를 변수에 바인딩하여 블록 내에서 사용합니다.
                CONTAINS 관계로 연결된 자식 노드들이 대상입니다.
                """,
            Syntax: """
                Collection EACH Variable DO
                    statements...
                END
                """,
            Examples: """
                // 인벤토리 순회
                Inventory IS Container
                Inventory CONTAINS Sword
                Inventory CONTAINS Shield
                Potion IN Inventory

                Inventory EACH Item DO
                    Item PRINT
                END
                // 출력: Sword, Shield, Potion의 Name

                // 쿼리 결과 순회 (쿼리 결과도 CONTAINS로 저장됨)
                ?enemies IS Monster
                enemies EACH e DO
                    e HAS Marked true
                END
                """
        ),

        ["CAN"] = new HelpTopic(
            Name: "CAN",
            Category: "능력 (Ability)",
            Brief: "노드에 능력 부여",
            Description: """
                CAN은 노드에 능력(Ability)을 부여합니다.
                능력은 상속되며, 부모가 가진 능력을 자식도 사용할 수 있습니다.
                LOSES로 능력을 제거할 수 있습니다.
                """,
            Syntax: "Subject CAN AbilityName",
            Examples: """
                // 능력 부여
                Player CAN ATTACK
                Player CAN MOVE
                Bird CAN FLY

                // 능력 상속
                Entity CAN EXIST
                Player IS Entity    // Player도 EXIST 능력을 가짐

                // 조건에서 사용
                Player CAN FLY WHEN DO
                    Player HAS MovementType "Air"
                END
                """
        ),

        ["LOSES"] = new HelpTopic(
            Name: "LOSES",
            Category: "관계 제거 (Remove)",
            Brief: "관계/속성/능력 제거",
            Description: """
                LOSES는 노드의 관계, 속성, 능력을 제거합니다.
                - LOSES IS: 상속 관계 제거
                - LOSES CONTAINS: 포함 관계 제거
                - LOSES Target: 능력 또는 속성 제거 (자동 감지)
                자동 감지 시 능력을 먼저 확인하고, 없으면 속성을 제거합니다.
                """,
            Syntax: """
                Subject LOSES IS Parent          // 상속 제거
                Subject LOSES CONTAINS Child     // 포함 제거
                Subject LOSES Target             // 능력/속성 제거
                """,
            Examples: """
                // 상속 관계 제거
                Player IS Entity
                Player IS Hero
                Player LOSES IS Hero    // Hero 상속만 제거, Entity는 유지

                // 포함 관계 제거
                Inventory CONTAINS Sword
                Inventory LOSES CONTAINS Sword  // Sword를 인벤토리에서 제거

                // 능력 제거
                Player CAN FLY
                Player LOSES FLY        // 능력 제거

                // 속성 제거
                Player HAS HP 100
                Player LOSES HP         // 속성 제거
                """
        ),

        ["PRINT"] = new HelpTopic(
            Name: "PRINT",
            Category: "출력 (Output)",
            Brief: "노드 이름 출력",
            Description: """
                PRINT는 노드의 Name 속성을 출력합니다.
                Name 속성이 없으면 노드 이름을 출력합니다.
                """,
            Syntax: "Subject PRINT",
            Examples: """
                // Name 속성 출력
                Player HAS Name "Hero"
                Player PRINT    // 출력: Hero

                // Name이 없으면 노드 이름
                Entity PRINT    // 출력: Entity

                // ALL과 함께 사용
                ALL Enemy PRINT    // 모든 Enemy의 Name 출력
                """
        ),

        ["DEBUG"] = new HelpTopic(
            Name: "DEBUG",
            Category: "디버그 (Debug)",
            Brief: "디버그 정보 출력",
            Description: """
                DEBUG는 내부 상태를 출력합니다.
                현재 GRAPH 옵션만 지원됩니다.
                """,
            Syntax: "DEBUG GRAPH",
            Examples: """
                Player IS Entity
                Player HAS HP 100
                Player CAN ATTACK

                DEBUG GRAPH
                // 출력:
                // --- Graph State ---
                // Node(Player) IS Entity { HP=100 } CAN [ATTACK]
                // Node(Entity)
                // -------------------
                """
        ),

        ["RELATION"] = new HelpTopic(
            Name: "RELATION",
            Category: "확장 (Extension)",
            Brief: "사용자 정의 관계 선언",
            Description: """
                RELATION은 새로운 관계를 정의할 때 사용합니다.
                역할(Role)을 HAS Name (Node)로 정의합니다.
                DO 블록에서 역할 이름으로 노드를 참조합니다.

                역할 순서:
                - 첫 번째 역할 = 호출자 (좌변)
                - 두 번째 역할 = 대상 (우변)
                - 세 번째 이상 = 추가 인자

                예: "Player Attack Enemy" 실행 시
                - Attacker = Player (첫 번째 역할)
                - Victim = Enemy (두 번째 역할)
                """,
            Syntax: """
                RelationName IS RELATION
                RelationName HAS Role1 (Node)
                RelationName HAS Role2 (Node)
                RelationName DO
                    statements...
                END
                """,
            Examples: """
                // 공격 관계 정의 (2개 역할)
                Attack IS RELATION
                Attack HAS Attacker (Node)
                Attack HAS Victim (Node)
                Attack DO
                    Victim HAS HP (Victim.HP - Attacker.Damage)
                END

                // 사용
                Player HAS Damage 25
                Enemy HAS HP 100
                Player Attack Enemy    // Enemy HP: 75

                // 3개 역할 관계 정의
                Give IS RELATION
                Give HAS Giver (Node)
                Give HAS Receiver (Node)
                Give HAS Gift (Node)
                Give DO
                    Giver LOSES IS Gift
                    Receiver IS Gift
                END

                // 사용: Player가 NPC에게 Potion을 줌
                Player Give NPC Potion
                """
        ),

        ["QUERY"] = new HelpTopic(
            Name: "QUERY",
            Category: "쿼리 (Query)",
            Brief: "패턴 매칭으로 노드 검색",
            Description: """
                쿼리는 그래프에서 조건에 맞는 노드를 검색합니다.
                ? (와일드카드) 또는 ?name (변수 바인딩)으로 시작합니다.
                IS, HAS, CAN 관계와 함께 사용하여 노드를 필터링합니다.
                WHERE 절로 추가 조건을 지정할 수 있습니다.
                """,
            Syntax: """
                ?var IS TypeName [WHERE condition]
                ?var HAS Property [Value] [WHERE condition]
                ?var CAN Ability [WHERE condition]
                """,
            Examples: """
                // 타입으로 검색
                ?enemy IS Enemy
                // 결과: Enemy 타입인 모든 노드

                // 속성으로 검색
                ?hasHP HAS HP
                // 결과: HP 속성이 있는 모든 노드

                // 특정 값으로 검색
                ?dead HAS HP 0
                // 결과: HP가 0인 모든 노드

                // 능력으로 검색
                ?flyer CAN FLY
                // 결과: FLY 능력이 있는 모든 노드

                // WHERE 조건 사용
                ?strong IS Monster WHERE ?strong.HP > 50
                // 결과: HP > 50인 Monster 노드

                // ALL과 결합
                ?weak IS Enemy WHERE ?weak.HP < 30
                ALL ?weak HAS Marked true
                // 결과: HP < 30인 적에게 Marked 속성 부여
                """
        ),

        ["WHERE"] = new HelpTopic(
            Name: "WHERE",
            Category: "쿼리 (Query)",
            Brief: "쿼리 조건 필터",
            Description: """
                WHERE는 쿼리 결과를 조건으로 필터링합니다.
                쿼리 변수의 속성을 사용하여 조건식을 작성합니다.
                비교 연산자 (==, !=, <, >, <=, >=)를 사용할 수 있습니다.
                """,
            Syntax: """
                ?var IS/HAS/CAN Target WHERE condition
                """,
            Examples: """
                // HP가 50 초과인 몬스터
                ?strong IS Monster WHERE ?strong.HP > 50

                // 레벨이 10인 플레이어
                ?player IS Player WHERE ?player.Level == 10

                // 이름이 특정 값인 노드
                ?hero HAS Name WHERE ?hero.Name == "Hero"

                // 복합 조건 예시
                ?enemy IS Enemy WHERE ?enemy.HP > 0
                ALL ?enemy PRINT
                """
        ),
    };

    private static readonly Dictionary<string, string[]> Categories = new()
    {
        ["관계 (Relation)"] = ["IS", "HAS", "CONTAINS", "IN"],
        ["능력 (Ability)"] = ["CAN"],
        ["관계 제거 (Remove)"] = ["LOSES"],
        ["블록 (Block)"] = ["DO"],
        ["제어 (Control)"] = ["WHEN", "ALL", "EACH"],
        ["쿼리 (Query)"] = ["QUERY", "WHERE"],
        ["출력 (Output)"] = ["PRINT"],
        ["디버그 (Debug)"] = ["DEBUG"],
        ["확장 (Extension)"] = ["RELATION"],
    };

    /// <summary>
    /// 전체 도움말 출력
    /// </summary>
    public static void PrintOverview()
    {
        Console.WriteLine("""
            ╔═══════════════════════════════════════════════════════════════╗
            ║                    Song Language Help                         ║
            ╚═══════════════════════════════════════════════════════════════╝

            Song은 관계 중심 프로그래밍 언어입니다.
            모든 것은 노드(Node)이며, 관계(Relation)로 연결됩니다.

            """);

        Console.WriteLine("📚 키워드 목록:");
        Console.WriteLine("─────────────────────────────────────────────────────────────────");

        foreach (var (category, keywords) in Categories)
        {
            Console.WriteLine($"\n  【{category}】");
            foreach (var keyword in keywords)
            {
                if (Topics.TryGetValue(keyword, out var topic))
                {
                    Console.WriteLine($"    {keyword,-12} {topic.Brief}");
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine("─────────────────────────────────────────────────────────────────");
        Console.WriteLine("💡 상세 도움말: :help <키워드>  (예: :help IS, :help WHEN)");
        Console.WriteLine();

        PrintReplCommands();
    }

    /// <summary>
    /// 특정 토픽 도움말 출력
    /// </summary>
    public static bool PrintTopic(string topicName)
    {
        if (!Topics.TryGetValue(topicName, out var topic))
        {
            Console.WriteLine($"알 수 없는 토픽: {topicName}");
            Console.WriteLine("사용 가능한 토픽: " + string.Join(", ", Topics.Keys));
            return false;
        }

        Console.WriteLine($"""
            ╔═══════════════════════════════════════════════════════════════╗
            ║  {topic.Name,-10}                                              ║
            ╚═══════════════════════════════════════════════════════════════╝

            📂 카테고리: {topic.Category}
            📝 설명: {topic.Brief}

            """);

        Console.WriteLine("📖 상세 설명:");
        Console.WriteLine("─────────────────────────────────────────────────────────────────");
        Console.WriteLine(topic.Description);
        Console.WriteLine();

        Console.WriteLine("⌨️  문법:");
        Console.WriteLine("─────────────────────────────────────────────────────────────────");
        Console.WriteLine(topic.Syntax);
        Console.WriteLine();

        Console.WriteLine("💻 예시:");
        Console.WriteLine("─────────────────────────────────────────────────────────────────");
        Console.WriteLine(topic.Examples);
        Console.WriteLine();

        return true;
    }

    /// <summary>
    /// REPL 명령어 도움말 출력
    /// </summary>
    public static void PrintReplCommands()
    {
        Console.WriteLine("""
            🔧 REPL 명령어:
            ─────────────────────────────────────────────────────────────────
              :help, :h            전체 도움말
              :help <키워드>       키워드별 상세 도움말
              :graph, :g           현재 그래프 상태 출력
              :clear, :c           그래프 초기화 (새 세션)
              :quit, :q            종료
            """);
    }
}

/// <summary>
/// 도움말 토픽
/// </summary>
public record HelpTopic(
    string Name,
    string Category,
    string Brief,
    string Description,
    string Syntax,
    string Examples
);
