using Song.Parser;
using Song.Runtime.Execution;
using SongExecutionContext = Song.Runtime.Execution.ExecutionContext;

namespace Song.Runtime;

/// <summary>
/// Song 언어의 인터프리터
/// Statement 리스트를 실행한다.
/// </summary>
public sealed class Interpreter
{
    private readonly Graph _graph = new();
    private readonly TextWriter _output;
    private readonly ExecutorRegistry _registry;
    private readonly SongExecutionContext _context;

    // 랜덤 생성기 (RANDOM, CHANCE용)
    private static readonly Random _random = new();

    public Graph Graph => _graph;

    public Interpreter() : this(Console.Out)
    {
    }

    public Interpreter(TextWriter output)
    {
        _output = output;
        _registry = new ExecutorRegistry();

        // ExecutionContext 초기화
        _context = new SongExecutionContext(_graph, output, [], _random)
        {
            ExecuteStatement = ExecuteStatement,
            Execute = Execute,
            ResolveNode = ResolveNode,
            ResolveNodeOrNull = ResolveNodeOrNull,
            EvaluateExpression = EvaluateExpression,
            EvaluateCondition = EvaluateCondition,
            IsTruthy = IsTruthy,
            GetQueryResults = GetQueryResults,
            IsBidirectionalRelation = IsBidirectionalRelation,
            ToNumber = ToNumber
        };
    }

    /// <summary>
    /// 문장 리스트 실행
    /// </summary>
    public void Execute(List<Statement> statements)
    {
        foreach (var stmt in statements)
        {
            ExecuteStatement(stmt);
        }
    }

    private void ExecuteStatement(Statement stmt)
    {
        var executor = _registry.GetExecutor(stmt.GetType());
        if (executor is null)
        {
            throw new InterpreterException($"Unknown statement type: {stmt.GetType().Name}", stmt.Line, stmt.Column);
        }
        executor.Execute(stmt, _context);
    }

    /// <summary>
    /// 그래프 상태 덤프
    /// </summary>
    public void DumpGraph()
    {
        // DebugExecutor의 로직을 직접 호출하기 위해 임시 DebugStatement 생성
        var debugStmt = new DebugStatement(DebugTarget.Graph, 0, 0);
        ExecuteStatement(debugStmt);
    }

    /// <summary>
    /// 노드 이름을 해석 (컨텍스트 바인딩 우선, 없으면 그래프에서 가져오기/생성)
    /// </summary>
    private Node ResolveNode(string name)
    {
        // 컨텍스트에서 먼저 찾기 (EACH 등에서 바인딩된 변수)
        if (_context.Variables.TryGetValue(name, out var contextValue) && contextValue is Node node)
        {
            return node;
        }

        // 그래프에서 가져오거나 생성
        return _graph.GetOrCreateNode(name);
    }

    /// <summary>
    /// 노드 이름을 해석 (없으면 null 반환)
    /// </summary>
    private Node? ResolveNodeOrNull(string name)
    {
        // 컨텍스트에서 먼저 찾기
        if (_context.Variables.TryGetValue(name, out var contextValue) && contextValue is Node node)
        {
            return node;
        }

        // 그래프에서 찾기 (생성하지 않음)
        return _graph.GetNode(name);
    }

    /// <summary>
    /// 조건 문장 평가
    /// HAS 조건: 노드가 해당 속성을 가지고 있고 값이 일치하면 참
    /// IS 조건: 노드가 해당 타입인지 확인
    /// </summary>
    private bool EvaluateCondition(RelationStatement condition)
    {
        var node = _graph.GetNode(condition.Subject);
        if (node is null) return false;

        return condition.Relation.ToUpperInvariant() switch
        {
            "HAS" => EvaluateHasCondition(node, condition),
            "IS" => condition.Object is not null && node.Is(condition.Object),
            "CAN" => condition.Object is not null && node.Can(condition.Object),
            _ => false
        };
    }

    private static bool EvaluateHasCondition(Node node, RelationStatement rel)
    {
        if (rel.Object is null) return false;

        var propValue = node.GetProperty(rel.Object);

        // 값이 지정되지 않았으면 속성 존재 여부만 확인
        if (rel.Value is null)
        {
            return propValue is not null;
        }

        // 값 비교
        if (propValue is null) return false;

        // 숫자 비교
        if (rel.Value is double dVal && propValue is double dProp)
        {
            return Math.Abs(dVal - dProp) < 0.0001;
        }

        return rel.Value.Equals(propValue);
    }

