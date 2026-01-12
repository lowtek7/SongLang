using Song.Parser;

namespace Song.Runtime.Execution.Executors;

/// <summary>
/// 표현식 HAS 실행: Expression HAS Property Value
/// 예: Player.HP HAS Current 100
/// </summary>
public sealed class ExpressionHasExecutor : IStatementExecutor<ExpressionHasStatement>
{
    public void Execute(ExpressionHasStatement stmt, ExecutionContext ctx)
    {
        // 주어 표현식을 평가하여 노드를 얻음
        var subjectValue = ctx.EvaluateExpression(stmt.Subject);
        if (subjectValue is not Node subjectNode)
        {
            throw new SongError(ErrorType.TypeMismatch, "HAS subject must be a node", stmt.Line, stmt.Column);
        }

        // 값 결정
        object? value;
        if (stmt.ValueExpression is not null)
        {
            value = ctx.EvaluateExpression(stmt.ValueExpression);
        }
        else
        {
            value = stmt.Value;
            // 값이 문자열이고 존재하는 노드 이름이면 노드 참조로 저장
            if (value is string nodeName)
            {
                var node = ctx.Graph.GetNode(nodeName);
                if (node is not null)
                {
                    value = node;
                }
            }
        }

        subjectNode.SetProperty(stmt.Property, value);
    }
}
