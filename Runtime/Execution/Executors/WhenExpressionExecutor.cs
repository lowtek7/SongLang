using Song.Parser;

namespace Song.Runtime.Execution.Executors;

/// <summary>
/// WHEN 표현식 실행: Subject WHEN (condition) DO ... [ELSE ...] END
/// 표현식이 참이면 본문 실행, 거짓이면 ELSE 실행
/// </summary>
public sealed class WhenExpressionExecutor : IStatementExecutor<WhenExpressionStatement>
{
    public void Execute(WhenExpressionStatement stmt, ExecutionContext ctx)
    {
        // Subject를 컨텍스트에 바인딩 (조건식에서 Subject.Property 접근 가능)
        var subjectNode = ctx.Graph.GetNode(stmt.Subject);

        if (subjectNode is not null)
        {
            ctx.Variables[stmt.Subject] = subjectNode;
        }

        // WHEN subject 설정 (bare 식별자를 속성으로 해석)
        var previousWhenSubject = ctx.WhenSubject;
        ctx.WhenSubject = subjectNode;

        try
        {
            // 조건 평가
            var result = ctx.EvaluateExpression(stmt.Condition);

            if (ctx.IsTruthy(result))
            {
                ctx.Execute(stmt.Body);
            }
            else
            {
                // ELSE WHEN 체이닝
                if (stmt.ElseWhen is not null)
                {
                    Execute(stmt.ElseWhen, ctx);  // 재귀 호출
                }
                // ELSE 블록
                else if (stmt.ElseBody is not null)
                {
                    ctx.Execute(stmt.ElseBody);
                }
            }
        }
        finally
        {
            ctx.WhenSubject = previousWhenSubject;
            if (subjectNode is not null)
            {
                ctx.Variables.Remove(stmt.Subject);
            }
        }
    }
}
