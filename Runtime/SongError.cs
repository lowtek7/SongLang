namespace Song.Runtime;

/// <summary>
/// Song 언어 에러 타입
/// </summary>
public enum ErrorType
{
    // 노드 관련
    NodeNotFound,       // 존재하지 않는 노드
    PropertyNotFound,   // 없는 속성 접근

    // 타입 관련
    TypeMismatch,       // 타입 불일치
    InvalidCondition,   // 조건이 Boolean 아님

    // 연산 관련
    DivisionByZero,     // 0으로 나누기
    InvalidOperand,     // 잘못된 피연산자

    // 능력 관련
    CannotPerform,      // CAN 없이 관계 사용

    // 구문 관련
    SyntaxError,        // 구문 오류
    UnexpectedToken,    // 예상치 못한 토큰

    // 기타
    RuntimeError        // 일반 런타임 에러
}

/// <summary>
/// Song 언어 에러
/// </summary>
public class SongError : Exception
{
    public ErrorType Type { get; }
    public int Line { get; }
    public int Column { get; }
    public string? SourceLine { get; }

    public SongError(ErrorType type, string message, int line, int column, string? sourceLine = null)
        : base(message)
    {
        Type = type;
        Line = line;
        Column = column;
        SourceLine = sourceLine;
    }

    /// <summary>
    /// 포맷된 에러 메시지 생성
    /// </summary>
    public string FormatError()
    {
        var typeName = Type switch
        {
            ErrorType.NodeNotFound => "Node not found",
            ErrorType.PropertyNotFound => "Property not found",
            ErrorType.TypeMismatch => "Type mismatch",
            ErrorType.InvalidCondition => "Invalid condition",
            ErrorType.DivisionByZero => "Division by zero",
            ErrorType.InvalidOperand => "Invalid operand",
            ErrorType.CannotPerform => "Cannot perform",
            ErrorType.SyntaxError => "Syntax error",
            ErrorType.UnexpectedToken => "Unexpected token",
            ErrorType.RuntimeError => "Runtime error",
            _ => "Error"
        };

        var result = $"[Error] {typeName}: {Message}";
        result += $"\n  at line {Line}";

        if (SourceLine is not null)
        {
            result += $": {SourceLine.Trim()}";
        }

        return result;
    }

    public override string ToString() => FormatError();
}

/// <summary>
/// 에러 보고 유틸리티
/// </summary>
public static class ErrorReporter
{
    /// <summary>
    /// 에러를 출력에 기록
    /// </summary>
    public static void Report(TextWriter output, SongError error)
    {
        output.WriteLine(error.FormatError());
    }

    /// <summary>
    /// 예외를 SongError로 변환
    /// </summary>
    public static SongError FromException(Exception ex, int line = 0, int column = 0)
    {
        return ex switch
        {
            SongError e => e,
            _ => new SongError(ErrorType.RuntimeError, ex.Message, line, column)
        };
    }
}
