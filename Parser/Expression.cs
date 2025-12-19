namespace Song.Parser;

/// <summary>
/// Song 언어의 표현식 기본 클래스
/// </summary>
public abstract class Expression
{
    public int Line { get; }
    public int Column { get; }

    protected Expression(int line, int column)
    {
        Line = line;
        Column = column;
    }
}

/// <summary>
/// 숫자 리터럴: 100, 3.14
/// </summary>
public sealed class NumberExpression : Expression
{
    public double Value { get; }

    public NumberExpression(double value, int line, int column) : base(line, column)
    {
        Value = value;
    }

    public override string ToString() => Value.ToString();
}

/// <summary>
/// 문자열 리터럴: "Hello"
/// </summary>
public sealed class StringExpression : Expression
{
    public string Value { get; }

    public StringExpression(string value, int line, int column) : base(line, column)
    {
        Value = value;
    }

    public override string ToString() => $"\"{Value}\"";
}

/// <summary>
/// 식별자: Player, HP
/// </summary>
public sealed class IdentifierExpression : Expression
{
    public string Name { get; }

    public IdentifierExpression(string name, int line, int column) : base(line, column)
    {
        Name = name;
    }

    public override string ToString() => Name;
}

/// <summary>
/// 속성 접근: Player.HP
/// </summary>
public sealed class PropertyAccessExpression : Expression
{
    public Expression Object { get; }
    public string Property { get; }

    public PropertyAccessExpression(Expression obj, string property, int line, int column) : base(line, column)
    {
        Object = obj;
        Property = property;
    }

    public override string ToString() => $"{Object}.{Property}";
}

/// <summary>
/// 이항 연산: a + b, a - b, a * b, a / b, a == b, etc.
/// </summary>
public sealed class BinaryExpression : Expression
{
    public Expression Left { get; }
    public BinaryOperator Operator { get; }
    public Expression Right { get; }

    public BinaryExpression(Expression left, BinaryOperator op, Expression right, int line, int column)
        : base(line, column)
    {
        Left = left;
        Operator = op;
        Right = right;
    }

    public override string ToString() => $"({Left} {Operator} {Right})";
}

/// <summary>
/// 단항 연산: -a
/// </summary>
public sealed class UnaryExpression : Expression
{
    public UnaryOperator Operator { get; }
    public Expression Operand { get; }

    public UnaryExpression(UnaryOperator op, Expression operand, int line, int column)
        : base(line, column)
    {
        Operator = op;
        Operand = operand;
    }

    public override string ToString() => $"({Operator}{Operand})";
}

/// <summary>
/// 괄호 표현식: (a + b)
/// </summary>
public sealed class GroupingExpression : Expression
{
    public Expression Inner { get; }

    public GroupingExpression(Expression inner, int line, int column) : base(line, column)
    {
        Inner = inner;
    }

    public override string ToString() => $"({Inner})";
}

/// <summary>
/// 랜덤 표현식: RANDOM min max
/// 예: RANDOM 10 20 → 10~20 사이 랜덤 정수
/// </summary>
public sealed class RandomExpression : Expression
{
    public Expression Min { get; }
    public Expression Max { get; }

    public RandomExpression(Expression min, Expression max, int line, int column) : base(line, column)
    {
        Min = min;
        Max = max;
    }

    public override string ToString() => $"RANDOM {Min} {Max}";
}

/// <summary>
/// 이항 연산자
/// </summary>
public enum BinaryOperator
{
    // 산술
    Add,        // +
    Subtract,   // -
    Multiply,   // *
    Divide,     // /
    Modulo,     // %

    // 비교
    Equal,      // ==
    NotEqual,   // !=
    LessThan,   // <
    GreaterThan,// >
    LessEqual,  // <=
    GreaterEqual,// >=

    // 논리
    And,        // AND
    Or          // OR
}

/// <summary>
/// 단항 연산자
/// </summary>
public enum UnaryOperator
{
    Negate,     // -
    Not         // NOT
}
