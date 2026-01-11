using Song.Parser;

namespace Song.Runtime.Execution.Executors;

/// <summary>
/// 표현식 PRINT 실행: Expression PRINT
/// 예: Player.HP.Current PRINT
/// </summary>
public sealed class ExpressionPrintExecutor : IStatementExecutor<ExpressionPrintStatement>
{
    public void Execute(ExpressionPrintStatement stmt, ExecutionContext ctx)
    {
        var value = ctx.EvaluateExpression(stmt.Subject);
        if (value is Node node)
        {
            // 노드인 경우 Name 속성 또는 노드 이름 출력
            var name = node.GetProperty("Name");
            ctx.Output.WriteLine(name ?? node.Name);
        }
        else
        {
            ctx.Output.WriteLine(value?.ToString() ?? "null");
        }
    }
}
