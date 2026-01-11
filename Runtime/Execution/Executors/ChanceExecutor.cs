using Song.Parser;

namespace Song.Runtime.Execution.Executors;

/// <summary>
/// CHANCE 실행: CHANCE percent DO ... [ELSE DO ...] END
/// 확률에 따라 본문 또는 ELSE 실행
/// </summary>
public sealed class ChanceExecutor : IStatementExecutor<ChanceStatement>
{
    public void Execute(ChanceStatement stmt, ExecutionContext ctx)
    {
        // 확률 평가 (0~100)
        var percentValue = ctx.EvaluateExpression(stmt.Percent);
        var percent = ctx.ToNumber(percentValue, stmt.Percent);

        // 0~100 사이 랜덤 값 생성
        var roll = ctx.Random.Next(0, 100);

        if (roll < percent)
        {
            ctx.Execute(stmt.Body);
        }
        else if (stmt.ElseBody is not null)
        {
            ctx.Execute(stmt.ElseBody);
        }
    }
}
