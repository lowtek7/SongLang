namespace Song.Tokenizer;

/// <summary>
/// Song 언어의 토큰 타입
/// </summary>
public enum TokenType
{
    // Keywords (원시 노드 + 내장 관계)
    IS,
    HAS,
    DO,
    END,        // DO 블록 종료
    PRINT,
    CAN,        // 능력 선언
    LOSES,      // 능력 제거
    RELATION,   // 사용자 정의 관계
    DEBUG,      // 디버그 명령어
    WHEN,       // 조건 분기
    ELSE,       // 조건 분기 (else)
    ALL,        // 쿼리 전체 적용
    EACH,       // 반복
    WHERE,      // 쿼리 조건
    OF,         // 속성 접근 (HP OF Player)
    RANDOM,     // 랜덤 숫자 (RANDOM min max)
    CHANCE,     // 확률 분기 (CHANCE percent DO ... END)

    // Query
    QUESTION,   // ? (와일드카드)
    QUERY_VAR,  // ?name (바인딩 변수)

    // Literals
    IDENTIFIER,
    NUMBER,
    STRING,

    // Delimiters
    LBRACE,     // {
    RBRACE,     // }
    LPAREN,     // (
    RPAREN,     // )
    COMMA,      // ,

    // Operators
    DOT,        // .
    PLUS,       // +
    MINUS,      // -
    STAR,       // *
    SLASH,      // /
    MODULO,     // %
    EQ,         // ==
    NEQ,        // !=
    LT,         // <
    GT,         // >
    LTE,        // <=
    GTE,        // >=

    // Logical Operators
    AND,        // AND
    OR,         // OR
    NOT,        // NOT

    // Special
    NEWLINE,
    EOF
}
