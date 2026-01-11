using Song.Parser;

namespace Song.Runtime.Execution.Executors;

/// <summary>
/// 표현식 기반 HAS 실행: Subject HAS Property (Expression)
/// </summary>
public sealed class HasExpressionExecutor : IStatementExecutor<HasExpressionStatement>
{
    public void Execute(HasExpressionStatement stmt, ExecutionContext ctx)
    {
        var subject = ctx.ResolveNode(stmt.Subject);
        var value = ctx.EvaluateExpression(stmt.ValueExpression);
        subject.SetProperty(stmt.Property, value);
    }
}
