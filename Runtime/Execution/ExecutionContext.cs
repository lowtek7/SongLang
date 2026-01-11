using Song.Parser;

namespace Song.Runtime.Execution;

/// <summary>
/// Executor들이 공유하는 실행 컨텍스트
/// Interpreter의 상태와 헬퍼 메서드에 대한 접근을 제공한다.
/// </summary>
public sealed class ExecutionContext
{
    /// <summary>
    /// 노드 그래프
    /// </summary>
    public Graph Graph { get; }

    /// <summary>
    /// 출력 스트림
    /// </summary>
    public TextWriter Output { get; }

    /// <summary>
    /// 변수 바인딩 (EACH, DO 블록 등에서 사용)
    /// </summary>
    public Dictionary<string, object?> Variables { get; }

    /// <summary>
    /// 랜덤 생성기 (RANDOM, CHANCE용)
    /// </summary>
    public Random Random { get; }

    /// <summary>
    /// WHEN 표현식 컨텍스트 (조건식 평가 시 bare 식별자를 Subject의 속성으로 해석)
    /// </summary>
    public Node? WhenSubject { get; set; }

    /// <summary>
    /// 단일 Statement 실행 (재귀 호출용)
    /// </summary>
    public Action<Statement> ExecuteStatement { get; internal set; } = null!;

    /// <summary>
    /// Statement 리스트 실행 (재귀 호출용)
    /// </summary>
    public Action<List<Statement>> Execute { get; internal set; } = null!;

    /// <summary>
    /// 노드 이름 해석 (컨텍스트 바인딩 우선, 없으면 그래프에서 가져오기/생성)
    /// </summary>
    public Func<string, Node> ResolveNode { get; internal set; } = null!;

    /// <summary>
    /// 노드 이름 해석 (없으면 null 반환)
    /// </summary>
    public Func<string, Node?> ResolveNodeOrNull { get; internal set; } = null!;

    /// <summary>
    /// 표현식 평가
    /// </summary>
    public Func<Expression, object?> EvaluateExpression { get; internal set; } = null!;

    /// <summary>
    /// 조건문 평가 (RelationStatement 기반)
    /// </summary>
    public Func<RelationStatement, bool> EvaluateCondition { get; internal set; } = null!;

    /// <summary>
    /// 값의 진위 판정 (truthy/falsy)
    /// </summary>
    public Func<object?, bool> IsTruthy { get; internal set; } = null!;

    /// <summary>
    /// 쿼리 결과 가져오기
    /// </summary>
    public Func<string, List<Node>> GetQueryResults { get; internal set; } = null!;

    /// <summary>
    /// 관계가 양방향인지 확인
    /// </summary>
    public Func<string, bool> IsBidirectionalRelation { get; internal set; } = null!;

    /// <summary>
    /// 값을 숫자(double)로 변환
    /// </summary>
    public Func<object?, Expression, double> ToNumber { get; internal set; } = null!;

    public ExecutionContext(
        Graph graph,
        TextWriter output,
        Dictionary<string, object?> variables,
        Random random)
    {
        Graph = graph;
        Output = output;
        Variables = variables;
        Random = random;
    }
}