    /// <summary>
    /// 관계가 양방향인지 확인
    /// </summary>
    private bool IsBidirectionalRelation(string relationName)
    {
        var relationNode = _graph.GetNode(relationName);
        if (relationNode is null) return false;
        return relationNode.GetInternalProperty("Bidirectional") as bool? ?? false;
    }

    /// <summary>
    /// 쿼리 결과 가져오기 (노드의 Items 내부 속성에서)
    /// </summary>
    public List<Node> GetQueryResults(string variableName)
    {
        var resultNode = _graph.GetNode(variableName);
        if (resultNode is not null)
        {
            // QueryResult 노드인지 확인
            if (resultNode.Is("QueryResult"))
            {
                var items = resultNode.GetInternalProperty("Items") as List<Node>;
                if (items is not null)
                {
                    return items;
                }
            }
        }
        return [];
    }

    #region Expression Evaluation

    private object? EvaluateExpression(Expression expr)
    {
        return expr switch
        {
            NumberExpression num => num.Value,
            StringExpression str => str.Value,
            IdentifierExpression id => ResolveIdentifier(id),
            PropertyAccessExpression prop => ResolvePropertyAccess(prop),
            BinaryExpression bin => EvaluateBinary(bin),
            UnaryExpression unary => EvaluateUnary(unary),
            GroupingExpression group => EvaluateExpression(group.Inner),
            RandomExpression rand => EvaluateRandom(rand),
            _ => throw new InterpreterException($"Unknown expression type: {expr.GetType().Name}", expr.Line, expr.Column)
        };
    }

    /// <summary>
    /// RANDOM 표현식 평가: RANDOM min max
    /// min 이상 max 이하 랜덤 정수 반환
    /// </summary>
    private double EvaluateRandom(RandomExpression expr)
    {
        var minValue = EvaluateExpression(expr.Min);
        var maxValue = EvaluateExpression(expr.Max);

        var min = (int)ToNumber(minValue, expr.Min);
        var max = (int)ToNumber(maxValue, expr.Max);

        // max 포함 (inclusive)
        return _random.Next(min, max + 1);
    }

    private object? ResolveIdentifier(IdentifierExpression id)
    {
        // 컨텍스트에서 먼저 찾기
        if (_context.Variables.TryGetValue(id.Name, out var contextValue))
        {
            return contextValue;
        }

        // 그래프에서 노드 찾기
        var node = _graph.GetNode(id.Name);
        if (node is not null)
        {
            return node;
        }

        // WHEN 컨텍스트에서 Subject의 속성으로 해석 시도
        if (_context.WhenSubject is not null)
        {
            var propValue = _context.WhenSubject.GetProperty(id.Name);
            if (propValue is not null)
            {
                return propValue;
            }
        }

        throw new SongError(ErrorType.NodeNotFound, $"\"{id.Name}\"", id.Line, id.Column);
    }

    private object? ResolvePropertyAccess(PropertyAccessExpression prop)
    {
        var obj = EvaluateExpression(prop.Object);

        if (obj is Node node)
        {
            var value = node.GetProperty(prop.Property);
            if (value is null)
            {
                throw new SongError(ErrorType.PropertyNotFound,
                    $"\"{node.Name}\" has no \"{prop.Property}\"", prop.Line, prop.Column);
            }
            return value;
        }

        throw new SongError(ErrorType.TypeMismatch,
            $"'{prop.Object}' is not a Node", prop.Line, prop.Column);
    }

    private object? EvaluateBinary(BinaryExpression bin)
    {
        // AND, OR은 short-circuit 평가
        if (bin.Operator == BinaryOperator.And)
        {
            var left = EvaluateExpression(bin.Left);
            if (!IsTruthy(left)) return false;
            return IsTruthy(EvaluateExpression(bin.Right));
        }

        if (bin.Operator == BinaryOperator.Or)
        {
            var left = EvaluateExpression(bin.Left);
            if (IsTruthy(left)) return true;
            return IsTruthy(EvaluateExpression(bin.Right));
        }

        var leftVal = EvaluateExpression(bin.Left);
        var rightVal = EvaluateExpression(bin.Right);

        return bin.Operator switch
        {
            BinaryOperator.Add => Add(leftVal, rightVal, bin),
            BinaryOperator.Subtract => Subtract(leftVal, rightVal, bin),
            BinaryOperator.Multiply => Multiply(leftVal, rightVal, bin),
            BinaryOperator.Divide => Divide(leftVal, rightVal, bin),
            BinaryOperator.Modulo => Modulo(leftVal, rightVal, bin),
            BinaryOperator.Equal => Equals(leftVal, rightVal),
            BinaryOperator.NotEqual => !Equals(leftVal, rightVal),
            BinaryOperator.LessThan => Compare(leftVal, rightVal, bin) < 0,
            BinaryOperator.GreaterThan => Compare(leftVal, rightVal, bin) > 0,
            BinaryOperator.LessEqual => Compare(leftVal, rightVal, bin) <= 0,
            BinaryOperator.GreaterEqual => Compare(leftVal, rightVal, bin) >= 0,
            _ => throw new InterpreterException($"Unknown operator: {bin.Operator}", bin.Line, bin.Column)
        };
    }

    private object? EvaluateUnary(UnaryExpression unary)
    {
        var operand = EvaluateExpression(unary.Operand);

        return unary.Operator switch
        {
            UnaryOperator.Negate => Negate(operand, unary),
            UnaryOperator.Not => !IsTruthy(operand),
            _ => throw new InterpreterException($"Unknown operator: {unary.Operator}", unary.Line, unary.Column)
        };
    }

    /// <summary>
    /// 값의 진위 판정 (truthy/falsy)
    /// </summary>
    private static bool IsTruthy(object? value)
    {
        return value switch
        {
            null => false,
            bool b => b,
            double d => d != 0,
            int i => i != 0,
            string s => !string.IsNullOrEmpty(s),
            Node _ => true,
            _ => true
        };
    }

    /// <summary>
    /// Converts a value to a number (double).
    /// Note: All numbers in SongLang are double. The int case is defensive code
    /// for potential future C# API extensions where int values might be passed.
    /// </summary>
    private static double ToNumber(object? value, Expression expr)
    {
        return value switch
        {
            double d => d,
            int i => i,  // Defensive: currently unreachable, all parsed numbers are double
            bool b => b ? 1 : 0,
            null => throw new SongError(ErrorType.TypeMismatch, "null cannot be converted to Number", expr.Line, expr.Column),
            _ => throw new SongError(ErrorType.TypeMismatch, $"'{value}' ({value.GetType().Name}) cannot be converted to Number", expr.Line, expr.Column)
        };
    }

    private static object Add(object? left, object? right, Expression expr)
    {
        // 문자열 연결
        if (left is string || right is string)
        {
            return $"{left}{right}";
        }

        return ToNumber(left, expr) + ToNumber(right, expr);
    }

    private static double Subtract(object? left, object? right, Expression expr)
    {
        return ToNumber(left, expr) - ToNumber(right, expr);
    }

    private static double Multiply(object? left, object? right, Expression expr)
    {
        return ToNumber(left, expr) * ToNumber(right, expr);
    }

    private static double Divide(object? left, object? right, Expression expr)
    {
        var divisor = ToNumber(right, expr);
        if (divisor == 0)
        {
            throw new SongError(ErrorType.DivisionByZero, "Cannot divide by zero", expr.Line, expr.Column);
        }
        return ToNumber(left, expr) / divisor;
    }

    private static double Modulo(object? left, object? right, Expression expr)
    {
        var divisor = ToNumber(right, expr);
        if (divisor == 0)
        {
            throw new SongError(ErrorType.DivisionByZero, "Cannot modulo by zero", expr.Line, expr.Column);
        }
        return ToNumber(left, expr) % divisor;
    }

    private static double Negate(object? operand, Expression expr)
    {
        return -ToNumber(operand, expr);
    }

    private static int Compare(object? left, object? right, Expression expr)
    {
        var l = ToNumber(left, expr);
        var r = ToNumber(right, expr);
        return l.CompareTo(r);
    }

    private static new bool Equals(object? left, object? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;
        return left.Equals(right);
    }

    #endregion
}

/// <summary>
/// 노드가 특정 능력을 가지고 있는지 확인하는 확장 메서드
/// </summary>
public static class NodeExtensions
{
    public static bool Can(this Node node, string ability)
    {
        return CanWithVisited(node, ability, []);
    }

    private static bool CanWithVisited(Node node, string ability, HashSet<Node> visited)
    {
        // 순환 참조 방어
        if (!visited.Add(node))
        {
            return false;
        }

        var abilities = node.GetInternalProperty("Abilities") as HashSet<string>;
        if (abilities?.Contains(ability) == true)
        {
            return true;
        }

        // 부모에서 능력 상속 확인
        foreach (var parent in node.Parents)
        {
            if (CanWithVisited(parent, ability, visited))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// 인터프리터 오류
/// </summary>
public class InterpreterException : Exception
{
    public int Line { get; }
    public int Column { get; }

    public InterpreterException(string message, int line, int column)
        : base($"[{line}:{column}] {message}")
    {
        Line = line;
        Column = column;
    }
}
